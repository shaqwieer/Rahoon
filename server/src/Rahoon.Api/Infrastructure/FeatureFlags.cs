namespace Rahoon.Api.Infrastructure;

/// <summary>
/// Product switches read from configuration (<c>Features:*</c>).
/// <para><see cref="LegacyMortgage"/>: the mortgage-default help model withdrawn on 2026-10-01
/// (docs/redefinition/legacy-inventory.md). Off by default: its endpoints are not mapped, its jobs don't run, its demo data
/// isn't seeded and only «فريق رهون» staff can sign in. Its database rows are kept untouched. On only for the archived
/// regression suite or a deliberate rollback.</para>
/// </summary>
public sealed record FeatureFlags(bool LegacyMortgage)
{
    public static FeatureFlags From(IConfiguration config) => new(config.GetValue("Features:LegacyMortgage", false));
}
