using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Rahoon.Api.Infrastructure.Http;
using Rahoon.Api.Infrastructure.Persistence;
using Rahoon.Api.Infrastructure.Storage;
using Rahoon.Api.Infrastructure.Tenancy;
using Rahoon.Api.Infrastructure.Time;
using Rahoon.Api.Modules.Audit;
using Rahoon.Api.Modules.Documents;
using Rahoon.Api.Modules.Identity;

namespace Rahoon.Api.Modules.Ecosystem;

public sealed record OnboardingStepBody(JsonElement Data, bool Advance);

/// <summary>
/// Provider onboarding (V06a) and own profile. Six autosaved steps; steps may be saved incomplete and are
/// validated on final submit (conflict #5). Licences are reviewed manually by platform compliance — no
/// automated check against official registries.
/// </summary>
public static class ProviderOnboardingEndpoints
{
    public static readonly string[] StepTitles = ["بيانات المنشأة", "الممثل النظامي", "التراخيص والتأمين", "الفريق", "بيانات الفوترة", "الاتفاقية والإقرارات"];

    public static void Map(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/provider/onboarding").RequireOrg(OrganizationKind.ServiceProvider).RequirePermission(P.OrgSettings);
        g.MapGet("", Get);
        g.MapPut("/steps/{step:int}", SaveStep).Idempotent();
        g.MapPost("/documents", Upload).DisableAntiforgery();
        g.MapPost("/submit", Submit).Idempotent();
        app.MapGet("/api/provider/profile", Profile).RequireOrg(OrganizationKind.ServiceProvider).RequireAnyPermission(P.OrgSettings, P.AssignmentWork, P.InvoiceSubmit);
    }

    public static string StatusLabel(ProviderRegistrationStatus s) => s switch
    {
        ProviderRegistrationStatus.Draft => "مسودة",
        ProviderRegistrationStatus.Submitted => "مقدَّم",
        ProviderRegistrationStatus.NeedsInfo => "يحتاج استكمال",
        ProviderRegistrationStatus.Accepted => "مقبول",
        ProviderRegistrationStatus.Rejected => "مرفوض",
        _ => "موقوف لانتهاء الترخيص",
    };

    private static async Task<ProviderProfile> OwnProfileAsync(RahoonDbContext db, RequestContext rc, bool track = false)
    {
        var q = db.Set<ProviderProfile>().Where(p => p.ProviderOrganizationId == rc.OrganizationId);
        if (!track) q = q.AsNoTracking();
        return await q.FirstOrDefaultAsync() ?? throw new NotFoundException();
    }

    internal static async Task<List<ProviderLicense>> CurrentLicensesAsync(RahoonDbContext db, Guid profileId) =>
        await db.Set<ProviderLicense>().AsNoTracking().Where(l => l.ProviderProfileId == profileId && l.IsCurrent).ToListAsync();

    internal static object DocumentCard(string kind, ProviderType type, ProviderLicense? l, DateOnly today)
    {
        var title = LicenseRules.KindLabel(kind, type);
        if (l is null) return new { kind, title, meta = "لم يُرفع", status = "مطلوب", icon = "upload_file", tone = "err", action = "رفع" };
        var state = LicenseRules.State(l.ExpiresOn, today);
        var days = l.ExpiresOn is { } e ? e.DayNumber - today.DayNumber : (int?)null;
        var meta = string.Join(" · ", new[] { l.Number is null ? null : $"رقم {LicenseRules.MaskNumber(l.Number)}", l.ExpiresOn is { } x ? $"{(kind == LicenseRules.Insurance ? "تنتهي" : "ينتهي")} {x:yyyy-MM-dd}" : null }.Where(s => s is not null));
        if (state == LicenseState.Expired) return new { kind, title, meta, status = "منتهٍ — ارفع التجديد", icon = "error", tone = "err", action = "رفع التجديد" };
        if (days is { } d && d <= LicenseRules.WarningDays)
            return new { kind, title, meta, status = $"{(kind == LicenseRules.Insurance ? "تنتهي" : "ينتهي")} خلال {Cases.CaseDisplay.Days(d)} — ارفع التجديد", icon = "warning", tone = "warn", action = "رفع التجديد" };
        return new
        {
            kind, title, meta, status = l.ReviewStatus == LicenseReviewStatus.Approved ? "رُفع · معتمد" : l.ReviewStatus == LicenseReviewStatus.Rejected ? "رُفض — ارفع نسخة جديدة" : "رُفع · بانتظار المراجعة",
            icon = l.ReviewStatus == LicenseReviewStatus.Rejected ? "error" : "check_circle", tone = l.ReviewStatus == LicenseReviewStatus.Rejected ? "err" : "ok", action = "استبدال",
        };
    }

    private static async Task<IResult> Get(RahoonDbContext db, RequestContext rc, IClock clock)
    {
        var p = await OwnProfileAsync(db, rc);
        var today = clock.TodayRiyadh;
        var licenses = await CurrentLicensesAsync(db, p.Id);
        var last = await db.Set<ProviderReviewDecision>().AsNoTracking().Where(d => d.ProviderProfileId == p.Id).OrderByDescending(d => d.DecidedAt).FirstOrDefaultAsync();
        return Results.Ok(new
        {
            applicationRef = p.ApplicationRef, status = p.Status.ToString(), statusLabel = StatusLabel(p.Status),
            editable = p.Status is ProviderRegistrationStatus.Draft or ProviderRegistrationStatus.NeedsInfo,
            savedAt = p.LastSavedAt, savedLabel = p.LastSavedAt is { } s ? $"محفوظ {s.ToOffset(TimeSpan.FromHours(3)):HH:mm}" : null,
            currentStep = p.CurrentStep,
            steps = StepTitles.Select((t, i) => new { no = i + 1, title = t, status = p.CompletedSteps.Contains(i + 1) && p.CurrentStep != i + 1 ? "done" : p.CurrentStep == i + 1 ? "current" : "todo" }),
            data = JsonNode.Parse(p.StepDataJson),
            licensesNote = "يراجعها فريق الامتثال يدوياً. لا نتحقق آلياً من سجلات رسمية في هذه المرحلة.",
            documents = LicenseRules.RequiredKinds.Select(k => DocumentCard(k, p.ProviderType, licenses.FirstOrDefault(l => l.Kind == k), today)),
            lastDecision = last is null ? null : new { last.Decision, last.Message, last.DecidedAt, last.OpenItems },
            nextLabel = p.CurrentStep < 6 ? $"التالي: {StepTitles[p.CurrentStep]}" : "إرسال الطلب",
        });
    }

    private static async Task<IResult> SaveStep(int step, OnboardingStepBody req, RahoonDbContext db, RequestContext rc, IClock clock)
    {
        if (step is < 1 or > 6) throw new NotFoundException();
        var p = await OwnProfileAsync(db, rc, track: true);
        if (p.Status is not (ProviderRegistrationStatus.Draft or ProviderRegistrationStatus.NeedsInfo))
            throw new ConflictException("not_editable", "لا يمكن تعديل الطلب بعد إرساله إلا عند طلب الاستكمال.");
        var data = req.Data.ValueKind == JsonValueKind.Object ? JsonNode.Parse(req.Data.GetRawText())!.AsObject() : new JsonObject();
        string? Str(string k) => data.TryGetPropertyValue(k, out var v) && v is JsonValue jv && jv.TryGetValue<string>(out var s) ? s.Trim() : null;
        bool? Bool(string k) => data.TryGetPropertyValue(k, out var v) && v is JsonValue jv && jv.TryGetValue<bool>(out var b) ? b : null;
        int? Int(string k) => data.TryGetPropertyValue(k, out var v) && v is JsonValue jv && jv.TryGetValue<int>(out var i) ? i : null;
        switch (step)
        {
            case 1:
                if (Str("legalName") is { Length: > 1 } name) p.LegalName = name.Length > 200 ? name[..200] : name;
                p.CrNumber = Str("crNumber") ?? p.CrNumber;
                p.City = Str("city") ?? p.City;
                if (Str("providerType") is { } pt && Enum.TryParse<ProviderType>(pt, true, out var type) && Enum.IsDefined(type)) p.ProviderType = type;
                break;
            case 2:
                p.RepresentativeName = Str("representativeName") ?? p.RepresentativeName;
                break;
            case 4:
                if (Int("teamCount") is { } n && n is >= 0 and <= 500) p.TeamCount = n;
                p.TeamIndividuallyLicensed = Bool("teamIndividuallyLicensed") ?? p.TeamIndividuallyLicensed;
                break;
            case 5:
                // Only a masked IBAN is kept; payment happens outside the platform.
                if (Str("iban") is { Length: >= 8 } iban)
                {
                    var digits = iban.Replace(" ", "");
                    p.BillingIbanMasked = $"{digits[..2]}•• •••• •••• {digits[^4..]}";
                    data.Remove("iban");
                    data["ibanMasked"] = p.BillingIbanMasked;
                }
                break;
            case 6:
                p.IndependenceDeclared = Bool("independenceDeclared") ?? p.IndependenceDeclared;
                p.DataProtectionSigned = Bool("dataProtectionSigned") ?? p.DataProtectionSigned;
                break;
        }
        var all = JsonNode.Parse(p.StepDataJson)!.AsObject();
        all[step.ToString()] = data;
        p.StepDataJson = all.ToJsonString();
        if (req.Advance)
        {
            if (!p.CompletedSteps.Contains(step)) p.CompletedSteps = [.. p.CompletedSteps, step];
            p.CurrentStep = Math.Min(6, step + 1);
        }
        p.LastSavedAt = clock.UtcNow;
        await db.SaveChangesAsync();
        return Results.Ok(new { savedAt = p.LastSavedAt, savedLabel = $"محفوظ {p.LastSavedAt.Value.ToOffset(TimeSpan.FromHours(3)):HH:mm}", currentStep = p.CurrentStep });
    }

    private static async Task<IResult> Upload(HttpRequest http, RahoonDbContext db, RequestContext rc, IDocumentStorage storage, IFileScanner scanner, IClock clock)
    {
        var form = await http.ReadFormAsync();
        var kind = form["kind"].ToString();
        var file = form.Files.GetFile("file");
        var v = new Validator()
            .Require(LicenseRules.RequiredKinds.Contains(kind), "kind", "اختر نوع المستند.")
            .Require(file is not null, "file", "اختر الملف.");
        DateOnly? expires = null;
        if (form["expiresOn"].ToString() is { Length: > 0 } ex)
        {
            if (DateOnly.TryParse(ex, out var d)) expires = d;
            else v.Require(false, "expiresOn", "تاريخ الانتهاء غير صالح.");
        }
        v.Require(kind == LicenseRules.CommercialRegister || expires is not null, "expiresOn", "تاريخ الانتهاء مطلوب.");
        v.ThrowIfInvalid();
        var p = await OwnProfileAsync(db, rc);
        if (p.Status is not (ProviderRegistrationStatus.Draft or ProviderRegistrationStatus.NeedsInfo or ProviderRegistrationStatus.Accepted or ProviderRegistrationStatus.SuspendedLicense))
            throw new ConflictException("not_editable", "لا يمكن رفع مستندات والطلب قيد المراجعة.");
        await using var stream = file!.OpenReadStream();
        var stored = await storage.SaveAsync(stream, file.FileName, p.ProviderOrganizationId);
        if (await scanner.ScanAsync(stored.StorageKey) == ScanStatus.Infected) throw new DomainException("file_infected", "تعذّر قبول الملف. جرّب ملفاً آخر.");
        foreach (var old in await db.Set<ProviderLicense>().Where(l => l.ProviderProfileId == p.Id && l.Kind == kind && l.IsCurrent).ToListAsync()) old.IsCurrent = false;
        await db.SaveChangesAsync();
        var number = form["number"].ToString().Trim();
        var license = new ProviderLicense
        {
            ProviderProfileId = p.Id, ProviderOrganizationId = p.ProviderOrganizationId, Kind = kind, Number = number.Length == 0 ? null : number, ExpiresOn = expires,
            FileName = Path.GetFileName(file.FileName), StorageKey = stored.StorageKey, Sha256 = stored.Sha256, ContentType = stored.ContentType, SizeBytes = stored.SizeBytes,
            UploadedAt = clock.UtcNow, UploadedByUserId = rc.UserId,
        };
        db.Set<ProviderLicense>().Add(license);
        var tracked = await db.Set<ProviderProfile>().FirstAsync(x => x.Id == p.Id);
        tracked.LastSavedAt = clock.UtcNow;
        await db.SaveChangesAsync();
        return Results.Ok(DocumentCard(kind, p.ProviderType, license, clock.TodayRiyadh));
    }

    private static async Task<IResult> Submit(RahoonDbContext db, RequestContext rc, IClock clock, AuditLog audit)
    {
        var p = await OwnProfileAsync(db, rc, track: true);
        if (p.Status is not (ProviderRegistrationStatus.Draft or ProviderRegistrationStatus.NeedsInfo))
            throw new ConflictException("already_submitted", "الطلب مُرسل للمراجعة.");
        var today = clock.TodayRiyadh;
        var licenses = await CurrentLicensesAsync(db, p.Id);
        var missing = new List<string>();
        for (var s = 1; s <= 5; s++) if (!p.CompletedSteps.Contains(s)) missing.Add($"الخطوة {s}: {StepTitles[s - 1]}");
        foreach (var k in LicenseRules.RequiredKinds)
        {
            var l = licenses.FirstOrDefault(x => x.Kind == k);
            if (l is null) missing.Add($"{LicenseRules.KindLabel(k, p.ProviderType)}: لم يُرفع");
            else if (LicenseRules.State(l.ExpiresOn, today) == LicenseState.Expired) missing.Add($"{LicenseRules.KindLabel(k, p.ProviderType)}: منتهٍ");
        }
        if (!p.IndependenceDeclared) missing.Add("إقرار الاستقلالية وتعارض المصالح");
        if (string.IsNullOrWhiteSpace(p.RepresentativeName)) missing.Add("الممثل النظامي");
        if (missing.Count > 0) throw new DomainException("incomplete", "الطلب غير مكتمل.", StatusCodes.Status422UnprocessableEntity, missing);
        if (!p.CompletedSteps.Contains(6)) p.CompletedSteps = [.. p.CompletedSteps, 6];
        p.Status = ProviderRegistrationStatus.Submitted;
        p.SubmittedAt = clock.UtcNow;
        await audit.RecordAsync(new AuditEntry("provider.application_submitted", $"إرسال طلب التسجيل {p.ApplicationRef}", Detail: p.LegalName, OrganizationId: p.ProviderOrganizationId));
        await db.SaveChangesAsync();
        return Results.Ok(new { status = p.Status.ToString(), message = "أُرسل طلبك. يراجعه فريق الامتثال في المنصة يدوياً، ونبلغك بالقرار أو بما يلزم استكماله." });
    }

    private static async Task<IResult> Profile(RahoonDbContext db, RequestContext rc, IClock clock)
    {
        var p = await OwnProfileAsync(db, rc);
        var today = clock.TodayRiyadh;
        var licenses = await CurrentLicensesAsync(db, p.Id);
        var practice = licenses.FirstOrDefault(l => l.Kind == LicenseRules.PracticeLicense);
        var approvedExpiry = await ProviderDirectory.ApprovedPracticeExpiryAsync(db, p.Id);
        var state = LicenseRules.State(approvedExpiry, today);
        var (key, label, tone, _) = LicenseRules.DirectoryStatus(p.Status, approvedExpiry, today);
        return Results.Ok(new
        {
            name = p.LegalName, type = LicenseRules.TypeLabel(p.ProviderType), p.City, applicationRef = p.ApplicationRef,
            registration = StatusLabel(p.Status), directoryStatus = new { key, label, tone },
            license = new
            {
                state = ProviderDirectory.Snake(state), text = LicenseRules.Text(approvedExpiry, today), number = LicenseRules.MaskNumber(practice?.Number),
                pendingRenewal = practice is { ReviewStatus: LicenseReviewStatus.Pending } && p.Status != ProviderRegistrationStatus.Submitted
                    ? "تجديد مرفوع بانتظار مراجعة الامتثال — يبقى الترخيص المعتمد السابق سارياً في الدليل" : null,
            },
            documents = LicenseRules.RequiredKinds.Select(k => DocumentCard(k, p.ProviderType, licenses.FirstOrDefault(l => l.Kind == k), today)),
            team = new { count = p.TeamCount, individuallyLicensed = p.TeamIndividuallyLicensed },
            billing = p.BillingIbanMasked,
        });
    }
}
