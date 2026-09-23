using Rahoon.Api.Infrastructure.Persistence;

namespace Rahoon.Api.Modules.Solutions;

public enum SolutionKind { Reschedule, ReducedPayoff, GracePeriod, VoluntarySale }

public enum SolutionStatus
{
    Draft,            // being built by preparer
    InReview,         // handed to case manager for review
    PendingApproval,  // locked; approval request open
    Approved,
    Returned,
    Rejected,
    Superseded,
    Offered,
    Accepted,
    Declined,
    Countered,
    Expired,
}

/// <summary>One version (v1, v2, …) of the case's solution. Locked once submitted; changes create a new version.</summary>
public sealed class SolutionVersion : OrgEntity, IConcurrencyVersioned
{
    public Guid CaseId { get; set; }
    public int VersionNo { get; set; }
    public SolutionKind Kind { get; set; }
    public SolutionStatus Status { get; set; } = SolutionStatus.Draft;
    public decimal OutstandingAtPreparation { get; set; }
    public int TermMonths { get; set; }
    public DateOnly FirstDueDate { get; set; }
    public DateOnly LastDueDate { get; set; }
    public decimal WaiverAmount { get; set; }
    public decimal WaiverPercent { get; set; }
    public decimal DownPayment { get; set; }
    public int GraceMonths { get; set; }
    public decimal RescheduledAmount { get; set; }
    public decimal InstallmentAmount { get; set; }
    public decimal FinalInstallmentAmount { get; set; }
    public decimal? DiscountedPayoffAmount { get; set; }
    public decimal? Dsr { get; set; }
    public decimal DsrLimit { get; set; } = 0.55m;
    public decimal? NetIncomeUsed { get; set; }
    public string? Justification { get; set; }
    public int BreachMissedConsecutive { get; set; } = 2;
    public int BreachCureDays { get; set; } = 15;
    public int OfferValidityDays { get; set; } = 10;
    public Guid PreparedByUserId { get; set; }
    public DateTimeOffset PreparedAt { get; set; }
    public Guid? ReviewedByUserId { get; set; }
    public DateTimeOffset? LockedAt { get; set; }
    public string? LockedSnapshotJson { get; set; }
    public string? ReturnReason { get; set; }
    public Guid? BasedOnVersionId { get; set; }
    public uint Version { get; set; }
}

public enum ApprovalStatus { Pending, Approved, Returned, Rejected, Superseded, Escalated }
public enum ApprovalSubject { Solution, Sale, Referral, Closure, Cancellation, Distribution, Reconciliation }

/// <summary>Maker-checker approval (C08). Approver ≠ preparer ≠ reviewer; amount within approver's tier.</summary>
public sealed class ApprovalRequest : OrgEntity, IConcurrencyVersioned
{
    public Guid CaseId { get; set; }
    public ApprovalSubject Subject { get; set; }
    public Guid SubjectId { get; set; }
    public int SubjectVersionNo { get; set; }
    public required string Title { get; set; }
    public Guid PreparedByUserId { get; set; }
    public Guid SubmittedByUserId { get; set; }
    public DateTimeOffset SubmittedAt { get; set; }
    public required string SubmitterNote { get; set; }
    public bool SubmitterAttested { get; set; }
    public Guid? AssignedApproverUserId { get; set; }
    public string RequiredTier { get; set; } = "approver";
    public decimal? Amount { get; set; }
    public decimal? WaiverPercent { get; set; }
    public DateOnly DueOn { get; set; }
    public ApprovalStatus Status { get; set; } = ApprovalStatus.Pending;
    public Guid? DecidedByUserId { get; set; }
    public DateTimeOffset? DecidedAt { get; set; }
    public string? DecisionReason { get; set; }
    public bool StepUpVerified { get; set; }
    public List<string> Evidence { get; set; } = [];
    public uint Version { get; set; }
}

/// <summary>Versioned approval-limit policy (A04), effective only after a second admin approves it.</summary>
public sealed class ApprovalLimitPolicy : OrgEntity
{
    public int VersionNo { get; set; }
    public required string Status { get; set; } // draft | pending_approval | effective | superseded
    public DateOnly EffectiveFrom { get; set; }
    public string? ChangeSummary { get; set; }
    public Guid ProposedByUserId { get; set; }
    public Guid? ApprovedByUserId { get; set; }
    public DateTimeOffset? ApprovedAt { get; set; }
    public List<ApprovalLimitTier> Tiers { get; set; } = [];
}

public sealed class ApprovalLimitTier : Entity
{
    public Guid PolicyId { get; set; }
    public int Rank { get; set; }
    public required string RoleKey { get; set; }
    public required string LevelLabel { get; set; }
    /// <summary>SolutionKind names this tier may approve (stored as text[]).</summary>
    public List<string> SolutionKinds { get; set; } = [];
    public string SolutionKindsLabel { get; set; } = "";
    /// <summary>Null = unlimited.</summary>
    public decimal? MaxAmount { get; set; }
    public decimal? MaxWaiverPercent { get; set; }
    public bool CanApprove { get; set; } = true;
    public string? EscalateToLabel { get; set; }
}

public sealed class ComplianceNotice : OrgEntity
{
    public Guid CaseId { get; set; }
    public Guid SolutionVersionId { get; set; }
    public required string Rule { get; set; }
}

public enum OfferStatus { Sent, Countered, Accepted, Declined, Expired, Withdrawn }

public sealed class Offer : OrgEntity, IConcurrencyVersioned
{
    public Guid CaseId { get; set; }
    public Guid SolutionVersionId { get; set; }
    public int VersionNo { get; set; }
    public DateTimeOffset SentAt { get; set; }
    public DateOnly ValidUntil { get; set; }
    public OfferStatus Status { get; set; } = OfferStatus.Sent;
    public Guid SentByUserId { get; set; }
    public DateTimeOffset? RespondedAt { get; set; }
    public string? DeclineReason { get; set; }
    public uint Version { get; set; }
}

public enum NegotiationKind { Offer, OwnerCounter, OwnerMessage, LenderClarification, InternalNote, LenderDecline, OwnerDecline, OwnerAccept }

public sealed class NegotiationEntry : OrgEntity
{
    public Guid CaseId { get; set; }
    public Guid? OfferId { get; set; }
    public NegotiationKind Kind { get; set; }
    public required string AuthorType { get; set; } // lender | owner | system
    public Guid? AuthorUserId { get; set; }
    public required string AuthorLabel { get; set; }
    public DateTimeOffset At { get; set; }
    public required string Body { get; set; }
    public int? RequestedDueDay { get; set; }
    public DateOnly? RequestedStartDate { get; set; }
    public decimal? RequestedInstallment { get; set; }
    public int? RequestedTermMonths { get; set; }
    public bool InternalOnly { get; set; }
}

/// <summary>
/// In-platform acceptance: a consent record plus an OTP, explicitly NOT a licensed
/// electronic signature (A-05). It cannot by itself activate an agreement.
/// </summary>
public sealed class ConsentRecord : OrgEntity
{
    public Guid CaseId { get; set; }
    public Guid? OfferId { get; set; }
    public Guid? AgreementId { get; set; }
    public Guid PartyId { get; set; }
    public required string Kind { get; set; } // offer_acceptance | sale_consent | ...
    public DateTimeOffset AcceptedAt { get; set; }
    public required string Channel { get; set; }
    public string? OtpDestinationMasked { get; set; }
    public DateTimeOffset? OtpVerifiedAt { get; set; }
    public string? Device { get; set; }
    public string? IpMasked { get; set; }
    public List<string> Acknowledgements { get; set; } = [];
    public required string TextHash { get; set; }
}
