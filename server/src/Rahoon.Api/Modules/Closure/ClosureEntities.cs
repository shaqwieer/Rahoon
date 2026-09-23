using Rahoon.Api.Infrastructure.Persistence;

namespace Rahoon.Api.Modules.Closure;

public enum ReconciliationStatus { Draft, Submitted, Approved, Returned }

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
}

public sealed class ReconciliationLine : Entity
{
    public Guid ReconciliationId { get; set; }
    public required string Label { get; set; }
    public decimal Amount { get; set; }
    public string? Reference { get; set; }
    public required string Kind { get; set; } // receipt | cost | lender_share | surplus | waiver
    public string MatchStatus { get; set; } = "matched";
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
}
