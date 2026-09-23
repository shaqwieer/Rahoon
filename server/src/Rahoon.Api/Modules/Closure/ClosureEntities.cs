using Rahoon.Api.Infrastructure.Persistence;

namespace Rahoon.Api.Modules.Closure;

// Reviewed was added for the three-person chain (preparer → reviewer → approver); appended to keep stored names stable.
public enum ReconciliationStatus { Draft, Submitted, Approved, Returned, Reviewed }

/// <summary>Manual reconciliation (L26/F01). Closure requires zero or explained difference.</summary>
public sealed class Reconciliation : OrgEntity, IConcurrencyVersioned
{
    public Guid CaseId { get; set; }
    public required string Basis { get; set; } // settlement | voluntary_sale | judicial_sale | discounted_payoff
    public decimal ExpectedAmount { get; set; }
    public decimal ReceivedAmount { get; set; }
    public decimal WaivedAmount { get; set; }
    public decimal Difference { get; set; }
    public string? DifferenceExplanation { get; set; }
    public ReconciliationStatus Status { get; set; } = ReconciliationStatus.Draft;
    public Guid PreparedByUserId { get; set; }
    public Guid? ReviewedByUserId { get; set; }
    public Guid? ApprovedByUserId { get; set; }
    public DateTimeOffset? ApprovedAt { get; set; }
    public List<ReconciliationLine> Lines { get; set; } = [];
    public uint Version { get; set; }

    // Review trail (added B10 F01).
    public string? ExpectedSource { get; set; }
    public string? Note { get; set; }
    public DateTimeOffset? SubmittedAt { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }
    public string? ReviewReason { get; set; }
    public string? ApprovalReason { get; set; }
    public string? ReturnReason { get; set; }
}

/// <summary>
/// Kinds: receipt (counts in received), waiver (counts in waived), cost | lender_share | surplus (breakdown of
/// the proceeds, not counted), info (display-only, e.g. a waiver already reflected in the agreed amount).
/// </summary>
public sealed class ReconciliationLine : Entity
{
    public Guid ReconciliationId { get; set; }
    public required string Label { get; set; }
    public decimal Amount { get; set; }
    public string? Reference { get; set; }
    public required string Kind { get; set; } // receipt | cost | lender_share | surplus | waiver | info
    public string MatchStatus { get; set; } = "matched";
    public string? SourceType { get; set; } // bank | official | core_banking | internal
    public DateOnly? ValueDate { get; set; }
}

public enum ClosureDocumentStatus { Pending, Ready }

public sealed class ClosureDocument : OrgEntity
{
    public Guid CaseId { get; set; }
    public required string Type { get; set; } // final_clearance | lien_release_letter | lien_release_submission_proof | owner_final_summary
    public required string Title { get; set; }
    public ClosureDocumentStatus Status { get; set; }
    public string? PreparedByDept { get; set; }
    public Guid? DocumentVersionId { get; set; }
    public string? ExternalReference { get; set; }
    public bool BlocksClosure { get; set; } = true;
    public bool VisibleToOwner { get; set; }
    public DateOnly? PreparedOn { get; set; }
    /// <summary>Published to the owner portal at closure (VisibleToOwner flips only when the case is closed).</summary>
    public bool ShareWithOwner { get; set; }
}

public enum DistributionStatus { Draft, InReview, InApproval, Approved, Executed, Returned }

/// <summary>
/// F02 waterfall over the net proceeds of a sale: costs (already netted) → debt → other approved fees → owner surplus.
/// Preparer ≠ checker ≠ approver; never created or executed automatically.
/// </summary>
public sealed class Distribution : OrgEntity, IConcurrencyVersioned
{
    public Guid CaseId { get; set; }
    public Guid ReconciliationId { get; set; }
    public decimal SalePrice { get; set; }
    public decimal ProcedureCosts { get; set; }
    public decimal NetProceeds { get; set; }
    public decimal DebtAmount { get; set; }
    public required string DebtBasisRef { get; set; }
    public decimal LenderShare { get; set; }
    public decimal OtherFees { get; set; }
    public decimal OwnerSurplus { get; set; }
    public decimal Shortfall { get; set; }
    public string? SurplusDestinationMasked { get; set; }
    public DistributionStatus Status { get; set; } = DistributionStatus.Draft;
    public Guid PreparedByUserId { get; set; }
    public DateTimeOffset? SubmittedAt { get; set; }
    public Guid? CheckedByUserId { get; set; }
    public DateTimeOffset? CheckedAt { get; set; }
    public string? CheckReason { get; set; }
    public Guid? ApprovedByUserId { get; set; }
    public DateTimeOffset? ApprovedAt { get; set; }
    public string? ApprovalReason { get; set; }
    public string? ReturnReason { get; set; }
    public DateTimeOffset? ExecutedAt { get; set; }
    public List<DistributionLine> Lines { get; set; } = [];
    public uint Version { get; set; }
}

public sealed class DistributionLine : OrgEntity
{
    public Guid DistributionId { get; set; }
    public int Seq { get; set; }
    public required string Type { get; set; } // debt_repayment | other_fee | owner_surplus
    public required string Label { get; set; }
    public decimal Amount { get; set; }
    public required string BasisRef { get; set; }
    public string? DestinationMasked { get; set; }
    public string? ExecutedTxnRef { get; set; }
    public DateOnly? ExecutedOn { get; set; }
}

public enum ClosureRequestStatus { Pending, Approved, Rejected }

/// <summary>F04 closure decision: every figure traced to a source; approver ≠ requester ≠ reconciliation preparer/reviewer.</summary>
public sealed class ClosureRequest : OrgEntity, IConcurrencyVersioned
{
    public Guid CaseId { get; set; }
    public Guid ReconciliationId { get; set; }
    public required string Note { get; set; }
    public Guid RequestedByUserId { get; set; }
    public DateTimeOffset RequestedAt { get; set; }
    public required string TraceJson { get; set; }
    public required string TraceSha256 { get; set; }
    public ClosureRequestStatus Status { get; set; } = ClosureRequestStatus.Pending;
    public Guid? DecidedByUserId { get; set; }
    public DateTimeOffset? DecidedAt { get; set; }
    public string? DecisionReason { get; set; }
    public bool TraceAcknowledged { get; set; }
    public uint Version { get; set; }
}
