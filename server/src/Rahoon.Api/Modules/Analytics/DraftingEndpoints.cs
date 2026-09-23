using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Rahoon.Api.Infrastructure.Http;
using Rahoon.Api.Infrastructure.Persistence;
using Rahoon.Api.Infrastructure.Tenancy;
using Rahoon.Api.Infrastructure.Time;
using Rahoon.Api.Modules.Audit;
using Rahoon.Api.Modules.Cases;
using Rahoon.Api.Modules.Complaints;
using Rahoon.Api.Modules.Identity;
using Rahoon.Api.Modules.Solutions;

namespace Rahoon.Api.Modules.Analytics;

public sealed record CreateDraftRequest(string Kind);
public sealed record EditDraftRequest(string Body, string? Note);
public sealed record DraftReasonRequest(string Reason);
public sealed record AttachDraftRequest(string Target);

public sealed record FilledValue(string Placeholder, string Value, string SourceType, string SourceRef);
public sealed record DraftFlag(int ClauseNo, string Text, string Message, string Severity, string SuggestedAction);

/// <summary>
/// O04 drafting assist. Deterministic: fills an approved template from case facts and flags conflicts by rule —
/// no language model is called and no clause outside the template is ever added. A draft is a suggestion until a
/// human edits or approves it (with step-up); only an approved draft can be attached.
/// </summary>
public static class DraftingEndpoints
{
    public const string ModelVersion = "DRAFT-template-v1 (تعبئة حتمية من القالب — دون نموذج لغوي)";
    private const string ImmediateTermination = "يحق للطرف الأول إنهاء الاتفاق فوراً عند أي إخلال.";

    public static void Map(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/cases/{reference}/drafts").RequireOrg(OrganizationKind.Lender).RequirePermission(P.CaseView)
            .RequireAnyPermission(P.AgreementPrepare, P.ReferralInitiate);
        g.MapGet("", List);
        g.MapPost("", Create).Idempotent();
        g.MapGet("/{id:guid}", Get);
        g.MapPut("/{id:guid}", Edit).Idempotent();
        g.MapPost("/{id:guid}/reject", Reject).Idempotent();
        g.MapPost("/{id:guid}/approve", Approve).Idempotent();
        g.MapPost("/{id:guid}/attach", Attach).Idempotent();
    }

    private static object View(DocumentDraft d, List<DocumentDraftVersion> versions)
    {
        var flags = JsonSerializer.Deserialize<List<DraftFlag>>(d.FlagsJson, JsonOptions.Web) ?? [];
        var open = flags.Where(f => d.CurrentBody.Contains(f.Text)).ToList();
        return new
        {
            d.Id, d.Kind, d.TemplateCode, d.TemplateVersion, status = d.Status.ToString(), d.Version,
            tag = d.Status switch { DraftStatus.Unreviewed => "مسودة آلية · لم تُراجع", DraftStatus.Edited => "مسودة معدّلة يدوياً · بانتظار الاعتماد", DraftStatus.Approved => "نص معتمد", _ => "مسودة مرفوضة" },
            isSuggestion = d.Status != DraftStatus.Approved,
            body = d.CurrentBody, generatedBody = d.GeneratedBody,
            filledValues = JsonSerializer.Deserialize<List<FilledValue>>(d.FilledValuesJson, JsonOptions.Web),
            flags, unresolvedFlags = open.Count,
            ai = new
            {
                modelVersion = d.ModelVersion, inputs = JsonDocument.Parse(d.InputsJson).RootElement, confidence = d.Confidence, limitations = d.Limitations,
                humanReview = d.HumanReview, reviewReason = d.ReviewReason,
            },
            d.ApprovedAt, d.RejectionReason, d.AttachedAt, d.AttachedTo,
            versions = versions.OrderBy(v => v.VersionNo).Select(v => new { v.VersionNo, v.ByLabel, v.At, v.Note }),
            auditNote = "يُسجَّل: المسودة الآلية، كل تعديل بشري، ومن اعتمد النص النهائي.",
        };
    }

    private static async Task<IResult> List(string reference, CaseAccess access, RahoonDbContext db)
    {
        var c = await access.GetAsync(reference, track: false);
        var rows = await db.Set<DocumentDraft>().AsNoTracking().Where(d => d.CaseId == c.Id).OrderByDescending(d => d.CreatedAt)
            .Select(d => new { d.Id, d.Kind, d.TemplateCode, status = d.Status.ToString(), d.CreatedAt, d.ApprovedAt, d.AttachedAt }).ToListAsync();
        return Results.Ok(new { items = rows });
    }

    private static async Task<IResult> Get(string reference, Guid id, CaseAccess access, RahoonDbContext db)
    {
        var c = await access.GetAsync(reference, track: false);
        var d = await db.Set<DocumentDraft>().AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && x.CaseId == c.Id) ?? throw new NotFoundException();
        var versions = await db.Set<DocumentDraftVersion>().AsNoTracking().Where(v => v.DraftId == d.Id).ToListAsync();
        return Results.Ok(View(d, versions));
    }

    private static async Task<IResult> Create(string reference, CreateDraftRequest req, CaseAccess access, RahoonDbContext db, RequestContext rc, IClock clock, AuditLog audit)
    {
        new Validator().Require(req.Kind is "agreement_reschedule" or "pre_referral_notice", "kind", "نوع المسودة: اتفاق إعادة جدولة أو إشعار قبل الإحالة.").ThrowIfInvalid();
        if (req.Kind == "agreement_reschedule" && !rc.Has(P.AgreementPrepare)) throw new ForbiddenException();
        if (req.Kind == "pre_referral_notice" && !rc.Has(P.ReferralInitiate)) throw new ForbiddenException();
        await using var tx = await db.Database.BeginTransactionAsync();
        var c = await access.GetAsync(reference, track: false);
        var generated = req.Kind == "agreement_reschedule" ? await AgreementDraftAsync(db, c) : await NoticeDraftAsync(db, c);
        var d = new DocumentDraft
        {
            OrganizationId = c.OrganizationId, CaseId = c.Id, Kind = req.Kind, TemplateCode = generated.Template, TemplateVersion = generated.TemplateVersion,
            ModelVersion = ModelVersion, GeneratedBody = generated.Body, CurrentBody = generated.Body,
            FilledValuesJson = JsonSerializer.Serialize(generated.Values, JsonOptions.Web), FlagsJson = JsonSerializer.Serialize(generated.Flags, JsonOptions.Web),
            InputsJson = JsonSerializer.Serialize(generated.Inputs, JsonOptions.Web),
            Confidence = $"قاعدي: {generated.Values.Count} قيم مُعبأة من مصادرها · {generated.Flags.Count} تعارض مُعلَّم. لا نسبة دقة إحصائية.",
            Limitations = "المساعد لا يضيف شروطاً قانونية جديدة؛ يملأ القالب المعتمد ويعلّم التعارضات بقواعد ثابتة. المسؤولية القانونية عن النص بشرية.",
            CreatedByUserId = rc.UserId,
        };
        db.Set<DocumentDraft>().Add(d);
        db.Set<DocumentDraftVersion>().Add(new DocumentDraftVersion { OrganizationId = c.OrganizationId, DraftId = d.Id, VersionNo = 1, Body = d.GeneratedBody, ByUserId = rc.UserId, ByLabel = "المساعد (تعبئة القالب)", At = clock.UtcNow, Note = "مسودة آلية" });
        await audit.RecordAsync(new AuditEntry("draft.generated", $"مسودة مقترحة: {(req.Kind == "agreement_reschedule" ? "اتفاق إعادة الجدولة" : "إشعار قبل الإحالة")}", c.Id, c.Reference,
            Detail: $"{ModelVersion} · القالب {generated.Template} v{generated.TemplateVersion} · تعارضات {generated.Flags.Count}", OrganizationId: c.OrganizationId));
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Results.Ok(View(d, []));
    }

    private sealed record Generated(string Template, int TemplateVersion, string Body, List<FilledValue> Values, List<DraftFlag> Flags, List<object> Inputs);

    private static async Task<Generated> AgreementDraftAsync(RahoonDbContext db, Case c)
    {
        SolutionStatus[] usable = [SolutionStatus.Approved, SolutionStatus.Offered, SolutionStatus.Accepted, SolutionStatus.Countered, SolutionStatus.PendingApproval, SolutionStatus.InReview];
        var v = await db.Solutions.AsNoTracking().Where(s => s.CaseId == c.Id && s.Kind == SolutionKind.Reschedule && usable.Contains(s.Status)).OrderByDescending(s => s.VersionNo).FirstOrDefaultAsync()
                ?? throw new ConflictException("no_solution", "لا يوجد حل إعادة جدولة لتعبئة الاتفاق منه.");
        var counter = await db.NegotiationEntries.AsNoTracking().Where(e => e.CaseId == c.Id && e.Kind == NegotiationKind.OwnerCounter && e.RequestedDueDay != null)
            .OrderByDescending(e => e.At).FirstOrDefaultAsync();
        var day = counter?.RequestedDueDay ?? v.FirstDueDate.Day;
        var values = new List<FilledValue>
        {
            new("{عدد_الأقساط}", $"{v.TermMonths} قسطاً شهرياً", "solution", $"الحل v{v.VersionNo}"),
            new("{القسط}", $"{v.InstallmentAmount:N2} ريال", "solution", $"الحل v{v.VersionNo}"),
            new("{يوم_الاستحقاق}", day.ToString(), counter is null ? "solution" : "owner_request",
                counter is null ? $"الحل v{v.VersionNo}" : $"طلب المالك {counter.At.ToOffset(TimeSpan.FromHours(3)):yyyy-MM-dd}"),
            new("{تاريخ_البدء}", v.FirstDueDate.ToString("yyyy-MM-dd"), "solution", $"الحل v{v.VersionNo}"),
            new("{أقساط_الإخلال}", v.BreachMissedConsecutive.ToString(), "policy", "سياسة المنشأة"),
            new("{مهلة_التصحيح}", v.BreachCureDays.ToString(), "policy", "سياسة المنشأة + البند 5"),
        };
        // TPL-AGR-02 v5 (approved template text; clause 7 is the legacy wording the checker is designed to catch).
        var body = string.Join("\n\n",
            $"البند 3 — الأقساط: يلتزم الطرف الثاني بسداد {values[0].Value} قيمة كل منها {values[1].Value}، تستحق في اليوم {values[2].Value} من كل شهر ابتداءً من {values[3].Value}.",
            $"البند 5 — التأخر: في حال تأخر الطرف الثاني عن سداد {values[4].Value} أقساط متتالية، يتواصل الطرف الأول معه ويمنحه مهلة تصحيح مدتها {values[5].Value} يوماً قبل مراجعة الاتفاق.",
            $"البند 7 — الإنهاء: {ImmediateTermination}");
        var flags = new List<DraftFlag>();
        if (v.BreachCureDays > 0)
            flags.Add(new(7, ImmediateTermination, "يتعارض مع البند 5 ومع سياسة المنشأة (مهلة تصحيح قبل أي مراجعة) — مقترح حذفه", "blocking", "delete"));
        var inputs = new List<object>
        {
            new { name = "solution", version = $"v{v.VersionNo}", asOf = v.LockedAt ?? v.PreparedAt },
            new { name = "owner_request", version = counter is null ? "—" : "counteroffer", asOf = counter?.At },
            new { name = "org_policy", version = "BreachCureDays", asOf = (DateTimeOffset?)null },
        };
        return new Generated("TPL-AGR-02", 5, body, values, flags, inputs);
    }

    private static async Task<Generated> NoticeDraftAsync(RahoonDbContext db, Case c)
    {
        var org = await db.Organizations.AsNoTracking().Where(o => o.Id == c.OrganizationId).Select(o => o.NameAr).FirstAsync();
        var days = await OperationalSettings.DaysAsync(db, c.OrganizationId, OperationalSettings.ObjectionDays, 15);
        var template = await db.Templates.AsNoTracking().Where(t => t.Code == Referral.ReferralEndpoints.NoticeTemplate).OrderByDescending(t => t.VersionNo).FirstOrDefaultAsync();
        var values = new List<FilledValue>
        {
            new("{المصرف}", org, "organization", "بيانات المنشأة"),
            new("{المهلة}", CaseDisplay.Days(days), "policy", "إعداد «مهلة الاعتراض قبل الإحالة»"),
            new("{تاريخ_انتهاء_المهلة}", "يُحسب عند الإرسال", "computed", "تاريخ الإرسال + المهلة"),
        };
        var body = (template?.BodyAr ?? Referral.ReferralEndpoints.DefaultNoticeBody).Replace("{المصرف}", org).Replace("{المهلة}", CaseDisplay.Days(days));
        var flags = new List<DraftFlag>();
        if (await db.Complaints.AnyAsync(x => x.CaseId == c.Id && x.Status != ComplaintStatus.Resolved && x.Status != ComplaintStatus.Closed))
            flags.Add(new(0, "{تاريخ_انتهاء_المهلة}", "توجد شكوى مفتوحة؛ الإحالة محجوبة حتى معالجتها — راجع التوقيت قبل الإرسال", "warning", "review"));
        var inputs = new List<object> { new { name = "template", version = $"{template?.Code ?? "default"} v{template?.VersionNo ?? 1}", asOf = template?.UpdatedAt }, new { name = "objection_days", version = days.ToString(), asOf = (DateTimeOffset?)null } };
        return new Generated(template?.Code ?? Referral.ReferralEndpoints.NoticeTemplate, template?.VersionNo ?? 1, body, values, flags, inputs);
    }

    private static async Task<(Case, DocumentDraft)> LoadAsync(string reference, Guid id, CaseAccess access, RahoonDbContext db)
    {
        var c = await access.GetAsync(reference, track: false);
        var d = await db.Set<DocumentDraft>().FirstOrDefaultAsync(x => x.Id == id && x.CaseId == c.Id) ?? throw new NotFoundException();
        return (c, d);
    }

    private static async Task<IResult> Edit(string reference, Guid id, EditDraftRequest req, CaseAccess access, RahoonDbContext db, RequestContext rc, IClock clock, AuditLog audit)
    {
        new Validator().Require(!string.IsNullOrWhiteSpace(req.Body) && req.Body.Length <= 20_000, "body", "النص مطلوب (حتى 20,000 حرف).").ThrowIfInvalid();
        await using var tx = await db.Database.BeginTransactionAsync();
        var (c, d) = await LoadAsync(reference, id, access, db);
        if (d.Status is DraftStatus.Approved or DraftStatus.Rejected) throw new ConflictException("draft_closed", "المسودة معتمدة أو مرفوضة؛ أنشئ مسودة جديدة.");
        var n = await db.Set<DocumentDraftVersion>().CountAsync(v => v.DraftId == d.Id) + 1;
        d.CurrentBody = req.Body;
        d.Status = DraftStatus.Edited;
        db.Set<DocumentDraftVersion>().Add(new DocumentDraftVersion { OrganizationId = d.OrganizationId, DraftId = d.Id, VersionNo = n, Body = req.Body, ByUserId = rc.UserId, ByLabel = rc.UserName, At = clock.UtcNow, Note = req.Note?.Trim() });
        await audit.RecordAsync(new AuditEntry("draft.edited", $"تحرير يدوي للمسودة (نسخة {n})", c.Id, c.Reference, Detail: req.Note?.Trim(), OrganizationId: c.OrganizationId));
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Results.Ok(View(d, await db.Set<DocumentDraftVersion>().AsNoTracking().Where(v => v.DraftId == d.Id).ToListAsync()));
    }

    private static async Task<IResult> Reject(string reference, Guid id, DraftReasonRequest req, CaseAccess access, RahoonDbContext db, RequestContext rc, IClock clock, AuditLog audit)
    {
        new Validator().Require(!string.IsNullOrWhiteSpace(req.Reason) && req.Reason.Trim().Length >= 5, "reason", "اذكر سبب رفض المسودة.").ThrowIfInvalid();
        await using var tx = await db.Database.BeginTransactionAsync();
        var (c, d) = await LoadAsync(reference, id, access, db);
        if (d.Status is DraftStatus.Approved or DraftStatus.Rejected) throw new ConflictException("draft_closed", "تم القرار على هذه المسودة.");
        d.Status = DraftStatus.Rejected;
        d.RejectionReason = req.Reason.Trim();
        d.HumanReview = HumanReview.NotUsed;
        d.ReviewReason = d.RejectionReason;
        await audit.RecordAsync(new AuditEntry("draft.rejected", "رفض المسودة المقترحة", c.Id, c.Reference, Reason: d.RejectionReason, Detail: $"الرأي البشري: {HumanReview.NotUsed}", OrganizationId: c.OrganizationId));
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Results.Ok(new { status = d.Status.ToString(), humanReview = d.HumanReview });
    }

    private static async Task<IResult> Approve(string reference, Guid id, DraftReasonRequest req, CaseAccess access, RahoonDbContext db, RequestContext rc, IClock clock, AuditLog audit)
    {
        new Validator().Require(!string.IsNullOrWhiteSpace(req.Reason) && req.Reason.Trim().Length >= 5, "reason", "سبب الاعتماد إلزامي.").ThrowIfInvalid();
        EndpointAccess.EnsureStepUp(rc, clock);
        await using var tx = await db.Database.BeginTransactionAsync();
        var (c, d) = await LoadAsync(reference, id, access, db);
        if (d.Status is DraftStatus.Approved or DraftStatus.Rejected) throw new ConflictException("draft_closed", "تم القرار على هذه المسودة.");
        var flags = JsonSerializer.Deserialize<List<DraftFlag>>(d.FlagsJson, JsonOptions.Web) ?? [];
        var unresolved = flags.Where(f => f.Severity == "blocking" && d.CurrentBody.Contains(f.Text)).Select(f => $"البند {f.ClauseNo}: {f.Message}").ToList();
        if (unresolved.Count > 0)
            throw new DomainException("unresolved_flags", "عالج التعارضات المُعلَّمة قبل الاعتماد.", StatusCodes.Status422UnprocessableEntity, unresolved);
        d.Status = DraftStatus.Approved;
        d.ApprovedByUserId = rc.UserId;
        d.ApprovedAt = clock.UtcNow;
        // Handoff mapping: approved unedited ≈ agree; edited then approved ≈ override (the human text replaces the suggestion).
        d.HumanReview = d.CurrentBody == d.GeneratedBody ? HumanReview.Agree : HumanReview.Override;
        d.ReviewReason = req.Reason.Trim();
        await audit.RecordAsync(new AuditEntry("draft.approved", "اعتماد النص النهائي بعد المراجعة", c.Id, c.Reference, Reason: d.ReviewReason,
            Detail: $"الرأي البشري: {d.HumanReview} · تأكيد برمز التحقق", OrganizationId: c.OrganizationId));
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Results.Ok(new { status = d.Status.ToString(), humanReview = d.HumanReview });
    }

    private static async Task<IResult> Attach(string reference, Guid id, AttachDraftRequest req, CaseAccess access, RahoonDbContext db, IClock clock, AuditLog audit)
    {
        new Validator().Require(!string.IsNullOrWhiteSpace(req.Target) && req.Target.Length <= 200, "target", "حدد ما تُرفق به المسودة (مثل: الاتفاق AGR-…).").ThrowIfInvalid();
        await using var tx = await db.Database.BeginTransactionAsync();
        var (c, d) = await LoadAsync(reference, id, access, db);
        if (d.Status != DraftStatus.Approved)
            throw new ConflictException("draft_not_approved", "لا تُرفق المسودة قبل مراجعتها واعتمادها من شخص؛ هي اقتراح فقط.");
        d.AttachedAt = clock.UtcNow;
        d.AttachedTo = req.Target.Trim();
        await audit.RecordAsync(new AuditEntry("draft.attached", $"إرفاق النص المعتمد بـ {d.AttachedTo}", c.Id, c.Reference, Detail: "لا إرسال آلي للمالك", OrganizationId: c.OrganizationId));
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Results.Ok(new { attachedTo = d.AttachedTo, d.AttachedAt });
    }
}
