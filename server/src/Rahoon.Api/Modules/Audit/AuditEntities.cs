namespace Rahoon.Api.Modules.Audit;

/// <summary>
/// Append-only, hash-chained audit event. Blocked attempts are recorded too.
/// Rows are never updated or deleted by the application.
/// </summary>
public sealed class AuditEvent
{
    public long Seq { get; set; }
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid? OrganizationId { get; set; }
    public Guid? CaseId { get; set; }
    public string? CaseReference { get; set; }
    public required string Type { get; set; } // case.transition, case.transition_blocked, approval.decision, pii.reveal, ...
    public required string Title { get; set; }
    public string? FromState { get; set; }
    public string? ToState { get; set; }
    public required string ActorType { get; set; } // user | owner | system | provider | platform
    public Guid? ActorUserId { get; set; }
    public string? ActorLabel { get; set; }
    public string? ActorRole { get; set; }
    public string? Reason { get; set; }
    public string? Detail { get; set; }
    public bool Blocked { get; set; }
    public List<string> Evidence { get; set; } = [];
    public string? DataJson { get; set; }
    public string? IpMasked { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
    public required string PrevHash { get; set; }
    public required string Hash { get; set; }
}
