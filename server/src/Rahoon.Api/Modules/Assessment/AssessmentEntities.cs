using Rahoon.Api.Infrastructure.Persistence;

namespace Rahoon.Api.Modules.Assessment;

public enum ValuationStatus { UnderReview, Accepted, Returned, Superseded }

/// <summary>A valuation report delivered through a provider assignment (or entered by the lender).</summary>
public sealed class ValuationReport : OrgEntity
{
    public Guid CaseId { get; set; }
    public Guid? AssignmentId { get; set; }
    public int VersionNo { get; set; } = 1;
    public required string ValuerName { get; set; }
    public decimal MarketValue { get; set; }
    public decimal? RangeLow { get; set; }
    public decimal? RangeHigh { get; set; }
    public string? Methodology { get; set; }
    public int? ComparablesCount { get; set; }
    public DateOnly? InspectionDate { get; set; }
    public DateOnly ReportDate { get; set; }
    public DateOnly ValidUntil { get; set; }
    public Guid? DocumentVersionId { get; set; }
    public ValuationStatus Status { get; set; } = ValuationStatus.UnderReview;
    public Guid? ReviewedByUserId { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }
    public string? ReviewNote { get; set; }
}

/// <summary>Affordability analysis (L12). DSR limit is institution policy (assumption 55%).</summary>
public sealed class AffordabilityAnalysis : OrgEntity, IConcurrencyVersioned
{
    public Guid CaseId { get; set; }
    public decimal? NetMonthlyIncome { get; set; }
    public Guid? IncomeDocumentVersionId { get; set; }
    public string? IncomeSourceLabel { get; set; }
    public DateOnly? IncomeVerifiedOn { get; set; }
    public decimal OtherObligations { get; set; }
    public decimal DsrLimit { get; set; } = 0.55m;
    public List<string> CircumstanceIndicators { get; set; } = [];
    public List<string> OptionNotes { get; set; } = [];
    public bool Completed { get; set; }
    public Guid? PreparedByUserId { get; set; }
    public uint Version { get; set; }
}
