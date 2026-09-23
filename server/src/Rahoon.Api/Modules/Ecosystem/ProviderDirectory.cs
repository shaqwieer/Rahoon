using Microsoft.EntityFrameworkCore;
using Rahoon.Api.Infrastructure.Persistence;
using Rahoon.Api.Infrastructure.Time;
using Rahoon.Api.Modules.Providers;

namespace Rahoon.Api.Modules.Ecosystem;

public enum LicenseState { Valid, ExpiringSoon, Expired, Missing }

/// <summary>
/// Licence rules (V05, conflict #3): amber «ينتهي قريباً» at ≤ 60 days; directory status «تحذير» at ≤ 30 days
/// (still assignable, with a warning); expired → «موقوف» and never assignable.
/// </summary>
public static class LicenseRules
{
    public const int ExpiringSoonDays = 60;
    public const int WarningDays = 30;
    public const string PracticeLicense = "practice_license";
    public const string Insurance = "professional_insurance";
    public const string CommercialRegister = "commercial_register";

    public static readonly string[] RequiredKinds = [PracticeLicense, Insurance, CommercialRegister];

    public static string KindLabel(string kind, ProviderType type) => kind switch
    {
        PracticeLicense => type switch { ProviderType.Valuer => "ترخيص مزاولة التقييم العقاري", ProviderType.Broker => "ترخيص الوساطة العقارية", _ => "ترخيص مزاولة الفحص الفني" },
        Insurance => "وثيقة التأمين ضد الأخطاء المهنية",
        CommercialRegister => "السجل التجاري",
        _ => kind,
    };

    public static LicenseState State(DateOnly? expires, DateOnly today) => expires switch
    {
        null => LicenseState.Missing,
        { } e when e < today => LicenseState.Expired,
        { } e when e.DayNumber - today.DayNumber <= ExpiringSoonDays => LicenseState.ExpiringSoon,
        _ => LicenseState.Valid,
    };

    public static string Text(DateOnly? expires, DateOnly today)
    {
        if (expires is not { } e) return "لا يوجد ترخيص مرفوع";
        var days = e.DayNumber - today.DayNumber;
        if (days < 0) return $"منتهٍ {e:yyyy-MM}";
        if (days <= WarningDays) return $"ينتهي خلال {Cases.CaseDisplay.Days(days)}";
        return $"ساري حتى {e:yyyy-MM}";
    }

    public static (string Icon, string Tone) Icon(LicenseState s) => s switch
    {
        LicenseState.Valid => ("verified", "ok"),
        LicenseState.ExpiringSoon => ("event_upcoming", "warn"),
        _ => ("event_busy", "err"),
    };

    /// <summary>Directory status: معتمد / تحذير / موقوف. Only accepted providers with an unexpired licence can be assigned.</summary>
    public static (string Key, string Label, string Tone, bool Assignable) DirectoryStatus(ProviderRegistrationStatus status, DateOnly? expires, DateOnly today)
    {
        if (status != ProviderRegistrationStatus.Accepted || State(expires, today) is LicenseState.Expired or LicenseState.Missing)
            return ("suspended", "موقوف", "err", false);
        return expires!.Value.DayNumber - today.DayNumber <= WarningDays ? ("warning", "تحذير", "warn", true) : ("approved", "معتمد", "ok", true);
    }

    public static string TypeLabel(ProviderType t) => t switch { ProviderType.Valuer => "مقيّم", ProviderType.Broker => "وسيط", _ => "فحص فني" };

    public static AssignmentType AssignmentTypeOf(ProviderType t) => t switch
    {
        ProviderType.Valuer => AssignmentType.Valuation, ProviderType.Broker => AssignmentType.Brokerage, _ => AssignmentType.Inspection,
    };

    public static string MaskNumber(string? number) => string.IsNullOrEmpty(number) ? "—" : "•••" + number[^Math.Min(4, number.Length)..];
}

public sealed record ProviderPerformance(int Delivered, int OnTime, int Reworked, int Active, decimal? OnTimeRate, decimal? ReworkRate, int? AvgDays);

public sealed record DirectoryRow(
    Guid ProviderOrganizationId, string Name, ProviderType Type, string TypeLabel,
    string LicenseState, string LicenseText, string LicenseIcon, string LicenseTone, DateOnly? LicenseExpires,
    ProviderPerformance Performance, string Status, string StatusLabel, string StatusTone, bool Assignable,
    decimal? FrameworkFee, decimal? CommissionRate);

/// <summary>
/// Institution provider directory (V05) and per-institution performance (V07). Performance is computed only
/// from the institution's own assignments over 12 months and is never shared with another institution.
/// </summary>
public sealed class ProviderDirectory(RahoonDbContext db, IClock clock)
{
    public async Task<DateOnly?> PracticeLicenseExpiryAsync(Guid profileId) =>
        await db.Set<ProviderLicense>().Where(l => l.ProviderProfileId == profileId && l.IsCurrent && l.Kind == LicenseRules.PracticeLicense)
            .Select(l => l.ExpiresOn).FirstOrDefaultAsync();

    /// <summary>Performance of one provider at one lender (lender rows only — explicit org filter, 12 months).</summary>
    public async Task<ProviderPerformance> PerformanceAsync(Guid lenderOrgId, Guid providerOrgId, bool reworkApplies = true)
    {
        var since = clock.UtcNow.AddMonths(-12);
        var now = clock.UtcNow;
        var rows = await db.Assignments.IgnoreQueryFilters()
            .Where(a => a.OrganizationId == lenderOrgId && a.ProviderOrganizationId == providerOrgId)
            .Select(a => new
            {
                a.Id, a.Status, a.DueOn, a.DeliveredAt, a.CreatedAt, a.AccessExpiresAt,
                Reworked = db.AssignmentSubmissions.IgnoreQueryFilters().Any(s => s.AssignmentId == a.Id && s.Status == SubmissionStatus.Returned),
            }).ToListAsync();
        var delivered = rows.Where(r => r.DeliveredAt != null && r.DeliveredAt >= since).ToList();
        var onTime = delivered.Count(r => DateOnly.FromDateTime(r.DeliveredAt!.Value.ToOffset(TimeSpan.FromHours(3)).DateTime) <= r.DueOn);
        var reworked = delivered.Count(r => r.Reworked);
        var active = rows.Count(r => r.Status is AssignmentStatus.New or AssignmentStatus.InProgress or AssignmentStatus.Returned or AssignmentStatus.Submitted
                                     && (r.AccessExpiresAt == null || r.AccessExpiresAt > now));
        int? avgDays = delivered.Count == 0 ? null : (int)Math.Round(delivered.Average(r => (r.DeliveredAt!.Value - r.CreatedAt).TotalDays));
        return new ProviderPerformance(delivered.Count, onTime, reworked, active,
            delivered.Count == 0 ? null : Math.Round((decimal)onTime / delivered.Count, 4),
            !reworkApplies || delivered.Count == 0 ? null : Math.Round((decimal)reworked / delivered.Count, 4), avgDays);
    }

    public async Task<List<DirectoryRow>> InstitutionDirectoryAsync(Guid lenderOrgId, ProviderType? type = null)
    {
        var today = clock.TodayRiyadh;
        var entries = await db.Set<InstitutionProvider>().AsNoTracking()
            .Where(e => e.OrganizationId == lenderOrgId && e.Active && (type == null || e.ProviderType == type)).ToListAsync();
        var orgIds = entries.Select(e => e.ProviderOrganizationId).ToList();
        var orgs = await db.Organizations.AsNoTracking().Where(o => orgIds.Contains(o.Id)).ToDictionaryAsync(o => o.Id, o => o.NameAr);
        var profiles = await db.Set<ProviderProfile>().AsNoTracking().Where(p => orgIds.Contains(p.ProviderOrganizationId)).ToDictionaryAsync(p => p.ProviderOrganizationId);
        var rows = new List<DirectoryRow>();
        foreach (var e in entries)
        {
            if (!profiles.TryGetValue(e.ProviderOrganizationId, out var p)) continue;
            var expires = await PracticeLicenseExpiryAsync(p.Id);
            var state = LicenseRules.State(expires, today);
            var (icon, tone) = LicenseRules.Icon(state);
            var (key, label, sTone, assignable) = LicenseRules.DirectoryStatus(p.Status, expires, today);
            var perf = await PerformanceAsync(lenderOrgId, e.ProviderOrganizationId, reworkApplies: e.ProviderType != ProviderType.Broker);
            rows.Add(new DirectoryRow(e.ProviderOrganizationId, orgs.GetValueOrDefault(e.ProviderOrganizationId, p.LegalName), e.ProviderType,
                LicenseRules.TypeLabel(e.ProviderType), Snake(state), LicenseRules.Text(expires, today), icon, tone, expires, perf, key, label, sTone, assignable,
                e.FrameworkFee, e.CommissionRate));
        }
        return rows.OrderBy(r => r.Type).ThenByDescending(r => r.Assignable).ThenBy(r => r.Name).ToList();
    }

    public static string Snake(LicenseState s) => s switch
    {
        LicenseState.Valid => "valid", LicenseState.ExpiringSoon => "expiring_soon", LicenseState.Expired => "expired", _ => "missing",
    };
}
