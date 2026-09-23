using Rahoon.Api.Infrastructure.Persistence;

namespace Rahoon.Api.Modules.Agreements;

public enum AgreementStatus { PendingActivation, Active, BreachReview, Completed, Terminated }
public enum SignatureMethod { ConsentRecordOnly, ManualWetSignature, LicensedProvider }

public sealed class Agreement : OrgEntity, IConcurrencyVersioned
{
    public required string Number { get; set; } // AGR-2026-004172-01
    public Guid CaseId { get; set; }
    public Guid SolutionVersionId { get; set; }
    public Guid? OfferId { get; set; }
    public required string VersionLabel { get; set; }
    public decimal RescheduledAmount { get; set; }
    public decimal WaiverAmount { get; set; }
    public int InstallmentCount { get; set; }
    public decimal InstallmentAmount { get; set; }
    public int DueDay { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public int BreachMissedConsecutive { get; set; } = 2;
    public int BreachCureDays { get; set; } = 15;
    public string PaymentMethod { get; set; } = "bank_transfer_offplatform";
    public AgreementStatus Status { get; set; } = AgreementStatus.PendingActivation;
    public SignatureMethod SignatureMethod { get; set; } = SignatureMethod.ConsentRecordOnly;
    public Guid? ConsentRecordId { get; set; }
    public Guid? SignedDocumentVersionId { get; set; }
    public bool LegalReviewDone { get; set; }
    public Guid? LegalReviewedByUserId { get; set; }
    public bool ScheduleCreated { get; set; }
    public bool CoreSystemUpdated { get; set; }
    public Guid? ActivatedByUserId { get; set; }
    public DateTimeOffset? ActivatedAt { get; set; }
    public uint Version { get; set; }
}

public enum InstallmentStatus { Upcoming, Due, RecordedPendingMatch, Matched, Partial, Overdue, Waived }

public sealed class Installment : OrgEntity, IConcurrencyVersioned
{
    public Guid AgreementId { get; set; }
    public Guid CaseId { get; set; }
    public int No { get; set; }
    public DateOnly DueDate { get; set; }
    public decimal Amount { get; set; }
    public decimal PaidAmount { get; set; }
    public InstallmentStatus Status { get; set; } = InstallmentStatus.Upcoming;
    public uint Version { get; set; }
}

public enum PaymentStatus { PendingMatch, Matched, Rejected }

/// <summary>Manually recorded payment (A-04). A different finance user must match it.</summary>
public sealed class PaymentRecord : OrgEntity, IConcurrencyVersioned
{
    public Guid CaseId { get; set; }
    public Guid? InstallmentId { get; set; }
    public decimal Amount { get; set; }
    public DateOnly ReceivedOn { get; set; }
    public required string BankReference { get; set; }
    public Guid? ProofDocumentVersionId { get; set; }
    public string? VarianceReason { get; set; }
    public string Source { get; set; } = "manual";
    public Guid RecordedByUserId { get; set; }
    public DateTimeOffset RecordedAt { get; set; }
    public PaymentStatus Status { get; set; } = PaymentStatus.PendingMatch;
    public Guid? MatchedByUserId { get; set; }
    public DateTimeOffset? MatchedAt { get; set; }
    public string? RejectReason { get; set; }
    public uint Version { get; set; }
}

public enum BreachStatus { Open, Cured, Restructuring, OtherOptions, Closed }

/// <summary>Breach review — never an automatic referral; leads to cure, restructuring or other options.</summary>
public sealed class BreachReview : OrgEntity
{
    public Guid CaseId { get; set; }
    public Guid AgreementId { get; set; }
    public DateTimeOffset TriggeredAt { get; set; }
    public required string Trigger { get; set; }
    public List<int> MissedInstallmentNos { get; set; } = [];
    public DateOnly CureDeadline { get; set; }
    public BreachStatus Status { get; set; } = BreachStatus.Open;
    public string? OutcomeNote { get; set; }
    public Guid? ClosedByUserId { get; set; }
    public DateTimeOffset? ClosedAt { get; set; }
}
