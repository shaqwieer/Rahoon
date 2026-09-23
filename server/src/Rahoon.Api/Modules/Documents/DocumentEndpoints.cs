using Microsoft.EntityFrameworkCore;
using Rahoon.Api.Infrastructure.Http;
using Rahoon.Api.Infrastructure.Persistence;
using Rahoon.Api.Infrastructure.Storage;
using Rahoon.Api.Infrastructure.Tenancy;
using Rahoon.Api.Infrastructure.Time;
using Rahoon.Api.Modules.Audit;
using Rahoon.Api.Modules.Cases;
using Rahoon.Api.Modules.Communications;
using Rahoon.Api.Modules.Identity;

namespace Rahoon.Api.Modules.Documents;

public sealed record ReviewDocumentRequest(string Decision, string? Note, string? OwnerReason, DateOnly? ValidUntil);
public sealed record RequestDocumentRequest(string DocumentTypeKey, DateOnly DueOn, string? OwnerMessage, List<string>? Channels);

public static class DocumentEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/cases/{reference}/documents").RequirePermission(P.CaseView);
        g.MapGet("", List);
        g.MapPost("", Upload).RequirePermission(P.DocumentUpload).DisableAntiforgery();
        g.MapPost("/requests", RequestFromOwner).RequirePermission(P.DocumentRequest).Idempotent();
        g.MapGet("/{documentId:guid}/versions", Versions);
        g.MapGet("/versions/{versionId:guid}/file", Download).RequirePermission(P.DocumentDownload);
        g.MapPost("/versions/{versionId:guid}/review", Review).RequirePermission(P.DocumentReview).Idempotent();
        app.MapGet("/api/document-types", async (RahoonDbContext db) =>
            Results.Ok(await db.DocumentTypes.OrderBy(t => t.NameAr).Select(t => new { t.Key, t.NameAr, t.Icon, t.ValidityDays }).ToListAsync())).RequireSession();
    }

    private static async Task<IResult> List(string reference, string? filter, CaseAccess access, RahoonDbContext db, IClock clock)
    {
        var c = await access.GetAsync(reference, track: false);
        var today = clock.TodayRiyadh;
        var docs = await db.Documents.AsNoTracking().Where(d => d.CaseId == c.Id).Include(d => d.Versions).OrderBy(d => d.CreatedAt).ToListAsync();
        var requests = await db.DocumentRequests.AsNoTracking().Where(r => r.CaseId == c.Id && r.Status == DocumentRequestStatus.Open).ToListAsync();
        var types = await db.DocumentTypes.AsNoTracking().ToDictionaryAsync(t => t.Key);
        var items = docs.Select(d =>
        {
            var current = d.Versions.OrderByDescending(v => v.VersionNo).FirstOrDefault();
            var req = requests.FirstOrDefault(r => r.DocumentId == d.Id);
            var expiringIn = d.ValidUntil is { } vu ? vu.DayNumber - today.DayNumber : (int?)null;
            var (status, tone, icon) = d.Status switch
            {
                DocumentStatus.Verified when expiringIn is < 0 => ("منتهٍ", "err", "event_busy"),
                DocumentStatus.Verified when expiringIn is <= 30 => ($"تنتهي خلال {CaseDisplay.Days(expiringIn!.Value)}", "warn", "event_upcoming"),
                DocumentStatus.Verified => (d.DocumentTypeKey == "valuation_report" && expiringIn is { } e ? $"صالح · {CaseDisplay.Days(e)}" : "تم التحقق", "ok", "check_circle"),
                DocumentStatus.Rejected when req is not null => ("أُعيد الطلب", "err", "undo"),
                DocumentStatus.Rejected => ("مرفوض", "err", "cancel"),
                DocumentStatus.Requested => ("مطلوب", "info", "upload_file"),
                DocumentStatus.InReview or DocumentStatus.Uploaded => ("قيد المراجعة", "warn", "pending"),
                DocumentStatus.Expired => ("منتهٍ", "err", "event_busy"),
                _ => ("—", "neutral", "radio_button_unchecked"),
            };
            return new
            {
                d.Id, d.Name, d.DocumentTypeKey, icon = types.GetValueOrDefault(d.DocumentTypeKey)?.Icon ?? "description",
                version = current is null ? "—" : $"v{current.VersionNo}", versionCount = d.VersionCount, source = d.Source.ToString(),
                statusText = status, tone, statusIcon = icon, rawStatus = d.Status.ToString(), validUntil = d.ValidUntil,
                visibleTo = d.VisibleTo, visibleToOwner = d.VisibleToOwner,
                meta = current is null
                    ? (req is null ? "لم يُرفع بعد" : $"مطلوب من المالك · المهلة {req.DueOn:yyyy-MM-dd}")
                    : $"رفعه {current.UploadedByLabel} · {current.UploadedAt.ToOffset(TimeSpan.FromHours(3)):yyyy-MM-dd}" +
                      (d.VersionCount > 1 && d.Versions.Any(v => v.ReviewStatus == ReviewStatus.Rejected) ? $" · v{d.Versions.First(v => v.ReviewStatus == ReviewStatus.Rejected).VersionNo} رُفض: {d.Versions.First(v => v.ReviewStatus == ReviewStatus.Rejected).ReviewNote}" : ""),
                currentVersionId = current?.Id, currentReview = current?.ReviewStatus.ToString(), scan = current?.ScanStatus.ToString(),
                request = req is null ? null : new { req.Id, req.DueOn, req.OwnerMessage },
            };
        }).ToList();

        var filtered = filter switch
        {
            "requested" => items.Where(i => i.rawStatus is "Requested" || i.request is not null).ToList(),
            "in_review" => items.Where(i => i.currentReview == "Pending").ToList(),
            "expiring" => items.Where(i => i.tone == "warn" && i.rawStatus == "Verified").ToList(),
            _ => items,
        };
        return Results.Ok(new
        {
            items = filtered,
            counts = new
            {
                all = items.Count, requested = items.Count(i => i.rawStatus is "Requested" || i.request is not null),
                inReview = items.Count(i => i.currentReview == "Pending"), expiring = items.Count(i => i.tone == "warn" && i.rawStatus == "Verified"),
            },
        });
    }

    private static async Task<IResult> Versions(string reference, Guid documentId, CaseAccess access, RahoonDbContext db)
    {
        var c = await access.GetAsync(reference, track: false);
        var versions = await db.DocumentVersions.AsNoTracking().Where(v => v.CaseId == c.Id && v.DocumentId == documentId).OrderByDescending(v => v.VersionNo)
            .Select(v => new { v.Id, v.VersionNo, v.FileName, v.ContentType, v.SizeBytes, v.UploadedByLabel, v.UploadedAt, review = v.ReviewStatus.ToString(), v.ReviewNote, v.OwnerFacingReason, scan = v.ScanStatus.ToString(), v.Sha256 })
            .ToListAsync();
        return Results.Ok(versions);
    }

    /// <summary>Multipart upload: file + documentTypeKey (+ optional documentId for a new version). Content is sniffed; never trusted.</summary>
    public static async Task<IResult> Upload(string reference, HttpRequest http, CaseAccess access, RahoonDbContext db, RequestContext rc,
        IDocumentStorage storage, IFileScanner scanner, IClock clock, AuditLog audit)
    {
        if (!http.HasFormContentType) throw new DomainException("form_required", "أرسل الملف كنموذج متعدد الأجزاء.", 400);
        var form = await http.ReadFormAsync();
        var file = form.Files.GetFile("file") ?? throw new ValidationFailedException(new Dictionary<string, string[]> { ["file"] = ["اختر ملفاً."] });
        var typeKey = form["documentTypeKey"].ToString();
        var c = await access.GetAsync(reference);
        var type = await db.DocumentTypes.FirstOrDefaultAsync(t => t.Key == typeKey) ?? throw new ValidationFailedException(new Dictionary<string, string[]> { ["documentTypeKey"] = ["نوع المستند غير معروف."] });

        await using var stream = file.OpenReadStream();
        var stored = await storage.SaveAsync(stream, file.FileName, c.OrganizationId);
        var scan = await scanner.ScanAsync(stored.StorageKey);
        if (scan == ScanStatus.Infected) throw new DomainException("file_infected", "رُفض الملف: فشل فحص الأمان.");

        await using var tx = await db.Database.BeginTransactionAsync();
        var docId = Guid.TryParse(form["documentId"], out var id) ? id : (Guid?)null;
        var doc = docId is { } did
            ? await db.Documents.FirstOrDefaultAsync(d => d.Id == did && d.CaseId == c.Id) ?? throw new NotFoundException()
            : await db.Documents.FirstOrDefaultAsync(d => d.CaseId == c.Id && d.DocumentTypeKey == typeKey && d.Status != DocumentStatus.Verified)
              ?? new CaseDocument { OrganizationId = c.OrganizationId, CaseId = c.Id, DocumentTypeKey = typeKey, Name = type.NameAr, Source = rc.IsOwner ? DocumentSource.Owner : DocumentSource.Lender };
        if (db.Entry(doc).State == EntityState.Detached) db.Documents.Add(doc);

        var version = new DocumentVersion
        {
            OrganizationId = c.OrganizationId, DocumentId = doc.Id, CaseId = c.Id, VersionNo = doc.VersionCount + 1, FileName = Path.GetFileName(file.FileName),
            ContentType = stored.ContentType, SizeBytes = stored.SizeBytes, Sha256 = stored.Sha256, StorageKey = stored.StorageKey,
            UploadedByUserId = rc.UserId, UploadedByLabel = rc.IsOwner ? "المالك" : rc.UserName, UploadedAt = clock.UtcNow, ScanStatus = scan,
        };
        db.DocumentVersions.Add(version);
        doc.VersionCount = version.VersionNo;
        doc.CurrentVersionId = version.Id;
        doc.Status = DocumentStatus.InReview;
        foreach (var r in await db.DocumentRequests.Where(r => r.DocumentId == doc.Id && r.Status == DocumentRequestStatus.Open).ToListAsync())
            r.Status = DocumentRequestStatus.Fulfilled;
        await audit.RecordAsync(new AuditEntry("document.uploaded", $"رفع {doc.Name} v{version.VersionNo}", c.Id, c.Reference, Detail: $"{stored.ContentType} · {stored.SizeBytes / 1024} ك.ب · فحص: {scan}", OrganizationId: c.OrganizationId));
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Results.Ok(new { documentId = doc.Id, versionId = version.Id, version = version.VersionNo, scan = scan.ToString() });
    }

    /// <summary>Audited download. Watermark text is recorded; stamping the file itself is outstanding (see docs).</summary>
    private static async Task<IResult> Download(string reference, Guid versionId, CaseAccess access, RahoonDbContext db, RequestContext rc, IDocumentStorage storage, IClock clock, AuditLog audit)
    {
        var c = await access.GetAsync(reference, track: false);
        var v = await db.DocumentVersions.AsNoTracking().FirstOrDefaultAsync(x => x.Id == versionId && x.CaseId == c.Id) ?? throw new NotFoundException();
        var doc = await db.Documents.AsNoTracking().FirstAsync(d => d.Id == v.DocumentId);
        if (doc.Internal && !rc.Has(P.CaseViewAll) && !rc.Has(P.DocumentReview)) throw new ForbiddenException();
        var watermark = $"{rc.UserName} · {clock.UtcNow.ToOffset(TimeSpan.FromHours(3)):yyyy-MM-dd HH:mm} · {c.Reference}";
        db.DownloadLogs.Add(new DownloadLog { OrganizationId = c.OrganizationId, VersionId = v.Id, UserId = rc.UserId, Watermark = watermark, At = clock.UtcNow });
        await audit.RecordAsync(new AuditEntry("document.downloaded", $"تنزيل {doc.Name} v{v.VersionNo}", c.Id, c.Reference, Detail: watermark, OrganizationId: c.OrganizationId));
        await db.SaveChangesAsync();
        var stream = await storage.OpenReadAsync(v.StorageKey);
        return Results.File(stream, v.ContentType, v.FileName);
    }

    private static async Task<IResult> Review(string reference, Guid versionId, ReviewDocumentRequest req, CaseAccess access, RahoonDbContext db, RequestContext rc,
        IClock clock, AuditLog audit, Notifier notifier)
    {
        var decision = req.Decision?.ToLowerInvariant();
        new Validator().Require(decision is "verify" or "reject", "decision", "اختر: تحقق أو رفض.")
            .Require(decision != "reject" || !string.IsNullOrWhiteSpace(req.OwnerReason), "ownerReason", "اكتب سبباً واضحاً يفهمه المالك.").ThrowIfInvalid();
        await using var tx = await db.Database.BeginTransactionAsync();
        var c = await access.GetAsync(reference, track: false);
        var v = await db.DocumentVersions.FirstOrDefaultAsync(x => x.Id == versionId && x.CaseId == c.Id) ?? throw new NotFoundException();
        if (v.ReviewStatus != ReviewStatus.Pending) throw new ConflictException("already_reviewed", "رُوجع هذا الإصدار مسبقاً.");
        if (v.UploadedByUserId == rc.UserId && !rc.IsOwner) throw new ForbiddenException("لا تراجع مستنداً رفعته بنفسك.");
        var doc = await db.Documents.FirstAsync(d => d.Id == v.DocumentId);
        v.ReviewStatus = decision == "verify" ? ReviewStatus.Verified : ReviewStatus.Rejected;
        v.ReviewedByUserId = rc.UserId;
        v.ReviewedAt = clock.UtcNow;
        v.ReviewNote = req.Note?.Trim();
        v.OwnerFacingReason = req.OwnerReason?.Trim();
        doc.Status = decision == "verify" ? DocumentStatus.Verified : DocumentStatus.Rejected;
        if (decision == "verify")
        {
            var type = await db.DocumentTypes.FirstAsync(t => t.Key == doc.DocumentTypeKey);
            doc.ValidUntil = req.ValidUntil ?? (type.ValidityDays is { } days ? clock.TodayRiyadh.AddDays(days) : doc.ValidUntil);
        }
        else if (doc.Source == DocumentSource.Owner)
        {
            var owner = await db.OwnerAccesses.Where(o => o.CaseId == c.Id && o.UserId != null).Select(o => o.UserId).FirstOrDefaultAsync();
            if (owner is { } ou) notifier.Notify(ou, c.OrganizationId, "document", $"نحتاج نسخة جديدة من {doc.Name}", req.OwnerReason, "/owner/documents", c.Id, "warn");
        }
        await audit.RecordAsync(new AuditEntry(decision == "verify" ? "document.verified" : "document.rejected",
            decision == "verify" ? $"تم التحقق من {doc.Name} v{v.VersionNo}" : $"رفض {doc.Name} v{v.VersionNo}", c.Id, c.Reference, Reason: req.Note, Detail: req.OwnerReason, OrganizationId: c.OrganizationId));
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Results.Ok(new { status = doc.Status.ToString() });
    }

    private static async Task<IResult> RequestFromOwner(string reference, RequestDocumentRequest req, CaseAccess access, RahoonDbContext db, RequestContext rc,
        IClock clock, AuditLog audit, Notifier notifier)
    {
        new Validator().Require(req.DueOn > clock.TodayRiyadh, "dueOn", "المهلة يجب أن تكون في المستقبل.").ThrowIfInvalid();
        await using var tx = await db.Database.BeginTransactionAsync();
        var c = await access.GetAsync(reference, track: false);
        var type = await db.DocumentTypes.FirstOrDefaultAsync(t => t.Key == req.DocumentTypeKey) ?? throw new NotFoundException();
        var doc = await db.Documents.FirstOrDefaultAsync(d => d.CaseId == c.Id && d.DocumentTypeKey == type.Key && d.Status != DocumentStatus.Verified)
                  ?? new CaseDocument { OrganizationId = c.OrganizationId, CaseId = c.Id, DocumentTypeKey = type.Key, Name = type.NameAr, Source = DocumentSource.Owner, Status = DocumentStatus.Requested, VisibleToOwner = true };
        if (db.Entry(doc).State == EntityState.Detached) db.Documents.Add(doc);
        var message = string.IsNullOrWhiteSpace(req.OwnerMessage) ? $"نحتاج منك {type.NameAr} لمتابعة حالتك. يمكنك رفعه من صفحة المستندات حتى {req.DueOn:yyyy-MM-dd}، وإن واجهت صعوبة فاكتب لنا." : req.OwnerMessage.Trim();
        db.DocumentRequests.Add(new DocumentRequest
        {
            OrganizationId = c.OrganizationId, CaseId = c.Id, DocumentId = doc.Id, DocumentTypeKey = type.Key, RequestedFrom = "owner", DueOn = req.DueOn,
            OwnerMessage = message, Channels = req.Channels ?? ["portal"], RequestedByUserId = rc.UserId,
        });
        var owner = await db.OwnerAccesses.Where(o => o.CaseId == c.Id && o.UserId != null).Select(o => o.UserId).FirstOrDefaultAsync();
        if (owner is { } ou) notifier.Notify(ou, c.OrganizationId, "document", $"مطلوب: {type.NameAr}", message, "/owner/documents", c.Id);
        await audit.RecordAsync(new AuditEntry("document.requested", $"طلب {type.NameAr} من المالك", c.Id, c.Reference, Detail: $"المهلة {req.DueOn:yyyy-MM-dd}", OrganizationId: c.OrganizationId));
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Results.Ok(new { documentId = doc.Id });
    }
}
