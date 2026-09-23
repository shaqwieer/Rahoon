using Rahoon.Api.Infrastructure.Persistence;

namespace Rahoon.Api.Modules.Administration;

/// <summary>SLA per case stage in business days (A06). Values are institution-configurable examples.</summary>
public sealed class SlaRule : OrgEntity
{
    public required string Status { get; set; } // CaseStatus name
    public int BusinessDays { get; set; }
    public required string RuleNote { get; set; }
    public bool PausesOnOpenComplaint { get; set; }
}

/// <summary>
/// State of an external integration (PA18). Every integration is one of:
/// enabled | simulated | pending | unavailable | failed. Simulated records carry no effect.
/// </summary>
public sealed class IntegrationSetting : Entity
{
    public required string Key { get; set; }
    public required string NameAr { get; set; }
    public required string State { get; set; }
    public required string Note { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public Guid? UpdatedByUserId { get; set; }
}

public enum TempAccessStatus { Pending, Active, Rejected, Expired, Revoked }

/// <summary>Audited temporary support access (PA06): dual approval, read-only, time-boxed.</summary>
public sealed class TempAccessRequest : Entity
{
    public Guid OrganizationId { get; set; }
    public Guid CaseId { get; set; }
    public required string MaskedCaseId { get; set; }
    public Guid RequesterUserId { get; set; }
    public required string Reason { get; set; }
    public required string SupportTicketRef { get; set; }
    public int DurationMinutes { get; set; }
    public TempAccessStatus Status { get; set; } = TempAccessStatus.Pending;
    public Guid? InstitutionApproverUserId { get; set; }
    public DateTimeOffset? InstitutionApprovedAt { get; set; }
    public Guid? AuditorApproverUserId { get; set; }
    public DateTimeOffset? AuditorApprovedAt { get; set; }
    public DateTimeOffset? StartsAt { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public enum ApplicationStatus { New, InReview, MoreInfoRequested, Approved, Rejected }

/// <summary>Institution demo request / onboarding application (S02 → PA02).</summary>
public sealed class InstitutionApplication : Entity
{
    public required string Reference { get; set; } // APP-2026-0031
    public required string OrgName { get; set; }
    public required string OrgType { get; set; }
    public required string ContactName { get; set; }
    public required string ContactEmail { get; set; }
    public string? ContactPhone { get; set; }
    public string? JobTitle { get; set; }
    public string? PortfolioSize { get; set; }
    public string? Message { get; set; }
    public ApplicationStatus Status { get; set; } = ApplicationStatus.New;
    public string? ReviewNote { get; set; }
    public Guid? ReviewedByUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class RetentionPolicy : Entity
{
    public required string DataCategory { get; set; }
    public required string Period { get; set; }
    public required string Basis { get; set; }
    public required string State { get; set; } // effective | pending_legal
}

/// <summary>Replay protection for mutation endpoints (Idempotency-Key).</summary>
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
