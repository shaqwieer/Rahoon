using Microsoft.EntityFrameworkCore;
using Rahoon.Api.Infrastructure.Http;
using Rahoon.Api.Infrastructure.Persistence;
using Rahoon.Api.Infrastructure.Tenancy;
using Rahoon.Api.Infrastructure.Time;
using Rahoon.Api.Modules.Audit;
using Rahoon.Api.Modules.Identity;

namespace Rahoon.Api.Modules.Ecosystem;

public sealed record AddProviderBody(Guid ProviderOrganizationId, decimal? FrameworkFee, decimal? CommissionRate);
public sealed record UpdateProviderBody(decimal? FrameworkFee, decimal? CommissionRate, bool? Active);

/// <summary>
/// The institution's own provider directory (V05). Licence status comes from the platform registry; performance is
/// computed from this institution's assignments only («لا تُشارك تقييمات منشأة مع أخرى»).
/// </summary>
public static class InstitutionProviderEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/institution/providers").RequireOrg(OrganizationKind.Lender);
        g.MapGet("", Directory).RequireAnyPermission(P.OrgSettings, P.ProviderAssign);
        g.MapGet("/platform-directory", PlatformDirectory).RequirePermission(P.OrgSettings);
        g.MapPost("", Add).RequirePermission(P.OrgSettings).Idempotent();
        g.MapPut("/{providerOrganizationId:guid}", Update).RequirePermission(P.OrgSettings).Idempotent();
        g.MapGet("/{providerOrganizationId:guid}/performance", Performance).RequireAnyPermission(P.OrgSettings, P.ProviderAssign);
    }

    private static async Task<IResult> Directory(string? type, string? licence, RahoonDbContext db, RequestContext rc, ProviderDirectory directory)
    {
        var all = await directory.InstitutionDirectoryAsync(rc.OrganizationId!.Value);
        var rows = all.AsEnumerable();
        if (Enum.TryParse<ProviderType>(type, true, out var t) && Enum.IsDefined(t)) rows = rows.Where(r => r.Type == t);
        if (licence == "expiring") rows = rows.Where(r => r.LicenseState == "expiring_soon");
        return Results.Ok(new
        {
            chips = new[]
            {
                new { key = "all", label = "الكل", count = all.Count, tone = "neutral" },
                new { key = "valuer", label = "مقيّمون", count = all.Count(r => r.Type == ProviderType.Valuer), tone = "neutral" },
                new { key = "broker", label = "وسطاء", count = all.Count(r => r.Type == ProviderType.Broker), tone = "neutral" },
                new { key = "inspection", label = "فحص فني", count = all.Count(r => r.Type == ProviderType.Inspection), tone = "neutral" },
                new { key = "expiring", label = "ترخيص ينتهي قريباً", count = all.Count(r => r.LicenseState == "expiring_soon"), tone = "warn" },
            },
            items = rows.Select(r => new
            {
                id = r.ProviderOrganizationId, name = r.Name, type = r.Type.ToString(), typeLabel = r.TypeLabel,
                license = new { state = r.LicenseState, text = r.LicenseText, icon = r.LicenseIcon, tone = r.LicenseTone, expires = r.LicenseExpires },
                onTimeRate = r.Performance.OnTimeRate, reworkRate = r.Performance.ReworkRate, delivered = r.Performance.Delivered, active = r.Performance.Active,
                status = r.Status, statusLabel = r.StatusLabel, statusTone = r.StatusTone, assignable = r.Assignable,
                frameworkFee = r.FrameworkFee, commissionRate = r.CommissionRate,
            }),
            footnote = $"الأداء محسوب من التكليفات المنجزة خلال 12 شهراً لدى {rc.OrganizationName} فقط. لا تُشارك تقييمات منشأة مع أخرى.",
        });
    }

    private static async Task<IResult> PlatformDirectory(RahoonDbContext db, RequestContext rc, IClock clock)
    {
        var mine = await db.Set<InstitutionProvider>().Where(e => e.OrganizationId == rc.OrganizationId).Select(e => e.ProviderOrganizationId).ToListAsync();
        var profiles = await db.Set<ProviderProfile>().AsNoTracking()
            .Where(p => p.Status == ProviderRegistrationStatus.Accepted && !mine.Contains(p.ProviderOrganizationId)).OrderBy(p => p.LegalName).ToListAsync();
        var today = clock.TodayRiyadh;
        var items = new List<object>();
        foreach (var p in profiles)
        {
            var exp = await ProviderDirectory.ApprovedPracticeExpiryAsync(db, p.Id);
            items.Add(new { id = p.ProviderOrganizationId, name = p.LegalName, type = p.ProviderType.ToString(), typeLabel = LicenseRules.TypeLabel(p.ProviderType), p.City, license = LicenseRules.Text(exp, today) });
        }
        return Results.Ok(new { items });
    }

    private static async Task<IResult> Add(AddProviderBody req, RahoonDbContext db, RequestContext rc, AuditLog audit)
    {
        new Validator().Require(req.FrameworkFee is null or > 0, "frameworkFee", "الرسوم أكبر من صفر.")
            .Require(req.CommissionRate is null or (> 0 and < 0.2m), "commissionRate", "العمولة بين 0 و20%.").ThrowIfInvalid();
        var p = await db.Set<ProviderProfile>().AsNoTracking().FirstOrDefaultAsync(x => x.ProviderOrganizationId == req.ProviderOrganizationId && x.Status == ProviderRegistrationStatus.Accepted)
                ?? throw new DomainException("not_in_platform_directory", "مقدم الخدمة غير مقبول في دليل المنصة.");
        if (await db.Set<InstitutionProvider>().AnyAsync(e => e.OrganizationId == rc.OrganizationId && e.ProviderOrganizationId == req.ProviderOrganizationId))
            throw new ConflictException("already_added", "مقدم الخدمة موجود في دليلك.");
        if (p.ProviderType == ProviderType.Broker && req.CommissionRate is null) Validate.Throw("commissionRate", "حدد عمولة الوسيط حسب الاتفاقية.");
        if (p.ProviderType != ProviderType.Broker && req.FrameworkFee is null) Validate.Throw("frameworkFee", "حدد الرسوم حسب الاتفاقية الإطارية.");
        db.Set<InstitutionProvider>().Add(new InstitutionProvider
        {
            OrganizationId = rc.OrganizationId!.Value, ProviderOrganizationId = p.ProviderOrganizationId, ProviderType = p.ProviderType,
            FrameworkFee = req.FrameworkFee, CommissionRate = req.CommissionRate, AddedByUserId = rc.UserId,
        });
        await audit.RecordAsync(new AuditEntry("provider.directory_added", $"إضافة {p.LegalName} إلى دليل المنشأة",
            Detail: req.FrameworkFee is { } f ? $"رسوم إطارية {f:N2}" : $"عمولة {req.CommissionRate * 100:0.##}%", OrganizationId: rc.OrganizationId));
        await db.SaveChangesAsync();
        return Results.Ok(new { added = p.LegalName });
    }

    private static async Task<IResult> Update(Guid providerOrganizationId, UpdateProviderBody req, RahoonDbContext db, RequestContext rc, AuditLog audit)
    {
        new Validator().Require(req.FrameworkFee is null or > 0, "frameworkFee", "الرسوم أكبر من صفر.")
            .Require(req.CommissionRate is null or (> 0 and < 0.2m), "commissionRate", "العمولة بين 0 و20%.").ThrowIfInvalid();
        var e = await db.Set<InstitutionProvider>().FirstOrDefaultAsync(x => x.OrganizationId == rc.OrganizationId && x.ProviderOrganizationId == providerOrganizationId) ?? throw new NotFoundException();
        e.FrameworkFee = req.FrameworkFee ?? e.FrameworkFee;
        e.CommissionRate = req.CommissionRate ?? e.CommissionRate;
        e.Active = req.Active ?? e.Active;
        await audit.RecordAsync(new AuditEntry("provider.directory_updated", "تعديل بيانات مقدم خدمة في دليل المنشأة", Detail: $"نشط: {e.Active}", OrganizationId: rc.OrganizationId));
        await db.SaveChangesAsync();
        return Results.Ok(new { e.FrameworkFee, e.CommissionRate, e.Active });
    }

    private static async Task<IResult> Performance(Guid providerOrganizationId, RahoonDbContext db, RequestContext rc, ProviderDirectory directory)
    {
        var e = await db.Set<InstitutionProvider>().AsNoTracking().FirstOrDefaultAsync(x => x.OrganizationId == rc.OrganizationId && x.ProviderOrganizationId == providerOrganizationId) ?? throw new NotFoundException();
        var perf = await directory.PerformanceAsync(rc.OrganizationId!.Value, providerOrganizationId, e.ProviderType != ProviderType.Broker);
        return Results.Ok(new { scope = "لدى منشأتك فقط · 12 شهراً", perf.Delivered, perf.OnTime, perf.Reworked, perf.Active, perf.OnTimeRate, perf.ReworkRate, perf.AvgDays });
    }
}
