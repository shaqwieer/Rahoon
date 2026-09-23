using Rahoon.Api.Infrastructure.Persistence;

namespace Rahoon.Api.Modules.Administration;

/// <summary>
/// One screen opened under an active temporary support grant (PA06: «كل شاشة تُفتح تُسجل»).
/// Kept on the platform side; the institution sees the count when it is notified after expiry.
/// </summary>
public sealed class TempAccessViewLog : Entity
{
    public Guid RequestId { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid UserId { get; set; }
    public required string Screen { get; set; }
    public DateTimeOffset At { get; set; }
}

/// <summary>
/// Platform minimum (PA07) that institutions may tighten but never relax. Numeric rules carry
/// <see cref="Value"/>; fixed rules (separation of duties, dual approvals) are not editable.
/// </summary>
public sealed class PlatformDefaultRule : Entity
{
    public required string Key { get; set; }
    public required string NameAr { get; set; }
    public required string MinimumLabel { get; set; }
    public required string Note { get; set; }
    public decimal? Value { get; set; }
    public bool Editable { get; set; }
    public int SortOrder { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public Guid? UpdatedByUserId { get; set; }
}

/// <summary>Last run of a background job (PA13 service monitoring).</summary>
public sealed class JobHeartbeat
{
    public required string Key { get; set; }
    public DateTimeOffset LastRunAt { get; set; }
    public string? LastResult { get; set; }
}

public static class PlatformRuleKeys
{
    public const string SeparationOfDuties = "separation_of_duties";
    public const string ReferralApprovals = "referral_dual_approval";
    public const string ClosureApprovals = "closure_finance_approver";
    public const string MaxWaiverWithoutCommittee = "max_waiver_without_committee";
    public const string ValuationMaxValidityDays = "valuation_max_validity_days";
    public const string OwnerResponseMinDays = "owner_response_min_days";
}
