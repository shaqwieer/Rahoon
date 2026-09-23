using Rahoon.Api.Infrastructure.Persistence;

namespace Rahoon.Api.Modules.Analytics;

/// <summary>Human opinion on a decision-support output (Handoff «دعم القرار»).</summary>
public static class HumanReview
{
    public const string Agree = "agree";
    public const string Override = "override";
    public const string NotUsed = "not_used";
    public static readonly string[] All = [Agree, Override, NotUsed];
}

public enum SettingChangeStatus { PendingApproval, Effective, Superseded, Rejected }

/// <summary>
/// O03 versioned operational setting (routing rules, team capacity, reminder cadence, referral objection
/// period, owner read-only window). A change is a new version that takes effect only after a second person approves it.
/// </summary>
public sealed class OperationalSetting : OrgEntity
{
    public required string Key { get; set; }
    public int VersionNo { get; set; }
    public required string ValueJson { get; set; }
    public SettingChangeStatus Status { get; set; } = SettingChangeStatus.PendingApproval;
    public Guid ProposedByUserId { get; set; }
    public DateTimeOffset ProposedAt { get; set; }
    public required string Reason { get; set; }
    public Guid? DecidedByUserId { get; set; }
    public DateTimeOffset? DecidedAt { get; set; }
    public string? DecisionReason { get; set; }
}

/// <summary>O02 feedback on an analytic insight («غير مفيدة…»).</summary>
public sealed class InsightFeedback : OrgEntity
{
    public required string InsightKey { get; set; }
    public required string ModelVersion { get; set; }
    public required string Value { get; set; } // useful | not_useful
    public string? Reason { get; set; }
    public Guid UserId { get; set; }
    public DateTimeOffset At { get; set; }
}

public enum DraftStatus { Unreviewed, Edited, Approved, Rejected }

/// <summary>
/// O04 template-based draft suggestion. Deterministic (no model call): fills an approved template from case
/// facts and flags conflicts. Must be edited or approved by a human before it can be attached to anything.
/// </summary>
public sealed class DocumentDraft : OrgEntity, IConcurrencyVersioned
{
    public Guid CaseId { get; set; }
    public required string Kind { get; set; } // agreement_reschedule | pre_referral_notice
    public required string TemplateCode { get; set; }
    public int TemplateVersion { get; set; }
    public required string ModelVersion { get; set; }
    public required string GeneratedBody { get; set; }
    public required string CurrentBody { get; set; }
    public required string FilledValuesJson { get; set; }
    public required string FlagsJson { get; set; }
    public required string InputsJson { get; set; }
    public required string Confidence { get; set; }
    public required string Limitations { get; set; }
    public DraftStatus Status { get; set; } = DraftStatus.Unreviewed;
    public string? HumanReview { get; set; }
    public string? ReviewReason { get; set; }
    public Guid CreatedByUserId { get; set; }
    public Guid? ApprovedByUserId { get; set; }
    public DateTimeOffset? ApprovedAt { get; set; }
    public string? RejectionReason { get; set; }
    public DateTimeOffset? AttachedAt { get; set; }
    public string? AttachedTo { get; set; }
    public uint Version { get; set; }
}

public sealed class DocumentDraftVersion : OrgEntity
{
    public Guid DraftId { get; set; }
    public int VersionNo { get; set; }
    public required string Body { get; set; }
    public Guid ByUserId { get; set; }
    public required string ByLabel { get; set; }
    public DateTimeOffset At { get; set; }
    public string? Note { get; set; }
}

/// <summary>
/// O05 decision-support estimate. A transparent heuristic (not a trained model): it carries its version,
/// inputs, factor contributions, an honest confidence label and limitations. Never shown to the owner and
/// never used by the case workflow.
/// </summary>
public sealed class Prediction : OrgEntity
{
    public Guid CaseId { get; set; }
    public required string SubjectRef { get; set; } // solution:v2
    public required string ModelId { get; set; }
    public required string ModelVersion { get; set; }
    public required string Question { get; set; }
    public decimal ScoreLow { get; set; }
    public decimal ScorePoint { get; set; }
    public decimal ScoreHigh { get; set; }
    public required string Confidence { get; set; } // heuristic_low | heuristic_medium
    public required string ConfidenceNote { get; set; }
    public required string FactorsJson { get; set; }
    public required string InputsJson { get; set; }
    public List<string> ExcludedAttributes { get; set; } = [];
    public required string Limitations { get; set; }
    public DateTimeOffset DataAsOf { get; set; }
    public Guid CreatedByUserId { get; set; }
}

public sealed class PredictionOpinion : OrgEntity
{
    public Guid PredictionId { get; set; }
    public Guid CaseId { get; set; }
    public Guid UserId { get; set; }
    public required string Value { get; set; } // agree | override | not_used
    public string? OverrideDirection { get; set; } // overestimates | underestimates
    public string? Reason { get; set; }
    public DateTimeOffset At { get; set; }
}
