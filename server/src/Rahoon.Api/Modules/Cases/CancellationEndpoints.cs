using Microsoft.EntityFrameworkCore;
using Rahoon.Api.Infrastructure.Http;
using Rahoon.Api.Infrastructure.Persistence;
using Rahoon.Api.Infrastructure.Tenancy;
using Rahoon.Api.Infrastructure.Time;
using Rahoon.Api.Modules.Audit;
using Rahoon.Api.Modules.Communications;
using Rahoon.Api.Modules.Identity;
using Rahoon.Api.Modules.Solutions;
using TaskStatus = Rahoon.Api.Modules.Communications.TaskStatus;

namespace Rahoon.Api.Modules.Cases;

public sealed record CancellationRequestBody(string? Reason, string? ExpectedStatus);
public sealed record CancellationDecisionBody(string? Decision, string? Reason);

/// <summary>
/// C01 sensitive action «إلغاء الحالة…»: maker (case.cancel + step-up + reason) → checker
/// (case.cancel_approve + step-up, never the requester) → the workflow «cancel» transition.
/// Blocked while a complaint or objection is open (guard no_open_complaint).
/// </summary>
public static class CancellationEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/cases/{reference}/cancellation-requests").RequirePermission(P.CaseView);
        g.MapGet("", List);
        g.MapPost("", Request).RequirePermission(P.CaseCancel).Idempotent();
        g.MapPost("/{id:guid}/decision", Decide).RequirePermission(P.CaseCancelApprove).Idempotent();
    }

    /// <summary>Pending cancellation for the workspace «sensitive» block (null when none).</summary>
    public static async Task<object?> PendingSummaryAsync(RahoonDbContext db, RequestContext rc, Guid caseId)
    {
        var a = await db.ApprovalRequests.AsNoTracking()
            .Where(x => x.CaseId == caseId && x.Subject == ApprovalSubject.Cancellation && x.Status == ApprovalStatus.Pending).FirstOrDefaultAsync();
        if (a is null) return null;
        var names = await db.Users.Where(u => u.Id == a.SubmittedByUserId || u.Id == a.AssignedApproverUserId).ToDictionaryAsync(u => u.Id, u => u.FullName);
        var mine = a.AssignedApproverUserId == rc.UserId;
        var isRequester = a.SubmittedByUserId == rc.UserId || a.PreparedByUserId == rc.UserId;
        return new
        {
            a.Id, reason = a.SubmitterNote, requestedBy = names.GetValueOrDefault(a.SubmittedByUserId), requestedAt = a.SubmittedAt,
            approver = a.AssignedApproverUserId is { } ap ? names.GetValueOrDefault(ap) : null, a.DueOn,
            assignedToMe = mine, canDecide = mine && !isRequester && rc.Has(P.CaseCancelApprove),
            note = isRequester ? "طلبت الإلغاء؛ الاعتماد لمعتمد آخر (فصل المهام)." : null,
        };
    }

    private static async Task<IResult> List(string reference, CaseAccess access, RahoonDbContext db, RequestContext rc)
    {
        var c = await access.GetAsync(reference, track: false);
        var rows = await db.ApprovalRequests.AsNoTracking().Where(a => a.CaseId == c.Id && a.Subject == ApprovalSubject.Cancellation)
            .OrderByDescending(a => a.SubmittedAt).ToListAsync();
        var ids = rows.SelectMany(r => new[] { r.SubmittedByUserId, r.AssignedApproverUserId ?? Guid.Empty, r.DecidedByUserId ?? Guid.Empty }).Distinct().ToList();
        var names = await db.Users.Where(u => ids.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName);
        return Results.Ok(new
        {
            items = rows.Select(a => new
            {
                a.Id, status = a.Status.ToString(), reason = a.SubmitterNote, requestedBy = names.GetValueOrDefault(a.SubmittedByUserId), requestedAt = a.SubmittedAt,
                approver = a.AssignedApproverUserId is { } ap ? names.GetValueOrDefault(ap) : null, a.DueOn,
                decidedBy = a.DecidedByUserId is { } d ? names.GetValueOrDefault(d) : null, a.DecidedAt, a.DecisionReason,
            }),
            pending = await PendingSummaryAsync(db, rc, c.Id),
            canRequest = rc.Has(P.CaseCancel) && !CaseStatusInfo.IsTerminal(c.Status) && rows.All(r => r.Status != ApprovalStatus.Pending),
        });
    }

    private static async Task<IResult> Request(string reference, CancellationRequestBody req, CaseAccess access, RahoonDbContext db, RequestContext rc,
        IClock clock, CaseWorkflow workflow, AuditLog audit, Notifier notifier)
    {
        new Validator().Require(!string.IsNullOrWhiteSpace(req.Reason) && req.Reason.Trim().Length is >= 10 and <= 2000, "reason", "سبب الإلغاء إلزامي (10 أحرف على الأقل).").ThrowIfInvalid();
        EndpointAccess.EnsureStepUp(rc, clock);
        var reason = req.Reason!.Trim();

        await using var tx = await db.Database.BeginTransactionAsync();
        var c = await access.GetAsync(reference);
        if (req.ExpectedStatus is { } exp && CaseStatusInfo.Parse(exp) != c.Status)
            throw new ConflictException("stale_state", "تغيّرت حالة الحالة منذ فتحها. حدّث الصفحة لرؤية الوضع الحالي.");
        var def = CaseWorkflow.Def("cancel");
        if (!def.From.Contains(c.Status))
            throw new ConflictException("transition_not_allowed", $"لا يمكن إلغاء الحالة وهي «{CaseStatusInfo.Of(c.Status).LabelAr}».");
        if (await db.ApprovalRequests.AnyAsync(a => a.CaseId == c.Id && a.Subject == ApprovalSubject.Cancellation && a.Status == ApprovalStatus.Pending))
            throw new ConflictException("cancellation_pending", "يوجد طلب إلغاء بانتظار الاعتماد لهذه الحالة.");

        var failures = await workflow.EvaluateGuardsAsync(c, def);
        if (failures.Count > 0)
        {
            // Own transaction, before anything is appended to the chain in ours (audit ordering rule).
            await audit.RecordBlockedAsync(new AuditEntry("cancellation.blocked", "محاولة طلب إلغاء محجوبة", c.Id, c.Reference, Reason: reason,
                Detail: "المانع: " + string.Join(" · ", failures) + ". لم يُنشأ الطلب.", OrganizationId: c.OrganizationId));
            throw new DomainException("guard_failed", "لا يمكن طلب إلغاء الحالة الآن.", StatusCodes.Status422UnprocessableEntity, failures);
        }

        var approver = await CaseStaffing.PickAsync(db, c.OrganizationId, P.CaseCancelApprove, [rc.UserId],
                           SystemRoles.Approver, SystemRoles.SeniorApprover, SystemRoles.OrgAdmin)
                       ?? throw new ConflictException("no_approver", "لا يوجد معتمد مخوّل غير مقدم الطلب لاعتماد الإلغاء.");
        var today = clock.TodayRiyadh;
        var a = new ApprovalRequest
        {
            OrganizationId = c.OrganizationId, CaseId = c.Id, Subject = ApprovalSubject.Cancellation, SubjectId = c.Id, SubjectVersionNo = 0,
            Title = $"اعتماد إلغاء الحالة {c.Reference}", PreparedByUserId = rc.UserId, SubmittedByUserId = rc.UserId, SubmittedAt = clock.UtcNow,
            SubmitterNote = reason, SubmitterAttested = true, AssignedApproverUserId = approver.UserId, RequiredTier = P.CaseCancelApprove,
            DueOn = BusinessDays.Add(today, 2), Evidence = [$"status:{CaseStatusInfo.Key(c.Status)}"],
        };
        db.ApprovalRequests.Add(a);
        notifier.Notify(approver.UserId, c.OrganizationId, "approval", $"طلب إلغاء الحالة {c.Reference} بانتظار اعتمادك", reason, $"/cases/{c.Reference}?cancellation={a.Id}", c.Id, "warn");
        notifier.Task(c.OrganizationId, c.Id, $"اعتماد إلغاء الحالة {c.Reference}", approver.UserId, a.DueOn, "approval", $"/cases/{c.Reference}?cancellation={a.Id}", rc.UserId);
        await audit.RecordAsync(new AuditEntry("cancellation.requested", "طلب إلغاء الحالة", c.Id, c.Reference, Reason: reason,
            Detail: $"أُسند للاعتماد إلى {approver.Name} · المهلة {a.DueOn:yyyy-MM-dd} · تأكيد برمز التحقق", Evidence: [$"cancellation:{a.Id}"], OrganizationId: c.OrganizationId));
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Results.Ok(new { a.Id, status = a.Status.ToString(), approver = approver.Name, a.DueOn });
    }

    private static async Task<IResult> Decide(string reference, Guid id, CancellationDecisionBody req, CaseAccess access, RahoonDbContext db, RequestContext rc,
        IClock clock, CaseWorkflow workflow, AuditLog audit, Notifier notifier)
    {
        var decision = req.Decision?.ToLowerInvariant();
        new Validator()
            .Require(decision is "approve" or "reject", "decision", "اختر القرار: اعتماد أو رفض.")
            .Require(!string.IsNullOrWhiteSpace(req.Reason) && req.Reason.Trim().Length is >= 10 and <= 2000, "reason", "سبب القرار إلزامي (10 أحرف على الأقل).")
            .ThrowIfInvalid();
        EndpointAccess.EnsureStepUp(rc, clock);
        var reason = req.Reason!.Trim();

        await using var tx = await db.Database.BeginTransactionAsync();
        var c = await access.GetAsync(reference);
        var a = await db.ApprovalRequests.FirstOrDefaultAsync(x => x.Id == id && x.CaseId == c.Id && x.Subject == ApprovalSubject.Cancellation) ?? throw new NotFoundException();
        if (a.Status != ApprovalStatus.Pending) throw new ConflictException("already_decided", "تم القرار على هذا الطلب مسبقاً.");
        if (a.PreparedByUserId == rc.UserId || a.SubmittedByUserId == rc.UserId)
        {
            await audit.RecordBlockedAsync(new AuditEntry("cancellation.blocked", "محاولة اعتماد طلب إلغاء من مقدمه", c.Id, c.Reference, Reason: reason,
                Detail: "المانع: مقدم الطلب لا يعتمد طلبه (فصل المهام). لم يُنفذ القرار.", OrganizationId: c.OrganizationId));
            throw new ForbiddenException("لا يعتمد مقدم طلب الإلغاء طلبه؛ الاعتماد لمعتمد آخر (فصل المهام).");
        }
        if (a.AssignedApproverUserId is { } assigned && assigned != rc.UserId) throw new ForbiddenException("الطلب مسند لمعتمد آخر.");

        if (decision == "approve")
        {
            // The workflow re-checks permission (case.cancel_approve), step-up and the open-complaint guard; it audits refusals itself.
            await workflow.TransitionAsync(c, "cancel", a.SubmitterNote, evidence: [$"cancellation:{a.Id}"]);
            foreach (var t in await db.Tasks.Where(t => t.CaseId == c.Id && t.Status == TaskStatus.Open).ToListAsync())
            {
                t.Status = t.Kind == "approval" ? TaskStatus.Done : TaskStatus.Cancelled;
                t.CompletedAt = clock.UtcNow;
            }
        }
        else
        {
            foreach (var t in await db.Tasks.Where(t => t.CaseId == c.Id && t.Kind == "approval" && t.Status == TaskStatus.Open && t.AssigneeUserId == a.AssignedApproverUserId).ToListAsync())
            { t.Status = TaskStatus.Done; t.CompletedAt = clock.UtcNow; }
        }
        a.Status = decision == "approve" ? ApprovalStatus.Approved : ApprovalStatus.Rejected;
        a.DecidedByUserId = rc.UserId;
        a.DecidedAt = clock.UtcNow;
        a.DecisionReason = reason;
        a.StepUpVerified = true;
        notifier.Notify(a.SubmittedByUserId, c.OrganizationId, "approval", decision == "approve" ? $"اعتُمد إلغاء الحالة {c.Reference}" : $"رُفض طلب إلغاء الحالة {c.Reference}",
            reason, $"/cases/{c.Reference}", c.Id, decision == "approve" ? "ok" : "warn");
        await audit.RecordAsync(new AuditEntry("cancellation.decision", decision == "approve" ? "اعتماد إلغاء الحالة" : "رفض طلب إلغاء الحالة", c.Id, c.Reference,
            Reason: reason, Detail: "تأكيد برمز التحقق", Evidence: [$"cancellation:{a.Id}"], OrganizationId: c.OrganizationId));
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Results.Ok(new { status = a.Status.ToString(), caseStatus = CaseStatusInfo.Key(c.Status) });
    }
}
