using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Rahoon.Api.Infrastructure.Http;
using Rahoon.Api.Infrastructure.Persistence;
using Rahoon.Api.Infrastructure.Tenancy;
using Rahoon.Api.Infrastructure.Time;
using Rahoon.Api.Modules.Agreements;
using Rahoon.Api.Modules.Audit;
using Rahoon.Api.Modules.Communications;
using Rahoon.Api.Modules.Complaints;
using Rahoon.Api.Modules.Identity;
using Rahoon.Api.Modules.Solutions;
using TaskStatus = Rahoon.Api.Modules.Communications.TaskStatus;

namespace Rahoon.Api.Modules.Cases;

public sealed record CaseListQuery(string? View, string? Q, string? Status, string? Sla, string? Region, Guid? Manager,
    decimal? MinAmount, decimal? MaxAmount, string? Sort, int Page = 1, int PageSize = 10);

public sealed record SaveViewRequest(string Name, string FiltersJson);
public sealed record ReassignRequest(List<string> References, Guid ManagerMembershipId, string Reason);

public static class CaseListEndpoints
{
    public static readonly CaseStatus[] ActiveStatuses =
    [
        CaseStatus.AwaitingData, CaseStatus.Verification, CaseStatus.Valuation, CaseStatus.ProposedSolution, CaseStatus.InternalApproval,
        CaseStatus.AwaitingCustomer, CaseStatus.Negotiation, CaseStatus.ActiveSettlement, CaseStatus.VoluntarySale, CaseStatus.JudicialReferral,
        CaseStatus.ExternalJudicialSale, CaseStatus.AwaitingReconciliation, CaseStatus.Paused,
    ];

    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/portfolio", Portfolio).RequirePermission(P.PortfolioView);
        app.MapGet("/api/nav/counts", NavCounts).RequireOrg(OrganizationKind.Lender);
        app.MapGet("/api/cases", List).RequirePermission(P.CaseView);
        app.MapGet("/api/cases/views", Views).RequirePermission(P.CaseView);
        app.MapPost("/api/cases/views", SaveView).RequirePermission(P.CaseView);
        app.MapDelete("/api/cases/views/{id:guid}", DeleteView).RequirePermission(P.CaseView);
        app.MapPost("/api/cases/reassign", Reassign).RequirePermission(P.CaseAssign).Idempotent();
        app.MapGet("/api/cases/export", Export).RequirePermission(P.CaseExport);
        app.MapGet("/api/org/members", Members).RequireOrg(OrganizationKind.Lender);
    }

    // ───────── Portfolio (L01) ─────────

    private static async Task<IResult> Portfolio(string? region, string? scope, CaseAccess access, RahoonDbContext db, RequestContext rc, IClock clock)
    {
        var today = clock.TodayRiyadh;
        var visible = await access.VisibleAsync();
        if (!string.IsNullOrEmpty(region)) visible = visible.Where(c => c.Region == region);
        if (scope == "mine") visible = visible.Where(c => c.AssignedManagerId == rc.MembershipId);
        var active = visible.Where(c => ActiveStatuses.Contains(c.Status));

        var byStatus = await active.GroupBy(c => c.Status).Select(g => new { Status = g.Key, N = g.Count() }).ToListAsync();
        var activeCount = byStatus.Sum(x => x.N);
        var monthStart = new DateOnly(today.Year, today.Month, 1);
        var newThisMonth = await active.CountAsync(c => c.OpenedOn >= monthStart);
        var closed90 = await visible.CountAsync(c => c.Status == CaseStatus.Closed && c.ClosedAt >= clock.UtcNow.AddDays(-90));
        var overdueQ = active.Where(c => c.StageDueOn < today && c.Status != CaseStatus.Paused && c.SlaPausedAt == null);
        var overdueByStatus = await overdueQ.GroupBy(c => c.Status).Select(g => new { Status = g.Key, N = g.Count() }).ToListAsync();
        var overdue = overdueByStatus.Sum(x => x.N);
        var topOverdue = overdueByStatus.OrderByDescending(x => x.N).FirstOrDefault();
        var outstanding = await active.SumAsync(c => c.OutstandingAmount ?? 0);
        var asOf = await active.MaxAsync(c => (DateTimeOffset?)c.OutstandingAsOf);

        var myTasks = db.Tasks.Where(t => t.AssigneeUserId == rc.UserId && t.Status == TaskStatus.Open);
        var taskCount = await myTasks.CountAsync();
        var dueSoon = await myTasks.CountAsync(t => t.DueOn <= today.AddDays(2));
        var attention = await myTasks.OrderBy(t => t.DueOn).Take(5)
            .Join(db.Cases, t => t.CaseId, c => c.Id, (t, c) => new { t, c })
            .Select(x => new { x.t.Id, x.t.Title, x.t.DueOn, x.t.Kind, x.c.Reference, x.c.Status, x.c.StageDueOn,
                Owner = db.Parties.Where(p => p.CaseId == x.c.Id && p.IsPrimary).Select(p => p.DisplayName).FirstOrDefault() })
            .ToListAsync();

        var slaRules = await db.Set<Administration.SlaRule>().Where(r => r.OrganizationId == rc.OrganizationId).ToListAsync();
        var slaRows = new List<object>();
        foreach (var status in new[] { CaseStatus.Verification, CaseStatus.Valuation, CaseStatus.InternalApproval, CaseStatus.AwaitingCustomer, CaseStatus.AwaitingReconciliation })
        {
            var rule = slaRules.FirstOrDefault(r => r.Status == status.ToString());
            var changed = await active.Where(c => c.Status == status).Select(c => c.StatusChangedAt).ToListAsync();
            var days = changed.Select(t => (int)(clock.UtcNow - t).TotalDays).OrderBy(x => x).ToList();
            var median = days.Count == 0 ? 0 : days[days.Count / 2];
            slaRows.Add(new
            {
                status = CaseStatusInfo.Key(status), label = CaseStatusInfo.Of(status).LabelAr, cases = days.Count, medianDays = median,
                target = rule is null ? "—" : status is CaseStatus.Verification or CaseStatus.AwaitingReconciliation ? $"{rule.BusinessDays} أيام عمل" : $"{rule.BusinessDays} أيام",
                overdue = overdueByStatus.FirstOrDefault(o => o.Status == status)?.N ?? 0,
            });
        }

        var user = await db.Users.Where(u => u.Id == rc.UserId).Select(u => u.FullName).FirstAsync();
        return Results.Ok(new
        {
            greetingName = user.Split(' ')[0],
            sync = new { source = "نظام التمويل الأساسي", at = asOf },
            kpis = new
            {
                active = activeCount, newThisMonth, closed90,
                myTasks = taskCount, myTasksDueSoon = dueSoon,
                overdue, overduePercent = activeCount == 0 ? 0 : Math.Round(100m * overdue / activeCount, 1),
                overdueTop = topOverdue is null ? null : new { label = CaseStatusInfo.Of(topOverdue.Status).LabelAr, n = topOverdue.N },
                outstanding,
            },
            attention = attention.Select(a =>
            {
                var d = a.DueOn is { } due ? due.DayNumber - today.DayNumber : (int?)null;
                var tone = d is null ? "ok" : d < 0 ? "err" : d <= 2 ? "warn" : "ok";
                var text = d is null ? "—" : d < 0 ? CaseDisplay.LateDays(-d.Value) : d == 0 ? "اليوم" : CaseDisplay.Days(d.Value);
                return new { a.Id, task = a.Title, caseRef = a.Reference, owner = a.Owner, state = a.Kind == "document_request" ? "مستند" : CaseStatusInfo.Of(a.Status).LabelAr, slaTone = tone, slaText = text };
            }),
            distribution = ActiveStatuses.Select(s => new { status = CaseStatusInfo.Key(s), label = CaseStatusInfo.Of(s).LabelAr, n = byStatus.FirstOrDefault(b => b.Status == s)?.N ?? 0 }),
            slaByStage = slaRows,
            regions = await visible.Where(c => c.Region != null).Select(c => c.Region!).Distinct().OrderBy(r => r).ToListAsync(),
        });
    }

    private static async Task<IResult> NavCounts(RahoonDbContext db, RequestContext rc)
    {
        var tasks = await db.Tasks.CountAsync(t => t.AssigneeUserId == rc.UserId && t.Status == TaskStatus.Open);
        var approvals = rc.Has(P.SolutionApprove) ? await db.ApprovalRequests.CountAsync(a => a.AssignedApproverUserId == rc.UserId && a.Status == ApprovalStatus.Pending) : 0;
        var complaints = rc.Has(P.ComplaintView) ? await db.Complaints.CountAsync(c => c.Status != ComplaintStatus.Resolved && c.Status != ComplaintStatus.Closed) : 0;
        return Results.Ok(new { tasks, approvals, complaints });
    }

    // ───────── Case list (L02) ─────────

    private static async Task<IQueryable<Case>> ApplyView(IQueryable<Case> q, string? view, RahoonDbContext db, RequestContext rc, DateOnly today)
    {
        return view switch
        {
            "mine" => q.Where(c => c.AssignedManagerId == rc.MembershipId && c.Status != CaseStatus.Closed && c.Status != CaseStatus.Cancelled),
            "action" => q.Where(c => db.Tasks.Any(t => t.CaseId == c.Id && t.AssigneeUserId == rc.UserId && t.Status == TaskStatus.Open)),
            "overdue" => q.Where(c => ActiveStatuses.Contains(c.Status) && c.Status != CaseStatus.Paused && c.StageDueOn < today),
            "expiring" => q.Where(c => ActiveStatuses.Contains(c.Status) && db.Documents.Any(d => d.CaseId == c.Id && d.ValidUntil != null && d.ValidUntil >= today && d.ValidUntil <= today.AddDays(30))),
            "closed" => q.Where(c => c.Status == CaseStatus.Closed || c.Status == CaseStatus.Cancelled),
            "drafts" => q.Where(c => c.Status == CaseStatus.Draft && c.CreatedByUserId == rc.UserId),
            _ => q.Where(c => ActiveStatuses.Contains(c.Status)),
        };
    }

    private static async Task<IResult> List([AsParameters] CaseListQuery query, CaseAccess access, RahoonDbContext db, RequestContext rc, IClock clock)
    {
        var today = clock.TodayRiyadh;
        var baseQ = await access.VisibleAsync();
        var q = await ApplyView(baseQ, query.View ?? "mine", db, rc, today);

        if (!string.IsNullOrWhiteSpace(query.Q))
        {
            var term = query.Q.Trim();
            q = q.Where(c => c.Reference.Contains(term)
                             || db.Parties.Any(p => p.CaseId == c.Id && p.IsPrimary && (p.DisplayName.Contains(term) || p.FullName.Contains(term)))
                             || db.FinancingContracts.Any(f => f.CaseId == c.Id && f.ContractNumber.Contains(term)));
        }
        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            var statuses = query.Status.Split(',').Select(CaseStatusInfo.Parse).Where(s => s is not null).Select(s => s!.Value).ToList();
            if (statuses.Count > 0) q = q.Where(c => statuses.Contains(c.Status));
        }
        q = query.Sla switch
        {
            "overdue" => q.Where(c => c.StageDueOn < today && c.Status != CaseStatus.Paused),
            "soon" => q.Where(c => c.StageDueOn >= today && c.StageDueOn <= today.AddDays(2)),
            "paused" => q.Where(c => c.Status == CaseStatus.Paused),
            _ => q,
        };
        if (!string.IsNullOrWhiteSpace(query.Region)) q = q.Where(c => c.Region == query.Region);
        if (query.Manager is { } mgr) q = q.Where(c => c.AssignedManagerId == mgr);
        if (query.MinAmount is { } min) q = q.Where(c => c.OutstandingAmount >= min);
        if (query.MaxAmount is { } max) q = q.Where(c => c.OutstandingAmount <= max);

        q = query.Sort switch
        {
            "amount" => q.OrderByDescending(c => c.OutstandingAmount),
            "recent" => q.OrderByDescending(c => c.StatusChangedAt),
            "reference" => q.OrderBy(c => c.Reference),
            _ => q.OrderBy(c => c.StageDueOn == null).ThenBy(c => c.StageDueOn).ThenBy(c => c.Reference),
        };

        var total = await q.CountAsync();
        var pageSize = Math.Clamp(query.PageSize, 5, 100);
        var page = Math.Max(1, query.Page);
        var rows = await q.Skip((page - 1) * pageSize).Take(pageSize)
            .Select(c => new
            {
                c.Id, c.Reference, c.Status, c.City, c.StageDueOn, c.SlaPausedAt, c.OutstandingAmount, c.ArrearsInstallments, c.PauseReason, c.DraftStep,
                Owner = db.Parties.Where(p => p.CaseId == c.Id && p.IsPrimary).Select(p => p.DisplayName).FirstOrDefault(),
                Manager = db.Memberships.Where(m => m.Id == c.AssignedManagerId).Select(m => m.User!.FullName).FirstOrDefault(),
            }).ToListAsync();

        var ids = rows.Select(r => r.Id).ToList();
        var nextActions = await NextActionTextsAsync(db, ids);
        var items = rows.Select(r =>
        {
            var probe = new Case { Reference = r.Reference, Status = r.Status, StageDueOn = r.StageDueOn, SlaPausedAt = r.SlaPausedAt };
            var sla = CaseDisplay.Sla(probe, today);
            var meta = CaseStatusInfo.Of(r.Status);
            return new
            {
                reference = r.Reference, owner = r.Owner ?? "—", city = r.City, status = meta.Key, statusLabel = meta.LabelAr,
                nextAction = r.Status == CaseStatus.Paused ? r.PauseReason ?? "الحالة موقوفة"
                    : r.Status == CaseStatus.Draft ? $"استكمال بيانات الحالة ({r.DraftStep} من 6)"
                    : nextActions.GetValueOrDefault(r.Id) ?? DefaultNext(r.Status),
                slaTone = sla.Tone, slaText = sla.Text, dueOn = sla.DueOn,
                outstanding = r.OutstandingAmount, arrearsInstallments = r.ArrearsInstallments,
                manager = r.Manager is null ? "—" : CaseDisplay.ShortName(r.Manager),
            };
        });

        // Saved-view counts shown on the tabs.
        var counts = new Dictionary<string, int>();
        foreach (var v in new[] { "mine", "action", "overdue", "expiring", "all" })
            counts[v] = await (await ApplyView(baseQ, v, db, rc, today)).CountAsync();

        return Results.Ok(new { items, total, page, pageSize, counts });
    }

    public static string DefaultNext(CaseStatus s) => s switch
    {
        CaseStatus.Draft => "استكمال بيانات الحالة",
        CaseStatus.AwaitingData => "استكمال البيانات الإلزامية",
        CaseStatus.Verification => "مراجعة المستندات والتحقق",
        CaseStatus.Valuation => "بانتظار تقرير المقيّم",
        CaseStatus.ProposedSolution => "إعداد الحل",
        CaseStatus.InternalApproval => "بانتظار قرار المعتمد",
        CaseStatus.AwaitingCustomer => "متابعة رد المالك على العرض",
        CaseStatus.Negotiation => "الرد على العرض المقابل",
        CaseStatus.ActiveSettlement => "متابعة الأقساط",
        CaseStatus.VoluntarySale => "متابعة مسار البيع",
        CaseStatus.JudicialReferral => "متابعة المرجع الخارجي",
        CaseStatus.ExternalJudicialSale => "متابعة الحالة الرسمية",
        CaseStatus.AwaitingReconciliation => "مطابقة المتحصلات",
        CaseStatus.Closed => "مغلقة",
        CaseStatus.Cancelled => "ملغاة",
        _ => "—",
    };

    private static async Task<Dictionary<Guid, string>> NextActionTextsAsync(RahoonDbContext db, List<Guid> caseIds)
    {
        var result = new Dictionary<Guid, string>();
        var solutions = await db.Solutions.Where(s => caseIds.Contains(s.CaseId))
            .GroupBy(s => s.CaseId).Select(g => g.OrderByDescending(s => s.VersionNo).First()).ToListAsync();
        foreach (var s in solutions)
        {
            if (s.Status == SolutionStatus.InReview) result[s.CaseId] = $"إرسال الحل v{s.VersionNo} للموافقة";
            else if (s.Status == SolutionStatus.Draft) result[s.CaseId] = $"استكمال الحل v{s.VersionNo}";
        }
        var approvals = await db.ApprovalRequests.Where(a => caseIds.Contains(a.CaseId) && a.Status == ApprovalStatus.Pending)
            .Select(a => new { a.CaseId, Name = db.Users.Where(u => u.Id == a.AssignedApproverUserId).Select(u => u.FullName).FirstOrDefault() }).ToListAsync();
        foreach (var a in approvals) result[a.CaseId] = $"بانتظار قرار {a.Name}";
        var agreements = await db.Agreements.Where(a => caseIds.Contains(a.CaseId) && a.Status == AgreementStatus.Active)
            .Select(a => new { a.CaseId, a.InstallmentCount, Paid = db.Installments.Count(i => i.AgreementId == a.Id && i.Status == InstallmentStatus.Matched) }).ToListAsync();
        foreach (var a in agreements) result[a.CaseId] = $"متابعة القسط {Math.Min(a.Paid + 1, a.InstallmentCount)} من {a.InstallmentCount}";
        return result;
    }

    private static async Task<IResult> Views(RahoonDbContext db, RequestContext rc) =>
        Results.Ok(await db.SavedViews.Where(v => v.MembershipId == rc.MembershipId).OrderBy(v => v.SortOrder)
            .Select(v => new { v.Id, v.Name, v.FiltersJson }).ToListAsync());

    private static async Task<IResult> SaveView(SaveViewRequest req, RahoonDbContext db, RequestContext rc)
    {
        new Validator().Require(!string.IsNullOrWhiteSpace(req.Name) && req.Name.Length <= 60, "name", "اسم العرض مطلوب (حتى 60 حرفاً).")
            .Require(req.FiltersJson.Length <= 2000 && IsJson(req.FiltersJson), "filtersJson", "المرشحات غير صالحة.").ThrowIfInvalid();
        var count = await db.SavedViews.CountAsync(v => v.MembershipId == rc.MembershipId);
        var view = new SavedView { MembershipId = rc.MembershipId!.Value, Name = req.Name.Trim(), FiltersJson = req.FiltersJson, SortOrder = count };
        db.SavedViews.Add(view);
        await db.SaveChangesAsync();
        return Results.Ok(new { view.Id });
    }

    private static bool IsJson(string s) { try { JsonDocument.Parse(s); return true; } catch { return false; } }

    private static async Task<IResult> DeleteView(Guid id, RahoonDbContext db, RequestContext rc)
    {
        var v = await db.SavedViews.FirstOrDefaultAsync(x => x.Id == id && x.MembershipId == rc.MembershipId) ?? throw new NotFoundException();
        db.SavedViews.Remove(v);
        await db.SaveChangesAsync();
        return Results.NoContent();
    }

    /// <summary>Bulk reassign — safe bulk action; sensitive actions (cancel, referral) are never bulk.</summary>
    private static async Task<IResult> Reassign(ReassignRequest req, CaseAccess access, RahoonDbContext db, AuditLog audit, RequestContext rc)
    {
        new Validator().Require(req.References is { Count: > 0 and <= 200 }, "references", "حدد حالة واحدة على الأقل.")
            .Require(!string.IsNullOrWhiteSpace(req.Reason), "reason", "سبب إعادة الإسناد مطلوب.").ThrowIfInvalid();
        var target = await db.Memberships.Include(m => m.User).FirstOrDefaultAsync(m => m.Id == req.ManagerMembershipId && m.Status == MembershipStatus.Active
            && m.Roles.Any(r => r.Role!.Key == SystemRoles.CaseManager || r.Role!.Key == SystemRoles.CaseOfficer)) ?? throw new NotFoundException();
        var visible = await access.VisibleAsync();
        var cases = await visible.Where(c => req.References.Contains(c.Reference)).ToListAsync();
        if (cases.Count != req.References.Distinct().Count()) throw new CaseForbiddenException();
        await using var tx = await db.Database.BeginTransactionAsync();
        foreach (var c in cases)
        {
            c.AssignedManagerId = target.Id;
            await audit.RecordAsync(new AuditEntry("case.reassigned", $"إعادة إسناد الحالة إلى {target.User!.FullName}", c.Id, c.Reference, Reason: req.Reason, OrganizationId: c.OrganizationId));
        }
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Results.Ok(new { updated = cases.Count });
    }

    /// <summary>Masked CSV export (no identity numbers, phones or full names).</summary>
    private static async Task<IResult> Export(string? view, string? refs, CaseAccess access, RahoonDbContext db, RequestContext rc, IClock clock, AuditLog audit)
    {
        var q = await ApplyView(await access.VisibleAsync(), view ?? "all", db, rc, clock.TodayRiyadh);
        if (!string.IsNullOrWhiteSpace(refs)) { var list = refs.Split(','); q = q.Where(c => list.Contains(c.Reference)); }
        var rows = await q.OrderBy(c => c.Reference).Take(5000).Select(c => new
        {
            c.Reference, c.Status, c.City, c.OutstandingAmount, c.ArrearsInstallments, c.StageDueOn,
            Owner = db.Parties.Where(p => p.CaseId == c.Id && p.IsPrimary).Select(p => p.DisplayName).FirstOrDefault(),
        }).ToListAsync();
        var sb = new StringBuilder("﻿المرجع,المالك (مخفي),الحالة,المدينة,القائم (ر.س),أقساط متأخرة,المهلة\n");
        foreach (var r in rows)
            sb.Append($"{r.Reference},{Csv(r.Owner)},{CaseStatusInfo.Of(r.Status).LabelAr},{r.City},{r.OutstandingAmount:0.00},{r.ArrearsInstallments},{r.StageDueOn:yyyy-MM-dd}\n");
        await audit.RecordAsync(new AuditEntry("case.export", $"تصدير مخفي البيانات ({rows.Count} حالة)", Detail: $"العرض: {view ?? "all"}"));
        await db.SaveChangesAsync();
        return Results.File(Encoding.UTF8.GetBytes(sb.ToString()), "text/csv; charset=utf-8", $"rahoon-cases-masked-{clock.TodayRiyadh:yyyyMMdd}.csv");
    }

    private static string Csv(string? s) => s is null ? "" : s.Contains(',') ? $"\"{s}\"" : s;

    private static async Task<IResult> Members(string? role, RahoonDbContext db, RequestContext rc)
    {
        var q = db.Memberships.Where(m => m.OrganizationId == rc.OrganizationId && m.Status == MembershipStatus.Active);
        if (!string.IsNullOrEmpty(role)) q = q.Where(m => m.Roles.Any(r => r.Role!.Key == role));
        return Results.Ok(await q.OrderBy(m => m.User!.FullName).Select(m => new
        {
            m.Id, name = m.User!.FullName, m.Title, team = m.Team != null ? m.Team.NameAr : null,
            roles = m.Roles.Select(r => r.Role!.NameAr).ToList(),
        }).ToListAsync());
    }
}
