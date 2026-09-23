using Rahoon.Api.Infrastructure.Persistence;

namespace Rahoon.Api.Modules.Ecosystem;

public enum ProviderType { Valuer, Broker, Inspection }

/// <summary>Registration states (specOnb): مسودة، مقدَّم، يحتاج استكمال، مقبول، مرفوض، موقوف لانتهاء الترخيص.</summary>
public enum ProviderRegistrationStatus { Draft, Submitted, NeedsInfo, Accepted, Rejected, SuspendedLicense }

public enum LicenseReviewStatus { Pending, Approved, Rejected }

/// <summary>
/// Platform provider registry entry (PA16 / V06). One per service-provider organization. Platform-level
/// metadata, not tenant data: it is a plain entity and every endpoint scopes it explicitly (the provider
/// sees its own profile; platform compliance reviews; institutions see accepted entries only).
/// </summary>
public sealed class ProviderProfile : Entity, IHasTimestamps, IConcurrencyVersioned
{
    public Guid ProviderOrganizationId { get; set; }
    public required string ApplicationRef { get; set; } // PRV-APP-0044
    public required string LegalName { get; set; }
    public ProviderType ProviderType { get; set; }
    public string? City { get; set; }
    public string? CrNumber { get; set; }
    public ProviderRegistrationStatus Status { get; set; } = ProviderRegistrationStatus.Draft;
    public int CurrentStep { get; set; } = 1;
    public List<int> CompletedSteps { get; set; } = [];
    /// <summary>Autosaved step payloads, keyed by step number (jsonb).</summary>
    public string StepDataJson { get; set; } = "{}";
    public DateTimeOffset? LastSavedAt { get; set; }
    public DateTimeOffset? SubmittedAt { get; set; }
    public string? RepresentativeName { get; set; }
    public int TeamCount { get; set; }
    public bool TeamIndividuallyLicensed { get; set; }
    public string? BillingIbanMasked { get; set; }
    public bool IndependenceDeclared { get; set; }
    public bool DataProtectionSigned { get; set; }
    public Guid? DecidedByUserId { get; set; }
    public DateTimeOffset? DecidedAt { get; set; }
    public string? DecisionMessage { get; set; }
    public DateTimeOffset? AcceptedAt { get; set; }
    public string? SuspendedReason { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public uint Version { get; set; }
}

/// <summary>A licence / insurance / registration document of a provider. Reviewed manually — no official-registry check.</summary>
public sealed class ProviderLicense : Entity
{
    public Guid ProviderProfileId { get; set; }
    public Guid ProviderOrganizationId { get; set; }
    public required string Kind { get; set; } // practice_license | professional_insurance | commercial_register
    public string? Number { get; set; }
    public DateOnly? ExpiresOn { get; set; }
    public string? FileName { get; set; }
    public string? StorageKey { get; set; }
    public string? Sha256 { get; set; }
    public string? ContentType { get; set; }
    public long SizeBytes { get; set; }
    public DateTimeOffset UploadedAt { get; set; }
    public Guid UploadedByUserId { get; set; }
    public LicenseReviewStatus ReviewStatus { get; set; } = LicenseReviewStatus.Pending;
    public bool IsCurrent { get; set; } = true;
}

/// <summary>Compliance review decision on a registration (V06b): accept / request more info / reject, with a message.</summary>
public sealed class ProviderReviewDecision : Entity
{
    public Guid ProviderProfileId { get; set; }
    public required string Decision { get; set; } // accept | request_info | reject | suspend | reinstate
    public required string Message { get; set; }
    public List<string> OpenItems { get; set; } = [];
    public Guid DecidedByUserId { get; set; }
    public DateTimeOffset DecidedAt { get; set; }
}

/// <summary>
/// An institution's own provider directory entry (V05). Each institution curates its list from the platform
/// directory; framework fee (valuers) or commission (brokers) comes from the institution's agreement.
/// </summary>
public sealed class InstitutionProvider : OrgEntity
{
    public Guid ProviderOrganizationId { get; set; }
    public ProviderType ProviderType { get; set; }
    public decimal? FrameworkFee { get; set; }
    public decimal? CommissionRate { get; set; }
    public Guid AddedByUserId { get; set; }
    public bool Active { get; set; } = true;
}

/// <summary>Invoice states (specInv): مسودة → قيد المراجعة → معتمدة → مدفوعة (مرجع) | مرفوضة بسبب.</summary>
public enum ProviderInvoiceStatus { Draft, UnderReview, Approved, Paid, Rejected }

/// <summary>
/// Provider invoice from a delivered and accepted assignment (V07). Shared between the provider (issuer) and
/// the lender (reviewer), so it is a plain entity scoped explicitly on both sides. Payment happens outside the
/// platform; only its reference is recorded.
/// </summary>
public sealed class ProviderInvoice : Entity, IHasTimestamps, IConcurrencyVersioned
{
    public required string Number { get; set; } // INV-B-2026-114
    public Guid AssignmentId { get; set; }
    public required string AssignmentReference { get; set; }
    public Guid LenderOrganizationId { get; set; }
    public Guid ProviderOrganizationId { get; set; }
    public decimal Amount { get; set; }
    public DateOnly IssuedOn { get; set; }
    public ProviderInvoiceStatus Status { get; set; } = ProviderInvoiceStatus.Draft;
    public Guid CreatedByUserId { get; set; }
    public Guid? SubmittedByUserId { get; set; }
    public DateTimeOffset? SubmittedAt { get; set; }
    public Guid? DecidedByUserId { get; set; }
    public DateTimeOffset? DecidedAt { get; set; }
    public string? RejectionReason { get; set; }
    public string? PaymentReference { get; set; }
    public Guid? PaidRecordedByUserId { get; set; }
    public DateTimeOffset? PaidAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public uint Version { get; set; }
}

public enum WorkflowVersionStatus { Draft, PendingApproval, Active, Superseded }

/// <summary>
/// Versioned workflow configuration of one institution (PA14): stage SLAs and rules. Platform-mandated rules are
/// locked; a draft is published only after a second authorised user approves it; applies to new cases only.
/// </summary>
public sealed class WorkflowVersion : Entity, IHasTimestamps, IConcurrencyVersioned
{
    public Guid InstitutionOrganizationId { get; set; }
    public int VersionNo { get; set; }
    public WorkflowVersionStatus Status { get; set; } = WorkflowVersionStatus.Draft;
    public DateOnly? EffectiveFrom { get; set; }
    /// <summary>Serialized <see cref="WorkflowStage"/> list (jsonb).</summary>
    public string StagesJson { get; set; } = "[]";
    public string? ChangeSummary { get; set; }
    public int? BasedOnVersionNo { get; set; }
    public Guid CreatedByUserId { get; set; }
    public Guid? SubmittedByUserId { get; set; }
    public DateTimeOffset? SubmittedAt { get; set; }
    public Guid? ApprovedByUserId { get; set; }
    public DateTimeOffset? ApprovedAt { get; set; }
    public string? ApprovalNote { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public uint Version { get; set; }
}

/// <summary>A scheduled monthly report send (PA15) — recipients must hold reports.view in the institution.</summary>
public sealed class ReportSchedule : OrgEntity
{
    public required string Metric { get; set; }
    public required string Breakdown { get; set; }
    public required string PeriodFrom { get; set; } // yyyy-MM
    public required string PeriodTo { get; set; }
    public string Frequency { get; set; } = "monthly";
    public List<Guid> RecipientUserIds { get; set; } = [];
    public Guid CreatedByUserId { get; set; }
}

/// <summary>Pricing plan (PA17). Pricing is an assumption pending product confirmation — plans are data.</summary>
public sealed class BillingPlan : Entity, IHasTimestamps
{
    public required string Key { get; set; }
    public required string NameAr { get; set; }
    public decimal? MonthlyPrice { get; set; } // null = «حسب الاتفاق»
    public int? ActiveCaseLimit { get; set; }  // null = unlimited / contract
    public required string LimitLabel { get; set; }
    public int SortOrder { get; set; }
    public Guid? UpdatedByUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class InstitutionSubscription : Entity, IHasTimestamps
{
    public Guid InstitutionOrganizationId { get; set; }
    public string? PlanKey { get; set; }
    public bool Trial { get; set; }
    public DateOnly StartedOn { get; set; }
    public Guid? UpdatedByUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public enum PlatformInvoiceStatus { Issued, Paid }

/// <summary>Platform subscription invoice BIL-YYYY-MM-NNN. Paid outside the platform; the reference is recorded.</summary>
public sealed class PlatformInvoice : Entity, IHasTimestamps
{
    public required string Number { get; set; }
    public Guid InstitutionOrganizationId { get; set; }
    public required string PlanKey { get; set; }
    public required string Period { get; set; } // 2026-09
    public int ActiveCases { get; set; }
    public decimal Amount { get; set; }
    public DateOnly IssuedOn { get; set; }
    public DateOnly DueOn { get; set; }
    public PlatformInvoiceStatus Status { get; set; } = PlatformInvoiceStatus.Issued;
    public string? PaymentReference { get; set; }
    public DateTimeOffset? PaidAt { get; set; }
    public Guid? RecordedByUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>
/// Per-institution configuration of a conditional licensed integration (X01 signing / X02 payment).
/// The licensed path is offered only when the platform state is enabled, this row is enabled and a real
/// adapter is registered. No such provider is contracted today.
/// </summary>
public sealed class InstitutionIntegration : Entity, IHasTimestamps
{
    public Guid InstitutionOrganizationId { get; set; }
    public required string Capability { get; set; } // licensed_signing | licensed_payment
    public string ProviderLabel { get; set; } = "مزود مرخّص (مكان محجوز)";
    public required string Mode { get; set; } // enabled | simulated | disabled
    public string Health { get; set; } = "unavailable"; // available | unavailable | pending | failed
    public DateTimeOffset? LastUpdateAt { get; set; }
    public Guid? UpdatedByUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>A refused/attempted licensed-integration call, kept so the UI can show «آخر محاولة». Never a signature or payment.</summary>
public sealed class IntegrationAttempt : Entity
{
    public Guid InstitutionOrganizationId { get; set; }
    public Guid? CaseId { get; set; }
    public required string Capability { get; set; }
    public required string State { get; set; }
    public string? SubjectReference { get; set; }
    public DateTimeOffset At { get; set; }
}
