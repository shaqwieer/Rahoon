using Rahoon.Api.Infrastructure.Persistence;

namespace Rahoon.Api.Modules.Sale;

/// <summary>
/// Lifecycle of a voluntary-sale track (B8). A track starts only from the owner's explicit portal
/// request; the formal scoped consent follows the lender's approved decision.
/// </summary>
public enum SaleStatus
{
    Requested,        // owner asked through the portal (documented, OTP) — satisfies the transition guard
    PendingDecision,  // lender decision submitted for approval (L27)
    AwaitingConsent,  // decision approved, case = بيع طوعي, formal scoped consent requested (L28)
    Active,           // consent signed: preparation, controlled listing, broker, offers
    OfferApproved,    // a buyer offer is approved by owner + bank; execution tracking (L33)
    Completed,        // price received + mortgage released → case awaits reconciliation
    Withdrawn,        // owner withdrew before an offer was accepted
    DecisionRejected, // lender approver rejected opening the track
    Expired,          // mandate ended without an accepted offer
}

public enum SaleConsentStatus { Signed, Withdrawn, Expired, Fulfilled }
public enum PrepItemStatus { Done, Scheduled, NotStarted }
public enum ListingStatus { Draft, ComplianceReviewed, Closed }
public enum BuyerPaymentMethod { Cash, FinancingPreapproved, FinancingPending }
public enum OfferCertainty { High, Medium, Low }

public enum BuyerOfferStatus
{
    Received,         // entered by the assigned broker or the lender
    SharedWithOwner,  // anonymised comparison published to the owner portal
    OwnerAccepted,    // owner accepted in the portal with OTP (consent fulfilled; withdrawal closed)
    OwnerDeclined,
    PendingApproval,  // bank approval requested (maker)
    Approved,         // bank approval (checker, step-up)
    Rejected,
    Expired,
    Superseded,
    Withdrawn,
}

public enum TrackingSource { Internal, ExternalManual }
public enum TrackingStatus { Done, Pending, NotStarted }

/// <summary>The voluntary-sale track of one case. Buyer-facing reference VS-YYYY-NNNN never reveals the case or bank.</summary>
public sealed class VoluntarySale : OrgEntity, IConcurrencyVersioned
{
    public Guid CaseId { get; set; }
    public required string BuyerReference { get; set; }
    public SaleStatus Status { get; set; } = SaleStatus.Requested;

    // Owner request (D15 before the decision)
    public required string RequestText { get; set; }
    public string RequestChannel { get; set; } = "portal";
    public DateTimeOffset RequestedAt { get; set; }
    public Guid? RequestConsentRecordId { get; set; }
    public decimal? ProposedMinPrice { get; set; }

    // Decision (L27)
    public string? DecisionReason { get; set; }
    public Guid? DecisionPreparedByUserId { get; set; }
    public DateTimeOffset? DecisionSubmittedAt { get; set; }
    public Guid? DecisionApprovalRequestId { get; set; }
    public Guid? DecisionApprovedByUserId { get; set; }
    public DateTimeOffset? OpenedAt { get; set; }

    // Estimate snapshot (source + as-of on every line)
    public Guid? ValuationReportId { get; set; }
    public decimal? ValuationAmount { get; set; }
    public DateOnly? ValuationDate { get; set; }
    public string? ValuerName { get; set; }
    public decimal? OutstandingDebt { get; set; }
    public DateTimeOffset? DebtAsOf { get; set; }
    public decimal BrokerRate { get; set; } = 0.02m;
    public decimal OtherFeesEstimate { get; set; } = 8_000m;

    // Controlled listing (L30)
    public ListingStatus ListingStatus { get; set; } = ListingStatus.Draft;
    public string? AreaLabel { get; set; }
    public decimal? AskingPrice { get; set; }
    public int EvacuationDays { get; set; } = 60;
    public string? VisitTerms { get; set; }
    public string? OccupancyNote { get; set; }
    public int ApprovedPhotoCount { get; set; }
    public Guid? ListingPreparedByUserId { get; set; }
    public Guid? ListingReviewedByUserId { get; set; }
    public DateTimeOffset? ListingReviewedAt { get; set; }

    // Broker (L31) — a ProviderAssignment of type Brokerage scoped to the sale file
    public Guid? BrokerAssignmentId { get; set; }
    public Guid? BrokerOrganizationId { get; set; }

    // Outcome
    public Guid? AcceptedOfferId { get; set; }
    public DateTimeOffset? ClosedAt { get; set; }
    public string? CloseReason { get; set; }
    public uint Version { get; set; }
}

/// <summary>
/// Formal scoped owner consent (L28/D15b): minimum price, 90-day mandate, visit times, withdrawal right.
/// A consent record + OTP inside the platform — not a licensed electronic signature (A-05).
/// </summary>
public sealed class SaleConsent : OrgEntity
{
    public Guid SaleId { get; set; }
    public Guid CaseId { get; set; }
    public int VersionNo { get; set; } = 1;
    public decimal MinPrice { get; set; }
    public int MandateDays { get; set; } = 90;
    public DateOnly MandateStart { get; set; }
    public DateOnly MandateEnd { get; set; }
    public List<string> VisitDays { get; set; } = [];
    public string? VisitWindow { get; set; }
    public SaleConsentStatus Status { get; set; } = SaleConsentStatus.Signed;
    public Guid ConsentRecordId { get; set; }
    public DateTimeOffset SignedAt { get; set; }
    public string TextVersion { get; set; } = SaleTexts.ConsentTextVersion;
    public required string TextHash { get; set; }
    public DateTimeOffset? WithdrawnAt { get; set; }
    public string? WithdrawReason { get; set; }
    public DateTimeOffset? FulfilledAt { get; set; }
}

/// <summary>Property preparation checklist item (L29) with a responsible party.</summary>
public sealed class SalePrepItem : OrgEntity
{
    public Guid SaleId { get; set; }
    public Guid CaseId { get; set; }
    public required string Key { get; set; }
    public required string Title { get; set; }
    public PrepItemStatus Status { get; set; } = PrepItemStatus.NotStarted;
    public string? Memo { get; set; }
    public DateOnly? ScheduledOn { get; set; }
    public required string Responsible { get; set; } // owner | legal | finance | broker | compliance | case_manager | owner_case_manager | none
    public int SortOrder { get; set; }
    public Guid? UpdatedByUserId { get; set; }
}

/// <summary>
/// A buyer's offer entered by the assigned broker or the lender. Buyers are not platform users;
/// no buyer identity is stored — only the qualified-buyer label and the broker's NDA confirmation.
/// </summary>
public sealed class BuyerOffer : OrgEntity, IConcurrencyVersioned
{
    public Guid SaleId { get; set; }
    public Guid CaseId { get; set; }
    public required string Code { get; set; } // OF-01
    public decimal Price { get; set; }
    public BuyerPaymentMethod PaymentMethod { get; set; }
    public string? ProofOfFunds { get; set; }
    public string? Conditions { get; set; }
    public int ProposedTransferDays { get; set; }
    public DateOnly ValidUntil { get; set; }
    public OfferCertainty Certainty { get; set; } = OfferCertainty.Medium;
    public string BuyerLabel { get; set; } = "مشترٍ مؤهل";
    public bool BuyerNdaConfirmed { get; set; }
    public required string EnteredBySide { get; set; } // broker | lender
    public Guid EnteredByUserId { get; set; }
    public DateTimeOffset ReceivedAt { get; set; }
    public BuyerOfferStatus Status { get; set; } = BuyerOfferStatus.Received;
    public DateTimeOffset? SharedWithOwnerAt { get; set; }
    public DateTimeOffset? OwnerDecisionAt { get; set; }
    public Guid? OwnerConsentRecordId { get; set; }
    public string? OwnerDeclineReason { get; set; }
    public string? Recommendation { get; set; }
    public Guid? ApprovalRequestId { get; set; }
    public DateTimeOffset? DecidedAt { get; set; }
    public uint Version { get; set; }
}

/// <summary>
/// Execution timeline (L33). External steps are entered manually with their source, time and
/// reference — never displayed as officially confirmed.
/// </summary>
public sealed class SaleTrackingStep : OrgEntity
{
    public Guid SaleId { get; set; }
    public Guid CaseId { get; set; }
    public required string Key { get; set; }
    public required string Title { get; set; }
    public string? Memo { get; set; }
    public TrackingSource Source { get; set; }
    public TrackingStatus Status { get; set; } = TrackingStatus.NotStarted;
    public DateOnly? ActualDate { get; set; }
    public DateOnly? ExpectedDate { get; set; }
    public string? Reference { get; set; }
    public Guid? DocumentVersionId { get; set; }
    public Guid? EnteredByUserId { get; set; }
    public DateTimeOffset? EnteredAt { get; set; }
    public int SortOrder { get; set; }
}

public static class SaleTexts
{
    public const string ConsentTextVersion = "SALE-CONSENT-v1";
    public const string OfferAcceptanceTextVersion = "SALE-OFFER-ACCEPT-v1";

    /// <summary>Owner-request acknowledgements (the four D15a explanation points).</summary>
    public static readonly string[] RequestAcks = ["proceeds_repay", "owner_decides", "privacy", "withdrawal_right"];

    /// <summary>The single D15b acknowledgement: «أوافق باختياري على عرض عقاري للبيع بهذه الشروط».</summary>
    public const string ConsentAck = "voluntary_sale_terms";

    public static readonly string[] VisitDayKeys = ["sun", "mon", "tue", "wed", "thu", "sat"];

    public static string VisitDayLabel(string key) => key switch
    {
        "sun" => "الأحد", "mon" => "الاثنين", "tue" => "الثلاثاء", "wed" => "الأربعاء", "thu" => "الخميس", "sat" => "السبت", _ => key,
    };
}
