using System.Text;
using Microsoft.EntityFrameworkCore;
using Rahoon.Api.Infrastructure.Http;
using Rahoon.Api.Infrastructure.Persistence;
using Rahoon.Api.Infrastructure.Tenancy;
using Rahoon.Api.Infrastructure.Time;
using Rahoon.Api.Modules.Audit;
using Rahoon.Api.Modules.Cases;
using Rahoon.Api.Modules.Identity;

namespace Rahoon.Api.Modules.Imports;

public sealed record ImportRowDecisionRequest(string? Decision, string? Reason);

/// <summary>
/// L04 bulk import: upload CSV (template v3) → per-row validation → decisions on possible duplicates →
/// commit ready rows (+ «create with reason» duplicates) as Draft cases. No owner communication is sent.
/// Rows are always reached through a tenant-filtered batch (ImportRow itself carries no tenant).
/// </summary>
public static class ImportEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/cases/imports").RequirePermission(P.CaseImport);
        g.MapGet("", List);
        g.MapGet("/template", Template);
        g.MapPost("", Upload).DisableAntiforgery();
        g.MapGet("/{id:guid}", Get);
        g.MapGet("/{id:guid}/errors.csv", ErrorsFile);
        g.MapPost("/{id:guid}/rows/{rowNumber:int}/decision", Decide).Idempotent();
        g.MapPost("/{id:guid}/commit", Commit).RequirePermission(P.CaseCreate).Idempotent();
    }

    private static string StatusKey(ImportRowStatus s) => s.ToString().ToLowerInvariant();

    private static async Task<ImportBatch> LoadAsync(RahoonDbContext db, RequestContext rc, Guid id, bool rows = true, bool track = false)
    {
        IQueryable<ImportBatch> q = db.ImportBatches.Where(b => b.Id == id && b.OrganizationId == rc.OrganizationId);
        if (rows) q = q.Include(b => b.Rows);
        if (!track) q = q.AsNoTracking();
        return await q.FirstOrDefaultAsync() ?? throw new NotFoundException();
    }

    private static object Summary(ImportBatch b, string? uploader)
    {
        var decidedCreate = b.Rows.Count(r => r.Status == ImportRowStatus.Duplicate && r.Decision == "create_with_reason");
        var undecided = b.Rows.Count(r => r.Status == ImportRowStatus.Duplicate && r.Decision is null);
        return new
        {
            b.Id, b.FileName, b.TemplateVersion, status = b.Status.ToString().ToLowerInvariant(), uploadedBy = uploader, uploadedAt = b.CreatedAt,
            step = b.Status == ImportBatchStatus.Validated ? 2 : 3,
            counts = new
            {
                total = b.TotalRows, ready = b.ReadyCount, duplicates = b.DuplicateCount, errors = b.ErrorCount, imported = b.ImportedCount,
                skipped = b.Rows.Count(r => r.Status == ImportRowStatus.Skipped), undecidedDuplicates = undecided,
                importable = b.Status == ImportBatchStatus.Validated ? b.ReadyCount + decidedCreate : 0,
            },
            meta = $"{b.TotalRows} صفاً · قالب {b.TemplateVersion}" + (uploader is null ? "" : $" · رفعه {uploader} {b.CreatedAt.ToOffset(TimeSpan.FromHours(3)):HH:mm}"),
            note = "الحالات المستوردة تبدأ «مسودة» ولا يُرسل أي تواصل للمالك",
        };
    }

    private static object RowView(ImportRow r) => new
    {
        r.RowNumber, contractNumberMasked = r.ContractNumberMasked, status = StatusKey(r.Status),
        issues = r.Issues.Select(ImportService.ParseIssue).Select(i => new { field = i.Field, fieldLabel = i.FieldLabel, code = i.Code, message = i.Message, fix = i.Fix, severity = i.Severity }),
        duplicateOf = r.DuplicateOfCaseRef, decision = r.Decision, decisionReason = r.DecisionReason, createdCaseId = r.CreatedCaseId,
        actions = r.Status != ImportRowStatus.Duplicate ? Array.Empty<string>()
            : r.Issues.Select(ImportService.ParseIssue).Any(i => i.Code == "open_case") ? ["skip", "link", "create_with_reason"] : ["skip", "create_with_reason"],
    };

    private static async Task<IResult> List(RahoonDbContext db, RequestContext rc)
    {
        var rows = await db.ImportBatches.AsNoTracking().Where(b => b.OrganizationId == rc.OrganizationId).OrderByDescending(b => b.CreatedAt).Take(20)
            .Select(b => new { b.Id, b.FileName, status = b.Status, b.TotalRows, b.ReadyCount, b.DuplicateCount, b.ErrorCount, b.ImportedCount, b.CreatedAt }).ToListAsync();
        return Results.Ok(rows.Select(b => new { b.Id, b.FileName, status = b.status.ToString().ToLowerInvariant(), b.TotalRows, b.ReadyCount, b.DuplicateCount, b.ErrorCount, b.ImportedCount, b.CreatedAt }));
    }

    private static IResult Template() =>
        Results.File(Encoding.UTF8.GetBytes(ImportService.TemplateCsv()), "text/csv; charset=utf-8", $"rahoon-import-template-{ImportService.TemplateVersion}.csv");

    private static async Task<IResult> Upload(HttpRequest http, RahoonDbContext db, RequestContext rc, CaseFactory factory, IClock clock, AuditLog audit)
    {
        if (!http.HasFormContentType) throw new DomainException("form_required", "أرسل الملف كنموذج متعدد الأجزاء.", 400);
        var form = await http.ReadFormAsync();
        var file = form.Files.GetFile("file") ?? throw new ValidationFailedException(new Dictionary<string, string[]> { ["file"] = ["اختر ملف CSV بالقالب المعتمد."] });
        if (file.Length == 0) Validate.Throw("file", "الملف فارغ.");
        if (file.Length > ImportService.MaxBytes) Validate.Throw("file", "حجم الملف يتجاوز 5 م.ب.");
        if (!file.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
            Validate.Throw("file", "الصيغة المقبولة CSV (UTF-8). احفظ ملف Excel بصيغة CSV باستخدام القالب.");
        string text;
        await using (var s = file.OpenReadStream())
        using (var reader = new StreamReader(s, new UTF8Encoding(false, throwOnInvalidBytes: true)))
        {
            try { text = await reader.ReadToEndAsync(); }
            catch (DecoderFallbackException) { throw new ValidationFailedException(new Dictionary<string, string[]> { ["file"] = ["ترميز الملف ليس UTF-8؛ احفظه بصيغة CSV UTF-8."] }); }
        }
        if (text.Contains('\0')) Validate.Throw("file", "الملف ليس نصاً (CSV).");

        await using var tx = await db.Database.BeginTransactionAsync();
        var service = new ImportService(db, factory, clock);
        var batch = await service.ValidateAsync(rc.OrganizationId!.Value, rc.UserId, file.FileName, text);
        await audit.RecordAsync(new AuditEntry("import.validated", $"رفع ملف استيراد {batch.FileName}",
            Detail: $"{batch.TotalRows} صفاً · جاهز {batch.ReadyCount} · تكرار محتمل {batch.DuplicateCount} · أخطاء {batch.ErrorCount}",
            Data: new { batchId = batch.Id }, OrganizationId: batch.OrganizationId));
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Results.Ok(new
        {
            batch = Summary(batch, rc.UserName),
            attention = batch.Rows.Where(r => r.Status is ImportRowStatus.Error or ImportRowStatus.Duplicate).OrderBy(r => r.RowNumber).Take(50).Select(RowView),
        });
    }

    private static async Task<IResult> Get(Guid id, string? status, int? page, int? pageSize, RahoonDbContext db, RequestContext rc)
    {
        var b = await LoadAsync(db, rc, id);
        var uploader = await db.Users.Where(u => u.Id == b.UploadedByUserId).Select(u => u.FullName).FirstOrDefaultAsync();
        IEnumerable<ImportRow> rows = b.Rows.OrderBy(r => r.RowNumber);
        rows = (status ?? "attention") switch
        {
            "all" => rows,
            "ready" => rows.Where(r => r.Status == ImportRowStatus.Ready),
            "duplicate" => rows.Where(r => r.Status == ImportRowStatus.Duplicate),
            "error" => rows.Where(r => r.Status == ImportRowStatus.Error),
            "imported" => rows.Where(r => r.Status == ImportRowStatus.Imported),
            "skipped" => rows.Where(r => r.Status == ImportRowStatus.Skipped),
            _ => rows.Where(r => r.Status is ImportRowStatus.Error or ImportRowStatus.Duplicate),
        };
        var list = rows.ToList();
        var size = Math.Clamp(pageSize ?? 50, 1, 200);
        var p = Math.Max(1, page ?? 1);
        return Results.Ok(new { batch = Summary(b, uploader), rows = list.Skip((p - 1) * size).Take(size).Select(RowView), total = list.Count, page = p, pageSize = size });
    }

    /// <summary>Error file: every error/duplicate row (masked identity data only) with its issues and fixes.</summary>
    private static async Task<IResult> ErrorsFile(Guid id, RahoonDbContext db, RequestContext rc)
    {
        var b = await LoadAsync(db, rc, id);
        var sb = new StringBuilder("﻿row,contract_number,status,field,problem,fix\n");
        static string Csv(string? v)
        {
            v ??= "";
            if (v.Length > 0 && v[0] is '=' or '+' or '-' or '@') v = "'" + v;
            return v.IndexOfAny([',', '"', '\n', '\r']) >= 0 ? "\"" + v.Replace("\"", "\"\"") + "\"" : v;
        }
        foreach (var r in b.Rows.Where(r => r.Status is ImportRowStatus.Error or ImportRowStatus.Duplicate).OrderBy(r => r.RowNumber))
            foreach (var i in r.Issues.Select(ImportService.ParseIssue))
                sb.Append($"{r.RowNumber},{Csv(r.ContractNumberMasked)},{StatusKey(r.Status)},{Csv(i.FieldLabel)},{Csv(i.Message)},{Csv(i.Fix)}\n");
        return Results.File(Encoding.UTF8.GetBytes(sb.ToString()), "text/csv; charset=utf-8", $"import-errors-{b.Id:N}.csv");
    }

    private static async Task<IResult> Decide(Guid id, int rowNumber, ImportRowDecisionRequest req, RahoonDbContext db, RequestContext rc, AuditLog audit)
    {
        var decision = req.Decision?.Trim().ToLowerInvariant();
        new Validator()
            .Require(decision is "skip" or "link" or "create_with_reason", "decision", "اختر: تخطي أو ربط أو إنشاء مع سبب.")
            .Require(decision != "create_with_reason" || (req.Reason?.Trim().Length ?? 0) is >= 10 and <= 1000, "reason", "سبب الإنشاء رغم التكرار إلزامي (10 أحرف على الأقل).")
            .ThrowIfInvalid();
        await using var tx = await db.Database.BeginTransactionAsync();
        var b = await LoadAsync(db, rc, id, track: true);
        if (b.Status != ImportBatchStatus.Validated) throw new ConflictException("batch_committed", "اكتمل استيراد هذه الدفعة؛ لا تُعدّل القرارات بعده.");
        var row = b.Rows.FirstOrDefault(r => r.RowNumber == rowNumber) ?? throw new NotFoundException();
        if (row.Status != ImportRowStatus.Duplicate) throw new ConflictException("not_duplicate", "القرار مطلوب لصفوف التكرار المحتمل فقط.");
        var issues = row.Issues.Select(ImportService.ParseIssue).ToList();
        if (decision == "link" && !issues.Any(i => i.Code == "open_case"))
            throw new DomainException("link_not_available", "الربط متاح فقط عند وجود حالة مفتوحة للعقد نفسه.");
        row.Decision = decision;
        row.DecisionReason = decision == "create_with_reason" ? req.Reason!.Trim() : string.IsNullOrWhiteSpace(req.Reason) ? null : req.Reason.Trim();
        await audit.RecordAsync(new AuditEntry("import.row_decision", $"قرار على صف تكرار {rowNumber}: {DecisionLabel(decision!)}",
            Reason: row.DecisionReason, Detail: $"{b.FileName} · {row.ContractNumberMasked} · {row.DuplicateOfCaseRef}", Data: new { batchId = b.Id, rowNumber }, OrganizationId: b.OrganizationId));
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        var uploader = await db.Users.Where(u => u.Id == b.UploadedByUserId).Select(u => u.FullName).FirstOrDefaultAsync();
        return Results.Ok(new { row = RowView(row), batch = Summary(b, uploader) });
    }

    private static string DecisionLabel(string d) => d switch { "skip" => "تخطي", "link" => "ربط بالحالة القائمة", _ => "إنشاء مع سبب" };

    /// <summary>
    /// Imports ready rows and «create with reason» duplicates as Draft cases, marks skipped/linked rows. Idempotent twice over:
    /// the Idempotency-Key replays the response, and a second commit (any key) finds the batch already completed and creates nothing.
    /// </summary>
    private static async Task<IResult> Commit(Guid id, RahoonDbContext db, RequestContext rc, CaseFactory factory, IClock clock, AuditLog audit)
    {
        await using var tx = await db.Database.BeginTransactionAsync();
        var orgId = rc.OrganizationId!.Value;
        // Atomic claim: only one commit can move the batch out of «Validated».
        var claimed = await db.ImportBatches.Where(b => b.Id == id && b.OrganizationId == orgId && b.Status == ImportBatchStatus.Validated)
            .ExecuteUpdateAsync(s => s.SetProperty(b => b.Status, ImportBatchStatus.Importing));
        var batch = await LoadAsync(db, rc, id, track: true);
        var uploader = await db.Users.Where(u => u.Id == batch.UploadedByUserId).Select(u => u.FullName).FirstOrDefaultAsync();
        if (claimed == 0)
        {
            if (batch.Status == ImportBatchStatus.Importing) throw new ConflictException("import_in_progress", "الاستيراد جارٍ لهذه الدفعة.");
            await tx.RollbackAsync();
            return Results.Ok(new { alreadyCommitted = true, batch = Summary(batch, uploader), created = Array.Empty<string>() });
        }

        var service = new ImportService(db, factory, clock);
        var mortgagee = await db.Organizations.Where(o => o.Id == orgId).Select(o => o.NameAr).FirstAsync();
        var created = new List<string>();
        var linked = 0;
        foreach (var row in batch.Rows.OrderBy(r => r.RowNumber))
        {
            if (row.Status == ImportRowStatus.Ready || (row.Status == ImportRowStatus.Duplicate && row.Decision == "create_with_reason"))
            {
                var c = await service.CreateDraftAsync(batch, row, rc.UserId, rc.MembershipId, mortgagee);
                created.Add(c.Reference);
                await audit.RecordAsync(new AuditEntry("case.draft_created", "بدء حالة جديدة (مسودة) من استيراد", c.Id, c.Reference, ToState: "draft",
                    Reason: c.DuplicateOverrideReason, Detail: $"الملف {batch.FileName} · الصف {row.RowNumber} · لا تواصل مع المالك", OrganizationId: orgId));
            }
            else if (row.Status == ImportRowStatus.Duplicate && row.Decision == "link")
            {
                row.Status = ImportRowStatus.Skipped;
                linked++;
                var target = await db.Cases.Where(x => x.OrganizationId == orgId && x.Reference == row.DuplicateOfCaseRef).Select(x => new { x.Id, x.Reference }).FirstOrDefaultAsync();
                if (target is not null)
                    await audit.RecordAsync(new AuditEntry("import.row_linked", $"ربط صف استيراد بالحالة (الصف {row.RowNumber})", target.Id, target.Reference,
                        Detail: $"الملف {batch.FileName} · لم تُنشأ حالة جديدة ولم تُعدّل بيانات الحالة", OrganizationId: orgId));
            }
            else if (row.Status == ImportRowStatus.Duplicate && row.Decision == "skip") row.Status = ImportRowStatus.Skipped;
        }
        ImportService.Recount(batch);
        batch.Status = ImportBatchStatus.Completed;
        await audit.RecordAsync(new AuditEntry("import.committed", $"استيراد {created.Count} حالة من {batch.FileName}",
            Detail: $"مسودات {created.Count} · ربط {linked} · تخطي {batch.Rows.Count(r => r.Status == ImportRowStatus.Skipped) - linked} · تكرار دون قرار {batch.DuplicateCount} · أخطاء {batch.ErrorCount}",
            Data: new { batchId = batch.Id }, OrganizationId: orgId));
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Results.Ok(new { alreadyCommitted = false, batch = Summary(batch, uploader), created, linked });
    }
}
