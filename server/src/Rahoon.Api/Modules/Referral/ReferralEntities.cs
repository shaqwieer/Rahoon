using Rahoon.Api.Infrastructure.Persistence;

namespace Rahoon.Api.Modules.Referral;

public enum ReferralStatus { Preparing, NoticeSent, PendingApproval, Approved, Rejected, HandedOff, Withdrawn }

/// <summary>
/// Manual judicial referral (A-06). A separate, approved decision — never automatic.
/// The platform does not operate courts or auctions; it records the external reference
/// and the official status verbatim with source and timestamp.
/// </summary>
public sealed class JudicialReferral : OrgEntity, IConcurrencyVersioned
{
    public Guid CaseId { get; set; }
    public ReferralStatus Status { get; set; } = ReferralStatus.Preparing;
    public Guid InitiatedByUserId { get; set; }
    public string? Reason { get; set; }
    public DateTimeOffset? NoticeSentAt { get; set; }
    public int ObjectionPeriodDays { get; set; } = 15;
    public DateOnly? ObjectionEndsOn { get; set; }
    public Guid? ApprovedByUserId { get; set; }
    public DateTimeOffset? ApprovedAt { get; set; }
    public string? DecisionReason { get; set; }
    public DateTimeOffset? EvidencePackExportedAt { get; set; }
    public string? EvidencePackHash { get; set; }
    public string? ExternalAuthority { get; set; }
    public string? ExternalRequestNumber { get; set; }
    public string? OfficialStatusText { get; set; }
    public string? OfficialStatusSource { get; set; }
    public DateTimeOffset? OfficialStatusSyncedAt { get; set; }
    public string IntegrationState { get; set; } = "unavailable";
    public uint Version { get; set; }
}

public sealed class ExternalStatusEntry : OrgEntity
{
    public Guid ReferralId { get; set; }
    public Guid CaseId { get; set; }
    public required string StatusText { get; set; }
    public required string Source { get; set; }
    public DateTimeOffset ObservedAt { get; set; }
    public Guid EnteredByUserId { get; set; }
    public string? Note { get; set; }
}
