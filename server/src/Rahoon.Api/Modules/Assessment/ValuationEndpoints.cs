using Microsoft.EntityFrameworkCore;
using Rahoon.Api.Infrastructure.Http;
using Rahoon.Api.Infrastructure.Persistence;
using Rahoon.Api.Infrastructure.Tenancy;
using Rahoon.Api.Infrastructure.Time;
using Rahoon.Api.Modules.Audit;
using Rahoon.Api.Modules.Cases;
using Rahoon.Api.Modules.Communications;
using Rahoon.Api.Modules.Documents;
using Rahoon.Api.Modules.Identity;
using Rahoon.Api.Modules.Providers;
using TaskStatus = Rahoon.Api.Modules.Communications.TaskStatus;

namespace Rahoon.Api.Modules.Assessment;

public sealed record ValuationReviewRequest(string? Decision, string? Note);
public sealed record RevaluationRequest(string? Reason);

/// <summary>
/// L11 valuation: current accepted report, reports under review, validity, assignment timeline and
/// provider access expiry. Revaluation is refused (409) unless the report expires within 14 days or a
/// documented reason is given; the valuer assignment itself is created by the providers module
/// (<c>POST /api/cases/{ref}/assignments</c>).
/// </summary>
public static class ValuationEndpoints
{
    public const int RevaluationWindowDays = 14;

    public static void Map(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/cases/{reference}").RequirePermission(P.CaseView);
        g.MapGet("/valuation", Get);
        g.MapPost("/valuation/{reportId:guid}/review", Review).RequirePermission(P.ValuationReview).Idempotent();
        g.MapPost("/valuation/revaluation-requests", RequestRevaluation).RequirePermission(P.ValuationAssign).Idempotent();
    }

    private static string StatusLabel(ValuationStatus s) => s switch
    {
        ValuationStatus.Accepted => "معتمد",
        ValuationStatus.UnderReview => "قيد المراجعة",
        ValuationStatus.Returned => "أُعيد للمقيّم",
        _ => "حل محله تقرير أحدث",
    };

    private static string Millions(decimal v) => v >= 1_000_000 ? $"{v / 1_000_000m:0.##}M" : $"{v / 1000m:0.#}K";

    private static async Task<IResult> Get(string reference, CaseAccess access, RahoonDbContext db, RequestContext rc, IClock clock)
    {
        var c = await access.GetAsync(reference, track: false);
        var today = clock.TodayRiyadh;
        var reports = await db.ValuationReports.AsNoTracking().Where(r => r.CaseId == c.Id)
            .OrderByDescending(r => r.ReportDate).ThenByDescending(r => r.VersionNo).ToListAsync();
        var current = reports.FirstOrDefault(r => r.Status == ValuationStatus.Accepted);
        var userIds = reports.Where(r => r.ReviewedByUserId != null).Select(r => r.ReviewedByUserId!.Value).Distinct().ToList();
        var users = await db.Users.Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName);
        var reportDoc = await db.Documents.AsNoTracking().Where(d => d.CaseId == c.Id && d.DocumentTypeKey == "valuation_report")
            .OrderByDescending(d => d.CreatedAt).Select(d => new { d.Id, d.CurrentVersionId }).FirstOrDefaultAsync();
        var assignments = await db.Assignments.AsNoTracking().Where(a => a.CaseId == c.Id && a.Type == AssignmentType.Valuation)
            .OrderByDescending(a => a.CreatedAt).ToListAsync();

        object Report(ValuationReport r)
        {
            var days = r.ValidUntil.DayNumber - today.DayNumber;
            var validity = days < 0 ? $"منتهٍ منذ {CaseDisplay.Days(-days)}" : $"صالح · {CaseDisplay.Days(days)}";
            return new
            {
                r.Id, r.VersionNo, title = $"ملخص تقرير التقييم v{r.VersionNo}", valuer = r.ValuerName, r.MarketValue,
                range = r.RangeLow is null || r.RangeHigh is null ? null : new { low = r.RangeLow, high = r.RangeHigh, label = $"{Millions(r.RangeLow.Value)} – {Millions(r.RangeHigh.Value)}" },
                r.Methodology, r.ComparablesCount, r.InspectionDate, r.ReportDate, r.ValidUntil, validityDays = r.ValidUntil.DayNumber - r.ReportDate.DayNumber,
                daysLeft = days, validityLabel = validity, validityTone = days < 0 ? "err" : days <= RevaluationWindowDays ? "warn" : "ok",
                status = r.Status.ToString(), statusLabel = StatusLabel(r.Status),
                reviewedBy = r.ReviewedByUserId is { } u ? users.GetValueOrDefault(u) : null, r.ReviewedAt, r.ReviewNote,
                documentVersionId = r.DocumentVersionId ?? (r.Status == ValuationStatus.Accepted ? reportDoc?.CurrentVersionId : null),
                assignment = assignments.FirstOrDefault(a => a.Id == r.AssignmentId)?.Reference,
                canReview = rc.Has(P.ValuationReview) && r.Status == ValuationStatus.UnderReview,
            };
        }

        decimal? ltv = current is null || c.OutstandingAmount is null ? null : Math.Round(c.OutstandingAmount.Value / current.MarketValue * 100, 1);
        var eligibleFrom = current?.ValidUntil.AddDays(-RevaluationWindowDays);
        var eligible = current is null || today >= eligibleFrom;
        var openRevaluation = await db.Tasks.AsNoTracking().AnyAsync(t => t.CaseId == c.Id && t.Kind == "revaluation" && t.Status == TaskStatus.Open);

        var asg = assignments.FirstOrDefault(a => a.Id == current?.AssignmentId) ?? assignments.FirstOrDefault();
        object? assignment = null;
        if (asg is not null)
        {
            var provider = await db.Organizations.AsNoTracking().Where(o => o.Id == asg.ProviderOrganizationId).Select(o => o.NameAr).FirstOrDefaultAsync();
            assignment = new
            {
                asg.Reference, provider, asg.Title, status = asg.Status.ToString(), asg.CreatedAt, asg.DueOn, asg.DeliveredAt,
                providerAccess = new
                {
                    expiresAt = asg.AccessExpiresAt,
                    expired = asg.AccessExpiresAt is { } ex && ex < clock.UtcNow,
                    label = asg.AccessExpiresAt is not { } ax ? "الوصول قائم حتى التسليم + 7 أيام"
                        : ax < clock.UtcNow ? $"انتهى وصول المقيّم {ax.ToOffset(TimeSpan.FromHours(3)):yyyy-MM-dd} · آلياً بعد 7 أيام من التسليم"
                        : $"وصول قراءة للمقيّم حتى {ax.ToOffset(TimeSpan.FromHours(3)):yyyy-MM-dd}",
                },
                timeline = await TimelineAsync(db, c, asg, clock),
            };
        }

        return Results.Ok(new
        {
            current = current is null ? null : Report(current),
            underReview = reports.Where(r => r.Status == ValuationStatus.UnderReview).Select(Report),
            history = reports.Where(r => r.Status is ValuationStatus.Returned or ValuationStatus.Superseded).Select(Report),
            ltv = new { value = ltv, label = "التمويل إلى القيمة", caption = ltv is null ? "يتطلب تقييماً معتمداً" : "محسوب", outstanding = c.OutstandingAmount },
            revaluation = new
            {
                canRequest = rc.Has(P.ValuationAssign) && !CaseStatusInfo.IsTerminal(c.Status) && !openRevaluation,
                eligible, eligibleFrom, windowDays = RevaluationWindowDays, requiresReason = !eligible, pending = openRevaluation,
                disabledReason = openRevaluation ? "يوجد طلب إعادة تقييم مفتوح."
                    : eligible ? null : "معطّل: التقرير الحالي صالح. يُتاح قبل 14 يوماً من الانتهاء أو بسبب موثق.",
            },
            assignment,
        });
    }

    /// <summary>
    /// Assignment timeline from the case audit trail. Convention (shared with the providers module): events of
    /// types «assignment.*» or «valuation.*», or any event whose Evidence contains «assignment:{reference}».
    /// Creation and provider-access expiry are derived from the assignment when no such event exists.
    /// </summary>
    private static async Task<List<object>> TimelineAsync(RahoonDbContext db, Case c, ProviderAssignment asg, IClock clock)
    {
        var tag = $"assignment:{asg.Reference}";
        var events = await db.AuditEvents.AsNoTracking()
            .Where(e => e.CaseId == c.Id && (e.Type.StartsWith("assignment.") || e.Type.StartsWith("valuation.") || e.Evidence.Contains(tag)))
            .OrderBy(e => e.OccurredAt).ThenBy(e => e.Seq)
            .Select(e => new { e.Type, e.Title, e.ActorLabel, e.Detail, e.OccurredAt, e.Blocked }).ToListAsync();

        static string Local(DateTimeOffset t, bool withTime = false) => t.ToOffset(TimeSpan.FromHours(3)).ToString(withTime ? "yyyy-MM-dd HH:mm" : "yyyy-MM-dd");
        var items = new List<(DateTimeOffset At, object Item)>();
        foreach (var e in events)
        {
            var (icon, tone) = e.Type switch
            {
                _ when e.Blocked => ("block", "err"),
                "assignment.created" => ("assignment", "ink"),
                "assignment.message" or "assignment.question" => ("forum", "info"),
                "assignment.inspection" => ("event", "ink"),
                "valuation.received" or "assignment.submitted" or "assignment.delivered" => ("upload_file", "ok"),
                "assignment.access_expired" => ("lock_clock", "muted"),
                "valuation.accepted" => ("check_circle", "ok"),
                "valuation.returned" => ("undo", "warn"),
                "valuation.revaluation_requested" => ("autorenew", "info"),
                _ => ("history", "muted"),
            };
            var withTime = e.Type == "assignment.inspection";
            items.Add((e.OccurredAt, new
            {
                type = e.Type, icon, tone, title = e.Title,
                meta = string.Join(" · ", new[] { e.ActorLabel is null or "النظام" ? null : e.ActorLabel, Local(e.OccurredAt, withTime), e.Detail }.Where(s => !string.IsNullOrWhiteSpace(s))),
                at = e.OccurredAt, derived = false,
            }));
        }
        if (!events.Any(e => e.Type == "assignment.created"))
        {
            var creator = await db.Users.Where(u => u.Id == asg.CreatedByUserId).Select(u => u.FullName).FirstOrDefaultAsync();
            var dueDays = asg.DueOn.DayNumber - DateOnly.FromDateTime(asg.CreatedAt.ToOffset(TimeSpan.FromHours(3)).DateTime).DayNumber;
            items.Add((asg.CreatedAt, new
            {
                type = "assignment.created", icon = "assignment", tone = "ink", title = "إنشاء التكليف",
                meta = string.Join(" · ", new[] { creator, Local(asg.CreatedAt), dueDays > 0 ? $"مهلة {CaseDisplay.Days(dueDays)}" : null }.Where(s => s is not null)),
                at = asg.CreatedAt, derived = true,
            }));
        }
        if (asg.AccessExpiresAt is { } exp && !events.Any(e => e.Type == "assignment.access_expired"))
        {
            var past = exp < clock.UtcNow;
            items.Add((exp, new
            {
                type = "assignment.access_expired", icon = "lock_clock", tone = "muted", title = past ? "انتهاء وصول المقيّم" : "ينتهي وصول المقيّم",
                meta = $"{Local(exp)} · آلي (7 أيام بعد التسليم)", at = exp, derived = true,
            }));
        }
        return items.OrderBy(i => i.At).Select(i => i.Item).ToList();
    }

    private static async Task<IResult> Review(string reference, Guid reportId, ValuationReviewRequest req, CaseAccess access, RahoonDbContext db,
        RequestContext rc, IClock clock, AuditLog audit, Notifier notifier)
    {
        var decision = req.Decision?.ToLowerInvariant();
        new Validator()
            .Require(decision is "accept" or "return", "decision", "اختر: اعتماد التقرير أو إعادته للمقيّم.")
            .Require(decision != "return" || (req.Note?.Trim().Length ?? 0) >= 10, "note", "سبب الإعادة إلزامي (10 أحرف على الأقل).")
            .Require(req.Note is null || req.Note.Length <= 2000, "note", "الملاحظة حتى 2000 حرف.")
            .ThrowIfInvalid();

        await using var tx = await db.Database.BeginTransactionAsync();
        var c = await access.GetAsync(reference, track: false);
        CaseStaffing.EnsureNotTerminal(c);
        // Serialize reviews per case so at most one report is ever the accepted current one.
        var lockKey = $"valuation:{c.Id}";
        await db.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock(hashtext({lockKey}))");
        var r = await db.ValuationReports.FirstOrDefaultAsync(x => x.Id == reportId && x.CaseId == c.Id) ?? throw new NotFoundException();
        if (r.Status != ValuationStatus.UnderReview) throw new ConflictException("already_reviewed", "رُوجع هذا التقرير مسبقاً.");
        var today = clock.TodayRiyadh;
        if (decision == "accept" && r.ValidUntil < today) throw new DomainException("valuation_expired", "التقرير منتهي الصلاحية ولا يُعتمد؛ اطلب إعادة تقييم.");

        var superseded = 0;
        if (decision == "accept")
        {
            foreach (var old in await db.ValuationReports.Where(x => x.CaseId == c.Id && x.Status == ValuationStatus.Accepted && x.Id != r.Id).ToListAsync())
            {
                old.Status = ValuationStatus.Superseded;
                superseded++;
            }
            r.Status = ValuationStatus.Accepted;
        }
        else r.Status = ValuationStatus.Returned;
        r.ReviewedByUserId = rc.UserId;
        r.ReviewedAt = clock.UtcNow;
        r.ReviewNote = string.IsNullOrWhiteSpace(req.Note) ? null : req.Note.Trim();

        var asgRef = r.AssignmentId is { } aid ? await db.Assignments.Where(a => a.Id == aid).Select(a => a.Reference).FirstOrDefaultAsync() : null;
        if (decision == "return" && r.AssignmentId is { } aid2
            && await db.Assignments.Where(a => a.Id == aid2).Select(a => a.CreatedByUserId).FirstOrDefaultAsync() is var creator && creator != Guid.Empty && creator != rc.UserId)
            notifier.Notify(creator, c.OrganizationId, "valuation", $"أُعيد تقرير التقييم v{r.VersionNo} للمقيّم", r.ReviewNote, $"/cases/{c.Reference}/valuation", c.Id, "warn");
        await audit.RecordAsync(new AuditEntry(decision == "accept" ? "valuation.accepted" : "valuation.returned",
            decision == "accept" ? $"اعتماد تقرير التقييم v{r.VersionNo}" : $"إعادة تقرير التقييم v{r.VersionNo} للمقيّم",
            c.Id, c.Reference, Reason: r.ReviewNote,
            Detail: $"القيمة السوقية {r.MarketValue:N2} ر.س · صالح حتى {r.ValidUntil:yyyy-MM-dd}" + (superseded > 0 ? " · حلّ محل التقرير المعتمد السابق" : ""),
            Evidence: asgRef is null ? [$"valuation:v{r.VersionNo}"] : [$"valuation:v{r.VersionNo}", $"assignment:{asgRef}"], OrganizationId: c.OrganizationId));
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Results.Ok(new { status = r.Status.ToString(), superseded });
    }

    private static async Task<IResult> RequestRevaluation(string reference, RevaluationRequest req, CaseAccess access, RahoonDbContext db,
        RequestContext rc, IClock clock, AuditLog audit, Notifier notifier)
    {
        var reason = string.IsNullOrWhiteSpace(req.Reason) ? null : req.Reason.Trim();
        new Validator().Require(reason is null || reason.Length is >= 10 and <= 1000, "reason", "السبب الموثق من 10 إلى 1000 حرف.").ThrowIfInvalid();
        var c = await access.GetAsync(reference, track: false);
        CaseStaffing.EnsureNotTerminal(c);
        var today = clock.TodayRiyadh;
        var current = await db.ValuationReports.AsNoTracking().Where(r => r.CaseId == c.Id && r.Status == ValuationStatus.Accepted)
            .OrderByDescending(r => r.ReportDate).FirstOrDefaultAsync();
        var eligibleFrom = current?.ValidUntil.AddDays(-RevaluationWindowDays);
        var eligible = current is null || today >= eligibleFrom;
        if (!eligible && reason is null)
        {
            // Refusals are audited in their own transaction, before anything is appended in ours.
            await audit.RecordBlockedAsync(new AuditEntry("valuation.revaluation_blocked", "محاولة طلب إعادة تقييم محجوبة", c.Id, c.Reference,
                Detail: $"المانع: التقرير v{current!.VersionNo} صالح حتى {current.ValidUntil:yyyy-MM-dd} ولا يوجد سبب موثق.", OrganizationId: c.OrganizationId));
            throw new ConflictException("revaluation_not_eligible",
                $"التقرير الحالي صالح حتى {current.ValidUntil:yyyy-MM-dd}. يُتاح طلب إعادة التقييم من {eligibleFrom:yyyy-MM-dd} (قبل 14 يوماً من الانتهاء) أو بسبب موثق.");
        }

        await using var tx = await db.Database.BeginTransactionAsync();
        if (await db.Tasks.AnyAsync(t => t.CaseId == c.Id && t.Kind == "revaluation" && t.Status == TaskStatus.Open))
            throw new ConflictException("revaluation_pending", "يوجد طلب إعادة تقييم مفتوح لهذه الحالة.");
        // The analyst who prepared the case analysis gets the task; otherwise the least-loaded credit analyst.
        var preparer = await db.Analyses.Where(a => a.CaseId == c.Id).Select(a => a.PreparedByUserId).FirstOrDefaultAsync();
        var analyst = (preparer is { } prep ? await CaseStaffing.MemberWithAsync(db, c.OrganizationId, prep, P.AnalysisEdit) : null)
                      ?? await CaseStaffing.PickAsync(db, c.OrganizationId, P.AnalysisEdit, [], SystemRoles.CreditAnalyst)
                      ?? throw new ConflictException("no_analyst", "لا يوجد محلل نشط لاستلام طلب إعادة التقييم.");

        var task = new CaseTask
        {
            OrganizationId = c.OrganizationId, CaseId = c.Id, Title = $"تكليف مقيّم لإعادة التقييم · {c.Reference}", AssigneeUserId = analyst.UserId,
            DueOn = BusinessDays.Add(today, 2), Kind = "revaluation", Link = $"/cases/{c.Reference}/valuation", CreatedByUserId = rc.UserId,
        };
        db.Tasks.Add(task);
        if (analyst.UserId != rc.UserId)
            notifier.Notify(analyst.UserId, c.OrganizationId, "task", $"طلب إعادة تقييم · {c.Reference}", reason ?? "التقرير الحالي يقترب من نهاية صلاحيته.", task.Link, c.Id);
        await audit.RecordAsync(new AuditEntry("valuation.revaluation_requested", "طلب إعادة تقييم", c.Id, c.Reference, Reason: reason,
            Detail: (current is null ? "لا يوجد تقرير معتمد" : eligible ? $"التقرير v{current.VersionNo} ينتهي {current.ValidUntil:yyyy-MM-dd} (ضمن 14 يوماً)" : $"بسبب موثق قبل موعد الأهلية {eligibleFrom:yyyy-MM-dd}")
                    + $" · أُسند إلى {analyst.Name}",
            Evidence: current is null ? null : [$"valuation:v{current.VersionNo}"], OrganizationId: c.OrganizationId));
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Results.Ok(new { taskId = task.Id, assignee = analyst.Name, eligibleByDate = eligible, assignmentRoute = $"/api/cases/{c.Reference}/assignments" });
    }
}
