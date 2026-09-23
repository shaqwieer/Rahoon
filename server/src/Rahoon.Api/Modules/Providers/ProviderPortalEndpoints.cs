using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Rahoon.Api.Infrastructure.Http;
using Rahoon.Api.Infrastructure.Persistence;
using Rahoon.Api.Infrastructure.Storage;
using Rahoon.Api.Infrastructure.Tenancy;
using Rahoon.Api.Infrastructure.Time;
using Rahoon.Api.Modules.Audit;
using Rahoon.Api.Modules.Cases;
using Rahoon.Api.Modules.Documents;
using Rahoon.Api.Modules.Identity;

namespace Rahoon.Api.Modules.Providers;

public sealed record ProviderMessageRequest(string Body);
public sealed record ConfirmInspectionRequest(DateTimeOffset? At);

/// <summary>
/// Provider portal V01–V04. A provider sees only its own organization's assignments, only the documents the
/// lender shared, never debt figures, other parties or the case reference; write access until delivery,
/// read-only for 7 days after delivery, then the assignment disappears (same refusal as an unknown id).
/// </summary>
public static class ProviderPortalEndpoints
{
    public static readonly (string Key, string Label)[] Checklist =
    [
        ("comparables_12m", "صفقات مقارنة خلال 12 شهراً"),
        ("occupancy_stated", "ذكر حالة الإشغال"),
        ("photos_no_faces", "الصور بلا وجوه أشخاص أو لوحات سيارات"),
    ];

    public const string IndependenceLabel = "إقرار الاستقلالية وعدم تعارض المصالح";
    private const string IsolationNotice = "وصولك لهذا التكليف فقط، وينتهي بعد 7 أيام من التسليم. لا ترى المديونية أو بيانات المالك الأخرى.";

    public static void Map(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/provider/assignments").RequireOrg(OrganizationKind.ServiceProvider).RequirePermission(P.AssignmentWork);
        g.MapGet("", Inbox);
        g.MapGet("/{id:guid}", Detail);
        g.MapGet("/{id:guid}/documents/{documentId:guid}", Download);
        g.MapGet("/{id:guid}/messages", Messages);
        g.MapPost("/{id:guid}/messages", PostMessage).Idempotent();
        g.MapPost("/{id:guid}/inspection/confirm", ConfirmInspection).Idempotent();
        g.MapPost("/{id:guid}/submissions", Submit).DisableAntiforgery().Idempotent();
    }

    private static DateOnly EffectiveDue(ProviderAssignment a, IEnumerable<AssignmentSubmission> subs) =>
        a.Status == AssignmentStatus.Returned && subs.Where(s => s.AssignmentId == a.Id && s.Status == SubmissionStatus.Returned).MaxBy(s => s.VersionNo)?.ResubmitDueOn is { } r
            ? r : a.DueOn;

    /// <summary>Inbox SLA in calendar days (B7 C12), unlike A06 stage deadlines which are business days.</summary>
    private static (string Text, string Tone) Sla(ProviderAssignment a, DateOnly due, DateOnly today)
    {
        if (!ProviderAssignmentService.IsWritable(a.Status))
            return (a.AccessExpiresAt is { } e ? $"للقراءة حتى {e.ToOffset(TimeSpan.FromHours(3)):yyyy-MM-dd}" : "مُسلَّم", "info");
        var left = due.DayNumber - today.DayNumber;
        var text = left < 0 ? CaseDisplay.LateDays(-left) : left == 0 ? "اليوم" : CaseDisplay.Days(left);
        var tone = a.Status == AssignmentStatus.Returned || left < 0 ? "error" : left <= 3 ? "warning" : "success";
        return (text, tone);
    }

    private static async Task<IResult> Inbox(string? status, ProviderAssignmentService service, RahoonDbContext db, IClock clock)
    {
        var today = clock.TodayRiyadh;
        var all = await service.ProviderVisible().AsNoTracking().OrderBy(a => a.DueOn).ToListAsync();
        var ids = all.Select(a => a.Id).ToList();
        var subs = await db.AssignmentSubmissions.AsNoTracking().Where(s => ids.Contains(s.AssignmentId)).ToListAsync();
        var lenderIds = all.Select(a => a.OrganizationId).Distinct().ToList();
        var lenders = await db.Organizations.AsNoTracking().Where(o => lenderIds.Contains(o.Id)).ToDictionaryAsync(o => o.Id, o => o.NameAr);

        var active = all.Where(a => ProviderAssignmentService.IsWritable(a.Status)).ToList();
        var delivered = all.Where(a => !ProviderAssignmentService.IsWritable(a.Status)).ToList();
        var shown = status == "delivered" ? delivered : active;
        return Results.Ok(new
        {
            counts = new { active = active.Count, delivered = delivered.Count },
            items = shown.Select(a =>
            {
                var due = EffectiveDue(a, subs);
                var (text, tone) = Sla(a, due, today);
                var lender = lenders.GetValueOrDefault(a.OrganizationId, "—");
                var meta = a.Status switch
                {
                    AssignmentStatus.New => $"{lender} · جديد",
                    AssignmentStatus.Returned => $"{lender} · أُعيد للتعديل",
                    AssignmentStatus.InProgress when a.InspectionAt is { } at => $"{lender} · معاينة {at.ToOffset(TimeSpan.FromHours(3)):yyyy-MM-dd}",
                    AssignmentStatus.InProgress => $"{lender} · قيد التنفيذ",
                    _ => $"{lender} · سُلِّم {a.DeliveredAt?.ToOffset(TimeSpan.FromHours(3)).ToString("yyyy-MM-dd") ?? ""}".TrimEnd(),
                };
                return new
                {
                    a.Id, a.Reference, title = (a.Status == AssignmentStatus.Returned ? "إعادة تسليم: " : "") + $"{a.Title} — {a.PropertyLabel}",
                    meta, lender, status = a.Status, statusLabel = ProviderAssignmentService.StatusLabel(a.Status),
                    dueOn = due, slaText = text, slaTone = tone, a.AccessExpiresAt,
                };
            }),
        });
    }

    private static async Task<IResult> Detail(Guid id, ProviderAssignmentService service, RahoonDbContext db, IClock clock)
    {
        var a = await service.GetForProviderAsync(id, track: false);
        var today = clock.TodayRiyadh;
        var lender = await db.Organizations.AsNoTracking().Where(o => o.Id == a.OrganizationId).Select(o => o.NameAr).FirstAsync();
        var subs = await db.AssignmentSubmissions.AsNoTracking().Where(s => s.AssignmentId == a.Id).OrderByDescending(s => s.VersionNo).ToListAsync();
        var reviewerIds = subs.Where(s => s.ReviewedByUserId != null).Select(s => s.ReviewedByUserId!.Value).Distinct().ToList();
        var reviewers = await db.Users.AsNoTracking().Where(u => reviewerIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName);
        var docs = await db.Documents.AsNoTracking().Where(d => d.CaseId == a.CaseId && a.SharedDocumentIds.Contains(d.Id)).OrderBy(d => d.CreatedAt).ToListAsync();
        var versionIds = docs.Where(d => d.CurrentVersionId != null).Select(d => d.CurrentVersionId!.Value).ToList();
        var versions = await db.DocumentVersions.AsNoTracking().Where(v => versionIds.Contains(v.Id)).ToDictionaryAsync(v => v.Id);
        var deedMasked = docs.Any(d => d.DocumentTypeKey == ProviderAssignmentService.MaskedDeedType)
            ? await db.Properties.AsNoTracking().Where(p => p.CaseId == a.CaseId).Select(p => p.DeedNumberMasked).FirstOrDefaultAsync() : null;

        var due = EffectiveDue(a, subs);
        var (slaText, slaTone) = Sla(a, due, today);
        var writable = ProviderAssignmentService.IsWritable(a.Status);
        var readOnlyReason = writable ? null : $"سُلِّم التكليف؛ الوصول للقراءة فقط حتى {a.AccessExpiresAt?.ToOffset(TimeSpan.FromHours(3)):yyyy-MM-dd HH:mm}.";
        var lastReturned = subs.FirstOrDefault(s => s.Status == SubmissionStatus.Returned);
        var nextVersion = (subs.FirstOrDefault()?.VersionNo ?? 0) + 1;
        var inspectionLocal = a.InspectionAt?.ToOffset(TimeSpan.FromHours(3));

        return Results.Ok(new
        {
            a.Id, a.Reference, lender, eyebrow = $"تكليف من {lender} · {a.Reference}", title = $"{a.Title} — {a.PropertyLabel}",
            type = a.Type, typeLabel = ProviderAssignmentService.TypeLabel(a.Type),
            status = a.Status, statusLabel = ProviderAssignmentService.StatusLabel(a.Status),
            due = new { on = due, text = writable ? $"التسليم خلال {slaText} · {due:yyyy-MM-dd}" : slaText, tone = slaTone },
            scope = a.Scope.Select(s => s.Split(" — ", 2) is [var k, var v] ? new { k, v } : new { k = "نطاق العمل", v = s }),
            inspection = new
            {
                at = a.InspectionAt, a.InspectionConfirmed, contact = a.InspectionContact,
                label = inspectionLocal is { } il ? $"{il:yyyy-MM-dd HH:mm} · {(a.InspectionConfirmed ? "مؤكد" : "بانتظار التأكيد")}" : "لم يُحدد بعد",
            },
            fees = a.FeesLabel,
            documents = docs.Select(d =>
            {
                var masked = d.DocumentTypeKey == ProviderAssignmentService.MaskedDeedType;
                var v = d.CurrentVersionId is { } cv ? versions.GetValueOrDefault(cv) : null;
                var downloadable = !masked && v is { ScanStatus: ScanStatus.Clean };
                return new
                {
                    d.Id, name = masked ? $"{d.Name} (مخفي الأرقام)" : d.Name, masked, deedNumberMasked = masked ? deedMasked : null,
                    downloadable, reason = masked ? "رقم الصك مخفي، والصك لا يُنزّل لمقدم الخدمة." : v is null ? "لم يُرفع ملف بعد." : null,
                    sizeBytes = v?.SizeBytes, contentType = v?.ContentType,
                };
            }),
            access = new
            {
                mode = writable ? "write" : "read_only", expiresAt = a.AccessExpiresAt, deliveredAt = a.DeliveredAt,
                notice = IsolationNotice,
                expiryText = a.AccessExpiresAt is { } e ? $"ينتهي وصولك آلياً في {e.ToOffset(TimeSpan.FromHours(3)):yyyy-MM-dd HH:mm}." : "ينتهي وصولك بعد 7 أيام من التسليم.",
            },
            returnAlert = a.Status == AssignmentStatus.Returned && lastReturned is not null ? new
            {
                title = $"أُعيد التقرير v{lastReturned.VersionNo} للتعديل", notes = lastReturned.ReturnNotes,
                meta = $"{(lastReturned.ReviewedByUserId is { } rid ? reviewers.GetValueOrDefault(rid) : null) ?? "فريق الحالة"} · {lastReturned.ReviewedAt?.ToOffset(TimeSpan.FromHours(3)):yyyy-MM-dd} · مهلة إعادة التسليم {lastReturned.ResubmitDueOn:yyyy-MM-dd}",
                resubmitDueOn = lastReturned.ResubmitDueOn,
            } : null,
            submissions = subs.Select(s => new
            {
                version = s.VersionNo, status = s.Status, s.SubmittedAt, s.MarketValue, s.InspectionDate, s.ReturnNotes, s.ResubmitDueOn,
            }),
            nextVersion,
            checklist = Checklist.Select(c => new { key = c.Key, label = c.Label }),
            independenceLabel = IndependenceLabel,
            actions = new
            {
                submit = new { label = a.Status == AssignmentStatus.Returned ? $"تسليم الإصدار v{nextVersion}" : "بدء التسليم", enabled = writable, reason = readOnlyReason },
                message = new { enabled = writable, reason = readOnlyReason },
                confirmInspection = new { enabled = writable && !a.InspectionConfirmed && a.InspectionAt != null, reason = writable ? null : readOnlyReason },
            },
        });
    }

    private static async Task<IResult> Download(Guid id, Guid documentId, ProviderAssignmentService service, RahoonDbContext db, RequestContext rc,
        IDocumentStorage storage, IClock clock, AuditLog audit)
    {
        var a = await service.GetForProviderAsync(id, track: false);
        if (!a.SharedDocumentIds.Contains(documentId)) throw new NotFoundException();
        var doc = await db.Documents.AsNoTracking().FirstOrDefaultAsync(d => d.Id == documentId && d.CaseId == a.CaseId) ?? throw new NotFoundException();
        if (doc.DocumentTypeKey == ProviderAssignmentService.MaskedDeedType)
            throw new ForbiddenException("رقم الصك مخفي، والصك لا يُنزّل لمقدم الخدمة.");
        var v = doc.CurrentVersionId is { } cv ? await db.DocumentVersions.AsNoTracking().FirstOrDefaultAsync(x => x.Id == cv) : null;
        if (v is not { ScanStatus: ScanStatus.Clean }) throw new NotFoundException();

        var watermark = $"{rc.UserName} · {rc.OrganizationName} · {clock.UtcNow.ToOffset(TimeSpan.FromHours(3)):yyyy-MM-dd HH:mm} · {a.Reference}";
        db.DownloadLogs.Add(new DownloadLog { OrganizationId = a.OrganizationId, VersionId = v.Id, UserId = rc.UserId, Watermark = watermark, At = clock.UtcNow });
        var caseRef = await db.Cases.Where(c => c.Id == a.CaseId).Select(c => c.Reference).FirstAsync();
        await audit.RecordAsync(new AuditEntry("assignment.document_downloaded", $"تنزيل مقدم الخدمة {doc.Name} v{v.VersionNo} · {a.Reference}", a.CaseId, caseRef,
            Detail: watermark, OrganizationId: a.OrganizationId));
        await db.SaveChangesAsync();
        return Results.File(await storage.OpenReadAsync(v.StorageKey), v.ContentType, v.FileName);
    }

    private static async Task<IResult> Messages(Guid id, ProviderAssignmentService service, RahoonDbContext db)
    {
        var a = await service.GetForProviderAsync(id, track: false);
        var msgs = await db.AssignmentMessages.AsNoTracking().Where(m => m.AssignmentId == a.Id).OrderBy(m => m.At).ToListAsync();
        return Results.Ok(new
        {
            canSend = ProviderAssignmentService.IsWritable(a.Status),
            messages = msgs.Select(m => new { m.Id, mine = m.AuthorSide == "provider", author = m.AuthorSide == "provider" ? "أنت" : m.AuthorLabel, m.Body, m.At }),
        });
    }

    private static async Task<IResult> PostMessage(Guid id, ProviderMessageRequest req, ProviderAssignmentService service, RahoonDbContext db, RequestContext rc,
        IClock clock, AuditLog audit)
    {
        new Validator().Require(!string.IsNullOrWhiteSpace(req.Body) && req.Body.Trim().Length <= 2000, "body", "اكتب استفساراً للجهة (حتى 2000 حرف).").ThrowIfInvalid();
        await using var tx = await db.Database.BeginTransactionAsync();
        var a = await service.GetForProviderAsync(id);
        service.EnsureWritable(a);
        service.MarkStarted(a);
        db.AssignmentMessages.Add(new AssignmentMessage
        {
            OrganizationId = a.OrganizationId, AssignmentId = a.Id, AuthorUserId = rc.UserId, AuthorLabel = $"{rc.UserName} · {rc.OrganizationName}",
            AuthorSide = "provider", Body = req.Body.Trim(), At = clock.UtcNow,
        });
        var caseRef = await db.Cases.Where(c => c.Id == a.CaseId).Select(c => c.Reference).FirstAsync();
        await audit.RecordAsync(new AuditEntry("assignment.question", $"استفسار من مقدم الخدمة · {a.Reference}", a.CaseId, caseRef, OrganizationId: a.OrganizationId));
        service.NotifyLender(a, caseRef, $"استفسار من {rc.OrganizationName} · {a.Reference}", req.Body.Trim().Length > 140 ? req.Body.Trim()[..140] + "…" : req.Body.Trim());
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Results.Ok(new { sent = true });
    }

    private static async Task<IResult> ConfirmInspection(Guid id, ConfirmInspectionRequest req, ProviderAssignmentService service, RahoonDbContext db,
        RequestContext rc, IClock clock, AuditLog audit)
    {
        await using var tx = await db.Database.BeginTransactionAsync();
        var a = await service.GetForProviderAsync(id);
        service.EnsureWritable(a);
        if (req.At is { } at)
        {
            if (at <= clock.UtcNow) Validate.Throw("at", "موعد المعاينة يجب أن يكون في المستقبل.");
            a.InspectionAt = at;
        }
        if (a.InspectionAt is null) Validate.Throw("at", "حدد موعد المعاينة.");
        a.InspectionConfirmed = true;
        service.MarkStarted(a);
        var caseRef = await db.Cases.Where(c => c.Id == a.CaseId).Select(c => c.Reference).FirstAsync();
        var local = a.InspectionAt!.Value.ToOffset(TimeSpan.FromHours(3));
        await audit.RecordAsync(new AuditEntry("assignment.inspection_confirmed", $"تأكيد موعد المعاينة · {a.Reference}", a.CaseId, caseRef,
            Detail: $"{local:yyyy-MM-dd HH:mm}", OrganizationId: a.OrganizationId));
        service.NotifyLender(a, caseRef, $"تأكيد المعاينة · {a.Reference}", $"{rc.OrganizationName}: {local:yyyy-MM-dd HH:mm}");
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Results.Ok(new { a.InspectionAt, a.InspectionConfirmed, status = a.Status });
    }

    private static decimal? ParseMoney(string? raw) =>
        decimal.TryParse((raw ?? "").Replace(",", "").Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var d) ? Math.Round(d, 2) : null;

    /// <summary>
    /// V04 submission / resubmission (multipart): report PDF, market value, range, methodology, comparables,
    /// inspection date, checklist and the independence declaration (all required, B7 C2). Creates version n and
    /// starts the 7-day read-only window.
    /// </summary>
    private static async Task<IResult> Submit(Guid id, HttpRequest http, ProviderAssignmentService service, RahoonDbContext db, RequestContext rc,
        IDocumentStorage storage, IFileScanner scanner, IClock clock, AuditLog audit)
    {
        if (!http.HasFormContentType) throw new DomainException("form_required", "أرسل التقرير كنموذج متعدد الأجزاء.", 400);
        var a = await service.GetForProviderAsync(id, track: false);
        service.EnsureWritable(a);

        var form = await http.ReadFormAsync();
        var file = form.Files.GetFile("file");
        var today = clock.TodayRiyadh;
        var valuation = a.Type is AssignmentType.Valuation or AssignmentType.Inspection;
        var market = ParseMoney(form["marketValue"]);
        var low = ParseMoney(form["rangeLow"]);
        var high = ParseMoney(form["rangeHigh"]);
        var methodology = form["methodology"].ToString().Trim();
        var comparables = int.TryParse(form["comparablesCount"], out var cc) ? cc : (int?)null;
        var inspection = DateOnly.TryParse(form["inspectionDate"], CultureInfo.InvariantCulture, out var idt) ? idt : (DateOnly?)null;
        var checklist = form["checklist"].SelectMany(x => (x ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)).Distinct().ToList();
        var independence = form["independenceDeclared"].ToString().Trim().ToLowerInvariant() is "true" or "on" or "1";

        new Validator()
            .Require(file is { Length: > 0 }, "file", "أرفق تقرير التقييم بصيغة PDF.")
            .Require(!valuation || market is > 0 and < 1_000_000_000_000m, "marketValue", "أدخل القيمة السوقية بالريال (أكبر من صفر).")
            .Require(!valuation || low is null || high is null || (low <= market && market <= high), "rangeLow", "النطاق يجب أن يحيط بالقيمة السوقية.")
            .Require((low is null or > 0) && (high is null or > 0), "rangeHigh", "حدود النطاق أكبر من صفر.")
            .Require(!valuation || methodology.Length is > 0 and <= 500, "methodology", "اذكر المنهجية المستخدمة.")
            .Require(!valuation || comparables is >= 1 and <= 100, "comparablesCount", "أدخل عدد صفقات المقارنة (1 على الأقل).")
            .Require(!valuation || inspection is { } d && d <= today, "inspectionDate", "أدخل تاريخ المعاينة (اليوم أو قبله).")
            .Require(Checklist.All(c => checklist.Contains(c.Key)), "checklist", "أكمل قائمة التحقق قبل التسليم.")
            .Require(independence, "independenceDeclared", "إقرار الاستقلالية وعدم تعارض المصالح مطلوب قبل التسليم.")
            .ThrowIfInvalid();

        // Stored under the lender's organization: the report belongs to the case file.
        await using var stream = file!.OpenReadStream();
        var stored = await storage.SaveAsync(stream, file.FileName, a.OrganizationId);
        if (stored.ContentType != "application/pdf") throw new ValidationFailedException(new Dictionary<string, string[]> { ["file"] = ["التقرير يجب أن يكون ملف PDF."] });
        var scan = await scanner.ScanAsync(stored.StorageKey);
        if (scan != ScanStatus.Clean) throw new DomainException("file_infected", "رُفض الملف: فشل فحص الأمان.");

        await using var tx = await db.Database.BeginTransactionAsync();
        var asg = await service.GetForProviderAsync(id);
        service.EnsureWritable(asg);
        var previous = await db.AssignmentSubmissions.Where(s => s.AssignmentId == asg.Id).OrderByDescending(s => s.VersionNo).FirstOrDefaultAsync();
        var versionNo = (previous?.VersionNo ?? 0) + 1;
        var c = await db.Cases.AsNoTracking().Where(x => x.Id == asg.CaseId).Select(x => new { x.Id, x.Reference }).FirstAsync();

        var docId = previous?.ReportDocumentVersionId is { } pv ? await db.DocumentVersions.Where(v => v.Id == pv).Select(v => (Guid?)v.DocumentId).FirstOrDefaultAsync() : null;
        var doc = docId is { } did ? await db.Documents.FirstAsync(d => d.Id == did) : null;
        if (doc is null)
        {
            doc = new CaseDocument
            {
                OrganizationId = asg.OrganizationId, CaseId = asg.CaseId, DocumentTypeKey = valuation ? "valuation_report" : "external_official_document",
                Name = $"{(valuation ? "تقرير التقييم" : "تقرير مقدم الخدمة")} — {asg.Reference}", Source = DocumentSource.Provider, VisibleTo = ["case_team", "approver"],
            };
            db.Documents.Add(doc);
        }
        var version = new DocumentVersion
        {
            OrganizationId = asg.OrganizationId, DocumentId = doc.Id, CaseId = asg.CaseId, VersionNo = doc.VersionCount + 1, FileName = Path.GetFileName(file.FileName),
            ContentType = stored.ContentType, SizeBytes = stored.SizeBytes, Sha256 = stored.Sha256, StorageKey = stored.StorageKey,
            UploadedByUserId = rc.UserId, UploadedByLabel = rc.OrganizationName ?? rc.UserName, UploadedAt = clock.UtcNow, ScanStatus = scan,
        };
        db.DocumentVersions.Add(version);
        doc.VersionCount = version.VersionNo;
        doc.CurrentVersionId = version.Id;
        doc.Status = DocumentStatus.InReview;

        var sub = new AssignmentSubmission
        {
            OrganizationId = asg.OrganizationId, AssignmentId = asg.Id, VersionNo = versionNo, MarketValue = market, RangeLow = low, RangeHigh = high,
            InspectionDate = inspection, Methodology = methodology.Length > 0 ? methodology : null, ComparablesCount = comparables, ReportDocumentVersionId = version.Id,
            Checklist = [.. checklist.Where(k => Checklist.Any(c => c.Key == k)), "independence"], IndependenceDeclared = true,
            SubmittedAt = clock.UtcNow, SubmittedByUserId = rc.UserId,
        };
        db.AssignmentSubmissions.Add(sub);
        asg.Status = AssignmentStatus.Submitted;
        asg.DeliveredAt = sub.SubmittedAt;
        asg.AccessExpiresAt = sub.SubmittedAt.AddDays(ProviderAssignmentService.ReadOnlyDays);

        await audit.RecordAsync(new AuditEntry(valuation ? "valuation.submitted" : "assignment.submitted", $"تسليم {asg.Reference} v{versionNo} من {rc.OrganizationName}", c.Id, c.Reference,
            Detail: market is { } m ? $"القيمة السوقية {m:N2} ر.س · فحص الملف: سليم" : "فحص الملف: سليم", Evidence: ["إقرار الاستقلالية"], OrganizationId: asg.OrganizationId));
        service.NotifyLender(asg, c.Reference, $"تسليم تقرير {asg.Reference} v{versionNo}", $"من {rc.OrganizationName} · بانتظار المراجعة", "info");
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Results.Ok(new { version = versionNo, status = asg.Status, deliveredAt = asg.DeliveredAt, accessExpiresAt = asg.AccessExpiresAt });
    }
}
