using Rahoon.Api.Infrastructure.Persistence;
using Rahoon.Api.Infrastructure.Storage;

namespace Rahoon.Api.Modules.Market;

/*
 * The exit/buy platform (docs/product/product-definition.md, 2026-10-01). Schema «market».
 * Every row is owned by the operator tenant «فريق رهون» and, where it belongs to one person, by that person
 * (IApplicantOwned): the seller for sale requests and opportunities, the buyer for buyer requests, interests and saved items.
 * Answers are stored as catalog keys → invariant strings (FieldCatalog decides which apply; inactive branches are dropped).
 * None of these entities reuses a mortgage-help entity: a sale request is not a renamed help request.
 */

public enum SaleRequestStatus { Draft, Submitted, UnderReview, NeedsCompletion, ApprovedForListing, Rejected, Withdrawn }

public sealed class SaleRequest : OrgEntity, IApplicantOwned, IConcurrencyVersioned
{
    public required string Reference { get; set; }
    public Guid ApplicantUserId { get; set; }
    /// <summary>The device draft this request was created from; (applicant, client draft) is unique so a retry never duplicates.</summary>
    public Guid? ClientDraftId { get; set; }
    public SaleRequestStatus Status { get; set; } = SaleRequestStatus.Draft;
    public DateTimeOffset StatusChangedAt { get; set; }
    public DateTimeOffset? SubmittedAt { get; set; }

    // Step 1 — property and obligation party
    public string? PropertyType { get; set; }
    public string? City { get; set; }
    public string? District { get; set; }
    public string? Project { get; set; }
    /// <summary>developer | financier | multiple</summary>
    public string? ObligationMode { get; set; }

    /// <summary>Property-scope answers (FieldCatalog, scope Property), only the keys that apply.</summary>
    public Dictionary<string, string> Answers { get; set; } = new();

    // Location chosen by the owner on the map (the real point). What the public sees is decided on the opportunity.
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    /// <summary>exact | approximate — the owner's wish for public display; the team decides on the opportunity.</summary>
    public string? LocationDisplayWish { get; set; }
    public string? LocationLabel { get; set; }

    // Step 3 — contact and confirmation
    public string? ContactName { get; set; }
    public string? ContactEmail { get; set; }
    /// <summary>owner | authorized</summary>
    public string? RelationshipDeclared { get; set; }
    public DateTimeOffset? DeclarationsAcceptedAt { get; set; }
    public string? DeclarationsVersion { get; set; }

    // Team
    public Guid? AssignedToUserId { get; set; }
    public string? AssignedToLabel { get; set; }
    public DateTimeOffset? AssignedAt { get; set; }
    public string? DecisionReason { get; set; }
    public DateTimeOffset? DecidedAt { get; set; }
    public Guid? DecidedByUserId { get; set; }
    public DateTimeOffset? OwnerMarkedCompleteAt { get; set; }
    public string? WithdrawReason { get; set; }

    /// <summary>Seeded test data, labelled «تجريبي» wherever it is shown (never real people, prices or properties).</summary>
    public bool IsDemo { get; set; }
    public uint Version { get; set; }

    public List<SaleObligation> Obligations { get; set; } = [];
}

/// <summary>One obligation of a sale request (developer or financier). Several when the property is tied to more than one party.</summary>
public sealed class SaleObligation : OrgEntity, IApplicantOwned
{
    public Guid SaleRequestId { get; set; }
    public Guid ApplicantUserId { get; set; }
    public int SortOrder { get; set; }
    /// <summary>developer | financier</summary>
    public required string Kind { get; set; }
    /// <summary>The directory organization the owner chose (null when they typed a name not in the directory).</summary>
    public Guid? PartyId { get; set; }
    /// <summary>The directory name at the time of choosing — kept so later directory edits never rewrite a request.</summary>
    public string? PartyName { get; set; }
    public string? PartyOtherName { get; set; }
    /// <summary>For several obligations: how this one relates to the others, in the owner's words.</summary>
    public string? RelationNote { get; set; }
    public Dictionary<string, string> Answers { get; set; } = new();
    /// <summary>Removed when the owner switches the obligation party (rows are kept for the record, never deleted).</summary>
    public DateTimeOffset? RemovedAt { get; set; }

    public string PartyDisplayName => PartyName ?? PartyOtherName ?? "";
}

public enum FileReviewStatus { Pending, Accepted, Rejected }

/// <summary>A private document (contract, statement, letter, ownership). Only the owner and the team ever read it; never published.</summary>
public sealed class PrivateDocument : OrgEntity, IApplicantOwned
{
    public Guid SaleRequestId { get; set; }
    public Guid ApplicantUserId { get; set; }
    public Guid? ObligationId { get; set; }
    /// <summary>FieldCatalog document key (developer_contract, payment_proof, payoff_letter, ownership_proof, other, …).</summary>
    public required string Kind { get; set; }
    /// <summary>The stored file (metadata in files.stored_files, bytes behind the storage provider).</summary>
    public Guid FileId { get; set; }
    public StoredFile? File { get; set; }
    /// <summary>applicant | team</summary>
    public string Source { get; set; } = "applicant";
    public Guid UploadedByUserId { get; set; }
    public FileReviewStatus ReviewStatus { get; set; }
    public string? ReviewNote { get; set; }
    public Guid? ReviewedByUserId { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }
    /// <summary>Soft removal (individuals never delete rows); removed files stay for the record.</summary>
    public DateTimeOffset? RemovedAt { get; set; }
}

/// <summary>A listing photo. Stored apart from private documents; public only inside a published opportunity once approved.</summary>
public sealed class ListingPhoto : OrgEntity, IApplicantOwned
{
    public Guid SaleRequestId { get; set; }
    public Guid ApplicantUserId { get; set; }
    /// <summary>The stored file, metadata already stripped (EXIF/XMP/IPTC, so no GPS position).</summary>
    public Guid FileId { get; set; }
    public StoredFile? File { get; set; }
    public int SortOrder { get; set; }
    public bool IsCover { get; set; }
    public FileReviewStatus ReviewStatus { get; set; }
    public string? ReviewNote { get; set; }
    public Guid UploadedByUserId { get; set; }
    public DateTimeOffset? RemovedAt { get; set; }
}

/// <summary>
/// A figure the team approved with its source and date (e.g. the paid amount from the developer's statement). Append-only:
/// a later verification or an owner edit of the same key supersedes it.
/// </summary>
public sealed class FigureVerification : OrgEntity, IApplicantOwned
{
    public Guid SaleRequestId { get; set; }
    public Guid ApplicantUserId { get; set; }
    /// <summary>Catalog key, prefixed for obligations: «o:{obligationId}:paid_approved»; property keys as is.</summary>
    public required string FieldKey { get; set; }
    public required string Value { get; set; }
    /// <summary>owner_declared | contract | developer_statement | financier_statement | payoff_letter | title_deed | other</summary>
    public required string Source { get; set; }
    public Guid? SourceDocumentId { get; set; }
    public DateOnly SourceDate { get; set; }
    public string? Note { get; set; }
    public Guid VerifiedByUserId { get; set; }
    public required string VerifiedByLabel { get; set; }
    public DateTimeOffset VerifiedAt { get; set; }
    public DateTimeOffset? SupersededAt { get; set; }
}

public enum ExternalApprovalStatus { NotRequested, Requested, Conditional, Approved, Rejected, Expired }

/// <summary>
/// The developer's or financier's approval of a transfer, recorded by the team from that party's document. Separate from
/// Rahoon's approval of the request: approving a request never makes the buyer able to take over the owner's installments.
/// Append-only history: the latest row per obligation is the current state.
/// </summary>
public sealed class ExternalApproval : OrgEntity, IApplicantOwned
{
    public Guid SaleRequestId { get; set; }
    public Guid ApplicantUserId { get; set; }
    public Guid ObligationId { get; set; }
    public ExternalApprovalStatus Status { get; set; }
    public string? Conditions { get; set; }
    public Guid? DocumentId { get; set; }
    public DateOnly? DecisionDate { get; set; }
    public DateOnly? ExpiresOn { get; set; }
    public string? Note { get; set; }
    public Guid RecordedByUserId { get; set; }
    public required string RecordedByLabel { get; set; }
    public DateTimeOffset RecordedAt { get; set; }
}

/// <summary>A specific completion request from the team: the items still missing and a note. Answered when the owner resubmits.</summary>
public sealed class CompletionRequest : OrgEntity, IApplicantOwned
{
    /// <summary>sale_request | buyer_request</summary>
    public required string SubjectType { get; set; }
    public Guid SubjectId { get; set; }
    public Guid ApplicantUserId { get; set; }
    /// <summary>Catalog field/document keys the team asks for (display labels resolved from the catalog).</summary>
    public List<string> Items { get; set; } = [];
    public required string Note { get; set; }
    public Guid RequestedByUserId { get; set; }
    public required string RequestedByLabel { get; set; }
    public DateTimeOffset RequestedAt { get; set; }
    public DateTimeOffset? AnsweredAt { get; set; }
}

public enum BuyerRequestStatus { Draft, Submitted, UnderReview, NeedsCompletion, ApprovedForMatching, Rejected, Withdrawn }

/// <summary>
/// The buyer's capacity and preferences. Three separate levels of capacity: declared by the buyer; proof reviewed by the team;
/// financing approval issued by a financing party (recorded by the team). Team approval of the profile is not bank approval.
/// </summary>
public sealed class BuyerRequest : OrgEntity, IApplicantOwned, IConcurrencyVersioned
{
    public required string Reference { get; set; }
    public Guid ApplicantUserId { get; set; }
    public Guid? ClientDraftId { get; set; }
    public BuyerRequestStatus Status { get; set; } = BuyerRequestStatus.Draft;
    public DateTimeOffset StatusChangedAt { get; set; }
    public DateTimeOffset? SubmittedAt { get; set; }

    // Step 1 — capacity (declared)
    public decimal? AvailableNow { get; set; }
    public decimal? InstallmentComfort { get; set; }
    /// <summary>monthly | quarterly | semiannual | annual</summary>
    public string? InstallmentFrequency { get; set; }
    public decimal? MaxPrice { get; set; }
    /// <summary>cash | external_finance | undecided</summary>
    public string? PurchaseMode { get; set; }
    /// <summary>With external finance: the bank or finance company the buyer prefers (directory id + the name when chosen), optional.</summary>
    public Guid? PreferredFinancierId { get; set; }
    public string? PreferredFinancierName { get; set; }

    // Step 2 — preferences
    public List<string> Cities { get; set; } = [];
    public string? AreasText { get; set; }
    public List<string> PropertyTypes { get; set; } = [];
    public decimal? AreaMin { get; set; }
    public decimal? AreaMax { get; set; }
    public int? BedroomsMin { get; set; }
    /// <summary>ready | under_construction | any</summary>
    public string? Readiness { get; set; }
    /// <summary>YYYY-MM: preferred delivery no later than.</summary>
    public string? DeliveryBy { get; set; }
    /// <summary>Bumped on every change of capacity or preferences; matches report the revision they were computed from.</summary>
    public int PreferencesRevision { get; set; } = 1;

    // Step 3
    public string? ContactName { get; set; }
    public DateTimeOffset? DeclarationsAcceptedAt { get; set; }
    public string? DeclarationsVersion { get; set; }

    // Team-reviewed capacity (separate from the declaration)
    public decimal? ReviewedAvailableNow { get; set; }
    public string? CapacityReviewNote { get; set; }
    public DateTimeOffset? CapacityReviewedAt { get; set; }
    public string? CapacityReviewedByLabel { get; set; }
    /// <summary>none | pre_approval | approved — as issued by a financing party, recorded by the team.</summary>
    public string FinanceApprovalStatus { get; set; } = "none";
    public string? FinanceApprovalSource { get; set; }
    public DateOnly? FinanceApprovalDate { get; set; }
    public decimal? FinanceApprovalAmount { get; set; }

    public Guid? AssignedToUserId { get; set; }
    public string? AssignedToLabel { get; set; }
    public string? DecisionReason { get; set; }
    public DateTimeOffset? DecidedAt { get; set; }
    public string? WithdrawReason { get; set; }
    /// <summary>Seeded test data, labelled «تجريبي» wherever it is shown (never real people, prices or properties).</summary>
    public bool IsDemo { get; set; }

    public uint Version { get; set; }
}

public enum OpportunityStatus
{
    Preparing, AwaitingOwnerConfirmation, ReadyToPublish, Published, Paused,
    /// <summary>Phase M3 (not reachable in this phase).</summary>
    ProvisionallyReserved,
    /// <summary>Phase M3 (not reachable in this phase).</summary>
    Closing,
    /// <summary>Phase M3 (not reachable in this phase).</summary>
    Completed,
    Withdrawn,
}

/// <summary>
/// An opportunity prepared by the team from an approved sale request. Owned by the seller (IApplicantOwned) so they can read it;
/// they never write it — confirmation goes through its terms version. Public readers get <see cref="PublicLatitude"/> only.
/// </summary>
public sealed class Opportunity : OrgEntity, IApplicantOwned, IConcurrencyVersioned
{
    public required string Reference { get; set; }
    public Guid SaleRequestId { get; set; }
    public Guid ApplicantUserId { get; set; }
    public OpportunityStatus Status { get; set; } = OpportunityStatus.Preparing;
    public DateTimeOffset StatusChangedAt { get; set; }

    public required string Title { get; set; }
    public string? Description { get; set; }
    public required string PropertyType { get; set; }
    public required string City { get; set; }
    public string? District { get; set; }
    public string? Project { get; set; }
    /// <summary>The developer of the live developer obligation (directory id + the name recorded), for the public developer filter.
    /// Public only when it comes from the directory (<see cref="DeveloperPartyId"/> set); a name the owner typed stays internal.
    /// The financier's identity is never copied here: it is never public.</summary>
    public Guid? DeveloperPartyId { get; set; }
    public string? DeveloperName { get; set; }
    /// <summary>developer | financier | mixed</summary>
    public required string Track { get; set; }
    public decimal? Area { get; set; }
    public int? Bedrooms { get; set; }
    public int? Bathrooms { get; set; }
    /// <summary>ready | under_construction (null for land)</summary>
    public string? Readiness { get; set; }
    /// <summary>YYYY-MM, only when under construction and known.</summary>
    public string? DeliveryMonth { get; set; }
    /// <summary>Display specs that fit the property type (floor, land area, frontages, …) as catalog key → value.</summary>
    public Dictionary<string, string> Specs { get; set; } = new();
    public List<string> Features { get; set; } = [];

    /// <summary>The real point (from the sale request), never sent to the public.</summary>
    public double? ExactLatitude { get; set; }
    public double? ExactLongitude { get; set; }
    /// <summary>exact | approximate — decided by the team with the owner's agreement.</summary>
    public string LocationPrecision { get; set; } = "approximate";
    /// <summary>The only coordinates any public payload or map link uses.</summary>
    public double? PublicLatitude { get; set; }
    public double? PublicLongitude { get; set; }

    /// <summary>Approved listing photos in display order; the first is the cover.</summary>
    public List<Guid> PhotoIds { get; set; } = [];

    /// <summary>Pre-publication checklist ticked by the team (relationship, figures, photos_location, completion_path, approvals).</summary>
    public List<string> Checklist { get; set; } = [];

    public Guid? DraftTermsId { get; set; }
    public Guid? PublishedTermsId { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }
    public Guid? PublishedByUserId { get; set; }
    public DateTimeOffset? FirstPublishedAt { get; set; }
    public string? PauseReason { get; set; }
    public string? WithdrawReason { get; set; }
    public Guid PreparedByUserId { get; set; }
    public required string PreparedByLabel { get; set; }
    public Guid? AssignedToUserId { get; set; }
    public string? AssignedToLabel { get; set; }
    /// <summary>Seeded test data, labelled «تجريبي» wherever it is shown (never real people, prices or properties).</summary>
    public bool IsDemo { get; set; }

    public uint Version { get; set; }
}

public enum TermsStatus { Draft, SentToOwner, OwnerConfirmed, OwnerRequestedChanges, Superseded }

/// <summary>
/// One version of an opportunity's figures and transfer terms. Immutable once sent to the owner: any change makes a new version
/// that needs the owner's confirmation and a new publication. Interests keep the version they were made on.
/// Inputs are the calculator's; the computed snapshot is stored typed for search (<see cref="DueNow"/> null = unknown, never 0).
/// </summary>
public sealed class OpportunityTerms : OrgEntity, IApplicantOwned
{
    public Guid OpportunityId { get; set; }
    public Guid ApplicantUserId { get; set; }
    public int VersionNo { get; set; }
    public TermsStatus Status { get; set; } = TermsStatus.Draft;
    /// <summary>developer | financier | mixed</summary>
    public required string Track { get; set; }

    /// <summary>Calculator inputs (MarketCalculator.TermsInput) as JSON, with each figure's state (verified/declared/estimated).</summary>
    public required string InputJson { get; set; }
    /// <summary>The computed breakdown shown to the owner and the public (MarketCalculator.TermsResult) as JSON.</summary>
    public required string ResultJson { get; set; }

    // Typed snapshot for search and sort.
    public decimal? DueNow { get; set; }
    public decimal? PurchaseTotal { get; set; }
    public decimal? FutureBalance { get; set; }
    public decimal? Installment { get; set; }
    public string? InstallmentFrequency { get; set; }
    public decimal? InstallmentMonthlyEquivalent { get; set; }
    public decimal? LargestExtraPayment { get; set; }
    public int? RemainingMonths { get; set; }
    public bool NeedsNewFinancing { get; set; }
    public bool Complete { get; set; }
    /// <summary>complete_verified | complete_estimate | incomplete (as computed when the version was made).</summary>
    public string? Quality { get; set; }
    /// <summary>once | annual — of the developer's extra payment; null when there is none or it isn't recorded.</summary>
    public string? ExtraPaymentRecurrence { get; set; }
    public decimal? AnnualExtraPayment { get; set; }
    public decimal? OneOffExtraPayment { get; set; }
    /// <summary>The next extra payment's date as recorded (text, YYYY-MM-DD); there is no next-installment date in the model.</summary>
    public string? NextExtraPaymentDate { get; set; }
    /// <summary>
    /// The future schedule is known well enough for a strict affordability check: no future balance, or the installment and its
    /// frequency known and any extra payment's amount and recurrence known. Otherwise affordability is «incomplete».
    /// </summary>
    public bool ScheduleKnown { get; set; }

    public string? TransferConditions { get; set; }
    /// <summary>What has been checked, in plain words (shown with the verification badge).</summary>
    public string? VerificationScope { get; set; }
    public DateOnly? VerifiedOn { get; set; }

    public Guid PreparedByUserId { get; set; }
    public required string PreparedByLabel { get; set; }
    public DateTimeOffset? SentToOwnerAt { get; set; }
    public DateTimeOffset? OwnerDecidedAt { get; set; }
    public string? OwnerNote { get; set; }
    public string? OwnerConfirmationText { get; set; }
}

public enum InterestStatus { Received, InFollowUp, Closed, Withdrawn }

/// <summary>
/// A buyer's interest in an opportunity, tied to the terms version they saw and to their buyer request if any. It never
/// reserves the property, never accepts an offer and never changes the opportunity's status.
/// </summary>
public sealed class Interest : OrgEntity, IApplicantOwned
{
    public required string Reference { get; set; }
    public Guid ApplicantUserId { get; set; }
    public Guid OpportunityId { get; set; }
    public Guid TermsId { get; set; }
    public Guid? BuyerRequestId { get; set; }
    public string? Message { get; set; }
    /// <summary>call | whatsapp | any</summary>
    public string? ContactPreference { get; set; }
    public string? ContactName { get; set; }
    public InterestStatus Status { get; set; } = InterestStatus.Received;
    public DateTimeOffset StatusChangedAt { get; set; }
    public Guid? AssignedToUserId { get; set; }
    public string? AssignedToLabel { get; set; }
    public string? CloseReason { get; set; }
}

public sealed class SavedOpportunity : OrgEntity, IApplicantOwned
{
    public Guid ApplicantUserId { get; set; }
    public Guid OpportunityId { get; set; }
    /// <summary>Unsaved (rows are toggled, never deleted by the person).</summary>
    public DateTimeOffset? RemovedAt { get; set; }
}

/// <summary>
/// Timeline and decision log for every market object: who changed what, when, why, and on what basis. Entries marked
/// <see cref="VisibleToApplicant"/> are the person's in-app notifications.
/// </summary>
public sealed class MarketEvent : OrgEntity, IApplicantOwned
{
    /// <summary>sale_request | buyer_request | opportunity | interest</summary>
    public required string SubjectType { get; set; }
    public Guid SubjectId { get; set; }
    public Guid ApplicantUserId { get; set; }
    public required string Kind { get; set; }
    public required string Title { get; set; }
    public string? Body { get; set; }
    public string? FromStatus { get; set; }
    public string? ToStatus { get; set; }
    public string? Reason { get; set; }
    /// <summary>applicant | team | system</summary>
    public required string ActorKind { get; set; }
    public Guid? ActorUserId { get; set; }
    public string? ActorLabel { get; set; }
    public bool VisibleToApplicant { get; set; }
    public string? DataJson { get; set; }
    public DateTimeOffset At { get; set; }
    public DateTimeOffset? ReadAt { get; set; }
}

/// <summary>Every attempt to notify someone outside the app, with its honest result (simulated / failed / sent).</summary>
public sealed class MarketNotification : OrgEntity
{
    public Guid? UserId { get; set; }
    public Guid? EventId { get; set; }
    /// <summary>sms</summary>
    public required string Channel { get; set; }
    public required string DestinationMasked { get; set; }
    public required string Body { get; set; }
    /// <summary>sent | simulated | failed | unavailable</summary>
    public required string Result { get; set; }
    public string? Error { get; set; }
    public DateTimeOffset At { get; set; }
}

public enum ContactMessageStatus { New, Handled }

/// <summary>A general enquiry from the contact page. Personal data is encrypted with masked copies.</summary>
public sealed class ContactMessage : OrgEntity
{
    public required string Reference { get; set; }
    public required string Name { get; set; }
    public required string PhoneEnc { get; set; }
    public required string PhoneMasked { get; set; }
    /// <summary>sell | buy | request | other</summary>
    public required string Topic { get; set; }
    public required string Message { get; set; }
    public Guid? UserId { get; set; }
    public ContactMessageStatus Status { get; set; }
    public string? HandledNote { get; set; }
    public Guid? HandledByUserId { get; set; }
    public string? HandledByLabel { get; set; }
    public DateTimeOffset? HandledAt { get; set; }
}
