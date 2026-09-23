using Microsoft.EntityFrameworkCore;
using Rahoon.Api.Infrastructure.Http;
using Rahoon.Api.Infrastructure.Persistence;
using Rahoon.Api.Infrastructure.Tenancy;
using Rahoon.Api.Infrastructure.Time;
using Rahoon.Api.Modules.Audit;
using Rahoon.Api.Modules.Cases;
using Rahoon.Api.Modules.Identity;

namespace Rahoon.Api.Modules.Communications;

public sealed record CreateTaskRequest(string? CaseReference, string Title, Guid? AssigneeMembershipId, DateOnly? DueOn);
public sealed record ReassignTaskRequest(Guid AssigneeMembershipId);
public sealed record CaseMessageRequest(string Body, bool Internal, string? Channel);
public sealed record AppointmentRequest(string Type, DateTimeOffset StartsAt, string? Attendees);

/// <summary>Notifications (S08), tasks (S09), global search (S10) and case communications (L22).</summary>
public static class WorkspaceEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/notifications", Notifications).RequireSession();
        app.MapPost("/api/notifications/{id:guid}/read", MarkRead).RequireSession();
        app.MapPost("/api/notifications/read-all", MarkAllRead).RequireSession();

        var t = app.MapGroup("/api/tasks").RequireOrg(OrganizationKind.Lender);
        t.MapGet("", Tasks);
        t.MapPost("", CreateTask).RequirePermission(P.TaskManage).Idempotent();
        t.MapPost("/{id:guid}/complete", CompleteTask).Idempotent();
        t.MapPost("/{id:guid}/reassign", ReassignTask).RequirePermission(P.TaskManage).Idempotent();

        app.MapGet("/api/search", Search).RequirePermission(P.CaseView);

        var c = app.MapGroup("/api/cases/{reference}/comms").RequirePermission(P.CaseView);
        c.MapGet("", CaseComms);
        c.MapPost("/messages", PostMessage).RequirePermission(P.CommsSend).Idempotent();
        c.MapPost("/appointments", ProposeAppointment).RequirePermission(P.CommsSend).Idempotent();
    }

    private static async Task<IResult> Notifications(string? tab, RahoonDbContext db, RequestContext rc)
    {
        var q = db.Notifications.AsNoTracking().Where(n => n.UserId == rc.UserId);
        if (tab == "unread") q = q.Where(n => n.ReadAt == null);
        else if (tab is "approval" or "document" or "payment" or "complaint" or "offer") q = q.Where(n => n.Category == tab);
        var items = await q.OrderByDescending(n => n.CreatedAt).Take(100)
            .Select(n => new { n.Id, n.Category, n.Title, n.Body, n.Link, n.Tone, n.CreatedAt, read = n.ReadAt != null }).ToListAsync();
        var unread = await db.Notifications.CountAsync(n => n.UserId == rc.UserId && n.ReadAt == null);
        return Results.Ok(new { items, unread });
    }

    private static async Task<IResult> MarkRead(Guid id, RahoonDbContext db, RequestContext rc, IClock clock)
    {
        var n = await db.Notifications.FirstOrDefaultAsync(x => x.Id == id && x.UserId == rc.UserId) ?? throw new NotFoundException();
        n.ReadAt ??= clock.UtcNow;
        await db.SaveChangesAsync();
        return Results.Ok(new { read = true });
    }

    private static async Task<IResult> MarkAllRead(RahoonDbContext db, RequestContext rc, IClock clock)
    {
        var count = await db.Notifications.Where(n => n.UserId == rc.UserId && n.ReadAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.ReadAt, clock.UtcNow));
        return Results.Ok(new { updated = count });
    }

    private static async Task<IResult> Tasks(string? scope, string? status, RahoonDbContext db, RequestContext rc, IClock clock)
    {
        var today = clock.TodayRiyadh;
        var q = db.Tasks.AsNoTracking().Where(x => x.OrganizationId == rc.OrganizationId);
        if (scope == "team")
        {
            var team = await db.Memberships.Where(m => m.Id == rc.MembershipId).Select(m => m.TeamId).FirstOrDefaultAsync();
            var teamUsers = await db.Memberships.Where(m => m.TeamId == team && team != null).Select(m => m.UserId).ToListAsync();
            q = q.Where(x => x.AssigneeUserId != null && teamUsers.Contains(x.AssigneeUserId.Value));
        }
        else q = q.Where(x => x.AssigneeUserId == rc.UserId);
        q = status == "done" ? q.Where(x => x.Status == TaskStatus.Done) : q.Where(x => x.Status == TaskStatus.Open);
        var rows = await q.OrderBy(x => x.DueOn == null).ThenBy(x => x.DueOn).Take(200).Select(x => new
        {
            x.Id, x.Title, x.DueOn, x.Kind, x.Link, x.Status, x.CompletedAt,
            CaseRef = db.Cases.Where(c => c.Id == x.CaseId).Select(c => c.Reference).FirstOrDefault(),
            Owner = db.Parties.Where(p => p.CaseId == x.CaseId && p.IsPrimary).Select(p => p.DisplayName).FirstOrDefault(),
            Assignee = db.Users.Where(u => u.Id == x.AssigneeUserId).Select(u => u.FullName).FirstOrDefault(),
        }).ToListAsync();
        return Results.Ok(new
        {
            items = rows.Select(r =>
            {
                var d = r.DueOn is { } due ? due.DayNumber - today.DayNumber : (int?)null;
                return new
                {
                    r.Id, r.Title, caseRef = r.CaseRef, owner = r.Owner, r.DueOn, r.Kind, link = r.Link, status = r.Status.ToString(), r.CompletedAt,
                    assignee = r.Assignee is null ? null : CaseDisplay.ShortName(r.Assignee),
                    slaTone = d is null ? "none" : d < 0 ? "err" : d <= 2 ? "warn" : "ok",
                    slaText = d is null ? "بلا مهلة" : d < 0 ? CaseDisplay.LateDays(-d.Value) : d == 0 ? "اليوم" : CaseDisplay.Days(d.Value),
                };
            }),
            counts = new
            {
                mine = await db.Tasks.CountAsync(x => x.AssigneeUserId == rc.UserId && x.Status == TaskStatus.Open),
                overdue = await db.Tasks.CountAsync(x => x.AssigneeUserId == rc.UserId && x.Status == TaskStatus.Open && x.DueOn < today),
            },
        });
    }

    private static async Task<IResult> CreateTask(CreateTaskRequest req, RahoonDbContext db, RequestContext rc, CaseAccess access, Notifier notifier)
    {
        new Validator().Require(!string.IsNullOrWhiteSpace(req.Title) && req.Title.Length <= 200, "title", "عنوان المهمة مطلوب.").ThrowIfInvalid();
        Case? c = req.CaseReference is { Length: > 0 } r ? await access.GetAsync(r, track: false) : null;
        Guid? assignee = rc.UserId;
        if (req.AssigneeMembershipId is { } mid)
            assignee = await db.Memberships.Where(m => m.Id == mid && m.OrganizationId == rc.OrganizationId && m.Status == MembershipStatus.Active).Select(m => (Guid?)m.UserId).FirstOrDefaultAsync()
                       ?? throw new NotFoundException();
        notifier.Task(rc.OrganizationId!.Value, c?.Id, req.Title.Trim(), assignee, req.DueOn, "manual", c is null ? null : $"/cases/{c.Reference}", rc.UserId);
        if (assignee != rc.UserId && assignee is { } a) notifier.Notify(a, rc.OrganizationId, "task", "مهمة جديدة مسندة إليك", req.Title.Trim(), "/tasks", c?.Id);
        await db.SaveChangesAsync();
        return Results.Ok(new { created = true });
    }

    private static async Task<IResult> CompleteTask(Guid id, RahoonDbContext db, RequestContext rc, IClock clock)
    {
        var task = await db.Tasks.FirstOrDefaultAsync(x => x.Id == id && x.OrganizationId == rc.OrganizationId) ?? throw new NotFoundException();
        if (task.AssigneeUserId != rc.UserId && !rc.Has(P.TaskManage)) throw new ForbiddenException();
        if (task.Kind is "approval" or "solution_review" or "complaint" or "agreement")
            throw new DomainException("task_auto", "تُغلق هذه المهمة تلقائياً عند تنفيذ الإجراء المرتبط بها.", 409);
        task.Status = TaskStatus.Done;
        task.CompletedAt = clock.UtcNow;
        await db.SaveChangesAsync();
        return Results.Ok(new { status = "Done" });
    }

    private static async Task<IResult> ReassignTask(Guid id, ReassignTaskRequest req, RahoonDbContext db, RequestContext rc, Notifier notifier)
    {
        var task = await db.Tasks.FirstOrDefaultAsync(x => x.Id == id && x.OrganizationId == rc.OrganizationId && x.Status == TaskStatus.Open) ?? throw new NotFoundException();
        var target = await db.Memberships.Where(m => m.Id == req.AssigneeMembershipId && m.OrganizationId == rc.OrganizationId && m.Status == MembershipStatus.Active)
            .Select(m => (Guid?)m.UserId).FirstOrDefaultAsync() ?? throw new NotFoundException();
        task.AssigneeUserId = target;
        notifier.Notify(target, rc.OrganizationId, "task", "مهمة أُسندت إليك", task.Title, "/tasks", task.CaseId);
        await db.SaveChangesAsync();
        return Results.Ok(new { reassigned = true });
    }

    /// <summary>Global search (S10): case reference, masked owner name, contract number, tasks. Tenant- and team-scoped.</summary>
    private static async Task<IResult> Search(string q, CaseAccess access, RahoonDbContext db, RequestContext rc)
    {
        var term = (q ?? "").Trim();
        if (term.Length < 2) return Results.Ok(new { cases = Array.Empty<object>(), tasks = Array.Empty<object>() });
        var visible = await access.VisibleAsync();
        var cases = await visible.Where(c => c.Reference.Contains(term)
                || db.Parties.Any(p => p.CaseId == c.Id && p.IsPrimary && (p.DisplayName.Contains(term) || p.FullName.Contains(term)))
                || db.FinancingContracts.Any(f => f.CaseId == c.Id && f.ContractNumber.Contains(term)))
            .OrderBy(c => c.Reference).Take(8)
            .Select(c => new { c.Reference, c.Status, c.City, Owner = db.Parties.Where(p => p.CaseId == c.Id && p.IsPrimary).Select(p => p.DisplayName).FirstOrDefault() })
            .ToListAsync();
        var tasks = await db.Tasks.Where(t => t.AssigneeUserId == rc.UserId && t.Status == TaskStatus.Open && t.Title.Contains(term)).Take(5)
            .Select(t => new { t.Id, t.Title, t.Link }).ToListAsync();
        return Results.Ok(new
        {
            cases = cases.Select(c => new { c.Reference, owner = c.Owner, c.City, status = CaseStatusInfo.Key(c.Status), statusLabel = CaseStatusInfo.Of(c.Status).LabelAr }),
            tasks,
        });
    }

    // ───────── Case communications (L22) ─────────

    private static async Task<IResult> CaseComms(string reference, CaseAccess access, RahoonDbContext db, RequestContext rc, IClock clock)
    {
        var c = await access.GetAsync(reference, track: false);
        var messages = await db.Messages.Where(m => m.CaseId == c.Id).OrderBy(m => m.At).ToListAsync();
        foreach (var m in messages.Where(m => m.AuthorType == "owner" && m.ReadByTeamAt == null)) m.ReadByTeamAt = clock.UtcNow;
        await db.SaveChangesAsync();
        var appointments = await db.Appointments.AsNoTracking().Where(a => a.CaseId == c.Id).OrderByDescending(a => a.StartsAt).ToListAsync();
        var tasks = await db.Tasks.AsNoTracking().Where(t => t.CaseId == c.Id).OrderBy(t => t.Status).ThenBy(t => t.DueOn)
            .Select(t => new { t.Id, t.Title, t.DueOn, status = t.Status.ToString(), Assignee = db.Users.Where(u => u.Id == t.AssigneeUserId).Select(u => u.FullName).FirstOrDefault() })
            .ToListAsync();
        var access2 = await db.OwnerAccesses.AsNoTracking().FirstOrDefaultAsync(o => o.CaseId == c.Id);
        var hardship = await db.HardshipRequests.AsNoTracking().Where(h => h.CaseId == c.Id).OrderByDescending(h => h.CreatedAt).FirstOrDefaultAsync();
        var complaintsOpen = await db.Complaints.CountAsync(x => x.CaseId == c.Id && x.Status != Complaints.ComplaintStatus.Resolved && x.Status != Complaints.ComplaintStatus.Closed);
        return Results.Ok(new
        {
            messages = messages.Select(m => new { m.Id, channel = m.Channel.ToString(), m.AuthorType, author = m.AuthorLabel, m.Body, m.At, m.InternalOnly, m.TemplateKey, readByOwner = m.ReadByOwnerAt != null }),
            appointments = appointments.Select(a => new { a.Id, a.Type, a.StartsAt, status = a.Status.ToString(), a.Attendees, a.ProposedBy }),
            tasks = tasks.Select(t => new { t.Id, t.Title, t.DueOn, t.status, assignee = t.Assignee }),
            preferences = new { contactHours = access2?.ContactHours, channels = access2?.AllowedChannels, invitation = access2?.InvitationStatus.ToString() },
            hardship = hardship is null ? null : new { hardship.ReasonKey, hardship.CreatedAt, hardship.Status },
            openComplaints = complaintsOpen,
            templates = await db.Templates.AsNoTracking().Where(t => t.Status == TemplateStatus.Published && (t.OrganizationId == null || t.OrganizationId == c.OrganizationId))
                .Select(t => new { t.Code, t.Title, t.BodyAr }).ToListAsync(),
        });
    }

    private static async Task<IResult> PostMessage(string reference, CaseMessageRequest req, CaseAccess access, RahoonDbContext db, RequestContext rc, IClock clock, Notifier notifier, AuditLog audit)
    {
        new Validator().Require(!string.IsNullOrWhiteSpace(req.Body) && req.Body.Length <= 4000, "body", "اكتب الرسالة (حتى 4000 حرف).").ThrowIfInvalid();
        var c = await access.GetAsync(reference, track: false);
        var channel = req.Internal ? MessageChannel.Internal : req.Channel switch { "sms" => MessageChannel.Sms, "call" => MessageChannel.Call, _ => MessageChannel.Portal };
        db.Messages.Add(new CaseMessage
        {
            OrganizationId = c.OrganizationId, CaseId = c.Id, Channel = channel, AuthorType = "lender", AuthorUserId = rc.UserId, AuthorLabel = rc.UserName,
            Body = req.Body.Trim(), InternalOnly = req.Internal, At = clock.UtcNow,
        });
        if (!req.Internal && channel == MessageChannel.Portal)
        {
            var owner = await db.OwnerAccesses.Where(o => o.CaseId == c.Id && o.UserId != null).Select(o => o.UserId).FirstOrDefaultAsync();
            if (owner is { } ou) notifier.Notify(ou, c.OrganizationId, "message", "رسالة جديدة من مسؤول حالتك", null, "/owner/messages", c.Id);
        }
        await audit.RecordAsync(new AuditEntry("message.sent", req.Internal ? "ملاحظة داخلية" : "رسالة للمالك", c.Id, c.Reference, OrganizationId: c.OrganizationId));
        await db.SaveChangesAsync();
        return Results.Ok(new { sent = true });
    }

    private static async Task<IResult> ProposeAppointment(string reference, AppointmentRequest req, CaseAccess access, RahoonDbContext db, RequestContext rc, IClock clock, Notifier notifier)
    {
        new Validator().Require(req.Type is "call" or "visit", "type", "اختر نوع الموعد.").Require(req.StartsAt > clock.UtcNow, "startsAt", "الموعد يجب أن يكون في المستقبل.").ThrowIfInvalid();
        var c = await access.GetAsync(reference, track: false);
        var a = new Appointment { OrganizationId = c.OrganizationId, CaseId = c.Id, Type = req.Type, StartsAt = req.StartsAt, Attendees = req.Attendees, ProposedBy = "lender" };
        db.Appointments.Add(a);
        var owner = await db.OwnerAccesses.Where(o => o.CaseId == c.Id && o.UserId != null).Select(o => o.UserId).FirstOrDefaultAsync();
        if (owner is { } ou) notifier.Notify(ou, c.OrganizationId, "appointment", req.Type == "call" ? "موعد مكالمة مقترح" : "موعد زيارة مقترح", $"{req.StartsAt.ToOffset(TimeSpan.FromHours(3)):yyyy-MM-dd HH:mm}", "/owner/messages", c.Id);
        await db.SaveChangesAsync();
        return Results.Ok(new { a.Id });
    }
}
