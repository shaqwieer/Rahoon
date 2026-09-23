using Rahoon.Api.Infrastructure.Persistence;

namespace Rahoon.Api.Modules.Cases;

/// <summary>
/// Internal platform status (Blueprint §state map). External official statuses
/// are never mapped into this enum — they live verbatim on the referral record.
/// </summary>
public enum CaseStatus
{
    Draft,
    AwaitingData,
    Verification,
    Valuation,
    ProposedSolution,
    InternalApproval,
    AwaitingCustomer,
    Negotiation,
    ActiveSettlement,
    VoluntarySale,
    JudicialReferral,
    ExternalJudicialSale,
    AwaitingReconciliation,
    Closed,
    Paused,
    Cancelled,
}

public enum CaseSource { Manual, Import }

public sealed class Case : OrgEntity, IConcurrencyVersioned
{
    public required string Reference { get; set; }
    public CaseStatus Status { get; set; } = CaseStatus.Draft;
    /// <summary>Status before Paused, restored on resume.</summary>
    public CaseStatus? StatusBeforePause { get; set; }
    public DateTimeOffset StatusChangedAt { get; set; }
    public CaseSource Source { get; set; } = CaseSource.Manual;
    public Guid? ImportBatchId { get; set; }
    public string ProductType { get; set; } = "تمويل سكني · مرابحة";
    public string? Region { get; set; }
    public string? City { get; set; }
    public DateOnly? OpenedOn { get; set; }
    public Guid? AssignedManagerId { get; set; }
    public Guid CreatedByUserId { get; set; }

    /// <summary>Wizard progress (1–6) while in Draft.</summary>
    public int DraftStep { get; set; } = 1;
    public string? DuplicateOverrideReason { get; set; }

    // SLA
    public DateOnly? StageDueOn { get; set; }
    public DateTimeOffset? SlaPausedAt { get; set; }
    public string? SlaPausedReason { get; set; }

    // Denormalized headline figures (source + timestamp shown in UI)
    public decimal? OutstandingAmount { get; set; }
    public DateTimeOffset? OutstandingAsOf { get; set; }
    public string? OutstandingSource { get; set; }
    public decimal? ArrearsAmount { get; set; }
    public int? ArrearsInstallments { get; set; }
    public DateOnly? ArrearsSince { get; set; }

    public string? PauseReason { get; set; }
    public string? CancelReason { get; set; }
    public DateTimeOffset? ClosedAt { get; set; }

    public uint Version { get; set; }

    public List<CaseParty> Parties { get; set; } = [];
    public Property? Property { get; set; }
    public Mortgage? Mortgage { get; set; }
    public FinancingContract? Financing { get; set; }
}

public enum PartyRole { OwnerBorrower, CoBorrower, Guarantor, Agent, LegalRepresentative, Occupant, InformalRepresentative }
public enum PartyKind { Individual, Organization }
public enum PoaStatus { None, Requested, Uploaded, Verified }

/// <summary>A party to the case. Identity number and phone are encrypted at rest and masked on read.</summary>
public sealed class CaseParty : OrgEntity
{
    public Guid CaseId { get; set; }
    public PartyRole Role { get; set; }
    public PartyKind Kind { get; set; } = PartyKind.Individual;
    public bool IsPrimary { get; set; }
    public bool IsContractParty { get; set; } = true;
    public required string FullName { get; set; }
    /// <summary>First name + initial (A-10), precomputed for lists.</summary>
    public required string DisplayName { get; set; }
    public string? Relation { get; set; }
    public string? NationalIdEnc { get; set; }
    public string? NationalIdMasked { get; set; }
    public string? NationalIdHash { get; set; }
    public string? PhoneEnc { get; set; }
    public string? PhoneMasked { get; set; }
    public string? Email { get; set; }
    public string PreferredLanguage { get; set; } = "ar";
    public string? SpecialNeeds { get; set; }
    public string? EmploymentStatus { get; set; }
    public bool ContactAllowed { get; set; } = true;
    public DateTimeOffset? IdentityVerifiedAt { get; set; }
    public string? IdentityVerifiedVia { get; set; }
    public PoaStatus PoaStatus { get; set; } = PoaStatus.None;
    public string? Notes { get; set; }
}

public enum OwnerInvitationStatus { NotSent, Sent, Accepted, Expired, Revoked }

/// <summary>Links a debtor/property-owner login to exactly one case. Owners see nothing else.</summary>
public sealed class OwnerAccess : OrgEntity
{
    public Guid CaseId { get; set; }
    public Guid PartyId { get; set; }
    public Guid? UserId { get; set; }
    public byte[]? InvitationTokenHash { get; set; }
    public OwnerInvitationStatus InvitationStatus { get; set; } = OwnerInvitationStatus.NotSent;
    public DateTimeOffset? InvitedAt { get; set; }
    public DateTimeOffset? InvitationExpiresAt { get; set; }
    public DateTimeOffset? AcceptedAt { get; set; }
    public DateTimeOffset? IdentityVerifiedAt { get; set; }
    public string? IdentityMethod { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public string? ContactHours { get; set; }
    public List<string> AllowedChannels { get; set; } = ["platform", "sms"];
    /// <summary>Communication needs shown prominently to the case team (L06), e.g. calls only with a relative present.</summary>
    public string? CommunicationNeeds { get; set; }
}

public sealed class FinancingContract : OrgEntity
{
    public Guid CaseId { get; set; }
    public required string ContractNumber { get; set; }
    public DateOnly? ContractDate { get; set; }
    public decimal? OriginalAmount { get; set; }
    public int? OriginalTermMonths { get; set; }
    public int? RemainingTermMonths { get; set; }
    public decimal? OriginalInstallment { get; set; }
    public string ProfitType { get; set; } = "ثابت";
    public DateOnly? FirstOverdueDate { get; set; }
}

public enum SyncStatus { Ok, Delayed, Failed, Manual }

/// <summary>Point-in-time debt figures with provenance. Total must equal Σ items.</summary>
public sealed class DebtSnapshot : OrgEntity
{
    public Guid CaseId { get; set; }
    public required string Source { get; set; }
    public DateTimeOffset AsOf { get; set; }
    public SyncStatus SyncStatus { get; set; } = SyncStatus.Manual;
    public decimal Principal { get; set; }
    public decimal Profit { get; set; }
    public decimal LateFees { get; set; }
    public decimal OtherFees { get; set; }
    public decimal Total { get; set; }
    public bool IsCurrent { get; set; } = true;
    public Guid RecordedByUserId { get; set; }
}

public enum InstallmentHistoryStatus { Paid, Unpaid, Partial, Due }

public sealed class InstallmentHistoryEntry : OrgEntity
{
    public Guid CaseId { get; set; }
    public required string Month { get; set; } // YYYY-MM
    public InstallmentHistoryStatus Status { get; set; }
    public decimal AmountDue { get; set; }
    public decimal AmountPaid { get; set; }
}

public enum OccupancyKind { OwnerFamily, Tenant, Vacant, Unknown }

public sealed class Property : OrgEntity
{
    public Guid CaseId { get; set; }
    public required string Type { get; set; }
    public required string City { get; set; }
    public string? District { get; set; }
    public decimal? LandAreaM2 { get; set; }
    public decimal? BuiltAreaM2 { get; set; }
    public int? YearBuilt { get; set; }
    public string? DeedNumberEnc { get; set; }
    public string? DeedNumberMasked { get; set; }
    public OccupancyKind Occupancy { get; set; } = OccupancyKind.Unknown;
    public string? OccupancyNote { get; set; }
    /// <summary>Short label used in case titles, e.g. «فيلا سكنية، حي النرجس، الرياض».</summary>
    public string? ShortLabel { get; set; }
}

public enum LegalReviewStatus { Pending, Complete }

public sealed class Mortgage : OrgEntity
{
    public Guid CaseId { get; set; }
    public required string Mortgagee { get; set; }
    public int Rank { get; set; } = 1;
    public DateOnly? RegisteredOn { get; set; }
    public string? OtherEncumbrances { get; set; }
    public DateOnly? InsuranceValidUntil { get; set; }
    public bool DeedMatched { get; set; }
    public DateOnly? DeedMatchedOn { get; set; }
    public string? VerificationSource { get; set; }
    public LegalReviewStatus LegalReviewStatus { get; set; } = LegalReviewStatus.Pending;
    public string? LegalNote { get; set; }
    public Guid? LegalReviewedByUserId { get; set; }
    public DateTimeOffset? LegalReviewedAt { get; set; }
}

/// <summary>Every full reveal of masked personal data (A-10): reason, 60-second window, logged.</summary>
public sealed class PiiRevealLog : OrgEntity
{
    public Guid CaseId { get; set; }
    public Guid? PartyId { get; set; }
    public Guid UserId { get; set; }
    public required string Field { get; set; }
    public required string Reason { get; set; }
    public DateTimeOffset RevealedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
}

public sealed class SavedView : OrgEntity
{
    public Guid MembershipId { get; set; }
    public required string Name { get; set; }
    public required string FiltersJson { get; set; }
    public int SortOrder { get; set; }
}

/// <summary>Per-organization monotonic case reference counter (RH-YYYY-NNNNNN is platform-unique).</summary>
public sealed class ReferenceCounter
{
    public required string Key { get; set; }
    public long Value { get; set; }
}
