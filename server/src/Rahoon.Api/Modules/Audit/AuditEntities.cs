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
    public required string Type { get; set; } // auth.login, individual.registered, market.contact_reveal, directory.updated, ...
    public required string Title { get; set; }
    public string? FromState { get; set; }
    public string? ToState { get; set; }
    public required string ActorType { get; set; } // rahoon_team | individual | system
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
    /// <summary>What the event is about: sale_request, interest, directory_organization… plus its reference.</summary>
    public string? SubjectType { get; set; }
    public string? SubjectReference { get; set; }
    /// <summary>Canonical hash format. 3 = current (the chain was restarted when the mortgage-help model was removed, 2026-10-01).</summary>
    public int HashVersion { get; set; } = AuditLog.CurrentHashVersion;
    public required string PrevHash { get; set; }
    public required string Hash { get; set; }
}
