namespace Rahoon.Api.Infrastructure.Persistence;

/// <summary>Stored response of a state-changing request, replayed for the same Idempotency-Key (schema «app»).</summary>
public sealed class IdempotencyRecord
{
    public required string Key { get; set; }
    public Guid UserId { get; set; }
    public required string Endpoint { get; set; }
    public required string RequestHash { get; set; }
    public int StatusCode { get; set; }
    public string? ResponseJson { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

/// <summary>Atomic counters behind human references (SR-/BR-/OP-/IN-/CM-YYYY-NNNNN), schema «app».</summary>
public sealed class ReferenceCounter
{
    public required string Key { get; set; }
    public long Value { get; set; }
}

/// <summary>Every SMS the platform tried to send, with the gateway's honest result (sandbox → simulated).</summary>
public sealed class OutboundSms : Entity
{
    public required string Destination { get; set; }
    public required string Body { get; set; }
    public required string Provider { get; set; }
    /// <summary>sent | simulated | failed</summary>
    public required string Result { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
