using Microsoft.EntityFrameworkCore;
using Rahoon.Api.Infrastructure.Http;
using Rahoon.Api.Infrastructure.Persistence;
using Rahoon.Api.Infrastructure.Tenancy;
using Rahoon.Api.Infrastructure.Time;
using Rahoon.Api.Modules.Audit;
using Rahoon.Api.Modules.Cases;
using Rahoon.Api.Modules.Identity;

namespace Rahoon.Api.Modules.Providers;

public sealed record CreateAssignmentRequest(
    Guid ProviderOrganizationId, string Type, DateOnly DueOn, List<string>? Scope, List<Guid>? SharedDocumentIds,
    DateTimeOffset? InspectionAt, string? Title, string? FeesLabel, decimal? FeeAmount, Guid? AssigneeUserId);
public sealed record ReviewSubmissionRequest(string Decision, List<string>? Notes, DateOnly? ResubmitDueOn, string? Note);
public sealed record AssignmentMessageRequest(string Body, List<Guid>? ShareDocumentIds);

/// <summary>Lender side of provider assignments (case workspace → valuation / providers).</summary>
public static class AssignmentEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/cases/{reference}/assignments").RequirePermission(P.CaseView);
        g.MapGet("", List);
        g.MapGet("/options", Options).RequireAnyPermission(P.ProviderAssign, P.ValuationAssign);
        g.MapPost("", Create).RequireAnyPermission(P.ProviderAssign, P.ValuationAssign).Idempotent();
        g.MapGet("/{id:guid}", Detail);
        g.MapGet("/{id:guid}/messages", Messages);
        g.MapPost("/{id:guid}/messages", PostMessage).Idempotent();
        g.MapPost("/{id:guid}/submissions/{version:int}/review", Review).RequirePermission(P.ValuationReview).Idempotent();
    }

    private static async Task<IResult> Options(string reference, CaseAccess access, RahoonDbContext db)
    {
        var c = await access.GetAsync(reference, track: false);
        var providers = await db.Organizations.AsNoTracking()
            .Where(o => o.Kind == OrganizationKind.ServiceProvider && o.Status == OrganizationStatus.Active)
            .OrderBy(o => o.NameAr).Select(o => new { o.Id, name = o.NameAr, o.City }).ToListAsync();
        var sensitive = await db.DocumentTypes.Where(t => t.Sensitive).Select(t => t.Key).ToListAsync();
        var docs = await db.Documents.AsNoTracking().Where(d => d.CaseId == c.Id && !d.Internal).OrderBy(d => d.CreatedAt).ToListAsync();
        return Results.Ok(new
        {
            providers,
            types = Enum.GetValues<AssignmentType>().Select(t => new { key = t, label = ProviderAssignmentService.TypeLabel(t) }),
            documents = docs.Select(d =>
            {
                var blocked = ProviderAssignmentService.NonShareableTypes.Contains(d.DocumentTypeKey) || sensitive.Contains(d.DocumentTypeKey);
                return new
                {
                    d.Id, d.Name, d.DocumentTypeKey, shareable = !blocked,
                    reason = blocked ? "لا يُشارك مع مقدم الخدمة (هوية أو دخل أو مديونية)." : d.DocumentTypeKey == ProviderAssignmentService.MaskedDeedType ? "يظهر للمقدم برقم مخفي ودون تنزيل." : null,
                };
            }),
        });
    }

    private static async Task<IResult> List(string reference, CaseAccess access, RahoonDbContext db, IClock clock)
    {
        var c = await access.GetAsync(reference, track: false);
        var now = clock.UtcNow;
        var list = await db.Assignments.AsNoTracking().Where(a => a.CaseId == c.Id).OrderByDescending(a => a.CreatedAt).ToListAsync();
        var orgs = await ProviderNamesAsync(db, list.Select(a => a.ProviderOrganizationId));
        var ids = list.Select(a => a.Id).ToList();
        var subs = await db.AssignmentSubmissions.AsNoTracking().Where(s => ids.Contains(s.AssignmentId)).ToListAsync();
        return Results.Ok(list.Select(a =>
        {
            var latest = subs.Where(s => s.AssignmentId == a.Id).MaxBy(s => s.VersionNo);
            return new
            {
                a.Id, a.Reference, a.Title, a.PropertyLabel, type = a.Type, typeLabel = ProviderAssignmentService.TypeLabel(a.Type),
                provider = orgs.GetValueOrDefault(a.ProviderOrganizationId), status = a.Status, statusLabel = ProviderAssignmentService.StatusLabel(a.Status),
                a.DueOn, a.InspectionAt, a.InspectionConfirmed, a.DeliveredAt, a.AccessExpiresAt,
                providerAccess = AccessLabel(a, now),
                latestSubmission = latest is null ? null : new { version = latest.VersionNo, status = latest.Status, latest.SubmittedAt, latest.MarketValue },
            };
        }));
    }

    private static string AccessLabel(ProviderAssignment a, DateTimeOffset now) => a switch
    {
        { Status: AssignmentStatus.Cancelled } => "لا وصول (ملغى)",
        { AccessExpiresAt: { } e } when e <= now => "انتهى وصول المقدم",
        { AccessExpiresAt: { } e } => $"قراءة فقط حتى {e.ToOffset(TimeSpan.FromHours(3)):yyyy-MM-dd HH:mm}",
        _ => "وصول للعمل حتى التسليم",
    };

    private static async Task<Dictionary<Guid, string>> ProviderNamesAsync(RahoonDbContext db, IEnumerable<Guid> ids)
    {
        var list = ids.Distinct().ToList();
        return await db.Organizations.AsNoTracking().Where(o => list.Contains(o.Id)).ToDictionaryAsync(o => o.Id, o => o.NameAr);
    }

    private static async Task<IResult> Create(string reference, CreateAssignmentRequest req, CaseAccess access, RahoonDbContext db, RequestContext rc,
        ProviderAssignmentService service)
    {
        if (!Enum.TryParse<AssignmentType>(req.Type, true, out var type) || !Enum.IsDefined(type))
            Validate.Throw("type", "اختر نوع التكليف.");
        // Valuation work may be assigned by valuation.assign holders; other work needs provider.assign.
        if (type is not (AssignmentType.Valuation or AssignmentType.Inspection) && !rc.Has(P.ProviderAssign)) throw new ForbiddenException();

        await using var tx = await db.Database.BeginTransactionAsync();
        var c = await access.GetAsync(reference, track: false);
        var asg = await service.CreateAsync(c, new CreateAssignmentInput(req.ProviderOrganizationId, type, req.DueOn, req.Scope ?? [], req.SharedDocumentIds ?? [],
            req.InspectionAt, req.Title, req.FeesLabel, req.FeeAmount, req.AssigneeUserId));
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Results.Ok(new { asg.Id, asg.Reference, status = asg.Status });
    }

    private static async Task<IResult> Detail(string reference, Guid id, CaseAccess access, RahoonDbContext db, IClock clock)
    {
        var c = await access.GetAsync(reference, track: false);
        var a = await db.Assignments.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && x.CaseId == c.Id) ?? throw new NotFoundException();
        var provider = await db.Organizations.AsNoTracking().Where(o => o.Id == a.ProviderOrganizationId).Select(o => o.NameAr).FirstAsync();
        var docs = await db.Documents.AsNoTracking().Where(d => a.SharedDocumentIds.Contains(d.Id)).Select(d => new { d.Id, d.Name, d.DocumentTypeKey }).ToListAsync();
        var subs = await db.AssignmentSubmissions.AsNoTracking().Where(s => s.AssignmentId == a.Id).OrderByDescending(s => s.VersionNo).ToListAsync();
        return Results.Ok(new
        {
            a.Id, a.Reference, a.Title, a.PropertyLabel, type = a.Type, typeLabel = ProviderAssignmentService.TypeLabel(a.Type), provider,
            status = a.Status, statusLabel = ProviderAssignmentService.StatusLabel(a.Status), a.DueOn, a.InspectionAt, a.InspectionConfirmed, a.InspectionContact,
            a.Scope, a.FeesLabel, a.DeliveredAt, a.AccessExpiresAt, providerAccess = AccessLabel(a, clock.UtcNow), sharedDocuments = docs,
            submissions = subs.Select(s => new
            {
                version = s.VersionNo, status = s.Status, s.SubmittedAt, s.MarketValue, s.RangeLow, s.RangeHigh, s.Methodology, s.ComparablesCount,
                s.InspectionDate, s.Checklist, s.IndependenceDeclared, reportVersionId = s.ReportDocumentVersionId, s.ReturnNotes, s.ResubmitDueOn, s.ReviewedAt,
                canReview = s.Status == SubmissionStatus.Submitted && a.Status == AssignmentStatus.Submitted,
            }),
        });
    }

    private static async Task<IResult> Messages(string reference, Guid id, CaseAccess access, RahoonDbContext db)
    {
        var c = await access.GetAsync(reference, track: false);
        var a = await db.Assignments.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && x.CaseId == c.Id) ?? throw new NotFoundException();
        var msgs = await db.AssignmentMessages.AsNoTracking().Where(m => m.AssignmentId == a.Id).OrderBy(m => m.At)
            .Select(m => new { m.Id, author = m.AuthorLabel, side = m.AuthorSide, m.Body, m.At }).ToListAsync();
        return Results.Ok(msgs);
    }

    private static async Task<IResult> PostMessage(string reference, Guid id, AssignmentMessageRequest req, CaseAccess access, RahoonDbContext db, RequestContext rc,
        IClock clock, AuditLog audit, ProviderAssignmentService service)
    {
        new Validator().Require(!string.IsNullOrWhiteSpace(req.Body) && req.Body.Trim().Length <= 2000, "body", "اكتب الرد (حتى 2000 حرف).").ThrowIfInvalid();
        await using var tx = await db.Database.BeginTransactionAsync();
        var c = await access.GetAsync(reference, track: false);
        var a = await db.Assignments.FirstOrDefaultAsync(x => x.Id == id && x.CaseId == c.Id) ?? throw new NotFoundException();
        if (a.Status == AssignmentStatus.Cancelled) throw new ConflictException("assignment_cancelled", "التكليف ملغى.");

        var share = (req.ShareDocumentIds ?? []).Distinct().Where(d => !a.SharedDocumentIds.Contains(d)).ToList();
        if (share.Count > 0)
        {
            var types = await db.DocumentTypes.AsNoTracking().ToDictionaryAsync(t => t.Key);
            var docs = await db.Documents.AsNoTracking().Where(d => d.CaseId == c.Id && share.Contains(d.Id)).ToListAsync();
            new Validator().Require(docs.Count == share.Count && docs.All(d => !d.Internal && !ProviderAssignmentService.NonShareableTypes.Contains(d.DocumentTypeKey)
                                                                                && !(types.GetValueOrDefault(d.DocumentTypeKey)?.Sensitive ?? false)),
                "shareDocumentIds", "لا تُشارك مع مقدم الخدمة مستندات الهوية أو الدخل أو المديونية أو الاتفاقات.").ThrowIfInvalid();
            a.SharedDocumentIds = [.. a.SharedDocumentIds, .. share];
        }
        var org = rc.OrganizationName;
        db.AssignmentMessages.Add(new AssignmentMessage
        {
            OrganizationId = a.OrganizationId, AssignmentId = a.Id, AuthorUserId = rc.UserId, AuthorLabel = $"{rc.UserName} · {org}", AuthorSide = "lender",
            Body = req.Body.Trim(), At = clock.UtcNow,
        });
        await audit.RecordAsync(new AuditEntry("assignment.message", $"رد على استفسار {a.Reference}", c.Id, c.Reference,
            Detail: share.Count > 0 ? $"أُضيف {share.Count} مستند لمستندات التكليف" : null, OrganizationId: c.OrganizationId));
        await service.NotifyProviderAsync(a, $"رد على استفسارك · {a.Reference}", req.Body.Trim().Length > 140 ? req.Body.Trim()[..140] + "…" : req.Body.Trim());
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Results.Ok(new { sent = true, sharedDocuments = a.SharedDocumentIds.Count });
    }

    private static async Task<IResult> Review(string reference, Guid id, int version, ReviewSubmissionRequest req, CaseAccess access, RahoonDbContext db,
        ProviderAssignmentService service)
    {
        await using var tx = await db.Database.BeginTransactionAsync();
        var c = await access.GetAsync(reference, track: false);
        var a = await db.Assignments.FirstOrDefaultAsync(x => x.Id == id && x.CaseId == c.Id) ?? throw new NotFoundException();
        var sub = await service.ReviewAsync(c, a, version, new ReviewSubmissionInput(req.Decision, req.Notes, req.ResubmitDueOn, req.Note));
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Results.Ok(new { status = a.Status, submission = sub.Status, a.DeliveredAt, a.AccessExpiresAt });
    }
}
