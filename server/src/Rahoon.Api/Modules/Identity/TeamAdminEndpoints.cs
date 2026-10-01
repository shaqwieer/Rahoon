using System.Net.Mail;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Rahoon.Api.Infrastructure.Http;
using Rahoon.Api.Infrastructure.Persistence;
using Rahoon.Api.Infrastructure.Security;
using Rahoon.Api.Infrastructure.Tenancy;
using Rahoon.Api.Infrastructure.Time;
using Rahoon.Api.Modules.Audit;
using Rahoon.Api.Modules.Market;

namespace Rahoon.Api.Modules.Identity;

public sealed record InviteInput(string? Email, string? FullName, string? Phone, string? Title, List<Guid>? RoleIds);
public sealed record MemberRolesInput(List<Guid>? RoleIds, string? Reason);
/// <param name="Reassign">"unassigned" or "member" (with <paramref name="ReassignToUserId"/>); required when the member has open work.</param>
public sealed record MemberStatusInput(string? Reason, string? Reassign, Guid? ReassignToUserId);
public sealed record GrantInput(string? Key, string? Scope);
public sealed record RoleInput(string? NameAr, string? NameEn, string? DescriptionAr, List<GrantInput>? Grants, uint? Version);
public sealed record ArchiveRoleInput(string? Reason);

/// <summary>
/// The team console's administration: members (roles, suspend, reactivate, remove, reassign work), identity-bound
/// invitations, custom roles over the permission catalog, effective access and the administration log.
/// Rules enforced here, whatever the UI shows:
/// - nobody changes their own roles or status;
/// - a manager acts only on members whose access they fully hold, and grants only what they hold (same or narrower scope);
/// - system roles are fixed; custom roles change grants, never the meaning of an action;
/// - the last active platform owner can't be suspended, removed or lose the owner role. Membership changes run under one
///   transaction-scoped advisory lock per team, re-reading the actor and target inside it, so concurrent requests can't
///   both pass the check.
/// </summary>
public static class TeamAdminEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/team/admin").RequireOrg(OrganizationKind.Operator);
        g.MapGet("/catalog", Catalog).RequireAnyPermission(P.TeamRead, P.RolesRead);

        g.MapGet("/members", Members).RequirePermission(P.TeamRead);
        g.MapGet("/members/{id:guid}", Member).RequirePermission(P.TeamRead);
        g.MapPost("/members/{id:guid}/roles", SetRoles).RequirePermission(P.TeamManage).Idempotent();
        g.MapPost("/members/{id:guid}/suspend", Suspend).RequirePermission(P.TeamManage).Idempotent();
        g.MapPost("/members/{id:guid}/reactivate", Reactivate).RequirePermission(P.TeamManage).Idempotent();
        g.MapPost("/members/{id:guid}/remove", Remove).RequirePermission(P.TeamManage).Idempotent();
        g.MapPost("/members/{id:guid}/reassign", Reassign).RequirePermission(P.TeamManage).Idempotent();

        g.MapGet("/invitations", Invitations).RequirePermission(P.TeamRead);
        // Not .Idempotent(): the response carries the one-time link, which must never be stored. A double submit hits the
        // unique pending-invitation index and gets a conflict instead.
        g.MapPost("/invitations", Invite).RequirePermission(P.TeamManage);
        g.MapPost("/invitations/{id:guid}/renew", Renew).RequirePermission(P.TeamManage);
        g.MapPost("/invitations/{id:guid}/revoke", RevokeInvitation).RequirePermission(P.TeamManage).Idempotent();

        g.MapGet("/roles", Roles).RequireAnyPermission(P.RolesRead, P.TeamManage);
        g.MapGet("/roles/{id:guid}", RoleDetail).RequireAnyPermission(P.RolesRead, P.TeamManage);
        g.MapPost("/roles", CreateRole).RequirePermission(P.RolesManage).Idempotent();
        g.MapPut("/roles/{id:guid}", UpdateRole).RequirePermission(P.RolesManage).Idempotent();
        g.MapPost("/roles/{id:guid}/archive", ArchiveRole).RequirePermission(P.RolesManage).Idempotent();

        g.MapGet("/audit", AuditTrail).RequirePermission(P.AuditRead);
    }

    // ── Shared rules ──

    internal static string ScopeLabel(GrantScope s) => s == GrantScope.All ? "all" : "assigned";

    private static async Task<IDbContextTransaction> LockTeamAsync(RahoonDbContext db, Guid orgId)
    {
        var tx = await db.Database.BeginTransactionAsync();
        var key = "team-admin:" + orgId;
        await db.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock(hashtext({key}))");
        return tx;
    }

    private static IQueryable<Membership> WithRoles(this IQueryable<Membership> q) =>
        q.Include(m => m.User).Include(m => m.Roles).ThenInclude(r => r.Role!).ThenInclude(r => r.Permissions);

    private static IReadOnlyDictionary<string, GrantScope> GrantsOf(Membership m) => EffectiveAccess.Grants(m.Roles.Select(r => r.Role!));
    private static IReadOnlyDictionary<string, GrantScope> GrantsOf(IEnumerable<Role> roles) => EffectiveAccess.Grants(roles);
    private static bool IsOwner(Membership m) => m.Roles.Any(r => r.Role!.Key == SystemRoles.PlatformOwner && r.Role.ArchivedAt is null);

    /// <summary>Re-reads the actor inside the lock: a member suspended a moment ago can't finish an action.</summary>
    private static async Task<IReadOnlyDictionary<string, GrantScope>> ActorGrantsAsync(RahoonDbContext db, RequestContext rc, string permission = P.TeamManage)
    {
        var me = await db.Memberships.WithRoles().FirstOrDefaultAsync(m => m.Id == rc.MembershipId);
        if (me is not { Status: MembershipStatus.Active }) throw new ForbiddenException();
        var grants = GrantsOf(me);
        if (!grants.ContainsKey(permission)) throw new ForbiddenException();
        return grants;
    }

    private static void EnsureCanManage(RequestContext rc, IReadOnlyDictionary<string, GrantScope> actor, Membership target)
    {
        if (target.UserId == rc.UserId)
            throw new DomainException("self_change", "لا يمكنك تعديل أدوارك أو حالتك بنفسك. اطلب ذلك من مسؤول آخر.", StatusCodes.Status403Forbidden);
        var missing = EffectiveAccess.Missing(actor, GrantsOf(target));
        if (missing.Count > 0)
            throw new DomainException("insufficient_authority", "لدى هذا العضو صلاحيات لا تملكها، فلا يمكنك إدارته.", StatusCodes.Status403Forbidden,
                missing.Select(k => P.Find(k)?.NameAr ?? k).ToList());
    }

    private static void EnsureCanGrant(IReadOnlyDictionary<string, GrantScope> actor, IReadOnlyDictionary<string, GrantScope> wanted)
    {
        var missing = EffectiveAccess.Missing(actor, wanted);
        if (missing.Count > 0)
            throw new DomainException("grant_exceeds_authority", "لا يمكنك منح صلاحية لا تملكها، أو بنطاق أوسع من نطاقك.", StatusCodes.Status403Forbidden,
                missing.Select(k => P.Find(k)?.NameAr ?? k).ToList());
    }

    private static async Task EnsureAnotherOwnerAsync(RahoonDbContext db, Membership target)
    {
        if (!IsOwner(target) || target.Status != MembershipStatus.Active) return;
        var others = await db.Memberships.CountAsync(m => m.OrganizationId == target.OrganizationId && m.Id != target.Id && m.Status == MembershipStatus.Active
            && m.Roles.Any(r => r.Role!.Key == SystemRoles.PlatformOwner && r.Role.ArchivedAt == null));
        if (others == 0)
            throw new ConflictException("last_owner", "هذا آخر مالك نشط للمنصة. أضف مالكًا آخر أولًا، ثم أعد المحاولة.");
    }

    private static async Task<List<Role>> LoadAssignableRolesAsync(RahoonDbContext db, Guid orgId, List<Guid>? ids)
    {
        var wanted = (ids ?? []).Distinct().ToList();
        if (wanted.Count == 0) Validate.Throw("roleIds", "اختر دورًا واحدًا على الأقل.");
        var roles = await db.Roles.Include(r => r.Permissions).Where(r => r.OrganizationId == orgId && wanted.Contains(r.Id)).ToListAsync();
        if (roles.Count != wanted.Count || roles.Any(r => r.ArchivedAt is not null)) Validate.Throw("roleIds", "أحد الأدوار غير موجود أو مؤرشف.");
        return roles;
    }

    private static object RolesData(IEnumerable<Role> roles) => roles.Select(r => new { r.Key, r.NameAr }).ToList();

    private static void RevokeSessions(RahoonDbContext db, Guid membershipId, DateTimeOffset now, string reason)
    {
        foreach (var s in db.Sessions.Where(s => s.MembershipId == membershipId && s.RevokedAt == null))
        {
            s.RevokedAt = now;
            s.RevokedReason = reason;
        }
    }

    // ── Catalog ──

    private static IResult Catalog() => Results.Ok(new
    {
        areas = P.Areas.Select(a => new
        {
            a.Key, a.NameAr,
            permissions = P.Catalog.Where(p => p.Area == a.Key).Select(p => new { p.Key, p.NameAr, p.NameEn, p.Scopable, p.Reserved, p.ReservedFor }),
        }),
        scopes = new[] { new { key = "assigned", nameAr = "المسند إليه فقط" }, new { key = "all", nameAr = "كل أعمال الفريق" } },
    });

    // ── Members ──

    private static async Task<IResult> Members(RahoonDbContext db, RequestContext rc, string? q, string? status)
    {
        var org = rc.OrganizationId!.Value;
        var query = db.Memberships.WithRoles().Where(m => m.OrganizationId == org);
        if (Enum.TryParse<MembershipStatus>(status, true, out var st)) query = query.Where(m => m.Status == st);
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim().ToLowerInvariant();
            query = query.Where(m => m.User!.FullName.Contains(term) || m.User.Email.Contains(term) || (m.Title != null && m.Title.Contains(term)));
        }
        var rows = await query.OrderBy(m => m.Status).ThenBy(m => m.User!.FullName).Take(300).ToListAsync();
        var work = await TeamWorkload.TotalsAsync(db, rows.Select(m => m.UserId).ToList());
        var counts = await db.Memberships.Where(m => m.OrganizationId == org).GroupBy(m => m.Status).Select(g => new { g.Key, n = g.Count() }).ToListAsync();
        var pending = await db.StaffInvitations.CountAsync(i => i.OrganizationId == org && i.Status == InvitationStatus.Pending);
        return Results.Ok(new
        {
            items = rows.Select(m => new
            {
                id = m.Id, userId = m.UserId, name = m.User!.FullName, email = m.User.Email, m.Title, status = m.Status,
                roles = m.Roles.Select(r => r.Role!).OrderByDescending(r => r.IsSystem).Select(r => new { r.Id, r.NameAr, r.IsSystem }),
                lastLoginAt = m.User.LastLoginAt, openWork = work.GetValueOrDefault(m.UserId), self = m.UserId == rc.UserId,
            }),
            counts = counts.ToDictionary(c => c.Key.ToString().ToLowerInvariant(), c => c.n),
            pendingInvitations = pending,
            canManage = rc.Has(P.TeamManage),
        });
    }

    private static async Task<Membership> LoadMemberAsync(RahoonDbContext db, RequestContext rc, Guid id) =>
        await db.Memberships.WithRoles().FirstOrDefaultAsync(m => m.Id == id && m.OrganizationId == rc.OrganizationId) ?? throw new NotFoundException();

    private static async Task<IResult> Member(Guid id, RahoonDbContext db, RequestContext rc, IClock clock)
    {
        var m = await LoadMemberAsync(db, rc, id);
        var roles = m.Roles.Select(r => r.Role!).ToList();
        var grants = GrantsOf(roles);
        var work = await TeamWorkload.CountAsync(db, m.UserId);
        var now = clock.UtcNow;
        var sessions = await db.Sessions.CountAsync(s => s.MembershipId == m.Id && s.RevokedAt == null && s.IdleExpiresAt > now && s.AbsoluteExpiresAt > now);
        var history = await db.AuditEvents.Where(a => a.OrganizationId == m.OrganizationId && a.SubjectType == "team_member" && a.SubjectReference == m.Id.ToString())
            .OrderByDescending(a => a.Seq).Take(50)
            .Select(a => new { a.Seq, a.Type, a.Title, a.ActorLabel, a.Reason, a.FromState, a.ToState, a.OccurredAt, a.Blocked }).ToListAsync();

        var self = m.UserId == rc.UserId;
        var covers = EffectiveAccess.Covers(rc.Grants, grants);
        var manage = rc.Has(P.TeamManage) && !self && covers;
        var isOwner = IsOwner(m);
        var lastOwner = isOwner && m.Status == MembershipStatus.Active && !await db.Memberships.AnyAsync(x => x.OrganizationId == m.OrganizationId && x.Id != m.Id
            && x.Status == MembershipStatus.Active && x.Roles.Any(r => r.Role!.Key == SystemRoles.PlatformOwner && r.Role.ArchivedAt == null));
        var assignable = rc.Has(P.TeamManage)
            ? (await db.Roles.Include(r => r.Permissions).Where(r => r.OrganizationId == m.OrganizationId && r.ArchivedAt == null).ToListAsync())
                .Where(r => EffectiveAccess.Covers(rc.Grants, GrantsOf([r]))).OrderByDescending(r => r.IsSystem).ThenBy(r => r.NameAr)
                .Select(r => new { r.Id, r.NameAr, r.IsSystem }).ToList()
            : [];

        string? blocked = self ? "لا يمكنك تعديل أدوارك أو حالتك بنفسك."
            : !rc.Has(P.TeamManage) ? "تحتاج صلاحية إدارة الفريق."
            : !covers ? "لدى هذا العضو صلاحيات لا تملكها، فلا يمكنك إدارته."
            : null;
        return Results.Ok(new
        {
            id = m.Id, userId = m.UserId, name = m.User!.FullName, email = m.User.Email, phoneMasked = m.User.Phone is { } p ? Mask.Phone(p) : null,
            m.Title, status = m.Status, m.StatusReason, m.StatusChangedAt, m.CreatedAt, lastLoginAt = m.User.LastLoginAt, activeSessions = sessions, version = m.Version,
            roles = roles.OrderByDescending(r => r.IsSystem).Select(r => new { r.Id, r.Key, r.NameAr, r.IsSystem, archived = r.ArchivedAt is not null }),
            effective = EffectiveAccess.Explain(roles).Select(e => new
            {
                key = e.Key, nameAr = P.Find(e.Key)!.NameAr, area = P.Find(e.Key)!.Area, scope = ScopeLabel(e.Scope), scopable = P.IsScopable(e.Key),
                from = e.From.Select(f => new { roleId = f.RoleId, roleName = f.RoleName, scope = ScopeLabel(f.Scope) }),
            }),
            workload = new { work.SaleRequests, work.BuyerRequests, work.Opportunities, work.Interests, total = work.Total },
            history,
            isOwner, lastOwner, self,
            actions = new
            {
                manage, blocked,
                changeRoles = manage && m.Status != MembershipStatus.Revoked,
                suspend = manage && m.Status == MembershipStatus.Active && !lastOwner,
                reactivate = manage && m.Status == MembershipStatus.Suspended,
                remove = manage && m.Status is MembershipStatus.Active or MembershipStatus.Suspended && !lastOwner,
                reassign = manage && work.Total > 0,
            },
            assignableRoles = assignable,
        });
    }

    private static async Task<IResult> SetRoles(Guid id, MemberRolesInput req, RahoonDbContext db, RequestContext rc, AuditLog audit)
    {
        var org = rc.OrganizationId!.Value;
        await using var tx = await LockTeamAsync(db, org);
        var actor = await ActorGrantsAsync(db, rc);
        var m = await LoadMemberAsync(db, rc, id);
        EnsureCanManage(rc, actor, m);
        if (m.Status == MembershipStatus.Revoked) throw new ConflictException("member_removed", "هذا العضو أُزيل من الفريق؛ أرسل له دعوة جديدة.");
        var roles = await LoadAssignableRolesAsync(db, org, req.RoleIds);
        EnsureCanGrant(actor, GrantsOf(roles));

        var before = m.Roles.Select(r => r.Role!).ToList();
        if (IsOwner(m) && roles.All(r => r.Key != SystemRoles.PlatformOwner)) await EnsureAnotherOwnerAsync(db, m);
        m.Roles.Clear();
        foreach (var r in roles) m.Roles.Add(new MembershipRole { MembershipId = m.Id, RoleId = r.Id });
        await audit.RecordAsync(new AuditEntry("team.roles_changed", $"تغيير أدوار {m.User!.FullName}", Reason: Trim(req.Reason, 500),
            Data: new
            {
                before = RolesData(before), after = RolesData(roles),
                grantsBefore = GrantsOf(before).ToDictionary(g => g.Key, g => ScopeLabel(g.Value)),
                grantsAfter = GrantsOf(roles).ToDictionary(g => g.Key, g => ScopeLabel(g.Value)),
            },
            SubjectType: "team_member", SubjectReference: m.Id.ToString()));
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        // Takes effect on this member's next request: permissions are re-read from the database every time.
        return Results.Ok(new { roles = roles.Select(r => new { r.Id, r.NameAr }) });
    }

    private static async Task<(Guid Id, string Label)?> ReassignTargetAsync(RahoonDbContext db, Guid org, Membership from, MemberStatusInput req)
    {
        switch (req.Reassign)
        {
            case "unassigned": return null;
            case "member":
                var to = await db.Memberships.WithRoles().FirstOrDefaultAsync(x => x.OrganizationId == org && x.UserId == req.ReassignToUserId && x.Status == MembershipStatus.Active);
                if (to is null || to.Id == from.Id) Validate.Throw("reassignToUserId", "اختر عضوًا نشطًا آخر من الفريق.");
                if (!GrantsOf(to!).ContainsKey(P.MarketView)) Validate.Throw("reassignToUserId", "هذا العضو لا يملك صلاحية عرض الطلبات.");
                return (to!.UserId, to.User!.FullName);
            default:
                Validate.Throw("reassign", "اختر: إعادة الأعمال إلى قائمة غير المسندة، أو إسنادها إلى عضو آخر.");
                return null;
        }
    }

    /// <summary>Open work must be moved explicitly before a member loses access; the answer lists what is open.</summary>
    private static async Task<Workload> MoveWorkAsync(RahoonDbContext db, MarketService market, Guid org, Membership m, MemberStatusInput req, string reason, IClock clock)
    {
        var work = await TeamWorkload.CountAsync(db, m.UserId);
        if (work.Total == 0) return work;
        if (req.Reassign is null)
            throw new DomainException("has_assigned_work", $"لدى {m.User!.FullName} أعمال مفتوحة مسندة إليه ({work.Total}). اختر أين تذهب قبل المتابعة.",
                StatusCodes.Status409Conflict,
                [$"طلبات بيع: {work.SaleRequests}", $"طلبات شراء: {work.BuyerRequests}", $"فرص: {work.Opportunities}", $"اهتمامات: {work.Interests}"]);
        var to = await ReassignTargetAsync(db, org, m, req);
        return await TeamWorkload.ReassignAsync(db, market, m.UserId, m.User!.FullName, to, reason, clock.UtcNow);
    }

    private static string RequireReason(string? reason)
    {
        var r = reason?.Trim() ?? "";
        if (r.Length < 5) Validate.Throw("reason", "اكتب السبب (5 أحرف على الأقل). يُحفظ في السجل.");
        return r[..Math.Min(r.Length, 500)];
    }

    private static async Task<IResult> Suspend(Guid id, MemberStatusInput req, RahoonDbContext db, RequestContext rc, AuditLog audit, MarketService market, IClock clock)
    {
        var reason = RequireReason(req.Reason);
        var org = rc.OrganizationId!.Value;
        await using var tx = await LockTeamAsync(db, org);
        var actor = await ActorGrantsAsync(db, rc);
        var m = await LoadMemberAsync(db, rc, id);
        EnsureCanManage(rc, actor, m);
        if (m.Status != MembershipStatus.Active) throw new ConflictException("invalid_status", "العضو ليس نشطًا.");
        await EnsureAnotherOwnerAsync(db, m);
        var moved = await MoveWorkAsync(db, market, org, m, req, reason, clock);
        m.Status = MembershipStatus.Suspended;
        m.StatusReason = reason;
        m.StatusChangedAt = clock.UtcNow;
        m.StatusChangedByUserId = rc.UserId;
        RevokeSessions(db, m.Id, clock.UtcNow, "membership_suspended");
        await audit.RecordAsync(new AuditEntry("team.member_suspended", $"إيقاف {m.User!.FullName}", FromState: "Active", ToState: "Suspended", Reason: reason,
            Data: new { moved = moved.Total, reassign = req.Reassign, to = req.ReassignToUserId }, SubjectType: "team_member", SubjectReference: m.Id.ToString()));
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Results.Ok(new { status = m.Status, moved = moved.Total });
    }

    private static async Task<IResult> Reactivate(Guid id, MemberStatusInput req, RahoonDbContext db, RequestContext rc, AuditLog audit, IClock clock)
    {
        var reason = RequireReason(req.Reason);
        var org = rc.OrganizationId!.Value;
        await using var tx = await LockTeamAsync(db, org);
        var actor = await ActorGrantsAsync(db, rc);
        var m = await LoadMemberAsync(db, rc, id);
        EnsureCanManage(rc, actor, m);
        if (m.Status != MembershipStatus.Suspended) throw new ConflictException("invalid_status", "العضو غير موقوف.");
        m.Status = MembershipStatus.Active;
        m.StatusReason = null;
        m.StatusChangedAt = clock.UtcNow;
        m.StatusChangedByUserId = rc.UserId;
        await audit.RecordAsync(new AuditEntry("team.member_reactivated", $"إعادة تفعيل {m.User!.FullName}", FromState: "Suspended", ToState: "Active", Reason: reason,
            SubjectType: "team_member", SubjectReference: m.Id.ToString()));
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Results.Ok(new { status = m.Status });
    }

    private static async Task<IResult> Remove(Guid id, MemberStatusInput req, RahoonDbContext db, RequestContext rc, AuditLog audit, MarketService market, IClock clock)
    {
        var reason = RequireReason(req.Reason);
        var org = rc.OrganizationId!.Value;
        await using var tx = await LockTeamAsync(db, org);
        var actor = await ActorGrantsAsync(db, rc);
        var m = await LoadMemberAsync(db, rc, id);
        EnsureCanManage(rc, actor, m);
        if (m.Status is not (MembershipStatus.Active or MembershipStatus.Suspended)) throw new ConflictException("invalid_status", "العضو أُزيل مسبقًا.");
        await EnsureAnotherOwnerAsync(db, m);
        var moved = await MoveWorkAsync(db, market, org, m, req, reason, clock);
        var from = m.Status;
        // History stays: the membership row, its roles at removal, every audit event and every action the member took.
        m.Status = MembershipStatus.Revoked;
        m.StatusReason = reason;
        m.StatusChangedAt = clock.UtcNow;
        m.StatusChangedByUserId = rc.UserId;
        RevokeSessions(db, m.Id, clock.UtcNow, "membership_removed");
        await audit.RecordAsync(new AuditEntry("team.member_removed", $"إزالة {m.User!.FullName} من الفريق", FromState: from.ToString(), ToState: "Revoked", Reason: reason,
            Data: new { roles = RolesData(m.Roles.Select(r => r.Role!)), moved = moved.Total, reassign = req.Reassign, to = req.ReassignToUserId },
            SubjectType: "team_member", SubjectReference: m.Id.ToString()));
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Results.Ok(new { status = m.Status, moved = moved.Total });
    }

    private static async Task<IResult> Reassign(Guid id, MemberStatusInput req, RahoonDbContext db, RequestContext rc, AuditLog audit, MarketService market, IClock clock)
    {
        var reason = RequireReason(req.Reason);
        var org = rc.OrganizationId!.Value;
        await using var tx = await LockTeamAsync(db, org);
        var actor = await ActorGrantsAsync(db, rc);
        var m = await LoadMemberAsync(db, rc, id);
        EnsureCanManage(rc, actor, m);
        var to = await ReassignTargetAsync(db, org, m, req);
        var moved = await TeamWorkload.ReassignAsync(db, market, m.UserId, m.User!.FullName, to, reason, clock.UtcNow);
        await audit.RecordAsync(new AuditEntry("team.work_reassigned", $"إعادة إسناد أعمال {m.User.FullName} ({moved.Total})", Reason: reason,
            Data: new { moved.SaleRequests, moved.BuyerRequests, moved.Opportunities, moved.Interests, to = to?.Id, toLabel = to?.Label },
            SubjectType: "team_member", SubjectReference: m.Id.ToString()));
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Results.Ok(new { moved = moved.Total });
    }

    // ── Invitations ──

    internal static int InvitationHours(IConfiguration config) => Math.Clamp(config.GetValue("Team:InvitationHours", 72), 1, 24 * 14);

    internal static string JoinLink(string token) => "/join#" + token;

    private static object InvitationDto(StaffInvitation i, IReadOnlyDictionary<Guid, string> roleNames, DateTimeOffset now) => new
    {
        i.Id, i.Email, i.FullName, phoneMasked = Mask.Phone(i.Phone), i.Title,
        roles = i.RoleIds.Select(r => roleNames.GetValueOrDefault(r, "—")),
        status = i.Status == InvitationStatus.Pending && i.ExpiresAt <= now ? "expired" : i.Status.ToString().ToLowerInvariant(),
        i.InvitedByLabel, i.CreatedAt, i.ExpiresAt, i.AcceptedAt, i.RevokedAt,
    };

    private static async Task<IResult> Invitations(RahoonDbContext db, RequestContext rc, IClock clock)
    {
        var org = rc.OrganizationId!.Value;
        var rows = await db.StaffInvitations.Where(i => i.OrganizationId == org).OrderByDescending(i => i.CreatedAt).Take(100).ToListAsync();
        var names = await db.Roles.Where(r => r.OrganizationId == org).ToDictionaryAsync(r => r.Id, r => r.NameAr);
        return Results.Ok(new { items = rows.Select(i => InvitationDto(i, names, clock.UtcNow)), canManage = rc.Has(P.TeamManage) });
    }

    private static string? NormalizeEmail(string? email)
    {
        var e = (email ?? "").Trim().ToLowerInvariant();
        if (e.Length is < 5 or > 254 || e.EndsWith(".invalid")) return null;
        try { return new MailAddress(e).Address == e ? e : null; } catch (FormatException) { return null; }
    }

    private static async Task<IResult> Invite(InviteInput req, RahoonDbContext db, RequestContext rc, AuditLog audit, IClock clock, IConfiguration config)
    {
        var email = NormalizeEmail(req.Email);
        var name = PhoneAuthEndpoints.CleanName(req.FullName);
        var phone = PhoneAuthEndpoints.NormalizeMobile(req.Phone);
        var title = Trim(req.Title, 120);
        new Validator()
            .Require(email is not null, "email", "أدخل بريدًا إلكترونيًا صحيحًا لعمل الموظف.")
            .Require(name is not null, "fullName", "أدخل الاسم الكامل.")
            .Require(phone is not null, "phone", "أدخل رقم جوال سعودي صحيح؛ يصل إليه رمز التحقق عند الدخول.")
            .ThrowIfInvalid();

        var org = rc.OrganizationId!.Value;
        await using var tx = await LockTeamAsync(db, org);
        var actor = await ActorGrantsAsync(db, rc);
        var roles = await LoadAssignableRolesAsync(db, org, req.RoleIds);
        EnsureCanGrant(actor, GrantsOf(roles));

        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email);
        if (user is { AccountKind: AccountKind.Individual }) Validate.Throw("email", "هذا البريد لا يصلح لحساب فريق.");
        if (user is not null && await db.Memberships.FirstOrDefaultAsync(m => m.OrganizationId == org && m.UserId == user.Id) is { } existing
            && existing.Status is MembershipStatus.Active or MembershipStatus.Suspended)
            throw new ConflictException("already_member", existing.Status == MembershipStatus.Suspended
                ? "هذا الشخص عضو موقوف في الفريق؛ أعد تفعيله من صفحته بدل دعوته."
                : "هذا الشخص عضو نشط في الفريق مسبقًا.");
        var now = clock.UtcNow;
        if (await db.StaffInvitations.FirstOrDefaultAsync(i => i.OrganizationId == org && i.Email == email && i.Status == InvitationStatus.Pending) is { } pending)
        {
            if (pending.ExpiresAt > now)
                throw new ConflictException("duplicate_invitation", "توجد دعوة سارية لهذا البريد. جدّد رابطها أو ألغها من قائمة الدعوات.");
            pending.Status = InvitationStatus.Revoked; // an expired link is closed before a new one is issued
            pending.RevokedAt = now;
            pending.RevokedByUserId = rc.UserId;
        }

        var token = Tokens.NewToken();
        var inv = new StaffInvitation
        {
            OrganizationId = org, Email = email!, FullName = name!, Phone = phone!, Title = title, RoleIds = roles.Select(r => r.Id).ToList(),
            TokenHash = Tokens.Sha256(token), InvitedByUserId = rc.UserId, InvitedByLabel = rc.UserName, CreatedAt = now,
            ExpiresAt = now.AddHours(InvitationHours(config)),
        };
        db.StaffInvitations.Add(inv);
        await audit.RecordAsync(new AuditEntry("team.invited", $"دعوة {name} إلى الفريق", Detail: Mask.Email(email!),
            Data: new { roles = RolesData(roles), expiresAt = inv.ExpiresAt }, SubjectType: "team_invitation", SubjectReference: inv.Id.ToString()));
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Results.Ok(InvitationIssued(inv, token));
    }

    /// <summary>The link exists only in this response (the database keeps its hash). Nothing is e-mailed: no provider is configured.</summary>
    private static object InvitationIssued(StaffInvitation inv, string token) => new
    {
        inv.Id, link = JoinLink(token), inv.ExpiresAt, delivered = false,
        delivery = "لم يُرسل أي بريد أو رسالة: لا توجد خدمة بريد مفعّلة. انسخ الرابط وسلّمه للموظف بنفسك عبر قناة موثوقة. يظهر الرابط مرة واحدة فقط.",
    };

    private static async Task<IResult> Renew(Guid id, RahoonDbContext db, RequestContext rc, AuditLog audit, IClock clock, IConfiguration config)
    {
        var org = rc.OrganizationId!.Value;
        await using var tx = await LockTeamAsync(db, org);
        var actor = await ActorGrantsAsync(db, rc);
        var inv = await db.StaffInvitations.FirstOrDefaultAsync(i => i.Id == id && i.OrganizationId == org) ?? throw new NotFoundException();
        if (inv.Status != InvitationStatus.Pending) throw new ConflictException("invalid_status", "لا يمكن تجديد دعوة مقبولة أو ملغاة.");
        var roles = await db.Roles.Include(r => r.Permissions).Where(r => inv.RoleIds.Contains(r.Id)).ToListAsync();
        if (roles.Count != inv.RoleIds.Count || roles.Any(r => r.ArchivedAt is not null))
            throw new ConflictException("roles_changed", "أحد أدوار هذه الدعوة لم يعد متاحًا. ألغها وأرسل دعوة جديدة.");
        EnsureCanGrant(actor, GrantsOf(roles));
        var token = Tokens.NewToken();
        inv.TokenHash = Tokens.Sha256(token); // the previous link stops working
        inv.ExpiresAt = clock.UtcNow.AddHours(InvitationHours(config));
        await audit.RecordAsync(new AuditEntry("team.invitation_renewed", $"تجديد رابط دعوة {inv.FullName}", SubjectType: "team_invitation", SubjectReference: inv.Id.ToString()));
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Results.Ok(InvitationIssued(inv, token));
    }

    private static async Task<IResult> RevokeInvitation(Guid id, RahoonDbContext db, RequestContext rc, AuditLog audit, IClock clock)
    {
        var org = rc.OrganizationId!.Value;
        await using var tx = await LockTeamAsync(db, org);
        await ActorGrantsAsync(db, rc);
        var inv = await db.StaffInvitations.FirstOrDefaultAsync(i => i.Id == id && i.OrganizationId == org) ?? throw new NotFoundException();
        if (inv.Status != InvitationStatus.Pending) throw new ConflictException("invalid_status", "الدعوة مقبولة أو ملغاة مسبقًا.");
        inv.Status = InvitationStatus.Revoked;
        inv.RevokedAt = clock.UtcNow;
        inv.RevokedByUserId = rc.UserId;
        await audit.RecordAsync(new AuditEntry("team.invitation_revoked", $"إلغاء دعوة {inv.FullName}", SubjectType: "team_invitation", SubjectReference: inv.Id.ToString()));
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Results.Ok(new { status = "revoked" });
    }

    // ── Roles ──

    private static object RoleDto(Role r, int members) => new
    {
        r.Id, r.Key, r.NameAr, r.NameEn, r.DescriptionAr, r.IsSystem, archived = r.ArchivedAt is not null, r.ArchivedAt, members, version = r.Version,
        grants = P.Catalog.Where(p => r.Permissions.Any(x => x.PermissionKey == p.Key))
            .Select(p => new { key = p.Key, scope = ScopeLabel(P.IsScopable(p.Key) ? r.Permissions.First(x => x.PermissionKey == p.Key).Scope : GrantScope.All) }),
    };

    private static async Task<Dictionary<Guid, int>> MemberCountsAsync(RahoonDbContext db, Guid org) =>
        await db.MembershipRoles.Where(mr => db.Memberships.Any(m => m.Id == mr.MembershipId && m.OrganizationId == org
                                                                     && (m.Status == MembershipStatus.Active || m.Status == MembershipStatus.Suspended)))
            .GroupBy(mr => mr.RoleId).Select(g => new { g.Key, n = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.n);

    private static async Task<IResult> Roles(RahoonDbContext db, RequestContext rc)
    {
        var org = rc.OrganizationId!.Value;
        var roles = await db.Roles.Include(r => r.Permissions).Where(r => r.OrganizationId == org).ToListAsync();
        var counts = await MemberCountsAsync(db, org);
        return Results.Ok(new
        {
            items = roles.OrderBy(r => r.ArchivedAt is not null).ThenByDescending(r => r.IsSystem).ThenBy(r => r.NameAr)
                .Select(r => RoleDto(r, counts.GetValueOrDefault(r.Id))),
            canManage = rc.Has(P.RolesManage),
        });
    }

    private static async Task<IResult> RoleDetail(Guid id, RahoonDbContext db, RequestContext rc)
    {
        var org = rc.OrganizationId!.Value;
        var r = await db.Roles.Include(x => x.Permissions).FirstOrDefaultAsync(x => x.Id == id && x.OrganizationId == org) ?? throw new NotFoundException();
        var holders = await db.Memberships.Include(m => m.User).Where(m => m.OrganizationId == org && m.Roles.Any(x => x.RoleId == id)
                                                                         && (m.Status == MembershipStatus.Active || m.Status == MembershipStatus.Suspended))
            .OrderBy(m => m.User!.FullName).Select(m => new { m.Id, name = m.User!.FullName, status = m.Status }).ToListAsync();
        var covers = EffectiveAccess.Covers(rc.Grants, GrantsOf([r]));
        string? blocked = r.IsSystem ? "دور نظامي ثابت: لا تتغير صلاحياته. أنشئ دورًا مخصصًا إن احتجت مزيجًا آخر."
            : r.ArchivedAt is not null ? "الدور مؤرشف."
            : !rc.Has(P.RolesManage) ? "تحتاج صلاحية إدارة الأدوار."
            : !covers ? "يتضمن هذا الدور صلاحيات لا تملكها، فلا يمكنك تعديله."
            : null;
        var dto = RoleDto(r, holders.Count);
        return Results.Ok(new
        {
            role = dto, holders,
            actions = new { edit = blocked is null, archive = blocked is null && holders.Count == 0, blocked },
            // What this caller could grant: used by the editor to disable grants beyond their authority.
            grantable = rc.Grants.Where(g => P.GrantableKeys.Contains(g.Key)).ToDictionary(g => g.Key, g => ScopeLabel(g.Value)),
        });
    }

    private static (string NameAr, string NameEn, string? Description, Dictionary<string, GrantScope> Grants) ParseRole(RoleInput req)
    {
        var nameAr = PhoneAuthEndpoints.CleanName(req.NameAr);
        var nameEn = Trim(req.NameEn, 120);
        var v = new Validator().Require(nameAr is not null, "nameAr", "أدخل اسم الدور بالعربية.");
        var grants = new Dictionary<string, GrantScope>();
        foreach (var g in req.Grants ?? [])
        {
            if (g.Key is null || !P.AllKeys.Contains(g.Key)) { v.Require(false, "grants", "صلاحية غير معروفة."); continue; }
            if (!P.GrantableKeys.Contains(g.Key)) { v.Require(false, "grants", $"«{P.Find(g.Key)!.NameAr}» لمرحلة لاحقة ولا يمكن منحها الآن."); continue; }
            var scope = g.Scope switch { "assigned" => GrantScope.Assigned, "all" or null => GrantScope.All, _ => (GrantScope?)null };
            if (scope is null) { v.Require(false, "grants", "النطاق إما «المسند إليه» أو «الكل»."); continue; }
            grants[g.Key] = P.IsScopable(g.Key) ? scope.Value : GrantScope.All;
        }
        v.Require(grants.Count > 0, "grants", "اختر صلاحية واحدة على الأقل.");
        v.ThrowIfInvalid();
        return (nameAr!, nameEn ?? nameAr!, Trim(req.DescriptionAr, 500), grants);
    }

    private static async Task EnsureUniqueNameAsync(RahoonDbContext db, Guid org, string nameAr, Guid? except)
    {
        if (await db.Roles.AnyAsync(r => r.OrganizationId == org && r.NameAr == nameAr && r.Id != except && r.ArchivedAt == null))
            Validate.Throw("nameAr", "يوجد دور بهذا الاسم.");
    }

    private static async Task<IResult> CreateRole(RoleInput req, RahoonDbContext db, RequestContext rc, AuditLog audit)
    {
        var (nameAr, nameEn, description, grants) = ParseRole(req);
        var org = rc.OrganizationId!.Value;
        await using var tx = await LockTeamAsync(db, org);
        var actor = await ActorGrantsAsync(db, rc, P.RolesManage);
        EnsureCanGrant(actor, grants);
        await EnsureUniqueNameAsync(db, org, nameAr, null);
        var role = new Role { OrganizationId = org, Key = "custom_" + Guid.NewGuid().ToString("N")[..10], NameAr = nameAr, NameEn = nameEn, DescriptionAr = description };
        role.Permissions.AddRange(grants.Select(g => new RolePermission { RoleId = role.Id, PermissionKey = g.Key, Scope = g.Value }));
        db.Roles.Add(role);
        await audit.RecordAsync(new AuditEntry("role.created", $"إنشاء الدور «{nameAr}»",
            Data: new { grants = grants.ToDictionary(g => g.Key, g => ScopeLabel(g.Value)) }, SubjectType: "role", SubjectReference: role.Id.ToString()));
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Results.Ok(new { role.Id });
    }

    private static async Task<IResult> UpdateRole(Guid id, RoleInput req, RahoonDbContext db, RequestContext rc, AuditLog audit)
    {
        var (nameAr, nameEn, description, grants) = ParseRole(req);
        var org = rc.OrganizationId!.Value;
        await using var tx = await LockTeamAsync(db, org);
        var actor = await ActorGrantsAsync(db, rc, P.RolesManage);
        var role = await db.Roles.Include(r => r.Permissions).FirstOrDefaultAsync(r => r.Id == id && r.OrganizationId == org) ?? throw new NotFoundException();
        if (role.IsSystem) throw new ConflictException("system_role", "دور نظامي ثابت: لا تتغير صلاحياته. أنشئ دورًا مخصصًا بدلًا منه.");
        if (role.ArchivedAt is not null) throw new ConflictException("archived", "الدور مؤرشف.");
        if (req.Version is { } v && v != role.Version)
            throw new ConflictException("concurrency", "عدّل شخص آخر هذا الدور منذ فتحته. حدّث الصفحة وراجع التغييرات.");
        var before = role.Permissions.ToDictionary(p => p.PermissionKey, p => p.Scope);
        // To edit a role you must hold everything it grants now and everything it will grant.
        EnsureCanGrant(actor, EffectiveAccess.Grants([role]));
        EnsureCanGrant(actor, grants);
        await EnsureUniqueNameAsync(db, org, nameAr, role.Id);
        role.NameAr = nameAr;
        role.NameEn = nameEn;
        role.DescriptionAr = description;
        role.Permissions.RemoveAll(p => !grants.ContainsKey(p.PermissionKey));
        foreach (var p in role.Permissions) p.Scope = grants[p.PermissionKey];
        foreach (var (key, scope) in grants.Where(g => role.Permissions.All(p => p.PermissionKey != g.Key)))
            role.Permissions.Add(new RolePermission { RoleId = role.Id, PermissionKey = key, Scope = scope });
        await audit.RecordAsync(new AuditEntry("role.updated", $"تعديل الدور «{nameAr}»",
            Data: new
            {
                before = before.ToDictionary(g => g.Key, g => ScopeLabel(g.Value)),
                after = grants.ToDictionary(g => g.Key, g => ScopeLabel(g.Value)),
            },
            SubjectType: "role", SubjectReference: role.Id.ToString()));
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Results.Ok(new { role.Id });
    }

    private static async Task<IResult> ArchiveRole(Guid id, ArchiveRoleInput req, RahoonDbContext db, RequestContext rc, AuditLog audit, IClock clock)
    {
        var org = rc.OrganizationId!.Value;
        await using var tx = await LockTeamAsync(db, org);
        var actor = await ActorGrantsAsync(db, rc, P.RolesManage);
        var role = await db.Roles.Include(r => r.Permissions).FirstOrDefaultAsync(r => r.Id == id && r.OrganizationId == org) ?? throw new NotFoundException();
        if (role.IsSystem) throw new ConflictException("system_role", "لا تُؤرشف الأدوار النظامية.");
        if (role.ArchivedAt is not null) return Results.Ok(new { archived = true });
        EnsureCanGrant(actor, EffectiveAccess.Grants([role]));
        var holders = await db.Memberships.Include(m => m.User).Where(m => m.OrganizationId == org && m.Roles.Any(x => x.RoleId == id)
                                                                         && (m.Status == MembershipStatus.Active || m.Status == MembershipStatus.Suspended))
            .Select(m => m.User!.FullName).ToListAsync();
        if (holders.Count > 0)
            throw new DomainException("role_in_use", "الدور مسند لأعضاء. غيّر أدوارهم أولًا ثم أرشفه.", StatusCodes.Status409Conflict, holders);
        if (await db.StaffInvitations.AnyAsync(i => i.OrganizationId == org && i.Status == InvitationStatus.Pending && i.RoleIds.Contains(id)))
            throw new ConflictException("role_in_invitation", "الدور مستخدم في دعوة سارية. ألغ الدعوة أولًا.");
        role.ArchivedAt = clock.UtcNow;
        await audit.RecordAsync(new AuditEntry("role.archived", $"أرشفة الدور «{role.NameAr}»", Reason: Trim(req.Reason, 500), SubjectType: "role", SubjectReference: role.Id.ToString()));
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Results.Ok(new { archived = true });
    }

    // ── Administration log ──

    private static readonly Dictionary<string, string> AuditGroups = new()
    {
        ["team"] = "team.", ["role"] = "role.", ["auth"] = "auth.", ["directory"] = "directory.", ["market"] = "market.",
    };

    /// <summary>Who did what in the team's chain. Event data is shown only for administration events (roles, grants); it never holds secrets.</summary>
    private static async Task<IResult> AuditTrail(RahoonDbContext db, RequestContext rc, string? group, string? q, int? page)
    {
        var org = rc.OrganizationId!.Value;
        var query = db.AuditEvents.Where(a => a.OrganizationId == org);
        if (group is not null && AuditGroups.TryGetValue(group, out var prefix)) query = query.Where(a => a.Type.StartsWith(prefix));
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(a => a.Title.Contains(term) || (a.ActorLabel != null && a.ActorLabel.Contains(term)) || (a.SubjectReference != null && a.SubjectReference.Contains(term)));
        }
        var total = await query.CountAsync();
        var p = Math.Max(1, page ?? 1);
        var rows = await query.OrderByDescending(a => a.Seq).Skip((p - 1) * 50).Take(50).ToListAsync();
        return Results.Ok(new
        {
            items = rows.Select(a => new
            {
                a.Seq, a.Type, a.Title, a.ActorLabel, a.ActorRole, a.SubjectType, a.SubjectReference, a.Reason, a.Detail, a.FromState, a.ToState,
                a.OccurredAt, a.Blocked, data = a.Type.StartsWith("team.") || a.Type.StartsWith("role.") ? a.DataJson : null,
            }),
            total, page = p, pages = (int)Math.Ceiling(total / 50.0),
        });
    }

    private static string? Trim(string? s, int max) => s?.Trim() is { Length: > 0 } t ? t[..Math.Min(t.Length, max)] : null;
}
