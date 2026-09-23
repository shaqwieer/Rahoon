using Microsoft.EntityFrameworkCore;
using Rahoon.Api.Infrastructure.Persistence;

namespace Rahoon.Api.Modules.Administration;

/// <summary>
/// Platform minima (PA07) enforced on institution configuration (A03, A04, A06). Institutions may
/// tighten but never relax them. Values come from <see cref="PlatformDefaultRule"/> with coded fallbacks.
/// </summary>
public sealed class PlatformMinima(RahoonDbContext db)
{
    public const decimal DefaultMaxWaiverWithoutCommittee = 0.10m;
    public const int DefaultValuationMaxValidityDays = 90;
    public const int DefaultOwnerResponseMinDays = 7;

    private async Task<decimal> ValueAsync(string key, decimal fallback) =>
        await db.Set<PlatformDefaultRule>().AsNoTracking().Where(r => r.Key == key).Select(r => r.Value).FirstOrDefaultAsync() ?? fallback;

    public Task<decimal> MaxWaiverWithoutCommitteeAsync() => ValueAsync(PlatformRuleKeys.MaxWaiverWithoutCommittee, DefaultMaxWaiverWithoutCommittee);

    public async Task<int> ValuationMaxValidityDaysAsync() => (int)await ValueAsync(PlatformRuleKeys.ValuationMaxValidityDays, DefaultValuationMaxValidityDays);

    public async Task<int> OwnerResponseMinDaysAsync() => (int)await ValueAsync(PlatformRuleKeys.OwnerResponseMinDays, DefaultOwnerResponseMinDays);
}

/// <summary>Opaque case ids for platform monitoring (PA06) — salted so they cannot be recomputed from a case id alone.</summary>
public static class PlatformMasking
{
    public static string Salt(IConfiguration config) =>
        config["Security:MonitorSalt"] is { Length: > 0 } s ? s : "monitor:" + (config["Security:PiiLookupKey"] ?? "dev-only-lookup-key-change-me");
}
