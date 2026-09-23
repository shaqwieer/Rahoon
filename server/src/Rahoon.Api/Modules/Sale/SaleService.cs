using Microsoft.EntityFrameworkCore;
using Rahoon.Api.Infrastructure.Http;
using Rahoon.Api.Infrastructure.Persistence;
using Rahoon.Api.Infrastructure.Tenancy;
using Rahoon.Api.Infrastructure.Time;
using Rahoon.Api.Modules.Assessment;
using Rahoon.Api.Modules.Audit;
using Rahoon.Api.Modules.Cases;
using Rahoon.Api.Modules.Communications;
using Rahoon.Api.Modules.Complaints;
using Rahoon.Api.Modules.Identity;
using Rahoon.Api.Modules.Providers;
using Rahoon.Api.Modules.Solutions;

namespace Rahoon.Api.Modules.Sale;

public sealed record SaleCheck(bool Ok, string Title, string Memo, string? Link);

/// <summary>Shared voluntary-sale rules used by the lender, owner, broker and approval endpoints.</summary>
public sealed class SaleService(RahoonDbContext db, RequestContext rc, IClock clock, CaseWorkflow workflow, AuditLog audit, Notifier notifier)
{
    public static readonly string[] StageNames = ["القرار", "موافقة المالكة", "التجهيز", "العرض المضبوط", "العروض", "الاعتماد", "نقل الملكية والتسوية"];

    public static readonly SaleStatus[] OpenStatuses =
        [SaleStatus.Requested, SaleStatus.PendingDecision, SaleStatus.AwaitingConsent, SaleStatus.Active, SaleStatus.OfferApproved];

    /// <summary>Offer states in which the owner has accepted: consent fulfilled, withdrawal no longer available.</summary>
    public static readonly BuyerOfferStatus[] AcceptedStates = [BuyerOfferStatus.OwnerAccepted, BuyerOfferStatus.PendingApproval, BuyerOfferStatus.Approved];

    public static readonly BuyerOfferStatus[] LiveOfferStates = [BuyerOfferStatus.Received, BuyerOfferStatus.SharedWithOwner];

    public static bool IsOpen(SaleStatus s) => OpenStatuses.Contains(s);

    /// <summary>The case's latest sale track (open or historical), or null.</summary>
    public async Task<VoluntarySale?> LatestAsync(Guid caseId, bool track = false)
    {
        var q = db.Set<VoluntarySale>().Where(s => s.CaseId == caseId);
        if (!track) q = q.AsNoTracking();
        return await q.OrderByDescending(s => s.CreatedAt).FirstOrDefaultAsync();
    }

    public async Task<VoluntarySale> RequireOpenAsync(Guid caseId, bool track = false)
    {
        var s = await LatestAsync(caseId, track);
        if (s is null || !IsOpen(s.Status)) throw new DomainException("no_open_sale", "لا يوجد مسار بيع طوعي مفتوح لهذه الحالة.", StatusCodes.Status409Conflict);
        return s;
    }

    public async Task<SaleConsent?> ConsentAsync(Guid saleId, bool track = false)
    {
        var q = db.Set<SaleConsent>().Where(x => x.SaleId == saleId);
        if (!track) q = q.AsNoTracking();
        return await q.OrderByDescending(x => x.VersionNo).FirstOrDefaultAsync();
    }

    /// <summary>A signed, unexpired scoped consent — required for preparation, listing, broker and offers.</summary>
    public async Task<SaleConsent> RequireSignedConsentAsync(VoluntarySale s)
    {
        var consent = await ConsentAsync(s.Id);
        if (consent is null || s.Status == SaleStatus.AwaitingConsent)
            throw new DomainException("consent_required", "بانتظار موافقة المالكة المكتوبة بنطاق البيع؛ يُحجب التجهيز والعرض وتكليف الوسيط قبلها.", StatusCodes.Status409Conflict);
        if (consent.Status == SaleConsentStatus.Withdrawn) throw new DomainException("consent_withdrawn", "انسحبت المالكة من البيع.", StatusCodes.Status409Conflict);
        if (consent.Status == SaleConsentStatus.Expired || consent.MandateEnd < clock.TodayRiyadh)
            throw new DomainException("mandate_expired", $"انتهت مدة التفويض في {consent.MandateEnd:yyyy-MM-dd}.", StatusCodes.Status409Conflict);
        return consent;
    }

    public async Task<bool> OwnerAcceptedAnyAsync(Guid saleId) =>
        await db.Set<BuyerOffer>().AnyAsync(o => o.SaleId == saleId && AcceptedStates.Contains(o.Status));

    public async Task<bool> CanWithdrawAsync(VoluntarySale s) =>
        s.Status is SaleStatus.Requested or SaleStatus.PendingDecision or SaleStatus.AwaitingConsent or SaleStatus.Active
        && !await OwnerAcceptedAnyAsync(s.Id);

    /// <summary>Allocates VS-YYYY-NNNN (buyer-facing; unrelated to the case reference).</summary>
    public async Task<string> NextBuyerReferenceAsync(int year)
    {
        var key = $"sale:{year}";
        var value = await db.Database.SqlQuery<long>($"""
            INSERT INTO cases.reference_counters (key, value) VALUES ({key}, 101)
            ON CONFLICT (key) DO UPDATE SET value = cases.reference_counters.value + 1
            RETURNING value AS "Value"
            """).ToListAsync();
        return $"VS-{year}-{value[0]:D4}";
    }

    /// <summary>
    /// Allocates ASG-YYYY-NNNN from the shared counter key <c>assignment:YYYY</c> (starts at 5001 to stay clear
    /// of seeded references). Integration point with the provider-portal assignment endpoints.
    /// </summary>
    public async Task<string> NextAssignmentReferenceAsync(int year)
    {
        var key = $"assignment:{year}";
        var value = await db.Database.SqlQuery<long>($"""
            INSERT INTO cases.reference_counters (key, value) VALUES ({key}, 5001)
            ON CONFLICT (key) DO UPDATE SET value = cases.reference_counters.value + 1
            RETURNING value AS "Value"
            """).ToListAsync();
        return $"ASG-{year}-{value[0]:D4}";
    }

    public async Task<ValuationReport?> ValidValuationAsync(Guid caseId) =>
        await db.ValuationReports.AsNoTracking().Where(r => r.CaseId == caseId && r.Status == ValuationStatus.Accepted && r.ValidUntil >= clock.TodayRiyadh)
            .OrderByDescending(r => r.ReportDate).FirstOrDefaultAsync();

    /// <summary>L27 «الشروط المسبقة» rows 1–4 (row 5, the scoped consent, is requested after approval).</summary>
    public async Task<List<SaleCheck>> DecisionPrerequisitesAsync(Case c, VoluntarySale? s)
    {
        var valuation = await ValidValuationAsync(c.Id);
        var mortgage = await db.Mortgages.AsNoTracking().FirstOrDefaultAsync(m => m.CaseId == c.Id);
        var legalName = mortgage?.LegalReviewedByUserId is { } lu ? await db.Users.Where(u => u.Id == lu).Select(u => u.FullName).FirstOrDefaultAsync() : null;
        var openComplaint = await db.Complaints.AnyAsync(x => x.CaseId == c.Id && x.Status != ComplaintStatus.Resolved && x.Status != ComplaintStatus.Closed);
        var requestOk = s is not null && IsOpen(s.Status) && s.RequestConsentRecordId is not null;
        return
        [
            new(requestOk, "طلب صريح من المالكة", requestOk ? $"{s!.RequestedAt.ToOffset(TimeSpan.FromHours(3)):yyyy-MM-dd} · عبر البوابة" : "لا يوجد طلب موثق من المالك عبر البوابة", requestOk ? "request" : null),
            new(valuation is not null, "تقييم صالح",
                valuation is null ? "لا يوجد تقرير تقييم معتمد صالح (≤ 90 يوماً)" : $"{valuation.MarketValue:N0} ر.س · {valuation.ValuerName} · {valuation.ReportDate:yyyy-MM-dd}", valuation is null ? null : "valuation"),
            new(mortgage is { LegalReviewStatus: LegalReviewStatus.Complete }, "مراجعة الرهن والقيود",
                mortgage is { LegalReviewStatus: LegalReviewStatus.Complete } ? $"{(string.IsNullOrWhiteSpace(mortgage.OtherEncumbrances) ? "لا قيود لاحقة" : mortgage.OtherEncumbrances)} · {legalName ?? "القانونية"}" : "مراجعة القانونية للرهن لم تكتمل", "legal_note"),
            new(!openComplaint, "لا شكاوى مفتوحة", openComplaint ? "توجد شكوى أو اعتراض مفتوح على الحالة" : "—", "audit"),
        ];
    }

    public static int Stage(VoluntarySale s, IReadOnlyCollection<BuyerOffer> offers)
    {
        switch (s.Status)
        {
            case SaleStatus.Requested or SaleStatus.PendingDecision or SaleStatus.DecisionRejected: return 0;
            case SaleStatus.AwaitingConsent: return 1;
            case SaleStatus.OfferApproved or SaleStatus.Completed: return 6;
            case SaleStatus.Active:
                if (offers.Any(o => o.Status is BuyerOfferStatus.OwnerAccepted or BuyerOfferStatus.PendingApproval)) return 5;
                if (offers.Count > 0) return 4;
                if (s.BrokerAssignmentId is not null && s.ListingStatus == ListingStatus.ComplianceReviewed) return 3;
                return 2;
            default: return 0;
        }
    }

    public static string StatusLabel(SaleStatus s) => s switch
    {
        SaleStatus.Requested => "طلب من المالك",
        SaleStatus.PendingDecision => "بانتظار اعتماد القرار",
        SaleStatus.AwaitingConsent => "بانتظار موافقة المالك",
        SaleStatus.Active => "قيد التنفيذ",
        SaleStatus.OfferApproved => "عرض معتمد — التنفيذ",
        SaleStatus.Completed => "اكتمل البيع",
        SaleStatus.Withdrawn => "انسحب المالك",
        SaleStatus.DecisionRejected => "لم يُعتمد فتح المسار",
        SaleStatus.Expired => "انتهى التفويض",
        _ => s.ToString(),
    };

    public static string PaymentLabel(BuyerPaymentMethod m) => m switch
    {
        BuyerPaymentMethod.Cash => "نقداً",
        BuyerPaymentMethod.FinancingPreapproved => "تمويل معتمد مبدئياً",
        _ => "تمويل — لم يُعتمد بعد",
    };

    public static string CertaintyLabel(OfferCertainty c) => c switch { OfferCertainty.High => "عالية", OfferCertainty.Medium => "متوسطة", _ => "منخفضة" };

    public static string OfferStatusLabel(BuyerOfferStatus s) => s switch
    {
        BuyerOfferStatus.Received => "مستلم",
        BuyerOfferStatus.SharedWithOwner => "معروض على المالك",
        BuyerOfferStatus.OwnerAccepted => "وافق المالك",
        BuyerOfferStatus.OwnerDeclined => "لم يوافق المالك",
        BuyerOfferStatus.PendingApproval => "بانتظار اعتماد المصرف",
        BuyerOfferStatus.Approved => "معتمد",
        BuyerOfferStatus.Rejected => "مرفوض",
        BuyerOfferStatus.Expired => "منتهي الصلاحية",
        BuyerOfferStatus.Superseded => "أُغلق",
        _ => "مسحوب",
    };

    public async Task<decimal> DebtAsync(Case c, VoluntarySale s)
    {
        var snap = await db.DebtSnapshots.AsNoTracking().Where(d => d.CaseId == c.Id && d.IsCurrent).OrderByDescending(d => d.AsOf).FirstOrDefaultAsync();
        return snap?.Total ?? s.OutstandingDebt ?? c.OutstandingAmount ?? 0;
    }

    /// <summary>Ends the broker's access to the sale file now (access derives from AccessExpiresAt, not the status).</summary>
    public async Task RevokeBrokerAsync(VoluntarySale s, string reason)
    {
        if (s.BrokerAssignmentId is not { } id) return;
        var a = await db.Assignments.FirstOrDefaultAsync(x => x.Id == id);
        if (a is null) return;
        var now = clock.UtcNow;
        if (a.AccessExpiresAt is null || a.AccessExpiresAt > now) a.AccessExpiresAt = now;
        if (a.Status is not (AssignmentStatus.Cancelled or AssignmentStatus.Closed)) a.Status = AssignmentStatus.Closed;
        db.AssignmentMessages.Add(new AssignmentMessage
        {
            OrganizationId = s.OrganizationId, AssignmentId = a.Id, AuthorUserId = rc.UserId, AuthorLabel = "رهون", AuthorSide = "lender",
            Body = $"انتهى الوصول إلى ملف البيع {s.BuyerReference}: {reason}", At = now,
        });
    }

    /// <summary>
    /// Owner withdrawal (D16), allowed until an offer is accepted. Returns the case to «حل مقترح» when the
    /// track is open (never to referral); before approval the pending decision is superseded. Caller owns the transaction.
    /// </summary>
    public async Task WithdrawAsync(Case c, VoluntarySale s, string reason)
    {
        if (!await CanWithdrawAsync(s))
            throw new DomainException("withdrawal_closed", "لم يعد الانسحاب متاحاً بعد قبول عرض شراء.", StatusCodes.Status409Conflict);

        var from = c.Status;
        // Ordering rule: the transition (which may audit a blocked attempt separately) precedes any audit append.
        if (c.Status == CaseStatus.VoluntarySale)
            await workflow.TransitionAsync(c, "sale_withdrawn", reason, CaseStatus.VoluntarySale, systemInitiated: true, evidence: [$"sale:{s.BuyerReference}"]);

        var now = clock.UtcNow;
        if (s.DecisionApprovalRequestId is { } reqId)
        {
            var req = await db.ApprovalRequests.FirstOrDefaultAsync(a => a.Id == reqId);
            if (req is { Status: ApprovalStatus.Pending }) { req.Status = ApprovalStatus.Superseded; req.DecidedAt = now; req.DecisionReason = "انسحاب المالك"; }
        }
        foreach (var o in await db.Set<BuyerOffer>().Where(o => o.SaleId == s.Id && LiveOfferStates.Contains(o.Status)).ToListAsync())
            o.Status = BuyerOfferStatus.Withdrawn;
        var consent = await ConsentAsync(s.Id, track: true);
        if (consent is { Status: SaleConsentStatus.Signed }) { consent.Status = SaleConsentStatus.Withdrawn; consent.WithdrawnAt = now; consent.WithdrawReason = reason; }
        await RevokeBrokerAsync(s, "انسحاب المالك");
        s.Status = SaleStatus.Withdrawn;
        s.ListingStatus = ListingStatus.Closed;
        s.ClosedAt = now;
        s.CloseReason = "withdrawn";

        await NotifyManagerAsync(c, "انسحب المالك من البيع الطوعي", reason, $"/cases/{c.Reference}/sale", "warn");
        await audit.RecordAsync(new AuditEntry("sale.withdrawn", "انسحاب المالك من البيع الطوعي", c.Id, c.Reference,
            CaseStatusInfo.Key(from), CaseStatusInfo.Key(c.Status), reason,
            Detail: from == CaseStatus.VoluntarySale ? "تعود الحالة إلى «حل مقترح»؛ سُحب وصول الوسيط وأُغلق العرض المضبوط." : "أُلغي طلب البيع قبل اعتماد القرار.",
            Evidence: [$"sale:{s.BuyerReference}"], OrganizationId: c.OrganizationId));
    }

    public async Task NotifyManagerAsync(Case c, string title, string? body, string link, string tone = "info")
    {
        var mgr = c.AssignedManagerId is null ? null : await db.Memberships.Where(m => m.Id == c.AssignedManagerId).Select(m => (Guid?)m.UserId).FirstOrDefaultAsync();
        if (mgr is { } u) notifier.Notify(u, c.OrganizationId, "sale", title, body, link, c.Id, tone);
    }

    public async Task NotifyOwnerAsync(Case c, string title, string? body, string link)
    {
        var owner = await db.OwnerAccesses.Where(o => o.CaseId == c.Id && o.UserId != null).Select(o => o.UserId).FirstOrDefaultAsync();
        if (owner is { } u) notifier.Notify(u, c.OrganizationId, "sale", title, body, link, c.Id);
    }

    /// <summary>
    /// Sale approvals go to a holder of sale.approve who neither prepared nor submitted the request, least loaded
    /// first. Solution approval-limit tiers do not apply to opening a sale (conflict #6); an offer that leaves a
    /// shortfall (lender recovers less than the debt) is a concession and requires a senior tier.
    /// </summary>
    public async Task<(Guid UserId, string Name, string Tier)?> ResolveApproverAsync(Guid orgId, IEnumerable<Guid> excluded, bool seniorOnly)
    {
        var ex = excluded.ToHashSet();
        string[] seniorRoles = [SystemRoles.SeniorApprover, SystemRoles.RiskCommittee];
        var candidates = await db.Memberships
            .Where(m => m.OrganizationId == orgId && m.Status == MembershipStatus.Active
                        && m.Roles.Any(r => r.Role!.Permissions.Any(p => p.PermissionKey == P.SaleApprove) && (!seniorOnly || seniorRoles.Contains(r.Role.Key))))
            .Select(m => new { m.UserId, m.User!.FullName, m.CreatedAt })
            .ToListAsync();
        candidates = candidates.Where(x => !ex.Contains(x.UserId)).ToList();
        if (candidates.Count == 0) return null;
        var ids = candidates.Select(x => x.UserId).ToList();
        var load = await db.ApprovalRequests.Where(a => a.Status == ApprovalStatus.Pending && a.AssignedApproverUserId != null && ids.Contains(a.AssignedApproverUserId.Value))
            .GroupBy(a => a.AssignedApproverUserId).Select(g => new { g.Key, N = g.Count() }).ToListAsync();
        var pick = candidates.OrderBy(x => load.FirstOrDefault(l => l.Key == x.UserId)?.N ?? 0).ThenBy(x => x.CreatedAt).First();
        return (pick.UserId, pick.FullName, seniorOnly ? "senior_approver" : "approver");
    }

    /// <summary>The 8-step preparation checklist (L29), created when the owner signs the scoped consent.</summary>
    public void CreatePrepItems(VoluntarySale s, SaleConsent consent)
    {
        var days = string.Join(" و", consent.VisitDays.Select(SaleTexts.VisitDayLabel));
        (string key, string title, PrepItemStatus status, string? memo, string resp)[] items =
        [
            ("signed_consent", "موافقة المالكة الموقعة", PrepItemStatus.Done, consent.SignedAt.ToOffset(TimeSpan.FromHours(3)).ToString("yyyy-MM-dd"), "owner"),
            ("deed_check", "صك الملكية ومطابقة البيانات", PrepItemStatus.NotStarted, null, "legal"),
            ("debt_letter", "خطاب المديونية للمشتري", PrepItemStatus.NotStarted, "يُصدر عند قبول عرض", "finance"),
            ("visit_times", "تحديد أوقات الزيارة", PrepItemStatus.Done, string.IsNullOrWhiteSpace(consent.VisitWindow) ? days : $"{days} {consent.VisitWindow}", "owner"),
            ("evacuation_plan", "خطة الإخلاء", PrepItemStatus.NotStarted, $"{s.EvacuationDays} يوماً بعد نقل الملكية", "owner_case_manager"),
            ("photography", "التصوير المعتمد", PrepItemStatus.NotStarted, "بموافقة المالكة، بلا أشخاص أو متعلقات", "broker"),
            ("inspection", "فحص فني اختياري", PrepItemStatus.NotStarted, "بطلب المشتري لاحقاً", "none"),
            ("listing_review", "مراجعة ملخص العرض", PrepItemStatus.NotStarted, "قبل المشاركة مع المشترين", "compliance"),
        ];
        var i = 0;
        foreach (var it in items)
            db.Set<SalePrepItem>().Add(new SalePrepItem
            {
                OrganizationId = s.OrganizationId, SaleId = s.Id, CaseId = s.CaseId, Key = it.key, Title = it.title, Status = it.status,
                Memo = it.memo, Responsible = it.resp, SortOrder = i++,
            });
    }

    public static string ResponsibleLabel(string r) => r switch
    {
        "owner" => "المالكة", "legal" => "القانونية", "finance" => "المالية", "broker" => "الوسيط", "compliance" => "الامتثال",
        "case_manager" => "مدير الحالة", "owner_case_manager" => "المالكة + مدير الحالة", _ => "—",
    };

    /// <summary>The 8-step execution timeline (L33), created when the bank approves the owner-accepted offer.</summary>
    public void CreateTrackingSteps(VoluntarySale s, BuyerOffer o, DateOnly ownerAcceptedOn, DateOnly approvedOn, string approverName, decimal ownerSurplus)
    {
        (string key, string title, string? memo, TrackingSource src, TrackingStatus st, DateOnly? actual)[] steps =
        [
            ("owner_offer_consent", $"موافقة المالكة على {o.Code}", "عبر البوابة برمز تحقق", TrackingSource.Internal, TrackingStatus.Done, ownerAcceptedOn),
            ("bank_approval", "اعتماد المصرف", approverName, TrackingSource.Internal, TrackingStatus.Done, approvedOn),
            ("debt_letter", "إصدار خطاب المديونية للمشتري", "صالح 30 يوماً", TrackingSource.Internal, TrackingStatus.NotStarted, null),
            ("sale_agreement", "توقيع اتفاقية البيع", "خارج المنصة · نسخة مرفوعة", TrackingSource.ExternalManual, TrackingStatus.NotStarted, null),
            ("payment_received", "استلام الثمن وسداد المديونية", "مرجع التحويل يُدخل عند الاستلام", TrackingSource.ExternalManual, TrackingStatus.NotStarted, null),
            ("lien_release", "فك الرهن ونقل الملكية", "لدى الجهة المختصة", TrackingSource.ExternalManual, TrackingStatus.NotStarted, null),
            ("surplus_transfer", "تحويل الفائض للمالكة", ownerSurplus > 0 ? $"{ownerSurplus:N0} تقديرياً" : "لا فائض متوقع", TrackingSource.ExternalManual, TrackingStatus.NotStarted, null),
            ("reconciliation", "التسوية المالية والإغلاق", "ينتقل إلى «بانتظار التسوية المالية»", TrackingSource.Internal, TrackingStatus.NotStarted, null),
        ];
        var i = 0;
        foreach (var st in steps)
            db.Set<SaleTrackingStep>().Add(new SaleTrackingStep
            {
                OrganizationId = s.OrganizationId, SaleId = s.Id, CaseId = s.CaseId, Key = st.key, Title = st.title, Memo = st.memo, Source = st.src,
                Status = st.st, ActualDate = st.actual, SortOrder = i++, EnteredByUserId = st.st == TrackingStatus.Done ? rc.UserId : null,
                EnteredAt = st.st == TrackingStatus.Done ? clock.UtcNow : null,
            });
    }
}
