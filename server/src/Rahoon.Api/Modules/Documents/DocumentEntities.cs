using Rahoon.Api.Infrastructure.Persistence;

namespace Rahoon.Api.Modules.Documents;

public enum DocumentSource { Owner, Lender, CoreSystem, Provider, Legal }
public enum DocumentStatus { Requested, Uploaded, InReview, Verified, Rejected, Expired }
public enum ReviewStatus { Pending, Verified, Rejected }
public enum ScanStatus { Pending, Clean, Infected, Skipped }

/// <summary>Platform catalog of document types (PA08); institutions add rules on top (A03).</summary>
public sealed class DocumentType : Entity
{
    public required string Key { get; set; }
    public required string NameAr { get; set; }
    public string? NameEn { get; set; }
    public string Icon { get; set; } = "description";
    public int? ValidityDays { get; set; }
    public bool Sensitive { get; set; }
    public List<string> AllowedFormats { get; set; } = ["pdf", "jpg", "png"];
}

/// <summary>Institution rule for a document type (A03).</summary>
public sealed class DocumentRule : OrgEntity
{
    public required string DocumentTypeKey { get; set; }
    /// <summary>Case status before which the document is required, or null for "as needed".</summary>
    public string? RequiredBeforeStatus { get; set; }
    public required string Uploader { get; set; }
    public int? ValidityDays { get; set; }
    public string? ValidityNote { get; set; }
    public List<string> VisibleTo { get; set; } = [];
    public bool OwnerSummaryOnly { get; set; }
}

/// <summary>A logical document on a case. Versions are append-only; nothing is deleted.</summary>
public sealed class CaseDocument : OrgEntity
{
    public Guid CaseId { get; set; }
    public required string DocumentTypeKey { get; set; }
    public required string Name { get; set; }
    public DocumentSource Source { get; set; }
    public DocumentStatus Status { get; set; } = DocumentStatus.Requested;
    public List<string> VisibleTo { get; set; } = ["case_team"];
    public bool VisibleToOwner { get; set; }
    public bool Internal { get; set; }
    public Guid? CurrentVersionId { get; set; }
    public int VersionCount { get; set; }
    public DateOnly? ValidUntil { get; set; }
    public List<DocumentVersion> Versions { get; set; } = [];
}

public sealed class DocumentVersion : OrgEntity
{
    public Guid DocumentId { get; set; }
    public Guid CaseId { get; set; }
    public int VersionNo { get; set; }
    public required string FileName { get; set; }
    public required string ContentType { get; set; }
    public long SizeBytes { get; set; }
    public required string Sha256 { get; set; }
    public required string StorageKey { get; set; }
    public Guid UploadedByUserId { get; set; }
    public string UploadedByLabel { get; set; } = "";
    public DateTimeOffset UploadedAt { get; set; }
    public ScanStatus ScanStatus { get; set; } = ScanStatus.Pending;
    public ReviewStatus ReviewStatus { get; set; } = ReviewStatus.Pending;
    public Guid? ReviewedByUserId { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }
    public string? ReviewNote { get; set; }
    /// <summary>Plain, humane reason shown to the owner when a version is rejected.</summary>
    public string? OwnerFacingReason { get; set; }
}

public enum DocumentRequestStatus { Open, Fulfilled, Overdue, Cancelled }

public sealed class DocumentRequest : OrgEntity
{
    public Guid CaseId { get; set; }
    public Guid DocumentId { get; set; }
    public required string DocumentTypeKey { get; set; }
    public required string RequestedFrom { get; set; } // owner | internal | provider
    public Guid? PartyId { get; set; }
    public DateOnly DueOn { get; set; }
    public string? OwnerMessage { get; set; }
    public List<string> Channels { get; set; } = ["portal"];
    public DocumentRequestStatus Status { get; set; } = DocumentRequestStatus.Open;
    public Guid RequestedByUserId { get; set; }
}

public sealed class DownloadLog : OrgEntity
{
    public Guid VersionId { get; set; }
    public Guid UserId { get; set; }
    public required string Watermark { get; set; }
    public DateTimeOffset At { get; set; }
}
