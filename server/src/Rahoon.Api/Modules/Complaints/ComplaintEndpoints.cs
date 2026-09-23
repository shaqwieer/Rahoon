using Microsoft.EntityFrameworkCore;
using Rahoon.Api.Infrastructure.Http;
using Rahoon.Api.Infrastructure.Persistence;
using Rahoon.Api.Infrastructure.Tenancy;
using Rahoon.Api.Infrastructure.Time;
using Rahoon.Api.Modules.Cases;
using Rahoon.Api.Modules.Identity;

namespace Rahoon.Api.Modules.Complaints;

public static class ComplaintEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/complaints").RequirePermission(P.ComplaintView);
        g.MapGet("", List);
        g.MapPost("", Register).RequirePermission(P.CaseView).Idempotent();
        g.MapGet("/{reference}", Detail);
        g.MapPost("/{reference}/findings", AddFinding).RequirePermission(P.ComplaintHandle);
        g.MapPut("/{reference}/draft", SaveDraft).RequirePermission(P.ComplaintHandle);
        g.MapPost("/{reference}/decision", Decide).RequirePermission(P.ComplaintHandle).Idempotent();
        g.MapPost("/{reference}/escalate", Escalate).RequirePermission(P.ComplaintHandle).Idempotent();
    }

    private static string StatusLabel(ComplaintStatus s) => s switch
    {
        ComplaintStatus.Received => "مستلمة", ComplaintStatus.InReview => "قيد المراجعة", ComplaintStatus.AwaitingOwner => "بانتظار المالك",
        ComplaintStatus.Resolved or ComplaintStatus.Closed => "مغلقة", ComplaintStatus.Escalated => "مصعّدة", _ => s.ToString(),
    };

    /// <summary>Reviewers see full text; other roles see tags only (reference, case, status, due).</summary>
    private static async Task<IResult> List(string? status, RahoonDbContext db, RequestContext rc, IClock clock)
    {
        var today = clock.TodayRiyadh;
        var q = db.Complaints.AsNoTracking().Where(c => c.OrganizationId == rc.OrganizationId);
        q = status switch
        {
            "closed" => q.Where(c => c.Status == ComplaintStatus.Resolved || c.Status == ComplaintStatus.Closed),
            "mine" => q.Where(c => c.ReviewerUserId == rc.UserId && c.Status != ComplaintStatus.Resolved && c.Status != ComplaintStatus.Closed),
            _ => q.Where(c => c.Status != ComplaintStatus.Resolved && c.Status != ComplaintStatus.Closed),
        };
        var fullText = rc.Has(P.ComplaintHandle);
        var rows = await q.OrderBy(c => c.DueOn).Select(c => new
        {
            c.Reference, c.Type, c.Subject, c.Status, c.DueOn, c.SubmittedAt, c.SubmittedVia,
            CaseRef = db.Cases.Where(x => x.Id == c.CaseId).Select(x => x.Reference).First(),
            Reviewer = db.Users.Where(u => u.Id == c.ReviewerUserId).Select(u => u.FullName).FirstOrDefault(),
        }).ToListAsync();
        return Results.Ok(rows.Select(r => new
        {
            r.Reference, caseRef = r.CaseRef, type = r.Type == ComplaintType.Objection ? "اعتراض" : "شكوى", subject = fullText ? r.Subject : null,
            status = r.Status.ToString(), statusLabel = StatusLabel(r.Status), r.DueOn, reviewer = r.Reviewer,
            slaText = r.DueOn < today ? CaseDisplay.LateDays(today.DayNumber - r.DueOn.DayNumber) : r.DueOn == today ? "اليوم" : CaseDisplay.Days(r.DueOn.DayNumber - today.DayNumber),
            slaTone = r.DueOn < today ? "err" : r.DueOn.DayNumber - today.DayNumber <= 2 ? "warn" : "ok",
        }));
    }

    private static async Task<IResult> Register(LenderComplaintRequest req, CaseAccess access, ComplaintService service, RequestContext rc)
    {
        new Validator().Require(req.Type is "complaint" or "objection", "type", "اختر النوع.")
            .Require(!string.IsNullOrWhiteSpace(req.Body), "body", "نص الشكوى مطلوب.")
            .Require(req.Via is "phone" or "email" or "branch", "via", "اختر قناة الاستلام.").ThrowIfInvalid();
        var c = await access.GetAsync(req.CaseReference);
        var complaint = await service.SubmitAsync(c, req.Type == "objection" ? ComplaintType.Objection : ComplaintType.Complaint, req.Subject, req.Body.Trim(), req.Via, null,
            $"المالك (سُجلت بواسطة {rc.UserName})");
        return Results.Ok(new { complaint.Reference });
    }

    private static async Task<Complaint> LoadAsync(RahoonDbContext db, RequestContext rc, string reference, bool track = false)
    {
        var q = db.Complaints.Where(c => c.Reference == reference && c.OrganizationId == rc.OrganizationId);
        if (!track) q = q.AsNoTracking();
        return await q.FirstOrDefaultAsync() ?? throw new NotFoundException();
    }

    private static async Task<IResult> Detail(string reference, RahoonDbContext db, RequestContext rc, IClock clock)
    {
        var c = await LoadAsync(db, rc, reference);
        var fullText = rc.Has(P.ComplaintHandle);
        var kase = await db.Cases.AsNoTracking().FirstAsync(x => x.Id == c.CaseId);
        var reviewer = await db.Users.Where(u => u.Id == c.ReviewerUserId).Select(u => u.FullName).FirstOrDefaultAsync();
        var events = await db.AuditEvents.AsNoTracking().Where(e => e.CaseId == c.CaseId && (e.Title.Contains(c.Reference))).OrderBy(e => e.Seq)
            .Select(e => new { e.Type, e.OccurredAt }).ToListAsync();
        var others = await db.Complaints.AsNoTracking().Where(x => x.CaseId == c.CaseId && x.Id != c.Id && x.Status != ComplaintStatus.Resolved && x.Status != ComplaintStatus.Closed)
            .Select(x => new { x.Reference, x.Type, x.DueOn }).ToListAsync();
        var today = clock.TodayRiyadh;
        var open = c.Status is not (ComplaintStatus.Resolved or ComplaintStatus.Closed);
        return Results.Ok(new
        {
            c.Reference, caseRef = kase.Reference, via = ComplaintService.ViaLabel(c.SubmittedVia), status = c.Status.ToString(), statusLabel = StatusLabel(c.Status),
            subject = fullText ? c.Subject : null, body = fullText ? c.Body : null, submittedBy = c.SubmittedByLabel, c.SubmittedAt, c.DueOn,
            dueText = c.DueOn < today ? CaseDisplay.LateDays(today.DayNumber - c.DueOn.DayNumber) : $"الرد خلال {(c.DueOn == today ? "اليوم" : CaseDisplay.Days(c.DueOn.DayNumber - today.DayNumber))} · {c.DueOn:yyyy-MM-dd}",
            reviewer, reviewerIsMe = c.ReviewerUserId == rc.UserId, findings = fullText ? c.Findings : [],
            decision = c.Decision?.ToString(), response = fullText ? c.ResponseText ?? c.ResponseDraft : null, respondedAt = c.RespondedAt,
            impact = open ? new[] { "تحجب الإحالة القضائية والإلغاء.", "تُوقف احتساب مهلة رد المالك.", "تظهر لفريق الحالة كوسم دون نص الشكوى." } : [],
            path = new[]
            {
                new { label = "الاستلام وإشعار المالك", done = true, current = false, date = (DateTimeOffset?)c.SubmittedAt },
                new { label = "الإسناد لمراجِع مستقل", done = c.ReviewerUserId != null, current = false, date = c.ReviewerUserId != null ? (DateTimeOffset?)c.SubmittedAt : null },
                new { label = "المراجعة والقرار", done = !open, current = open && c.ReviewerUserId != null, date = c.RespondedAt },
                new { label = "الرد المكتوب للمالك", done = c.RespondedAt != null, current = false, date = c.RespondedAt },
                new { label = "إغلاق أو تصعيد", done = !open, current = false, date = c.RespondedAt },
            },
            otherOpen = others.Select(o => new { o.Reference, type = o.Type == ComplaintType.Objection ? "اعتراض" : "شكوى", o.DueOn }),
            canDecide = open && c.ReviewerUserId == rc.UserId,
            version = c.Version,
        });
    }

    private static async Task<IResult> AddFinding(string reference, FindingRequest req, RahoonDbContext db, RequestContext rc)
    {
        if (req.Severity is not ("ok" or "issue" or "info") || string.IsNullOrWhiteSpace(req.Text)) Validate.Throw("text", "اكتب الملاحظة واختر نوعها.");
        var c = await LoadAsync(db, rc, reference, track: true);
        if (c.ReviewerUserId != rc.UserId) throw new ForbiddenException("المراجعة مسندة لمراجِع آخر.");
        c.Findings = [.. c.Findings, $"{req.Severity}|{req.Text.Trim()}"];
        await db.SaveChangesAsync();
        return Results.Ok(new { findings = c.Findings });
    }

    private static async Task<IResult> SaveDraft(string reference, ComplaintDraftRequest req, RahoonDbContext db, RequestContext rc)
    {
        var c = await LoadAsync(db, rc, reference, track: true);
        if (c.ReviewerUserId != rc.UserId) throw new ForbiddenException("المراجعة مسندة لمراجِع آخر.");
        c.ResponseDraft = req.Response?.Trim();
        await db.SaveChangesAsync();
        return Results.Ok(new { saved = true });
    }

    private static async Task<IResult> Decide(string reference, ComplaintDecisionRequest req, RahoonDbContext db, RequestContext rc, ComplaintService service)
    {
        await using var tx = await db.Database.BeginTransactionAsync();
        var c = await LoadAsync(db, rc, reference, track: true);
        var kase = await db.Cases.FirstAsync(x => x.Id == c.CaseId);
        await service.DecideAsync(c, kase, req);
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Results.Ok(new { status = c.Status.ToString() });
    }

    private static async Task<IResult> Escalate(string reference, RahoonDbContext db, RequestContext rc, Audit.AuditLog audit)
    {
        var c = await LoadAsync(db, rc, reference, track: true);
        if (c.ReviewerUserId != rc.UserId) throw new ForbiddenException("المراجعة مسندة لمراجِع آخر.");
        c.Status = ComplaintStatus.Escalated;
        await audit.RecordAsync(new Audit.AuditEntry("complaint.escalated", $"تصعيد {c.Reference} لإدارة المنصة", c.CaseId, null, OrganizationId: c.OrganizationId));
        await db.SaveChangesAsync();
        return Results.Ok(new { status = c.Status.ToString() });
    }
}
