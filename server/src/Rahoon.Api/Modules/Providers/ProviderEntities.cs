using Rahoon.Api.Infrastructure.Persistence;

namespace Rahoon.Api.Modules.Providers;

public enum AssignmentType { Valuation, Inspection, Brokerage, Legal, JudicialSale }
public enum AssignmentStatus { New, InProgress, Returned, Submitted, Accepted, Closed, Cancelled }

/// <summary>
/// Work given to a service provider (valuer, broker) or judicial agent. Owned by the
/// lender organization (OrganizationId); the provider org sees only this assignment,
/// only the shared documents, and only until AccessExpiresAt (delivery + 7 days read-only).
/// </summary>
public sealed class ProviderAssignment : OrgEntity, IConcurrencyVersioned
{
    public required string Reference { get; set; } // ASG-2026-0418
    public Guid CaseId { get; set; }
    public Guid ProviderOrganizationId { get; set; }
    public Guid? AssigneeUserId { get; set; }
    public AssignmentType Type { get; set; }
    public required string Title { get; set; }
    public required string PropertyLabel { get; set; }
    public AssignmentStatus Status { get; set; } = AssignmentStatus.New;
    public DateOnly DueOn { get; set; }
    public DateTimeOffset? InspectionAt { get; set; }
    public bool InspectionConfirmed { get; set; }
    public string? InspectionContact { get; set; }
    public List<string> Scope { get; set; } = [];
    public List<Guid> SharedDocumentIds { get; set; } = [];
    public string? FeesLabel { get; set; }
    public decimal? FeeAmount { get; set; }
    public Guid CreatedByUserId { get; set; }
    public DateTimeOffset? DeliveredAt { get; set; }
    public DateTimeOffset? AccessExpiresAt { get; set; }
    public uint Version { get; set; }
}

public sealed class AssignmentMessage : OrgEntity
{
    public Guid AssignmentId { get; set; }
    public Guid AuthorUserId { get; set; }
    public required string AuthorLabel { get; set; }
    public required string AuthorSide { get; set; } // lender | provider
    public required string Body { get; set; }
    public DateTimeOffset At { get; set; }
}

public enum SubmissionStatus { Submitted, Returned, Accepted }

public sealed class AssignmentSubmission : OrgEntity
{
    public Guid AssignmentId { get; set; }
    public int VersionNo { get; set; }
    public decimal? MarketValue { get; set; }
    public decimal? RangeLow { get; set; }
    public decimal? RangeHigh { get; set; }
    public DateOnly? InspectionDate { get; set; }
    public string? Methodology { get; set; }
    public int? ComparablesCount { get; set; }
    public Guid? ReportDocumentVersionId { get; set; }
    public List<string> Checklist { get; set; } = [];
    public bool IndependenceDeclared { get; set; }
    public SubmissionStatus Status { get; set; } = SubmissionStatus.Submitted;
    public DateTimeOffset SubmittedAt { get; set; }
    public Guid SubmittedByUserId { get; set; }
    public List<string> ReturnNotes { get; set; } = [];
    public Guid? ReviewedByUserId { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }
    public DateOnly? ResubmitDueOn { get; set; }
}
