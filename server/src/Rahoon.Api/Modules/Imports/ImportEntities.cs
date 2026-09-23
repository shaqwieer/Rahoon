using Rahoon.Api.Infrastructure.Persistence;

namespace Rahoon.Api.Modules.Imports;

public enum ImportBatchStatus { Validated, Importing, Completed, Failed }
public enum ImportRowStatus { Ready, Duplicate, Error, Imported, Skipped }

public sealed class ImportBatch : OrgEntity
{
    public required string FileName { get; set; }
    public string TemplateVersion { get; set; } = "v2";
    public Guid UploadedByUserId { get; set; }
    public int TotalRows { get; set; }
    public int ReadyCount { get; set; }
    public int DuplicateCount { get; set; }
    public int ErrorCount { get; set; }
    public int ImportedCount { get; set; }
    public ImportBatchStatus Status { get; set; }
    public List<ImportRow> Rows { get; set; } = [];
}

public sealed class ImportRow : Entity
{
    public Guid BatchId { get; set; }
    public int RowNumber { get; set; }
    public required string ContractNumberMasked { get; set; }
    public required string RawJson { get; set; }
    public ImportRowStatus Status { get; set; }
    public List<string> Issues { get; set; } = [];
    public string? DuplicateOfCaseRef { get; set; }
    public string? Decision { get; set; }
    public string? DecisionReason { get; set; }
    public Guid? CreatedCaseId { get; set; }
}
