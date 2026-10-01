using Rahoon.Api.Infrastructure.Persistence;

namespace Rahoon.Api.Modules.Market.Discovery;

/// <summary>
/// A search a signed-in person saved (Phase 2). The criteria are stored as the canonical query string of
/// <see cref="SearchCriteria"/>, so a saved search, its URL and its alerts run exactly the same filters as the list and the map.
/// Owned by the person (IApplicantOwned): nobody else can read or change it by altering an id.
/// </summary>
public sealed class SavedSearch : OrgEntity, IApplicantOwned, IConcurrencyVersioned
{
    public Guid ApplicantUserId { get; set; }
    public required string Name { get; set; }
    /// <summary>Canonical query (sorted keys, no page/sort/bbox), e.g. «city=riyadh&maxNow=400000».</summary>
    public required string Query { get; set; }
    /// <summary>SHA-256 of <see cref="Query"/>: one live search per person and criteria.</summary>
    public required string QueryHash { get; set; }
    public bool AlertsEnabled { get; set; }
    /// <summary>in_app | sms — in-app notifications are always written; sms is attempted in addition.</summary>
    public string AlertChannel { get; set; } = "in_app";
    /// <summary>When the person agreed to receive alerts for this search (required while alerts are on).</summary>
    public DateTimeOffset? AlertsConsentAt { get; set; }
    /// <summary>Alerts paused by the person (opt-out); matches published meanwhile are taken as seen on resume.</summary>
    public DateTimeOffset? PausedAt { get; set; }
    /// <summary>Deleted by the person (rows are kept so alert history stays consistent).</summary>
    public DateTimeOffset? DeletedAt { get; set; }
    public DateTimeOffset? LastCheckedAt { get; set; }
    public uint Version { get; set; }
}

public enum SearchAlertStatus
{
    /// <summary>A match that existed when the search was saved or resumed: recorded as seen, never sent.</summary>
    Baseline,
    Pending,
    /// <summary>Claimed for an SMS attempt; never retried automatically (a missed alert is preferred over a duplicate).</summary>
    Sending,
    Sent,
    /// <summary>Delivered in the app; the SMS channel is the sandbox, which sends nothing.</summary>
    Simulated,
    Failed,
    /// <summary>Not sent: the opportunity is no longer published on that version, or the person stopped the alerts.</summary>
    Skipped,
}

/// <summary>
/// One (saved search, opportunity, terms version) seen by the alert job. The unique key is the deduplication: a retry, a
/// second worker or a second run can never alert twice for the same thing.
/// </summary>
public sealed class SearchAlert : OrgEntity, IApplicantOwned
{
    public Guid SavedSearchId { get; set; }
    public Guid ApplicantUserId { get; set; }
    public Guid OpportunityId { get; set; }
    public Guid TermsId { get; set; }
    /// <summary>Cash due now on that version when it matched (a re-alert for a new version needs it to drop).</summary>
    public decimal? DueNow { get; set; }
    /// <summary>baseline | new | revision</summary>
    public required string Kind { get; set; }
    public SearchAlertStatus Status { get; set; }
    public string? Channel { get; set; }
    public int Attempts { get; set; }
    public string? Reason { get; set; }
    public Guid? EventId { get; set; }
    public DateTimeOffset? SentAt { get; set; }
}
