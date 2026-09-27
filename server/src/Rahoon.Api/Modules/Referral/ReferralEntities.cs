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

    // L25 decision trail (added B10).
    public string? NoticeTemplateCode { get; set; }
    public Guid? NoticeSentByUserId { get; set; }
    public DateTimeOffset? RequestedAt { get; set; }
    public Guid? DecidedByUserId { get; set; }
    public DateTimeOffset? DecidedAt { get; set; }
    public string? ExternalReferenceSource { get; set; }
    public DateTimeOffset? ExternalReferenceEnteredAt { get; set; }
    public Guid? ExternalReferenceEnteredByUserId { get; set; }
}

/// <summary>Where an external-status row came from (J03 source tags).</summary>
public static class ExternalSourceKind
{
    public const string Manual = "manual";          // إدخال يدوي — typed by legal from an official notice
    public const string Channel = "channel";        // القناة المعتمدة (not connected yet)
    public const string AgentReported = "agent_reported"; // أبلغ بها الوكيل — never official until confirmed
    public const string Internal = "internal";      // داخلي (e.g. sync failure)
}

/// <summary>
/// One observation of the official external status. <see cref="StatusText"/> is stored exactly as
/// received and is never mapped to an internal case status.
/// </summary>
public sealed class ExternalStatusEntry : OrgEntity
{
    public Guid ReferralId { get; set; }
    public Guid CaseId { get; set; }
    public required string StatusText { get; set; }
    public required string Source { get; set; }
    public DateTimeOffset ObservedAt { get; set; }
    public Guid EnteredByUserId { get; set; }
    public string? Note { get; set; }
    public string SourceKind { get; set; } = ExternalSourceKind.Manual;
    public string Kind { get; set; } = "status"; // status | agent_report | sync_failure
    public bool OfficiallyConfirmed { get; set; }
}

/// <summary>Evidence recorded by legal for a readiness item that cannot be computed (e.g. written refusal of a voluntary sale).</summary>
public sealed class ReferralChecklistEvidence : OrgEntity
{
    public Guid CaseId { get; set; }
    public required string Key { get; set; }
    public required string Note { get; set; }
    public Guid? DocumentVersionId { get; set; }
    public Guid RecordedByUserId { get; set; }
    public DateTimeOffset RecordedAt { get; set; }
}

public enum EvidencePackStatus { Draft, Locked, Exported }

/// <summary>J02/L25 numbered evidence pack with a hash per item and a manifest hash.</summary>
public sealed class EvidencePack : OrgEntity
{
    public Guid CaseId { get; set; }
    public Guid ReferralId { get; set; }
    public required string Reference { get; set; } // PKG-3511-01
    public int SeqNo { get; set; }
    public EvidencePackStatus Status { get; set; } = EvidencePackStatus.Draft;
    public required string ManifestSha256 { get; set; }
    public required string MappingsJson { get; set; }
    public Guid BuiltByUserId { get; set; }
    public DateTimeOffset BuiltAt { get; set; }
    public DateTimeOffset? LockedAt { get; set; }
    public DateTimeOffset? ExportedAt { get; set; }
    public Guid? ExportedByUserId { get; set; }
    public List<EvidencePackItem> Items { get; set; } = [];
}

public sealed class EvidencePackItem : OrgEntity
{
    public Guid PackId { get; set; }
    public Guid CaseId { get; set; }
    public int Seq { get; set; }
    public required string Title { get; set; }
    public required string SourceType { get; set; } // document | generated | record
    public required string SourceRef { get; set; }
    public required string VersionLabel { get; set; }
    public required string Sha256 { get; set; }
    public bool Verified { get; set; }
}

public enum ReferralExceptionStatus { Open, Pending, Info, Resolved }

/// <summary>J04 exception (status mismatch, missing document, objection filed…). Never auto-transitions the case.</summary>
public sealed class ReferralException : OrgEntity, IConcurrencyVersioned
{
    public Guid CaseId { get; set; }
    public Guid? ReferralId { get; set; }
    public required string Reference { get; set; } // EXC-2026-0001
    public required string Type { get; set; } // status_mismatch | missing_document | objection_filed | package_rejected | no_response | channel_unavailable | other
    public required string Title { get; set; }
    public required string Description { get; set; }
    public string? PlatformState { get; set; }
    public string? ExternalStateText { get; set; }
    public Guid? OwnerUserId { get; set; }
    public DateOnly? DueOn { get; set; }
    public ReferralExceptionStatus Status { get; set; } = ReferralExceptionStatus.Open;
    public Guid CreatedByUserId { get; set; }
    public string? ResolutionAction { get; set; } // record_and_wait | escalate | corrected | other
    public string? ResolutionNote { get; set; }
    public Guid? ResolvedByUserId { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }
    public uint Version { get; set; }
}

public enum SaleResultStatus { Draft, Submitted, Confirmed, Returned }

/// <summary>
/// J07 sale result reported by the judicial agent. Tagged «أبلغ بها الوكيل» until legal confirms it
/// against the official record; never triggers a distribution or a state change by itself.
/// </summary>
public sealed class SaleResult : OrgEntity, IConcurrencyVersioned
{
    public Guid CaseId { get; set; }
    public Guid AssignmentId { get; set; }
    public decimal? OfficialSalePrice { get; set; }
    public DateOnly? SaleMinutesDate { get; set; }
    public decimal? DeclaredCosts { get; set; }
    public List<Guid> EvidenceVersionIds { get; set; } = [];
    public SaleResultStatus Status { get; set; } = SaleResultStatus.Draft;
    public string Source { get; set; } = ExternalSourceKind.AgentReported;
    public Guid? SubmittedByUserId { get; set; }
    public DateTimeOffset? SubmittedAt { get; set; }
    public Guid? ConfirmedByUserId { get; set; }
    public DateTimeOffset? ConfirmedAt { get; set; }
    public string? ConfirmationSource { get; set; }
    public DateOnly? OfficialConfirmationDate { get; set; }
    public string? ConfirmationNote { get; set; }
    public uint Version { get; set; }
}

/// <summary>J07 «التحديثات المرسلة» from the agent.</summary>
public sealed class AgentUpdate : OrgEntity
{
    public Guid AssignmentId { get; set; }
    public Guid CaseId { get; set; }
    public Guid AuthorUserId { get; set; }
    public required string AuthorLabel { get; set; }
    public required string Text { get; set; }
    public string Kind { get; set; } = "general"; // general | inspection_done | plan_submitted | minutes_issued
    public DateTimeOffset At { get; set; }
    public List<Guid> AttachmentVersionIds { get; set; } = [];
}

/// <summary>J06 plan milestones as submitted to the authority (a mirror; Rahoon does not approve sale decisions).</summary>
public sealed class SalePlanMilestone : OrgEntity
{
    public Guid AssignmentId { get; set; }
    public Guid CaseId { get; set; }
    public int Seq { get; set; }
    public DateOnly On { get; set; }
    public required string Text { get; set; }
}
