using Microsoft.EntityFrameworkCore;
using Rahoon.Api.Infrastructure.Http;
using Rahoon.Api.Infrastructure.Persistence;
using Rahoon.Api.Infrastructure.Tenancy;
using Rahoon.Api.Infrastructure.Time;
using Rahoon.Api.Modules.Cases;
using Rahoon.Api.Modules.Identity;

namespace Rahoon.Api.Modules.Administration;

/// <summary>PA06 case monitoring (opaque ids only) and the temporary-access workflow on both sides.</summary>
public static class TempAccessEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var p = app.MapGroup("/api/platform").RequireOrg(OrganizationKind.Platform);
        p.MapGet("/cases/monitor", Monitor).RequirePermission(P.PlatformOps);
        p.MapGet("/temp-access", PlatformList).RequireAnyPermission(P.PlatformTempAccess, P.PlatformTempAccessApprove, P.PlatformAudit);
        p.MapPost("/temp-access", Create).RequirePermission(P.PlatformTempAccess).Idempotent();
        p.MapPost("/temp-access/{id:guid}/approve", (Guid id, TempAccessService s, RahoonDbContext db) => Tx(db, () => s.ApproveAsync(id, asInstitution: false)))
            .RequirePermission(P.PlatformTempAccessApprove).Idempotent();
        p.MapPost("/temp-access/{id:guid}/reject", (Guid id, TempAccessDecisionRequest req, TempAccessService s, RahoonDbContext db) => Tx(db, () => s.RejectAsync(id, false, req.Reason)))
            .RequirePermission(P.PlatformTempAccessApprove).Idempotent();
        p.MapPost("/temp-access/{id:guid}/revoke", (Guid id, TempAccessDecisionRequest req, TempAccessService s, RahoonDbContext db) => Tx(db, () => s.RevokeAsync(id, req.Reason)))
            .RequireAnyPermission(P.PlatformTempAccess, P.PlatformTempAccessApprove).Idempotent();
        p.MapGet("/temp-access/{id:guid}/view", async (Guid id, string? screen, TempAccessService s, RahoonDbContext db) =>
        {
            await using var tx = await db.Database.BeginTransactionAsync();
            var result = await s.ViewAsync(id, screen);
            await tx.CommitAsync();
            return Results.Ok(result);
        }).RequirePermission(P.PlatformTempAccess);

        // Institution side: an org_admin of the case's institution approves or rejects.
        var i = app.MapGroup("/api/settings/temp-access").RequireOrg(OrganizationKind.Lender).RequirePermission(P.UserManage);
        i.MapGet("", InstitutionList);
        i.MapPost("/{id:guid}/approve", (Guid id, TempAccessService s, RahoonDbContext db) => Tx(db, () => s.ApproveAsync(id, asInstitution: true))).Idempotent();
        i.MapPost("/{id:guid}/reject", (Guid id, TempAccessDecisionRequest req, TempAccessService s, RahoonDbContext db) => Tx(db, () => s.RejectAsync(id, true, req.Reason))).Idempotent();
    }

    private static async Task<IResult> Tx(RahoonDbContext db, Func<Task<TempAccessRequest>> action)
    {
        await using var tx = await db.Database.BeginTransactionAsync();
        var t = await action();
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Results.Ok(new { t.Id, status = t.Status, t.ExpiresAt });
    }

    private static string StatusLabel(TempAccessStatus s) => s switch
    {
        TempAccessStatus.Pending => "بانتظار الموافقات",
        TempAccessStatus.Active => "نشط",
        TempAccessStatus.Rejected => "مرفوض",
        TempAccessStatus.Expired => "منتهٍ",
        _ => "مُنهى",
    };

    /// <summary>Aggregate, anonymous rows: opaque id, institution, state, stage age. No names, ids or amounts.</summary>
    private static async Task<IResult> Monitor(Guid? organizationId, string? status, int? page, RahoonDbContext db, RequestContext rc, IClock clock, TempAccessService temp)
    {
        await temp.ExpireDueAsync();
        var now = clock.UtcNow;
        var size = 50;
        var p = Math.Max(1, page ?? 1);
        var st = status is null ? null : CaseStatusInfo.Parse(status);
        var grants = await db.TempAccessRequests.AsNoTracking()
            .Where(t => t.RequesterUserId == rc.UserId && (t.Status == TempAccessStatus.Pending || (t.Status == TempAccessStatus.Active && t.ExpiresAt > now)))
            .Select(t => new { t.Id, t.CaseId, t.Status, t.ExpiresAt }).ToListAsync();
        List<(Guid Id, Guid Org, CaseStatus Status, DateTimeOffset Changed)> rows;
        int total;
        Dictionary<Guid, string> orgs;
        using (rc.BeginSystemScope())
        {
            var q = db.Cases.AsNoTracking().Where(c => c.Status != CaseStatus.Draft);
            if (organizationId is { } oid) q = q.Where(c => c.OrganizationId == oid);
            if (st is { } s) q = q.Where(c => c.Status == s);
            total = await q.CountAsync();
            rows = (await q.OrderBy(c => c.StatusChangedAt).ThenBy(c => c.Id).Skip((p - 1) * size).Take(size)
                .Select(c => new { c.Id, c.OrganizationId, c.Status, c.StatusChangedAt }).ToListAsync())
                .Select(c => (c.Id, c.OrganizationId, c.Status, c.StatusChangedAt)).ToList();
            orgs = await db.Organizations.AsNoTracking().Where(o => o.Kind == OrganizationKind.Lender).ToDictionaryAsync(o => o.Id, o => o.NameAr);
        }
        var today = clock.TodayRiyadh;
        return Results.Ok(new
        {
            total, page = p, pageSize = size,
            footnote = "الأسماء والهويات والمبالغ غير معروضة. المرجع الحقيقي يظهر فقط أثناء وصول مؤقت معتمد.",
            items = rows.Select(r =>
            {
                var grant = grants.FirstOrDefault(g => g.CaseId == r.Id);
                var age = today.DayNumber - DateOnly.FromDateTime(r.Changed.ToOffset(TimeSpan.FromHours(3)).DateTime).DayNumber;
                return new
                {
                    maskedId = temp.MaskedId(r.Id), handle = temp.HandleFor(r.Id), organization = orgs.GetValueOrDefault(r.Org, "—"),
                    status = CaseStatusInfo.Of(r.Status).LabelAr, statusKey = CaseStatusInfo.Key(r.Status),
                    stageAge = CaseStatusInfo.IsTerminal(r.Status) || r.Status == CaseStatus.ActiveSettlement ? "—" : CaseDisplay.Days(Math.Max(age, 1)),
                    tempAccess = grant is null ? null : new { requestId = grant.Id, status = grant.Status, grant.ExpiresAt },
                };
            }),
        });
    }

    private static async Task<IResult> Create(TempAccessCreateRequest req, TempAccessService s, RahoonDbContext db)
    {
        await using var tx = await db.Database.BeginTransactionAsync();
        var t = await s.RequestAsync(req);
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Results.Ok(new { t.Id, status = t.Status, maskedId = t.MaskedCaseId, scope = TempAccessService.ScopeLabel });
    }

    private static async Task<IResult> PlatformList(string? status, RahoonDbContext db, RequestContext rc, TempAccessService s)
    {
        await s.ExpireDueAsync();
        var q = db.TempAccessRequests.AsNoTracking();
        if (!rc.Has(P.PlatformTempAccessApprove) && !rc.Has(P.PlatformAudit)) q = q.Where(t => t.RequesterUserId == rc.UserId);
        if (status == "open") q = q.Where(t => t.Status == TempAccessStatus.Pending || t.Status == TempAccessStatus.Active);
        var list = await q.OrderByDescending(t => t.CreatedAt).Take(100).ToListAsync();
        return Results.Ok(await ProjectAsync(db, rc, list, platformView: true));
    }

    private static async Task<IResult> InstitutionList(RahoonDbContext db, RequestContext rc, TempAccessService s)
    {
        await s.ExpireDueAsync();
        var list = await db.TempAccessRequests.AsNoTracking().Where(t => t.OrganizationId == rc.OrganizationId).OrderByDescending(t => t.CreatedAt).Take(100).ToListAsync();
        return Results.Ok(await ProjectAsync(db, rc, list, platformView: false));
    }

    private static async Task<List<object>> ProjectAsync(RahoonDbContext db, RequestContext rc, List<TempAccessRequest> list, bool platformView)
    {
        var userIds = list.SelectMany(t => new[] { (Guid?)t.RequesterUserId, t.InstitutionApproverUserId, t.AuditorApproverUserId }).Where(x => x is not null).Select(x => x!.Value).Distinct().ToList();
        var users = await db.Users.AsNoTracking().Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName);
        var orgIds = list.Select(t => t.OrganizationId).Distinct().ToList();
        var orgs = await db.Organizations.AsNoTracking().Where(o => orgIds.Contains(o.Id)).ToDictionaryAsync(o => o.Id, o => o.NameAr);
        var ids = list.Select(t => t.Id).ToList();
        var views = await db.Set<TempAccessViewLog>().AsNoTracking().Where(v => ids.Contains(v.RequestId)).GroupBy(v => v.RequestId)
            .Select(g => new { g.Key, N = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.N);
        var isOrgAdmin = rc.RoleKeys.Contains(SystemRoles.OrgAdmin);
        return list.Select(t => (object)new
        {
            t.Id, maskedId = t.MaskedCaseId, organization = orgs.GetValueOrDefault(t.OrganizationId), requester = users.GetValueOrDefault(t.RequesterUserId),
            t.Reason, t.SupportTicketRef, t.DurationMinutes, duration = TempAccessService.DurationLabel(t.DurationMinutes), scope = TempAccessService.ScopeLabel,
            status = t.Status, statusLabel = StatusLabel(t.Status), t.CreatedAt, t.StartsAt, t.ExpiresAt, t.DecisionNote,
            approvals = new[]
            {
                new { role = "institution_admin", label = $"مسؤول منشأة {orgs.GetValueOrDefault(t.OrganizationId)}", by = t.InstitutionApproverUserId is { } a ? users.GetValueOrDefault(a) : null, at = t.InstitutionApprovedAt },
                new { role = "platform_auditor", label = "مدقق المنصة", by = t.AuditorApproverUserId is { } b ? users.GetValueOrDefault(b) : null, at = t.AuditorApprovedAt },
            },
            views = views.GetValueOrDefault(t.Id),
            mine = t.RequesterUserId == rc.UserId,
            canApprove = t.Status == TempAccessStatus.Pending && t.RequesterUserId != rc.UserId
                         && (platformView ? rc.Has(P.PlatformTempAccessApprove) && t.AuditorApproverUserId is null : isOrgAdmin && t.InstitutionApproverUserId is null),
        }).ToList();
    }
}
