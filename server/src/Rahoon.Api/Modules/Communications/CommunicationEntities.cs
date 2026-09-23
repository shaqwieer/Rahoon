using Rahoon.Api.Infrastructure.Persistence;

namespace Rahoon.Api.Modules.Communications;

public enum MessageChannel { Portal, Sms, Email, Call, Internal }

/// <summary>Case message between team and owner, or internal note (InternalOnly).</summary>
public sealed class CaseMessage : OrgEntity
{
    public Guid CaseId { get; set; }
    public MessageChannel Channel { get; set; }
    public required string AuthorType { get; set; } // lender | owner | system
    public Guid? AuthorUserId { get; set; }
    public required string AuthorLabel { get; set; }
    public required string Body { get; set; }
    public string? TemplateKey { get; set; }
    public bool InternalOnly { get; set; }
    public DateTimeOffset At { get; set; }
    public DateTimeOffset? ReadByOwnerAt { get; set; }
    public DateTimeOffset? ReadByTeamAt { get; set; }
}

public enum TaskStatus { Open, Done, Cancelled }

public sealed class CaseTask : OrgEntity
{
    public Guid? CaseId { get; set; }
    public required string Title { get; set; }
    public Guid? AssigneeUserId { get; set; }
    public string? AssigneeRoleKey { get; set; }
    public DateOnly? DueOn { get; set; }
    public TaskStatus Status { get; set; } = TaskStatus.Open;
    public string? Kind { get; set; }
    public string? Link { get; set; }
    public Guid CreatedByUserId { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
}

public enum AppointmentStatus { Proposed, Confirmed, Rescheduled, Cancelled, Done }

public sealed class Appointment : OrgEntity
{
    public Guid CaseId { get; set; }
    public required string Type { get; set; } // call | visit | inspection
    public DateTimeOffset StartsAt { get; set; }
    public string? Attendees { get; set; }
    public AppointmentStatus Status { get; set; } = AppointmentStatus.Proposed;
    public required string ProposedBy { get; set; } // lender | owner
    public DateTimeOffset? ConfirmedAt { get; set; }
}

/// <summary>In-app notification for a single user. Not org-filtered; always queried by UserId.</summary>
public sealed class Notification : Entity
{
    public Guid UserId { get; set; }
    public Guid? OrganizationId { get; set; }
    public Guid? CaseId { get; set; }
    public required string Category { get; set; }
    public required string Title { get; set; }
    public string? Body { get; set; }
    public string? Link { get; set; }
    public string Tone { get; set; } = "info";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ReadAt { get; set; }
}

public enum TemplateStatus { Draft, PendingCompliance, Published, Superseded }

public sealed class CommunicationTemplate : Entity
{
    public Guid? OrganizationId { get; set; } // null = platform base template
    public required string Code { get; set; }
    public required string Title { get; set; }
    public required string Audience { get; set; }
    public int VersionNo { get; set; } = 1;
    public TemplateStatus Status { get; set; }
    public required string BodyAr { get; set; }
    public string? BodyEn { get; set; }
    public string? BodySms { get; set; }
    public List<string> Variables { get; set; } = [];
    public Guid? LastEditedByUserId { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public Guid? PublishedByUserId { get; set; }
}

/// <summary>Owner-initiated hardship notice (D11). Reason is optional and visible to the case team only.</summary>
public sealed class HardshipRequest : OrgEntity
{
    public Guid CaseId { get; set; }
    public string? ReasonKey { get; set; } // income_loss | health | family | other | prefer_not_say
    public bool CallbackRequested { get; set; } = true;
    public string Status { get; set; } = "open"; // open | contacted | closed
}

/// <summary>Owner's notice that a transfer was made (D10). Never counts as payment until finance matches it.</summary>
public sealed class PaymentNotice : OrgEntity
{
    public Guid CaseId { get; set; }
    public int InstallmentNo { get; set; }
    public DateOnly TransferDate { get; set; }
    public decimal Amount { get; set; }
    public string? Reference { get; set; }
}

public enum OutboundStatus { Queued, Sent, Failed, Simulated }

/// <summary>Outbound SMS/e-mail. In development every row is Simulated — no message leaves the system.</summary>
public sealed class OutboundMessage : Entity
{
    public Guid? OrganizationId { get; set; }
    public Guid? CaseId { get; set; }
    public MessageChannel Channel { get; set; }
    public required string Destination { get; set; }
    public required string Body { get; set; }
    public string? TemplateCode { get; set; }
    public required string Provider { get; set; }
    public OutboundStatus Status { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
