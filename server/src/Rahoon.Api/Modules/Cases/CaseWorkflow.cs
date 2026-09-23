using Microsoft.EntityFrameworkCore;
using Rahoon.Api.Infrastructure.Http;
using Rahoon.Api.Infrastructure.Persistence;
using Rahoon.Api.Infrastructure.Tenancy;
using Rahoon.Api.Infrastructure.Time;
using Rahoon.Api.Modules.Agreements;
using Rahoon.Api.Modules.Assessment;
using Rahoon.Api.Modules.Audit;
using Rahoon.Api.Modules.Closure;
using Rahoon.Api.Modules.Complaints;
using Rahoon.Api.Modules.Documents;
using Rahoon.Api.Modules.Identity;
using Rahoon.Api.Modules.Solutions;

namespace Rahoon.Api.Modules.Cases;

/// <summary>
/// A permitted case transition. <see cref="Manual"/> transitions are offered to users as
/// actions; the others are only executed by domain services (approval decision, owner
/// response, closure…) that perform their own additional checks first.
/// </summary>
public sealed record TransitionDef(
    string Key,
    CaseStatus[] From,
    CaseStatus To,
    string LabelAr,
    string Permission,
    bool Manual,
    bool RequiresReason = false,
    bool RequiresStepUp = false,
    string[]? Guards = null);

public sealed record AvailableAction(string Key, string LabelAr, string To, bool Enabled, IReadOnlyList<string> Reasons, bool RequiresReason, bool RequiresStepUp);

public sealed class CaseWorkflow(RahoonDbContext db, RequestContext rc, IClock clock, AuditLog audit)
{
    private static readonly CaseStatus[] NonTerminalActive =
    [
        CaseStatus.AwaitingData, CaseStatus.Verification, CaseStatus.Valuation, CaseStatus.ProposedSolution, CaseStatus.InternalApproval,
        CaseStatus.AwaitingCustomer, CaseStatus.Negotiation, CaseStatus.ActiveSettlement, CaseStatus.VoluntarySale,
        CaseStatus.JudicialReferral, CaseStatus.ExternalJudicialSale, CaseStatus.AwaitingReconciliation,
    ];

    /// <summary>Blueprint transition table («جدول الانتقالات والحواجز»).</summary>
    public static readonly IReadOnlyList<TransitionDef> Transitions =
    [
        new("finalize_intake", [CaseStatus.Draft], CaseStatus.AwaitingData, "إنشاء الحالة", P.CaseCreate, false,
            Guards: ["intake_complete", "duplicate_resolved"]),
        new("start_verification", [CaseStatus.AwaitingData], CaseStatus.Verification, "بدء التحقق", P.CaseTransition, true,
            Guards: ["mandatory_data"]),
        new("start_valuation", [CaseStatus.Verification], CaseStatus.Valuation, "اعتماد التحقق والانتقال للتقييم", P.CaseTransition, true,
            Guards: ["core_docs_verified"]),
        new("propose_solution", [CaseStatus.Valuation], CaseStatus.ProposedSolution, "الانتقال لإعداد الحل", P.CaseTransition, true,
            Guards: ["valuation_valid", "legal_review_complete"]),
        new("submit_for_approval", [CaseStatus.ProposedSolution, CaseStatus.Negotiation], CaseStatus.InternalApproval, "إرسال للموافقة الداخلية", P.SolutionReview, false,
            RequiresReason: true, Guards: ["valuation_valid", "analysis_complete", "no_open_complaint"]),
        new("approve_and_offer", [CaseStatus.InternalApproval], CaseStatus.AwaitingCustomer, "اعتماد وإرسال العرض", P.SolutionApprove, false,
            RequiresReason: true, RequiresStepUp: true),
        new("return_to_solution", [CaseStatus.InternalApproval], CaseStatus.ProposedSolution, "إعادة للتعديل", P.SolutionApprove, false, RequiresReason: true),
        new("owner_counteroffer", [CaseStatus.AwaitingCustomer], CaseStatus.Negotiation, "عرض مقابل من المالك", "", false),
        new("owner_declined", [CaseStatus.AwaitingCustomer], CaseStatus.ProposedSolution, "رفض المالك للعرض", "", false),
        // L18: apologising for a counteroffer returns to «حل مقترح» — never to referral.
        new("counter_declined", [CaseStatus.Negotiation], CaseStatus.ProposedSolution, "الاعتذار عن الطلب", P.NegotiationManage, false, RequiresReason: true),
        new("activate_agreement", [CaseStatus.AwaitingCustomer], CaseStatus.ActiveSettlement, "تفعيل الاتفاق", P.AgreementActivate, false,
            RequiresReason: false, Guards: ["consent_recorded", "agreement_ready"]),
        new("restructure_after_breach", [CaseStatus.ActiveSettlement], CaseStatus.ProposedSolution, "إعادة الهيكلة بعد الإخلال", P.BreachManage, true,
            RequiresReason: true, Guards: ["open_breach_review"]),
        new("settlement_completed", [CaseStatus.ActiveSettlement], CaseStatus.AwaitingReconciliation, "اكتمال السداد — للتسوية المالية", P.ReconciliationPrepare, true,
            Guards: ["all_installments_matched"]),
        new("start_voluntary_sale", CaseStatusInfo.SolutionStates, CaseStatus.VoluntarySale, "بدء مسار البيع الطوعي", P.SaleManage, false,
            RequiresReason: true, Guards: ["owner_sale_consent", "valuation_valid", "no_open_complaint"]),
        new("sale_completed", [CaseStatus.VoluntarySale], CaseStatus.AwaitingReconciliation, "اكتمال البيع — للتسوية المالية", P.SaleManage, false),
        new("refer_judicial", CaseStatusInfo.SolutionStates, CaseStatus.JudicialReferral, "اعتماد الإحالة القضائية", P.ReferralApprove, false,
            RequiresReason: true, RequiresStepUp: true, Guards: ["no_open_complaint", "no_live_offer"]),
        new("external_sale_started", [CaseStatus.JudicialReferral], CaseStatus.ExternalJudicialSale, "تسجيل بدء البيع القضائي الخارجي", P.ReferralExternalUpdate, false,
            RequiresReason: true),
        new("external_result_recorded", [CaseStatus.ExternalJudicialSale], CaseStatus.AwaitingReconciliation, "تسجيل النتيجة — للتسوية المالية", P.ReferralExternalUpdate, false,
            RequiresReason: true),
        new("close", [CaseStatus.AwaitingReconciliation], CaseStatus.Closed, "إغلاق الحالة", P.CaseClose, false,
            RequiresReason: true, RequiresStepUp: true, Guards: ["reconciliation_approved", "closure_documents_ready"]),
        new("pause", NonTerminalActive, CaseStatus.Paused, "إيقاف الحالة مؤقتاً", P.CasePause, true, RequiresReason: true),
        new("resume", [CaseStatus.Paused], CaseStatus.Paused /* resolved to StatusBeforePause */, "استئناف الحالة", P.CasePause, true, RequiresReason: true),
        new("cancel", [CaseStatus.Draft, .. NonTerminalActive, CaseStatus.Paused], CaseStatus.Cancelled, "إلغاء الحالة", P.CaseCancelApprove, false,
            RequiresReason: true, RequiresStepUp: true, Guards: ["no_open_complaint"]),
    ];

    public static TransitionDef Def(string key) => Transitions.First(t => t.Key == key);

    /// <summary>
    /// Manual actions for the caller. Hidden when the caller lacks the permission;
    /// returned disabled with reasons when permitted but not yet eligible (Handoff rule).
    /// </summary>
    public async Task<IReadOnlyList<AvailableAction>> AvailableActionsAsync(Case c)
    {
        var result = new List<AvailableAction>();
        foreach (var t in Transitions.Where(t => t.Manual && t.From.Contains(c.Status)))
        {
            if (!rc.Has(t.Permission)) continue;
            var reasons = await EvaluateGuardsAsync(c, t);
            var to = t.Key == "resume" ? c.StatusBeforePause ?? CaseStatus.AwaitingData : t.To;
            result.Add(new AvailableAction(t.Key, t.LabelAr, CaseStatusInfo.Key(to), reasons.Count == 0, reasons, t.RequiresReason, t.RequiresStepUp));
        }
        return result;
    }

    /// <summary>
    /// Performs a transition inside the caller's unit of work. Validates expected state,
    /// permission, reason, step-up and guards; refusals are audited in a separate transaction.
    /// </summary>
    public async Task TransitionAsync(Case c, string key, string? reason, CaseStatus? expectedStatus = null,
        bool systemInitiated = false, IReadOnlyList<string>? evidence = null, IReadOnlyList<string>? extraGuardFailures = null)
    {
        var t = Def(key);
        var from = c.Status;
        var to = key == "resume" ? c.StatusBeforePause ?? CaseStatus.AwaitingData : t.To;

        if (expectedStatus is { } exp && exp != c.Status)
            throw new ConflictException("stale_state", "تغيّرت حالة الحالة منذ فتحها. حدّث الصفحة لرؤية الوضع الحالي.");

        if (!t.From.Contains(from))
        {
            await Blocked(c, t, to, reason, [$"الانتقال «{t.LabelAr}» غير مسموح من حالة «{CaseStatusInfo.Of(from).LabelAr}»."]);
            throw new DomainException("transition_not_allowed", $"لا يمكن تنفيذ «{t.LabelAr}» والحالة «{CaseStatusInfo.Of(from).LabelAr}».", StatusCodes.Status409Conflict);
        }

        if (!systemInitiated && t.Permission.Length > 0 && !rc.Has(t.Permission))
        {
            await Blocked(c, t, to, reason, ["لا يملك الفاعل صلاحية هذا الانتقال."]);
            throw new ForbiddenException();
        }

        if (t.RequiresReason && string.IsNullOrWhiteSpace(reason))
            throw new ValidationFailedException(new Dictionary<string, string[]> { ["reason"] = ["السبب إلزامي لهذا الإجراء."] });

        if (t.RequiresStepUp && !systemInitiated)
            EndpointAccess.EnsureStepUp(rc, clock);

        var failures = (await EvaluateGuardsAsync(c, t)).Concat(extraGuardFailures ?? []).ToList();
        if (failures.Count > 0)
        {
            await Blocked(c, t, to, reason, failures);
            throw new DomainException("guard_failed", $"لا يمكن تنفيذ «{t.LabelAr}» الآن.", StatusCodes.Status422UnprocessableEntity, failures);
        }

        if (to == CaseStatus.Paused)
        {
            c.StatusBeforePause = from;
            c.PauseReason = reason;
            c.SlaPausedAt = clock.UtcNow;
            c.SlaPausedReason = reason;
        }
        else if (from == CaseStatus.Paused)
        {
            c.StatusBeforePause = null;
            c.PauseReason = null;
            c.SlaPausedAt = null;
            c.SlaPausedReason = null;
        }
        if (to == CaseStatus.Cancelled) c.CancelReason = reason;
        if (to == CaseStatus.Closed) c.ClosedAt = clock.UtcNow;

        c.Status = to;
        c.StatusChangedAt = clock.UtcNow;
        if (to != CaseStatus.Paused) c.StageDueOn = await ComputeDueAsync(c.OrganizationId, to);

        await audit.RecordAsync(new AuditEntry("case.transition",
            $"{CaseStatusInfo.Of(from).LabelAr} ← {CaseStatusInfo.Of(to).LabelAr}",
            c.Id, c.Reference, CaseStatusInfo.Key(from), CaseStatusInfo.Key(to), reason,
            Detail: t.LabelAr, Evidence: evidence, OrganizationId: c.OrganizationId));
    }

    public async Task<DateOnly?> ComputeDueAsync(Guid orgId, CaseStatus status)
    {
        var rule = await db.Set<Administration.SlaRule>().AsNoTracking()
            .FirstOrDefaultAsync(r => r.OrganizationId == orgId && r.Status == status.ToString());
        return rule is null ? null : BusinessDays.Add(clock.TodayRiyadh, rule.BusinessDays);
    }

    private Task Blocked(Case c, TransitionDef t, CaseStatus to, string? reason, IReadOnlyList<string> failures) =>
        audit.RecordBlockedAsync(new AuditEntry("case.transition_blocked",
            $"محاولة انتقال محجوبة: {CaseStatusInfo.Of(c.Status).LabelAr} ← {CaseStatusInfo.Of(to).LabelAr}",
            c.Id, c.Reference, CaseStatusInfo.Key(c.Status), CaseStatusInfo.Key(to), reason,
            Detail: "المانع: " + string.Join(" · ", failures) + ". لم يُنفذ الانتقال.", Blocked: true, OrganizationId: c.OrganizationId));

    // ───────── Guards ─────────

    public async Task<List<string>> EvaluateGuardsAsync(Case c, TransitionDef t)
    {
        var failures = new List<string>();
        foreach (var g in t.Guards ?? [])
        {
            var msg = await CheckAsync(c, g);
            if (msg is not null) failures.Add(msg);
        }
        return failures;
    }

    private async Task<string?> CheckAsync(Case c, string guard)
    {
        var today = clock.TodayRiyadh;
        switch (guard)
        {
            case "intake_complete":
            case "mandatory_data":
            {
                var missing = await MissingIntakeAsync(c);
                return missing.Count == 0 ? null : "بيانات إلزامية ناقصة: " + string.Join("، ", missing);
            }
            case "duplicate_resolved":
            {
                var contract = await db.FinancingContracts.Where(f => f.CaseId == c.Id).Select(f => f.ContractNumber).FirstOrDefaultAsync();
                if (contract is null) return null;
                var openDuplicate = await db.FinancingContracts.Where(f => f.ContractNumber == contract && f.CaseId != c.Id)
                    .Join(db.Cases, f => f.CaseId, x => x.Id, (f, x) => x)
                    .AnyAsync(x => x.Status != CaseStatus.Closed && x.Status != CaseStatus.Cancelled && x.Status != CaseStatus.Draft);
                return openDuplicate && string.IsNullOrWhiteSpace(c.DuplicateOverrideReason)
                    ? "العقد مرتبط بحالة مفتوحة أخرى في منشأتك؛ المتابعة تتطلب سبباً موثقاً." : null;
            }
            case "core_docs_verified":
            {
                string[] required = ["title_deed", "national_id", "financing_contract"];
                var docs = await db.Documents.Where(d => d.CaseId == c.Id && required.Contains(d.DocumentTypeKey)).ToListAsync();
                var problems = new List<string>();
                foreach (var key in required)
                {
                    var d = docs.FirstOrDefault(x => x.DocumentTypeKey == key);
                    var name = key switch { "title_deed" => "صك الملكية", "national_id" => "صورة الهوية الوطنية", _ => "عقد التمويل" };
                    if (d is null || d.Status != DocumentStatus.Verified) problems.Add($"{name} غير متحقق منه");
                    else if (d.ValidUntil is { } v && v < today) problems.Add($"{name} منتهي الصلاحية");
                }
                return problems.Count == 0 ? null : string.Join("، ", problems);
            }
            case "valuation_valid":
            {
                var v = await db.ValuationReports.Where(r => r.CaseId == c.Id && r.Status == ValuationStatus.Accepted)
                    .OrderByDescending(r => r.ReportDate).FirstOrDefaultAsync();
                if (v is null) return "لا يوجد تقرير تقييم معتمد.";
                if (v.ValidUntil < today) return "تقرير التقييم منتهي الصلاحية (أكثر من 90 يوماً).";
                return null;
            }
            case "legal_review_complete":
            {
                var m = await db.Mortgages.FirstOrDefaultAsync(x => x.CaseId == c.Id);
                return m is { LegalReviewStatus: LegalReviewStatus.Complete } ? null : "مراجعة القانونية للرهن لم تكتمل.";
            }
            case "analysis_complete":
            {
                var a = await db.Analyses.FirstOrDefaultAsync(x => x.CaseId == c.Id);
                return a is { Completed: true, NetMonthlyIncome: > 0 } ? null : "تحليل القدرة على السداد غير مكتمل.";
            }
            case "no_open_complaint":
            {
                var open = await db.Complaints.AnyAsync(x => x.CaseId == c.Id && x.Status != ComplaintStatus.Resolved && x.Status != ComplaintStatus.Closed);
                return open ? "توجد شكوى أو اعتراض مفتوح على الحالة." : null;
            }
            case "no_live_offer":
            {
                var live = await db.Offers.AnyAsync(o => o.CaseId == c.Id && (o.Status == OfferStatus.Sent || o.Status == OfferStatus.Countered) && o.ValidUntil >= today);
                return live ? "يوجد عرض قائم للمالك لم تنتهِ صلاحيته." : null;
            }
            case "consent_recorded":
            {
                var ok = await db.ConsentRecords.AnyAsync(x => x.CaseId == c.Id && x.Kind == "offer_acceptance" && x.OtpVerifiedAt != null);
                return ok ? null : "لا يوجد سجل موافقة من المالك مؤكد برمز تحقق.";
            }
            case "agreement_ready":
            {
                var a = await db.Agreements.Where(x => x.CaseId == c.Id && x.Status == AgreementStatus.PendingActivation).FirstOrDefaultAsync();
                if (a is null) return "لا يوجد اتفاق بانتظار التفعيل.";
                var missing = new List<string>();
                if (!a.LegalReviewDone) missing.Add("مراجعة القانونية");
                if (!a.ScheduleCreated) missing.Add("إنشاء جدول السداد");
                return missing.Count == 0 ? null : "خطوات تفعيل ناقصة: " + string.Join("، ", missing);
            }
            case "open_breach_review":
            {
                var open = await db.BreachReviews.AnyAsync(b => b.CaseId == c.Id && b.Status == BreachStatus.Open);
                return open ? null : "لا توجد مراجعة إخلال مفتوحة.";
            }
            case "all_installments_matched":
            {
                var agreement = await db.Agreements.FirstOrDefaultAsync(a => a.CaseId == c.Id && a.Status == AgreementStatus.Active);
                if (agreement is null) return "لا يوجد اتفاق نشط.";
                var unpaid = await db.Installments.CountAsync(i => i.AgreementId == agreement.Id && i.Status != InstallmentStatus.Matched && i.Status != InstallmentStatus.Waived);
                return unpaid == 0 ? null : $"يوجد {unpaid} قسط غير مطابق بعد.";
            }
            case "owner_sale_consent":
            {
                var ok = await db.ConsentRecords.AnyAsync(x => x.CaseId == c.Id && x.Kind == "sale_consent");
                return ok ? null : "لا توجد موافقة موثقة من المالك على البيع.";
            }
            case "reconciliation_approved":
            {
                var r = await db.Reconciliations.Where(x => x.CaseId == c.Id).OrderByDescending(x => x.CreatedAt).FirstOrDefaultAsync();
                if (r is null || r.Status != ReconciliationStatus.Approved) return "التسوية المالية غير معتمدة.";
                if (r.Difference != 0 && string.IsNullOrWhiteSpace(r.DifferenceExplanation)) return "فرق غير مفسَّر في المطابقة.";
                return null;
            }
            case "closure_documents_ready":
            {
                var pending = await db.ClosureDocuments.AnyAsync(d => d.CaseId == c.Id && d.BlocksClosure && d.Status != ClosureDocumentStatus.Ready);
                return pending ? "مستندات الإغلاق (المخالصة وفك الرهن) غير جاهزة." : null;
            }
            default:
                throw new InvalidOperationException($"Unknown guard {guard}");
        }
    }

    public async Task<List<string>> MissingIntakeAsync(Case c)
    {
        var missing = new List<string>();
        var primary = await db.Parties.FirstOrDefaultAsync(p => p.CaseId == c.Id && p.IsPrimary);
        if (primary is null) missing.Add("المالك الأساسي");
        else
        {
            if (primary.NationalIdEnc is null) missing.Add("رقم هوية المالك");
            if (primary.PhoneEnc is null) missing.Add("جوال المالك");
        }
        if (!await db.FinancingContracts.AnyAsync(f => f.CaseId == c.Id)) missing.Add("بيانات العقد");
        if (!await db.Properties.AnyAsync(p => p.CaseId == c.Id)) missing.Add("بيانات العقار");
        if (!await db.Mortgages.AnyAsync(p => p.CaseId == c.Id)) missing.Add("بيانات الرهن");
        if (!await db.DebtSnapshots.AnyAsync(d => d.CaseId == c.Id && d.IsCurrent)) missing.Add("المديونية");
        return missing;
    }
}
