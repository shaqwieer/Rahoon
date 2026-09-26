using Rahoon.Api.Infrastructure.Persistence;
using Rahoon.Api.Modules.Documents;

namespace Rahoon.Api.Modules.Requests;

/*
 * The individual's request (ADR 0001 §4.4). Owned by the operator tenant «فريق رهون» and by the applicant
 * (IApplicantOwned). The lender is a counterparty in a directory, never a tenant: no lender user reads a request.
 * Figures here are declared by the individual (source «العميل») and approximate; they never overwrite lender data.
 */

/// <summary>Directory of financing institutions the individual can name (V6 interim: list + «أخرى»). Not tenant data.</summary>
public sealed class FinancingInstitution : Entity
{
    public required string NameAr { get; set; }
    public string? NameEn { get; set; }
    /// <summary>bank | finance_company</summary>
    public string Kind { get; set; } = "bank";
    public bool Active { get; set; } = true;
    public int SortOrder { get; set; }
    /// <summary>Set only if the institution later joins as a Lender tenant; moving a request there needs new consent.</summary>
    public Guid? LinkedOrganizationId { get; set; }
}

public enum RequestStatus
{
    Draft, Submitted, TeamReview, InfoRequested, LenderCoordination, OfferAvailable, ResponseRecorded, Closed, NotEligible, Withdrawn,
}

/// <summary>Who the request is waiting on (Q6: shown instead of any deadline).</summary>
public enum RequestWaitingOn { None, Team, Applicant, Lender }

/// <summary>The individual's preference (OA03). A hint for the team's study, never a promised outcome (Q11).</summary>
public enum RequestPathPreference { KeepHome, Settlement, SellMyself, NotSure }

public sealed class Request : OrgEntity, IApplicantOwned, IConcurrencyVersioned
{
    public required string Reference { get; set; }
    public Guid ApplicantUserId { get; set; }
    public RequestStatus Status { get; set; } = RequestStatus.Draft;
    public RequestWaitingOn WaitingOn { get; set; } = RequestWaitingOn.Applicant;
    /// <summary>The team's plain-language next step (null → the default text for the status).</summary>
    public string? NextStepText { get; set; }
    public DateTimeOffset StatusChangedAt { get; set; }
    public DateTimeOffset? SubmittedAt { get; set; }
    public Guid? AssignedCoordinatorId { get; set; }
    public DateTimeOffset? AssignedAt { get; set; }
    /// <summary>V12: identity is self-declared until Q10; a team member checks it (documents, lender match) before any coordination.</summary>
    public DateTimeOffset? IdentityCheckedAt { get; set; }
    public Guid? IdentityCheckedByUserId { get; set; }
    public string? IdentityCheckNote { get; set; }
    /// <summary>Status to return to when the individual answers an information request.</summary>
    public RequestStatus? StatusBeforeInfoRequest { get; set; }
    /// <summary>True when «نحتاج معلومة منك» was caused by the individual withdrawing consent (resumed by a new consent).</summary>
    public bool InfoRequestIsConsent { get; set; }

    // OA01 — lender
    public Guid? InstitutionId { get; set; }
    public FinancingInstitution? Institution { get; set; }
    public string? InstitutionOtherName { get; set; }

    // OA02 — finance and property (declared, approximate; Q5 interim required set)
    public string? ApplicantFullName { get; set; }
    public string? ContractNumber { get; set; }
    public decimal? MonthlyInstallment { get; set; }
    /// <summary>lt3m | 3to6m | 6to12m | gt12m | not_late</summary>
    public string? ArrearsDuration { get; set; }
    public string? PropertyCity { get; set; }

    // OA03 — situation and preference
    public RequestPathPreference? PathPreference { get; set; }
    public decimal? AffordableMonthly { get; set; }
    /// <summary>«بكلمات العميل» — optional free text.</summary>
    public string? SituationText { get; set; }

    // V7 interim: warn and link, don't block
    public Guid? DuplicateOfRequestId { get; set; }
    public bool DuplicateAcknowledged { get; set; }

    public DateTimeOffset? WithdrawnAt { get; set; }
    public string? WithdrawReason { get; set; }
    public DateTimeOffset? ClosedAt { get; set; }
    public string? OutcomeCode { get; set; }
    public string? OutcomeSummary { get; set; }
    public string? NotEligibleReason { get; set; }

    public uint Version { get; set; }

    public string InstitutionDisplayName => Institution?.NameAr ?? InstitutionOtherName ?? "";
}

/// <summary>
/// Documented consent to share the request with the named lender (V4: provisional text). Append-only: a change of
/// recipient or a withdrawal closes this row (WithdrawnAt) and a new consent is a new row.
/// </summary>
public sealed class RequestConsent : OrgEntity, IApplicantOwned
{
    public Guid RequestId { get; set; }
    public Guid ApplicantUserId { get; set; }
    public required string TextVersion { get; set; }
    public required string TextSnapshot { get; set; }
    public Guid? InstitutionId { get; set; }
    public required string RecipientName { get; set; }
    public List<string> DataCategories { get; set; } = [];
    public DateTimeOffset OtpVerifiedAt { get; set; }
    public string? IpMasked { get; set; }
    public DateTimeOffset? WithdrawnAt { get; set; }
    /// <summary>withdrawn_by_applicant | recipient_changed | request_withdrawn</summary>
    public string? WithdrawnReason { get; set; }
}

public enum RequestDocumentVisibility { ApplicantAndTeam, TeamOnly }

public sealed class RequestDocument : OrgEntity, IApplicantOwned
{
    public Guid RequestId { get; set; }
    public Guid ApplicantUserId { get; set; }
    /// <summary>salary_statement | bank_statement | title_deed | financing_contract | other | lender_letter</summary>
    public required string Kind { get; set; }
    public required string Name { get; set; }
    /// <summary>applicant | team</summary>
    public string Source { get; set; } = "applicant";
    public RequestDocumentVisibility Visibility { get; set; } = RequestDocumentVisibility.ApplicantAndTeam;
    public bool AddedAfterSubmit { get; set; }
    public int VersionCount { get; set; }
    public Guid? CurrentVersionId { get; set; }
    public List<RequestDocumentVersion> Versions { get; set; } = [];
}

public sealed class RequestDocumentVersion : OrgEntity, IApplicantOwned
{
    public Guid RequestId { get; set; }
    public Guid ApplicantUserId { get; set; }
    public Guid DocumentId { get; set; }
    public int VersionNo { get; set; }
    public required string FileName { get; set; }
    public required string ContentType { get; set; }
    public long SizeBytes { get; set; }
    public required string Sha256 { get; set; }
    public required string StorageKey { get; set; }
    public Guid UploadedByUserId { get; set; }
    public required string UploadedByLabel { get; set; }
    public DateTimeOffset UploadedAt { get; set; }
    public ScanStatus ScanStatus { get; set; }
}

/// <summary>
/// Timeline entry. <see cref="VisibleToApplicant"/> false = internal (team-only) and never projected to the individual.
/// Past events only — never a future date (Q6).
/// </summary>
public sealed class RequestUpdate : OrgEntity, IApplicantOwned
{
    public Guid RequestId { get; set; }
    public Guid ApplicantUserId { get; set; }
    /// <summary>created | submitted | info_added | withdrawn | status | team_update | consent | …</summary>
    public required string Kind { get; set; }
    public required string Title { get; set; }
    public string? Body { get; set; }
    /// <summary>applicant | team | system</summary>
    public string AuthorKind { get; set; } = "system";
    public string? AuthorLabel { get; set; }
    public Guid? AuthorUserId { get; set; }
    public bool VisibleToApplicant { get; set; } = true;
    public DateTimeOffset At { get; set; }
}

/// <summary>
/// The manual channel with the lender (V2 wording provisional). Append-only: a correction is a new entry that references
/// the corrected one. Only entries marked <see cref="VisibleToApplicant"/> reach the individual, as <see cref="ApplicantText"/>.
/// Coordination means sharing consented data and relaying — never acting on the individual's behalf (V1).
/// </summary>
public sealed class CoordinationEntry : OrgEntity, IApplicantOwned
{
    public Guid RequestId { get; set; }
    public Guid ApplicantUserId { get; set; }
    /// <summary>general | response_relay | offer_received | lender_response</summary>
    public string Kind { get; set; } = "general";
    /// <summary>phone | email | letter | visit | other</summary>
    public required string Channel { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
    /// <summary>The lender-side counterpart, name/role as given.</summary>
    public required string Counterpart { get; set; }
    public required string Summary { get; set; }
    public List<Guid> EvidenceDocumentIds { get; set; } = [];
    public bool VisibleToApplicant { get; set; }
    public string? ApplicantText { get; set; }
    public Guid? CorrectsEntryId { get; set; }
    public Guid RecordedByUserId { get; set; }
    public required string RecordedByLabel { get; set; }
}

/// <summary>Messages between the individual and the Rahoon team. Internal notes are team-only and never projected.</summary>
public sealed class RequestMessage : OrgEntity, IApplicantOwned
{
    public Guid RequestId { get; set; }
    public Guid ApplicantUserId { get; set; }
    /// <summary>applicant | team</summary>
    public required string AuthorKind { get; set; }
    public Guid AuthorUserId { get; set; }
    public required string AuthorLabel { get; set; }
    public required string Body { get; set; }
    public bool IsInternal { get; set; }
    public DateTimeOffset At { get; set; }
}
