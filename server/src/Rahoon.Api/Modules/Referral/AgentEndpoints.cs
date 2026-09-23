using Microsoft.EntityFrameworkCore;
using Rahoon.Api.Infrastructure.Http;
using Rahoon.Api.Infrastructure.Persistence;
using Rahoon.Api.Infrastructure.Storage;
using Rahoon.Api.Infrastructure.Tenancy;
using Rahoon.Api.Infrastructure.Time;
using Rahoon.Api.Modules.Audit;
using Rahoon.Api.Modules.Communications;
using Rahoon.Api.Modules.Documents;
using Rahoon.Api.Modules.Identity;
using Rahoon.Api.Modules.Providers;

namespace Rahoon.Api.Modules.Referral;

public sealed record PlanMilestoneInput(DateOnly On, string Text);
public sealed record AgentPlanRequest(List<PlanMilestoneInput> Milestones);
public sealed record AgentUpdateRequest(string Text, string? Kind, List<Guid>? AttachmentVersionIds);
public sealed record SaleResultDraftRequest(decimal? OfficialSalePrice, DateOnly? SaleMinutesDate, decimal? DeclaredCosts, List<Guid>? EvidenceVersionIds);

/// <summary>
/// Judicial sale agent portal (J05–J07). An agent reaches a lender's data only through a JudicialSale assignment
/// for its own organization. Every query starts from that assignment row — the tenant filter alone would expose the
/// whole lender while any assignment is active. Access ends 7 days after delivery (as for providers) or when the lender ends it.
/// </summary>
public static class AgentEndpoints
{
    private static readonly string[] UpdateKinds = ["general", "inspection_done", "plan_submitted", "minutes_issued"];

    public static void Map(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/agent").RequireOrg(OrganizationKind.JudicialAgent).RequirePermission(P.AgentWork);
        g.MapGet("/assignments", List);
        g.MapGet("/assignments/{assignmentRef}", Detail);
        g.MapGet("/assignments/{assignmentRef}/documents/{versionId:guid}/file", Download);
        g.MapPost("/assignments/{assignmentRef}/plan", SavePlan).Idempotent();
        g.MapPost("/assignments/{assignmentRef}/updates", AddUpdate).Idempotent();
        g.MapPost("/assignments/{assignmentRef}/evidence", UploadEvidence).DisableAntiforgery();
        g.MapPut("/assignments/{assignmentRef}/result", SaveResult).Idempotent();
        g.MapPost("/assignments/{assignmentRef}/result/submit", SubmitResult).Idempotent();
    }

    private static IQueryable<ProviderAssignment> Mine(RahoonDbContext db, RequestContext rc) =>
        db.Assignments.Where(a => a.ProviderOrganizationId == rc.OrganizationId && a.Type == AssignmentType.JudicialSale && a.Status != AssignmentStatus.Cancelled);

    private static async Task<ProviderAssignment> LoadAsync(RahoonDbContext db, RequestContext rc, string assignmentRef, bool track = false)
    {
        var q = Mine(db, rc);
        if (!track) q = q.AsNoTracking();
        return await q.FirstOrDefaultAsync(a => a.Reference == assignmentRef) ?? throw new NotFoundException();
    }

    private static void EnsureWritable(ProviderAssignment a, IClock clock)
    {
        if (a.Status is not (AssignmentStatus.New or AssignmentStatus.InProgress or AssignmentStatus.Returned) || a.AccessExpiresAt is not null)
            throw new DomainException("read_only", "انتهى العمل على هذا التكليف؛ وصولك للقراءة فقط حتى انتهاء المهلة.", StatusCodes.Status409Conflict);
        _ = clock;
    }

    private static async Task<string?> ExternalRefAsync(RahoonDbContext db, Guid caseId) =>
        await db.Referrals.AsNoTracking().Where(r => r.CaseId == caseId && r.Status != ReferralStatus.Rejected && r.Status != ReferralStatus.Withdrawn)
            .OrderByDescending(r => r.CreatedAt).Select(r => r.ExternalRequestNumber).FirstOrDefaultAsync();

    private static async Task<IResult> List(RahoonDbContext db, RequestContext rc, IClock clock)
    {
        var rows = await Mine(db, rc).AsNoTracking().OrderByDescending(a => a.CreatedAt).ToListAsync();
        var lenders = await db.Organizations.AsNoTracking().Where(o => rows.Select(r => r.OrganizationId).Contains(o.Id)).ToDictionaryAsync(o => o.Id, o => o.NameAr);
        var items = new List<object>();
        foreach (var a in rows)
        {
            var ext = await ExternalRefAsync(db, a.CaseId);
            items.Add(new
            {
                a.Reference, externalRef = ReferralService.MaskExternal(ext), title = a.PropertyLabel, lender = lenders.GetValueOrDefault(a.OrganizationId),
                assignedOn = a.CreatedAt, a.DueOn, status = a.Status.ToString(),
                meta = a.AccessExpiresAt is null ? $"تكليف {a.CreatedAt.ToOffset(TimeSpan.FromHours(3)):yyyy-MM-dd} · {lenders.GetValueOrDefault(a.OrganizationId)}" : "مكتمل · للقراءة فقط",
                readOnly = a.AccessExpiresAt is not null, a.AccessExpiresAt,
            });
        }
        return Results.Ok(new { items, note = "ترى فقط الحالات المكلف بها ومستندات الحزمة المشتركة." });
    }

    private static async Task<IResult> Detail(string assignmentRef, RahoonDbContext db, RequestContext rc, IClock clock)
    {
        var a = await LoadAsync(db, rc, assignmentRef);
        var ext = await ExternalRefAsync(db, a.CaseId);
        var lender = await db.Organizations.AsNoTracking().Where(o => o.Id == a.OrganizationId).Select(o => o.NameAr).FirstAsync();
        var property = await db.Properties.AsNoTracking().Where(p => p.CaseId == a.CaseId).Select(p => new { p.Type, p.City, p.District, p.ShortLabel, p.Occupancy, p.BuiltAreaM2, p.LandAreaM2 }).FirstOrDefaultAsync();
        var docs = await db.Documents.AsNoTracking().Include(d => d.Versions).Where(d => d.CaseId == a.CaseId && a.SharedDocumentIds.Contains(d.Id)).ToListAsync();
        var plan = await db.Set<SalePlanMilestone>().AsNoTracking().Where(m => m.AssignmentId == a.Id).OrderBy(m => m.Seq).ToListAsync();
        var updates = await db.Set<AgentUpdate>().AsNoTracking().Where(u => u.AssignmentId == a.Id).OrderByDescending(u => u.At).ToListAsync();
        var result = await db.Set<SaleResult>().AsNoTracking().FirstOrDefaultAsync(s => s.AssignmentId == a.Id);
        var packExported = await db.Referrals.AsNoTracking().AnyAsync(r => r.CaseId == a.CaseId && r.EvidencePackExportedAt != null);
        var evidence = await EvidenceOfAsync(db, a);

        var readiness = new object[]
        {
            new { key = "pack_received", label = "الحزمة المصدّرة مستلمة", done = packExported && docs.Count > 0, meta = $"{docs.Count} مستندات" },
            new { key = "inspection", label = "المعاينة", done = updates.Any(u => u.Kind == "inspection_done"), meta = updates.Where(u => u.Kind == "inspection_done").Select(u => u.At.ToOffset(TimeSpan.FromHours(3)).ToString("yyyy-MM-dd")).FirstOrDefault() ?? "—" },
            new { key = "plan", label = "رفع خطة البيع للجهة", done = plan.Count > 0 && updates.Any(u => u.Kind == "plan_submitted"), meta = plan.Count == 0 ? "—" : $"{plan.Count} مراحل" },
            new { key = "eviction", label = "ترتيبات إخلاء إنسانية", done = false, meta = "بالتنسيق مع الجهة" },
            new { key = "advertising", label = "الإعلان وفق إجراءات الجهة", done = true, meta = "خارج رهون" },
        };
        return Results.Ok(new
        {
            a.Reference, externalRef = ReferralService.MaskExternal(ext), externalRefLabel = "مرجع الجهة", lender, title = a.PropertyLabel, a.DueOn, status = a.Status.ToString(), a.Scope,
            property, readiness,
            plan = plan.Select(m => new { m.On, m.Text }),
            planNote = "اعتماد الخطة وقرارات البيع لدى الجهة المختصة، لا في رهون.",
            documents = docs.Select(d =>
            {
                var v = d.Versions.OrderByDescending(x => x.VersionNo).FirstOrDefault();
                return new { d.Id, d.Name, versionId = v?.Id, v?.FileName, v?.Sha256, version = v is null ? "—" : $"v{v.VersionNo}" };
            }),
            evidence,
            updates = updates.Select(u => new { u.At, u.Text, u.Kind, u.AuthorLabel, attachments = u.AttachmentVersionIds.Count }),
            result = result is null ? null : new
            {
                status = result.Status.ToString(), result.OfficialSalePrice, result.SaleMinutesDate, result.DeclaredCosts, result.EvidenceVersionIds, result.SubmittedAt,
                tag = result.Status == SaleResultStatus.Confirmed ? "مؤكد رسمياً" : "أبلغ بها الوكيل",
                returnNote = result.Status == SaleResultStatus.Returned ? result.ConfirmationNote : null,
            },
            resultNote = "تُعرض النتيجة للمصرف موسومة «أبلغ بها الوكيل» حتى تؤكدها القناة الرسمية. لا تؤدي إلى توزيع تلقائي.",
            access = new
            {
                sees = "العقار، المستندات المصدّرة، مرجع الجهة.",
                doesNotSee = "سجل التفاوض، الشكاوى الداخلية، بيانات المالك الشخصية، بيانات حالات أخرى للمصرف.",
                expiresAt = a.AccessExpiresAt,
                notice = "ينتهي وصولك بانتهاء التكليف أو بإشعار من الجهة.",
                readOnly = a.AccessExpiresAt is not null,
            },
        });
    }

    /// <summary>Versions this agent organization uploaded for the assignment's case.</summary>
    private static async Task<List<object>> EvidenceOfAsync(RahoonDbContext db, ProviderAssignment a)
    {
        var agentUsers = db.Memberships.Where(m => m.OrganizationId == a.ProviderOrganizationId).Select(m => m.UserId);
        return await db.DocumentVersions.AsNoTracking().Where(v => v.CaseId == a.CaseId && agentUsers.Contains(v.UploadedByUserId))
            .OrderByDescending(v => v.UploadedAt).Select(v => (object)new { versionId = v.Id, v.FileName, v.Sha256, v.UploadedAt, scan = v.ScanStatus.ToString() }).ToListAsync();
    }

    private static async Task<IResult> Download(string assignmentRef, Guid versionId, RahoonDbContext db, RequestContext rc, IDocumentStorage storage, IClock clock, AuditLog audit)
    {
        var a = await LoadAsync(db, rc, assignmentRef);
        var v = await db.DocumentVersions.AsNoTracking().FirstOrDefaultAsync(x => x.Id == versionId && x.CaseId == a.CaseId) ?? throw new NotFoundException();
        var ownUpload = await db.Memberships.AnyAsync(m => m.OrganizationId == rc.OrganizationId && m.UserId == v.UploadedByUserId);
        if (!a.SharedDocumentIds.Contains(v.DocumentId) && !ownUpload) throw new NotFoundException();
        var reference = await db.Cases.AsNoTracking().Where(c => c.Id == a.CaseId).Select(c => c.Reference).FirstAsync();
        var watermark = $"{rc.UserName} · {rc.OrganizationName} · {clock.UtcNow.ToOffset(TimeSpan.FromHours(3)):yyyy-MM-dd HH:mm} · {a.Reference}";
        db.DownloadLogs.Add(new DownloadLog { OrganizationId = a.OrganizationId, VersionId = v.Id, UserId = rc.UserId, Watermark = watermark, At = clock.UtcNow });
        await audit.RecordAsync(new AuditEntry("document.downloaded", $"تنزيل الوكيل {v.FileName}", a.CaseId, reference, Detail: watermark, OrganizationId: a.OrganizationId));
        await db.SaveChangesAsync();
        return Results.File(await storage.OpenReadAsync(v.StorageKey), v.ContentType, v.FileName);
    }

    private static async Task<IResult> SavePlan(string assignmentRef, AgentPlanRequest req, RahoonDbContext db, RequestContext rc, IClock clock, AuditLog audit)
    {
        var v = new Validator().Require(req.Milestones is { Count: > 0 and <= 20 }, "milestones", "أضف مرحلة واحدة على الأقل (حتى 20).");
        if (req.Milestones is not null)
            v.Require(req.Milestones.All(m => !string.IsNullOrWhiteSpace(m.Text) && m.Text.Length <= 500), "milestones", "لكل مرحلة تاريخ ونص.");
        v.ThrowIfInvalid();
        await using var tx = await db.Database.BeginTransactionAsync();
        var a = await LoadAsync(db, rc, assignmentRef, track: true);
        EnsureWritable(a, clock);
        var set = db.Set<SalePlanMilestone>();
        set.RemoveRange(await set.Where(m => m.AssignmentId == a.Id).ToListAsync());
        var seq = 0;
        foreach (var m in req.Milestones!.OrderBy(m => m.On))
            set.Add(new SalePlanMilestone { OrganizationId = a.OrganizationId, AssignmentId = a.Id, CaseId = a.CaseId, Seq = ++seq, On = m.On, Text = m.Text.Trim() });
        if (a.Status == AssignmentStatus.New) a.Status = AssignmentStatus.InProgress;
        var reference = await db.Cases.AsNoTracking().Where(c => c.Id == a.CaseId).Select(c => c.Reference).FirstAsync();
        await audit.RecordAsync(new AuditEntry("agent.plan_saved", $"تحديث خطة البيع المقدمة للجهة ({seq} مراحل)", a.CaseId, reference, Detail: $"{rc.OrganizationName} · {a.Reference}", OrganizationId: a.OrganizationId));
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Results.Ok(new { milestones = seq });
    }

    private static async Task<IResult> AddUpdate(string assignmentRef, AgentUpdateRequest req, RahoonDbContext db, RequestContext rc, IClock clock, AuditLog audit, Notifier notifier)
    {
        var kind = req.Kind ?? "general";
        new Validator().Require(!string.IsNullOrWhiteSpace(req.Text) && req.Text.Trim().Length is >= 5 and <= 2000, "text", "اكتب التحديث (5 أحرف على الأقل).")
            .Require(UpdateKinds.Contains(kind), "kind", "نوع التحديث غير معروف.").ThrowIfInvalid();
        await using var tx = await db.Database.BeginTransactionAsync();
        var a = await LoadAsync(db, rc, assignmentRef, track: true);
        EnsureWritable(a, clock);
        var attachments = req.AttachmentVersionIds ?? [];
        await EnsureOwnEvidenceAsync(db, rc, a, attachments);
        db.Set<AgentUpdate>().Add(new AgentUpdate
        {
            OrganizationId = a.OrganizationId, AssignmentId = a.Id, CaseId = a.CaseId, AuthorUserId = rc.UserId, AuthorLabel = $"{rc.UserName} — {rc.OrganizationName}",
            Text = req.Text.Trim(), Kind = kind, At = clock.UtcNow, AttachmentVersionIds = attachments,
        });
        if (a.Status == AssignmentStatus.New) a.Status = AssignmentStatus.InProgress;
        var reference = await db.Cases.AsNoTracking().Where(c => c.Id == a.CaseId).Select(c => c.Reference).FirstAsync();
        notifier.Notify(a.CreatedByUserId, a.OrganizationId, "referral", $"تحديث من وكيل البيع · {reference}", req.Text.Trim(), $"/cases/{reference}/referral", a.CaseId);
        await audit.RecordAsync(new AuditEntry("agent.update", "تحديث من وكيل البيع (أبلغ به الوكيل)", a.CaseId, reference, Detail: req.Text.Trim(), OrganizationId: a.OrganizationId));
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Results.Ok(new { recorded = true });
    }

    private static async Task EnsureOwnEvidenceAsync(RahoonDbContext db, RequestContext rc, ProviderAssignment a, List<Guid> versionIds)
    {
        if (versionIds.Count == 0) return;
        var agentUsers = db.Memberships.Where(m => m.OrganizationId == rc.OrganizationId).Select(m => m.UserId);
        var ok = await db.DocumentVersions.CountAsync(v => versionIds.Contains(v.Id) && v.CaseId == a.CaseId && agentUsers.Contains(v.UploadedByUserId));
        if (ok != versionIds.Distinct().Count()) Validate.Throw("evidenceVersionIds", "الأدلة يجب أن تكون ملفات رفعتها لهذا التكليف.");
    }

    /// <summary>Evidence upload (multipart «file»). Stored in the lender's case as an external official document, never visible to the owner.</summary>
    private static async Task<IResult> UploadEvidence(string assignmentRef, HttpRequest http, RahoonDbContext db, RequestContext rc, IDocumentStorage storage,
        IFileScanner scanner, IClock clock, AuditLog audit)
    {
        if (!http.HasFormContentType) throw new DomainException("form_required", "أرسل الملف كنموذج متعدد الأجزاء.", 400);
        var form = await http.ReadFormAsync();
        var file = form.Files.GetFile("file") ?? throw new ValidationFailedException(new Dictionary<string, string[]> { ["file"] = ["اختر ملفاً."] });
        var a = await LoadAsync(db, rc, assignmentRef);
        EnsureWritable(a, clock);

        await using var stream = file.OpenReadStream();
        var stored = await storage.SaveAsync(stream, file.FileName, a.OrganizationId);
        var scan = await scanner.ScanAsync(stored.StorageKey);
        if (scan == ScanStatus.Infected) throw new DomainException("file_infected", "رُفض الملف: فشل فحص الأمان.");

        await using var tx = await db.Database.BeginTransactionAsync();
        var label = form["label"].ToString() is { Length: > 0 } l ? l.Trim() : "مستند من وكيل البيع";
        var doc = new CaseDocument
        {
            OrganizationId = a.OrganizationId, CaseId = a.CaseId, DocumentTypeKey = "external_official_document", Name = label, Source = DocumentSource.Provider,
            Status = DocumentStatus.InReview, VisibleTo = ["case_team", "legal"], VisibleToOwner = false, VersionCount = 1,
        };
        db.Documents.Add(doc);
        var version = new DocumentVersion
        {
            OrganizationId = a.OrganizationId, DocumentId = doc.Id, CaseId = a.CaseId, VersionNo = 1, FileName = Path.GetFileName(file.FileName),
            ContentType = stored.ContentType, SizeBytes = stored.SizeBytes, Sha256 = stored.Sha256, StorageKey = stored.StorageKey,
            UploadedByUserId = rc.UserId, UploadedByLabel = $"{rc.UserName} — {rc.OrganizationName}", UploadedAt = clock.UtcNow, ScanStatus = scan,
        };
        db.DocumentVersions.Add(version);
        doc.CurrentVersionId = version.Id;
        var reference = await db.Cases.AsNoTracking().Where(c => c.Id == a.CaseId).Select(c => c.Reference).FirstAsync();
        await audit.RecordAsync(new AuditEntry("agent.evidence_uploaded", $"رفع دليل من وكيل البيع: {label}", a.CaseId, reference,
            Detail: $"{stored.ContentType} · sha256:{stored.Sha256[..12]}… · فحص: {scan}", OrganizationId: a.OrganizationId));
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Results.Ok(new { versionId = version.Id, documentId = doc.Id, sha256 = stored.Sha256, scan = scan.ToString() });
    }

    private static async Task<IResult> SaveResult(string assignmentRef, SaleResultDraftRequest req, RahoonDbContext db, RequestContext rc, IClock clock)
    {
        new Validator().Require(req.OfficialSalePrice is null or > 0, "officialSalePrice", "ثمن البيع يجب أن يكون أكبر من صفر.")
            .Require(req.DeclaredCosts is null or >= 0, "declaredCosts", "التكاليف لا تكون سالبة.")
            .Require(req.SaleMinutesDate is null || req.SaleMinutesDate <= clock.TodayRiyadh, "saleMinutesDate", "تاريخ المحضر لا يكون في المستقبل.").ThrowIfInvalid();
        await using var tx = await db.Database.BeginTransactionAsync();
        var a = await LoadAsync(db, rc, assignmentRef);
        EnsureWritable(a, clock);
        var evidence = req.EvidenceVersionIds ?? [];
        await EnsureOwnEvidenceAsync(db, rc, a, evidence);
        var set = db.Set<SaleResult>();
        var result = await set.FirstOrDefaultAsync(s => s.AssignmentId == a.Id);
        if (result is { Status: SaleResultStatus.Submitted or SaleResultStatus.Confirmed }) throw new ConflictException("result_submitted", "أُرسلت النتيجة مسبقاً.");
        if (result is null)
        {
            result = new SaleResult { OrganizationId = a.OrganizationId, CaseId = a.CaseId, AssignmentId = a.Id };
            set.Add(result);
        }
        result.OfficialSalePrice = req.OfficialSalePrice;
        result.SaleMinutesDate = req.SaleMinutesDate;
        result.DeclaredCosts = req.DeclaredCosts;
        result.EvidenceVersionIds = evidence.Distinct().ToList();
        result.Status = SaleResultStatus.Draft;
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Results.Ok(new { status = result.Status.ToString() });
    }

    /// <summary>Submits the agent-reported result. Never changes the case state and never creates a distribution.</summary>
    private static async Task<IResult> SubmitResult(string assignmentRef, RahoonDbContext db, RequestContext rc, IClock clock, AuditLog audit, Notifier notifier)
    {
        await using var tx = await db.Database.BeginTransactionAsync();
        var a = await LoadAsync(db, rc, assignmentRef, track: true);
        EnsureWritable(a, clock);
        var result = await db.Set<SaleResult>().FirstOrDefaultAsync(s => s.AssignmentId == a.Id) ?? throw new ConflictException("no_draft", "احفظ النتيجة أولاً.");
        if (result.Status is SaleResultStatus.Submitted or SaleResultStatus.Confirmed) throw new ConflictException("result_submitted", "أُرسلت النتيجة مسبقاً.");
        new Validator().Require(result.OfficialSalePrice is > 0, "officialSalePrice", "ثمن البيع الرسمي مطلوب.")
            .Require(result.SaleMinutesDate is not null, "saleMinutesDate", "تاريخ محضر البيع مطلوب.")
            .Require(result.EvidenceVersionIds.Count > 0, "evidenceVersionIds", "أرفق دليلاً واحداً على الأقل (محضر البيع).").ThrowIfInvalid();
        result.Status = SaleResultStatus.Submitted;
        result.Source = ExternalSourceKind.AgentReported;
        result.SubmittedByUserId = rc.UserId;
        result.SubmittedAt = clock.UtcNow;
        a.Status = AssignmentStatus.Submitted;

        var referral = await db.Referrals.Where(r => r.CaseId == a.CaseId && r.Status != ReferralStatus.Rejected && r.Status != ReferralStatus.Withdrawn)
            .OrderByDescending(r => r.CreatedAt).FirstAsync();
        db.ExternalStatusEntries.Add(new ExternalStatusEntry
        {
            OrganizationId = a.OrganizationId, ReferralId = referral.Id, CaseId = a.CaseId,
            StatusText = $"محضر البيع · {result.OfficialSalePrice:N2} ر.س · تاريخ المحضر {result.SaleMinutesDate:yyyy-MM-dd}",
            Source = $"أبلغ بها الوكيل — {rc.OrganizationName}", ObservedAt = clock.UtcNow, EnteredByUserId = rc.UserId,
            SourceKind = ExternalSourceKind.AgentReported, Kind = "agent_report", OfficiallyConfirmed = false,
        });
        var reference = await db.Cases.AsNoTracking().Where(c => c.Id == a.CaseId).Select(c => c.Reference).FirstAsync();
        notifier.Notify(a.CreatedByUserId, a.OrganizationId, "referral", $"نتيجة بيع من الوكيل بانتظار التأكيد · {reference}",
            $"{result.OfficialSalePrice:N2} ر.س · غير مؤكدة رسمياً بعد", $"/cases/{reference}/referral", a.CaseId, "warn");
        await audit.RecordAsync(new AuditEntry("agent.result_submitted", "إرسال نتيجة البيع (أبلغ بها الوكيل)", a.CaseId, reference,
            Detail: $"ثمن البيع {result.OfficialSalePrice:N2} · التكاليف المعلنة {result.DeclaredCosts ?? 0:N2} · أدلة {result.EvidenceVersionIds.Count} · لا توزيع ولا انتقال تلقائي",
            OrganizationId: a.OrganizationId));
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Results.Ok(new { status = result.Status.ToString(), tag = "أبلغ بها الوكيل" });
    }
}
