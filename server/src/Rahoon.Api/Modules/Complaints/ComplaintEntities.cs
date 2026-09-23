using Rahoon.Api.Infrastructure.Persistence;

namespace Rahoon.Api.Modules.Complaints;

public enum ComplaintType { Complaint, Objection, Appeal }
public enum ComplaintStatus { Received, InReview, AwaitingOwner, Resolved, Escalated, Closed }
public enum ComplaintDecision { Accepted, PartiallyAccepted, Rejected }

/// <summary>
/// Complaint or objection. While open it blocks referral and cancellation and
/// pauses the owner-response SLA (F-03). Reviewer is independent of the case team.
/// </summary>
public sealed class Complaint : OrgEntity, Infrastructure.Persistence.IConcurrencyVersioned
{
    public required string Reference { get; set; } // CMP-2026-0142
    public Guid CaseId { get; set; }
    public ComplaintType Type { get; set; }
    public required string Subject { get; set; }
    public required string Body { get; set; }
    public required string SubmittedVia { get; set; } // owner_portal | phone | email | branch
    public Guid? SubmittedByUserId { get; set; }
    public required string SubmittedByLabel { get; set; }
    public DateTimeOffset SubmittedAt { get; set; }
    public ComplaintStatus Status { get; set; } = ComplaintStatus.Received;
    public Guid? ReviewerUserId { get; set; }
    public DateOnly DueOn { get; set; }
    public ComplaintDecision? Decision { get; set; }
    public string? ResponseText { get; set; }
    public string? ResponseDraft { get; set; }
    public DateTimeOffset? RespondedAt { get; set; }
    public string? OwnerReaction { get; set; } // accepted | escalated
    public List<string> Findings { get; set; } = [];
    public uint Version { get; set; }

    public bool IsOpen => Status is not (ComplaintStatus.Resolved or ComplaintStatus.Closed);
}
