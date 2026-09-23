using Microsoft.EntityFrameworkCore;
using Rahoon.Api.Infrastructure.Http;
using Rahoon.Api.Infrastructure.Persistence;
using Rahoon.Api.Infrastructure.Storage;
using Rahoon.Api.Infrastructure.Tenancy;
using Rahoon.Api.Infrastructure.Time;
using Rahoon.Api.Modules.Audit;
using Rahoon.Api.Modules.Communications;
using Rahoon.Api.Modules.Identity;

namespace Rahoon.Api.Modules.Ecosystem;

public sealed record ProviderDecisionBody(string Decision, string Message);
public sealed record ProviderStatusBody(string Action, string Reason);

/// <summary>
/// Platform provider registry (PA16) and registration review (V06b). Metadata only — no institution data and no
/// per-institution performance. Accepted providers appear in the platform directory; each institution then chooses
/// whom to add to its own directory. Institutions never see registration applications.
/// </summary>
public static class PlatformProviderEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/platform/providers").RequireOrg(OrganizationKind.Platform).RequireAnyPermission(P.PlatformInstitutions, P.PlatformDefaults);
        g.MapGet("", Registry);
        g.MapGet("/applications", Applications);
        g.MapGet("/applications/{profileId:guid}", Application);
        g.MapPost("/applications/{profileId:guid}/decision", Decide).Idempotent();
        g.MapGet("/applications/{profileId:guid}/documents/{licenseId:guid}/file", Download);
        g.MapPost("/{providerOrganizationId:guid}/status", ChangeStatus).Idempotent();
    }

    private static async Task<IResult> Registry(string? status, RahoonDbContext db, IClock clock)
    {
        var today = clock.TodayRiyadh;
        var q = db.Set<ProviderProfile>().AsNoTracking().Where(p => p.Status != ProviderRegistrationStatus.Draft);
        if (Enum.TryParse<ProviderRegistrationStatus>(status, true, out var st)) q = q.Where(p => p.Status == st);
        var profiles = await q.OrderBy(p => p.LegalName).ToListAsync();
        var ids = profiles.Select(p => p.Id).ToList();
        var practice = await db.Set<ProviderLicense>().AsNoTracking().Where(l => ids.Contains(l.ProviderProfileId) && l.IsCurrent && l.Kind == LicenseRules.PracticeLicense)
            .ToDictionaryAsync(l => l.ProviderProfileId, l => l.ExpiresOn);
        return Results.Ok(new
        {
            note = "دليل المنصة يضم المقبولين بعد مراجعة الامتثال؛ كل منشأة تختار من تضيف. لا يُعرض أداء منشأة لأخرى.",
            items = profiles.Select(p =>
            {
                var exp = practice.GetValueOrDefault(p.Id);
                var (icon, tone) = LicenseRules.Icon(LicenseRules.State(exp, today));
                var (key, label, sTone, _) = LicenseRules.DirectoryStatus(p.Status, exp, today);
                return new
                {
                    id = p.ProviderOrganizationId, profileId = p.Id, name = p.LegalName, type = LicenseRules.TypeLabel(p.ProviderType), p.City, applicationRef = p.ApplicationRef,
                    registration = p.Status.ToString(), registrationLabel = ProviderOnboardingEndpoints.StatusLabel(p.Status),
                    license = LicenseRules.Text(exp, today), licenseState = ProviderDirectory.Snake(LicenseRules.State(exp, today)), licenseIcon = icon, licenseTone = tone,
                    directoryStatus = key, directoryLabel = label, directoryTone = sTone, acceptedAt = p.AcceptedAt,
                };
            }),
        });
    }

    private static async Task<IResult> Applications(RahoonDbContext db)
    {
        var rows = await db.Set<ProviderProfile>().AsNoTracking()
            .Where(p => p.Status == ProviderRegistrationStatus.Submitted || p.Status == ProviderRegistrationStatus.NeedsInfo)
            .OrderBy(p => p.SubmittedAt).Select(p => new { p.Id, p.ApplicationRef, p.LegalName, p.ProviderType, p.Status, p.SubmittedAt }).ToListAsync();
        return Results.Ok(new
        {
            items = rows.Select(r => new
            {
                r.Id, r.ApplicationRef, name = r.LegalName, type = LicenseRules.TypeLabel(r.ProviderType), status = r.Status.ToString(),
                statusLabel = ProviderOnboardingEndpoints.StatusLabel(r.Status), r.SubmittedAt,
            }),
        });
    }

    internal sealed record ChecklistItem(string Key, string Status, string Title, string Memo, Guid? EvidenceId, string? EvidenceLabel);

    internal static List<ChecklistItem> Checklist(ProviderProfile p, List<ProviderLicense> licenses, DateOnly today)
    {
        ChecklistItem Doc(string key, string kind, string title, string evidence)
        {
            var l = licenses.FirstOrDefault(x => x.Kind == kind);
            if (l is null) return new(key, "todo", title, "لم يُرفع", null, null);
            var state = LicenseRules.State(l.ExpiresOn, today);
            var days = l.ExpiresOn is { } e ? e.DayNumber - today.DayNumber : int.MaxValue;
            if (state == LicenseState.Expired) return new(key, "todo", title, "منتهٍ — يلزم التجديد", l.Id, evidence);
            if (days <= LicenseRules.WarningDays) return new(key, "warn", title, $"{(kind == LicenseRules.Insurance ? "تنتهي" : "ينتهي")} خلال {Cases.CaseDisplay.Days(days)}", l.Id, evidence);
            if (l.ReviewStatus == LicenseReviewStatus.Rejected) return new(key, "todo", title, "مرفوض — يلزم رفع نسخة جديدة", l.Id, evidence);
            return new(key, "ok", title, kind == LicenseRules.PracticeLicense ? $"ساري · الرقم {LicenseRules.MaskNumber(l.Number)} مطابق للشهادة المرفوعة" : "ساري", l.Id, evidence);
        }
        var dataOk = p.CompletedSteps.Contains(1) && p.CompletedSteps.Contains(2) && !string.IsNullOrWhiteSpace(p.RepresentativeName);
        return
        [
            new("org_data", dataOk ? "ok" : "todo", "بيانات المنشأة والممثل", dataOk ? "مطابقة للمستندات" : "غير مكتملة", null, "عرض"),
            Doc("practice_license", LicenseRules.PracticeLicense, "ترخيص المزاولة", "الشهادة"),
            Doc("insurance", LicenseRules.Insurance, "التأمين المهني", "الوثيقة"),
            Doc("commercial_register", LicenseRules.CommercialRegister, "السجل التجاري", "السجل"),
            new("independence", p.IndependenceDeclared ? "ok" : "todo", "إقرار الاستقلالية وتعارض المصالح", p.IndependenceDeclared ? "موقّع" : "غير موقّع", null, p.IndependenceDeclared ? "الإقرار" : null),
            new("team", p.TeamCount > 0 && p.TeamIndividuallyLicensed ? "ok" : "todo", $"أعضاء الفريق ({p.TeamCount})", p.TeamIndividuallyLicensed ? "كل عضو مرخّص فردياً" : "لم يثبت ترخيص الأعضاء", null, "الفريق"),
            new("data_protection", p.DataProtectionSigned ? "ok" : "todo", "اتفاقية حماية البيانات", p.DataProtectionSigned ? "موقّعة" : "بانتظار توقيع مقدم الخدمة", null, null),
        ];
    }

    private static async Task<IResult> Application(Guid profileId, RahoonDbContext db, IClock clock)
    {
        var p = await db.Set<ProviderProfile>().AsNoTracking().FirstOrDefaultAsync(x => x.Id == profileId && x.Status != ProviderRegistrationStatus.Draft) ?? throw new NotFoundException();
        var licenses = await ProviderOnboardingEndpoints.CurrentLicensesAsync(db, p.Id);
        var checklist = Checklist(p, licenses, clock.TodayRiyadh);
        var history = await db.Set<ProviderReviewDecision>().AsNoTracking().Where(d => d.ProviderProfileId == p.Id).OrderByDescending(d => d.DecidedAt).ToListAsync();
        var open = checklist.Where(c => c.Status != "ok").Select(c => $"{c.Title}: {c.Memo}").ToList();
        return Results.Ok(new
        {
            header = $"{p.ApplicationRef} · {(p.ProviderType == ProviderType.Valuer ? "مقيّم عقاري" : LicenseRules.TypeLabel(p.ProviderType))}",
            title = $"مراجعة تسجيل: {p.LegalName}", status = p.Status.ToString(), statusLabel = ProviderOnboardingEndpoints.StatusLabel(p.Status), p.SubmittedAt,
            checklist = checklist.Select(c => new { c.Key, c.Status, c.Title, c.Memo, evidenceId = c.EvidenceId, evidence = c.EvidenceLabel }),
            documents = licenses.Select(l => new { l.Id, l.Kind, title = LicenseRules.KindLabel(l.Kind, p.ProviderType), number = LicenseRules.MaskNumber(l.Number), l.ExpiresOn, review = l.ReviewStatus.ToString(), hasFile = l.StorageKey != null }),
            canDecide = p.Status == ProviderRegistrationStatus.Submitted,
            canAccept = p.Status == ProviderRegistrationStatus.Submitted && open.Count == 0, acceptBlockers = open,
            note = "بعد القبول يظهر في دليل المنصة، وتختار كل منشأة إضافته لدليلها.",
            history = history.Select(h => new { h.Decision, h.Message, h.DecidedAt, h.OpenItems }),
        });
    }

    private static async Task<IResult> Decide(Guid profileId, ProviderDecisionBody req, RahoonDbContext db, RequestContext rc, IClock clock, AuditLog audit, Notifier notifier)
    {
        var decision = req.Decision?.ToLowerInvariant();
        new Validator().Require(decision is "accept" or "request_info" or "reject", "decision", "اختر القرار.")
            .Require(!string.IsNullOrWhiteSpace(req.Message) && req.Message.Trim().Length >= 10, "message", "الرسالة لمقدم الطلب إلزامية (10 أحرف على الأقل).").ThrowIfInvalid();
        var p = await db.Set<ProviderProfile>().FirstOrDefaultAsync(x => x.Id == profileId) ?? throw new NotFoundException();
        if (p.Status != ProviderRegistrationStatus.Submitted) throw new ConflictException("not_submitted", "الطلب ليس بانتظار المراجعة.");
        var licenses = await ProviderOnboardingEndpoints.CurrentLicensesAsync(db, p.Id);
        var open = Checklist(p, licenses, clock.TodayRiyadh).Where(c => c.Status != "ok").Select(c => $"{c.Title}: {c.Memo}").ToList();
        if (decision == "accept" && open.Count > 0)
            throw new DomainException("checklist_incomplete", "لا يمكن القبول وبنود المراجعة غير مكتملة؛ اطلب الاستكمال.", StatusCodes.Status422UnprocessableEntity, open);
        var now = clock.UtcNow;
        var message = req.Message.Trim();
        p.Status = decision switch { "accept" => ProviderRegistrationStatus.Accepted, "request_info" => ProviderRegistrationStatus.NeedsInfo, _ => ProviderRegistrationStatus.Rejected };
        p.DecidedAt = now;
        p.DecidedByUserId = rc.UserId;
        p.DecisionMessage = message;
        if (decision == "accept")
        {
            p.AcceptedAt = now;
            foreach (var l in await db.Set<ProviderLicense>().Where(l => l.ProviderProfileId == p.Id && l.IsCurrent).ToListAsync()) l.ReviewStatus = LicenseReviewStatus.Approved;
        }
        db.Set<ProviderReviewDecision>().Add(new ProviderReviewDecision { ProviderProfileId = p.Id, Decision = decision!, Message = message, OpenItems = open, DecidedByUserId = rc.UserId, DecidedAt = now });
        var admins = await db.Memberships.IgnoreQueryFilters().Where(m => m.OrganizationId == p.ProviderOrganizationId && m.Status == MembershipStatus.Active).Select(m => m.UserId).ToListAsync();
        foreach (var u in admins)
            notifier.Notify(u, p.ProviderOrganizationId, "registration", decision switch { "accept" => "قُبل تسجيلك في رهون", "request_info" => "طلب استكمال لتسجيلك", _ => "لم يُقبل طلب التسجيل" }, message, "/provider/onboarding",
                tone: decision == "accept" ? "ok" : "warn");
        await audit.RecordAsync(new AuditEntry("provider.application_decision", $"قرار تسجيل {p.ApplicationRef}: {decision}", Reason: message,
            Detail: open.Count == 0 ? null : "بنود مفتوحة: " + string.Join(" · ", open), OrganizationId: rc.OrganizationId));
        await db.SaveChangesAsync();
        return Results.Ok(new { status = p.Status.ToString(), statusLabel = ProviderOnboardingEndpoints.StatusLabel(p.Status) });
    }

    private static async Task<IResult> Download(Guid profileId, Guid licenseId, RahoonDbContext db, RequestContext rc, IDocumentStorage storage, AuditLog audit)
    {
        var l = await db.Set<ProviderLicense>().AsNoTracking().FirstOrDefaultAsync(x => x.Id == licenseId && x.ProviderProfileId == profileId) ?? throw new NotFoundException();
        if (l.StorageKey is null || l.ContentType is null) throw new NotFoundException();
        await audit.RecordAsync(new AuditEntry("provider.document_downloaded", $"تنزيل مستند مقدم خدمة ({l.Kind})", OrganizationId: rc.OrganizationId));
        await db.SaveChangesAsync();
        return Results.File(await storage.OpenReadAsync(l.StorageKey), l.ContentType, l.FileName);
    }

    /// <summary>Suspension (e.g. expired licence) and reinstatement; reinstating requires a valid practice licence.</summary>
    private static async Task<IResult> ChangeStatus(Guid providerOrganizationId, ProviderStatusBody req, RahoonDbContext db, RequestContext rc, IClock clock, AuditLog audit)
    {
        new Validator().Require(req.Action is "suspend" or "reinstate", "action", "اختر الإجراء.")
            .Require(!string.IsNullOrWhiteSpace(req.Reason) && req.Reason.Trim().Length >= 5, "reason", "السبب إلزامي.").ThrowIfInvalid();
        var p = await db.Set<ProviderProfile>().FirstOrDefaultAsync(x => x.ProviderOrganizationId == providerOrganizationId) ?? throw new NotFoundException();
        if (req.Action == "suspend")
        {
            if (p.Status != ProviderRegistrationStatus.Accepted) throw new ConflictException("not_active", "مقدم الخدمة ليس مقبولاً حالياً.");
            p.Status = ProviderRegistrationStatus.SuspendedLicense;
            p.SuspendedReason = req.Reason.Trim();
        }
        else
        {
            if (p.Status != ProviderRegistrationStatus.SuspendedLicense) throw new ConflictException("not_suspended", "مقدم الخدمة غير موقوف.");
            var exp = await db.Set<ProviderLicense>().Where(l => l.ProviderProfileId == p.Id && l.IsCurrent && l.Kind == LicenseRules.PracticeLicense).Select(l => l.ExpiresOn).FirstOrDefaultAsync();
            if (LicenseRules.State(exp, clock.TodayRiyadh) is LicenseState.Expired or LicenseState.Missing)
                throw new DomainException("license_expired", "لا يُعاد التفعيل قبل رفع ترخيص ساري ومراجعته.");
            p.Status = ProviderRegistrationStatus.Accepted;
            p.SuspendedReason = null;
        }
        db.Set<ProviderReviewDecision>().Add(new ProviderReviewDecision { ProviderProfileId = p.Id, Decision = req.Action, Message = req.Reason.Trim(), DecidedByUserId = rc.UserId, DecidedAt = clock.UtcNow });
        await audit.RecordAsync(new AuditEntry("provider.status_changed", $"{(req.Action == "suspend" ? "إيقاف" : "إعادة تفعيل")} مقدم الخدمة {p.LegalName}", Reason: req.Reason.Trim(), OrganizationId: rc.OrganizationId));
        await db.SaveChangesAsync();
        return Results.Ok(new { status = p.Status.ToString() });
    }
}
