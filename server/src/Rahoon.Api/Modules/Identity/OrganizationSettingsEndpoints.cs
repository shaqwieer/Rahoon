using Microsoft.EntityFrameworkCore;
using Rahoon.Api.Infrastructure.Http;
using Rahoon.Api.Infrastructure.Persistence;
using Rahoon.Api.Infrastructure.Security;
using Rahoon.Api.Infrastructure.Tenancy;
using Rahoon.Api.Infrastructure.Time;
using Rahoon.Api.Modules.Audit;
using Rahoon.Api.Modules.Communications;

namespace Rahoon.Api.Modules.Identity;

public sealed record OrganizationSettingsRequest(string NameAr, string? City, string DefaultOwnerLanguage, int IdleTimeoutMinutes, List<string>? AllowedEmailDomains, bool? MfaRequired);
public sealed record ReasonRequest(string? Reason);
public sealed record RoleChangeCreateRequest(string RoleKey, string? Reason);
public sealed record PendingRoleChange(Guid Id, string? ToRole, bool RequestedBySelf);
public sealed record UserRow(
    Guid? MembershipId, Guid? InvitationId, string Name, string Email, string? Role, List<string> RoleKeys, string? Team, string Mfa, string MfaTone,
    string Status, string StatusLabel, bool IsSelf, DateTimeOffset? InvitationExpiresAt, PendingRoleChange? PendingRoleChange);

/// <summary>Institution admin A01 (organization settings) and A02 (users, roles, permission matrix, maker-checker role changes).</summary>
public static class OrganizationSettingsEndpoints
{
    /// <summary>Platform bounds for the institution idle timeout (A01; assumption — B2 default 30 minutes).</summary>
    public const int MinIdleMinutes = 10, MaxIdleMinutes = 60;

    public static void Map(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/settings").RequireOrg(OrganizationKind.Lender);
        g.MapGet("/organization", GetOrganization).RequirePermission(P.OrgSettings);
        g.MapPut("/organization", PutOrganization).RequirePermission(P.OrgSettings).Idempotent();

        g.MapGet("/users", Users).RequireAnyPermission(P.UserManage, P.RoleChangeApprove);
        g.MapGet("/roles", Roles).RequireAnyPermission(P.UserManage, P.RoleChangeApprove);
        g.MapPost("/users/{membershipId:guid}/suspend", Suspend).RequirePermission(P.UserManage).Idempotent();
        g.MapPost("/users/{membershipId:guid}/reactivate", Reactivate).RequirePermission(P.UserManage).Idempotent();
        g.MapPost("/users/{membershipId:guid}/role-change-requests", RequestRoleChange).RequirePermission(P.UserManage).Idempotent();
        g.MapGet("/role-change-requests", RoleChangeRequests).RequireAnyPermission(P.UserManage, P.RoleChangeApprove);
        g.MapPost("/role-change-requests/{id:guid}/approve", ApproveRoleChange).RequirePermission(P.RoleChangeApprove).Idempotent();
        g.MapPost("/role-change-requests/{id:guid}/reject", RejectRoleChange).RequirePermission(P.RoleChangeApprove).Idempotent();
        g.MapGet("/permission-matrix", PermissionMatrix).RequireAnyPermission(P.UserManage, P.RoleChangeApprove, P.OrgSettings);
    }

    // ───────── A01 ─────────

    private static async Task<IResult> GetOrganization(RahoonDbContext db, RequestContext rc)
    {
        var o = await db.Organizations.AsNoTracking().FirstAsync(x => x.Id == rc.OrganizationId);
        return Results.Ok(new
        {
            o.Id, name = o.NameAr, o.NameEn, o.City, o.ShortCode, o.LicenseNumber, defaultOwnerLanguage = o.DefaultOwnerLanguage,
            idleTimeoutMinutes = o.IdleTimeoutMinutes, idleTimeoutBounds = new { min = MinIdleMinutes, max = MaxIdleMinutes },
            mfaRequired = true, mfaPolicyLabel = "إلزامي لكل الأدوار", allowedEmailDomains = o.AllowedEmailDomains, status = o.Status,
        });
    }

    private static async Task<IResult> PutOrganization(OrganizationSettingsRequest req, RahoonDbContext db, RequestContext rc, AuditLog audit)
    {
        var domains = (req.AllowedEmailDomains ?? []).Select(d => d.Trim().ToLowerInvariant()).Where(d => d.Length > 0).Distinct().ToList();
        new Validator()
            .Require(!string.IsNullOrWhiteSpace(req.NameAr) && req.NameAr.Trim().Length <= 200, "nameAr", "أدخل اسم المنشأة.")
            .Require(req.DefaultOwnerLanguage is "ar" or "en", "defaultOwnerLanguage", "اختر العربية أو الإنجليزية.")
            .Require(req.IdleTimeoutMinutes is >= MinIdleMinutes and <= MaxIdleMinutes, "idleTimeoutMinutes", $"مهلة الخمول بين {MinIdleMinutes} و{MaxIdleMinutes} دقيقة (حدود المنصة).")
            .Require(req.MfaRequired != false, "mfaRequired", "التحقق بخطوتين إلزامي لكل الأدوار ولا يمكن تعطيله.")
            .Require(domains.Count is >= 1 and <= 5, "allowedEmailDomains", "حدد نطاق بريد واحداً على الأقل (حتى 5).")
            .Require(domains.All(EmailDomains.IsValidDomain), "allowedEmailDomains", "نطاق بريد غير صالح.")
            .Require(!domains.Any(EmailDomains.IsPersonal), "allowedEmailDomains", "لا تُقبل نطاقات البريد الشخصي.")
            .ThrowIfInvalid();

        await using var tx = await db.Database.BeginTransactionAsync();
        var o = await db.Organizations.FirstAsync(x => x.Id == rc.OrganizationId);
        var changes = new List<string>();
        if (o.NameAr != req.NameAr.Trim()) changes.Add("الاسم");
        if (o.City != req.City?.Trim()) changes.Add("المدينة");
        if (o.DefaultOwnerLanguage != req.DefaultOwnerLanguage) changes.Add("لغة المالك");
        if (o.IdleTimeoutMinutes != req.IdleTimeoutMinutes) changes.Add($"مهلة الخمول {o.IdleTimeoutMinutes}→{req.IdleTimeoutMinutes}");
        if (!o.AllowedEmailDomains.SequenceEqual(domains)) changes.Add("نطاقات البريد");
        o.NameAr = req.NameAr.Trim();
        o.City = req.City?.Trim();
        o.DefaultOwnerLanguage = req.DefaultOwnerLanguage;
        o.IdleTimeoutMinutes = req.IdleTimeoutMinutes;
        o.AllowedEmailDomains = domains;
        o.MfaRequired = true;
        await audit.RecordAsync(new AuditEntry("org.settings_updated", "تعديل إعدادات المنشأة", Detail: changes.Count == 0 ? "دون تغيير" : string.Join(" · ", changes)));
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Results.Ok(new { saved = true, changes });
    }

    // ───────── A02 ─────────

    private static string StatusLabel(MembershipStatus s) => s switch
    {
        MembershipStatus.Active => "نشط",
        MembershipStatus.Invited => "دعوة معلقة",
        MembershipStatus.Suspended => "معلّق",
        _ => "ملغى",
    };

    private static async Task<IResult> Users(RahoonDbContext db, RequestContext rc, IClock clock)
    {
        var now = clock.UtcNow;
        var members = await db.Memberships.AsNoTracking().Where(m => m.OrganizationId == rc.OrganizationId && m.Status != MembershipStatus.Revoked)
            .Include(m => m.User).Include(m => m.Team).Include(m => m.Roles).ThenInclude(r => r.Role)
            .OrderBy(m => m.CreatedAt).ToListAsync();
        var pendingChanges = await db.RoleChangeRequests.AsNoTracking().Where(r => r.OrganizationId == rc.OrganizationId && r.Status == RoleChangeStatus.Pending).ToListAsync();
        var roles = await db.Roles.AsNoTracking().Where(r => r.OrganizationId == rc.OrganizationId).ToDictionaryAsync(r => r.Id, r => r.NameAr);
        var memberEmails = members.Select(m => m.User!.Email).ToHashSet();
        var invitations = await db.Invitations.AsNoTracking()
            .Where(i => i.OrganizationId == rc.OrganizationId && i.Status == InvitationStatus.Pending).ToListAsync();
        var teams = await db.Teams.AsNoTracking().Where(t => t.OrganizationId == rc.OrganizationId).ToDictionaryAsync(t => t.Id, t => t.NameAr);

        var rows = members.Select(m =>
        {
            var change = pendingChanges.FirstOrDefault(r => r.MembershipId == m.Id);
            var invite = invitations.FirstOrDefault(i => i.Email == m.User!.Email);
            return new UserRow(m.Id, invite?.Id, m.User!.FullName, m.User.Email,
                m.Title ?? m.Roles.Select(r => r.Role!.NameAr).FirstOrDefault(), m.Roles.Select(r => r.Role!.Key).ToList(),
                m.Team?.NameAr, m.User.MfaEnrolled ? "مفعّل" : "لم يُفعّل", m.User.MfaEnrolled ? "success" : "error",
                m.Status.ToString().ToLowerInvariant(), StatusLabel(m.Status), m.UserId == rc.UserId, invite?.ExpiresAt,
                change is null ? null : new PendingRoleChange(change.Id, roles.GetValueOrDefault(change.ToRoleId), change.RequestedByUserId == rc.UserId));
        }).ToList();
        rows.AddRange(invitations.Where(i => !memberEmails.Contains(i.Email)).Select(i => new UserRow(
            null, i.Id, i.FullName, i.Email, roles.GetValueOrDefault(i.RoleId), [], i.TeamId is { } t ? teams.GetValueOrDefault(t) : null,
            "لم يُفعّل", "error", i.ExpiresAt <= now ? "expired" : "invited", i.ExpiresAt <= now ? "دعوة منتهية" : "دعوة معلقة", false, i.ExpiresAt, null)));
        return Results.Ok(rows);
    }

    private static async Task<IResult> Roles(RahoonDbContext db, RequestContext rc) =>
        Results.Ok(new
        {
            roles = await db.Roles.AsNoTracking().Where(r => r.OrganizationId == rc.OrganizationId).OrderBy(r => r.NameAr).Select(r => new { r.Id, r.Key, name = r.NameAr }).ToListAsync(),
            teams = await db.Teams.AsNoTracking().Where(t => t.OrganizationId == rc.OrganizationId).OrderBy(t => t.NameAr).Select(t => new { t.Id, name = t.NameAr }).ToListAsync(),
        });

    private static async Task<Membership> MemberAsync(RahoonDbContext db, RequestContext rc, Guid membershipId) =>
        await db.Memberships.Include(m => m.User).Include(m => m.Roles).ThenInclude(r => r.Role)
            .FirstOrDefaultAsync(m => m.Id == membershipId && m.OrganizationId == rc.OrganizationId) ?? throw new NotFoundException();

    private static async Task<IResult> Suspend(Guid membershipId, ReasonRequest req, RahoonDbContext db, RequestContext rc, IClock clock, AuditLog audit)
    {
        new Validator().Require(!string.IsNullOrWhiteSpace(req.Reason) && req.Reason.Trim().Length <= 500, "reason", "اكتب سبب التعليق.").ThrowIfInvalid();
        await using var tx = await db.Database.BeginTransactionAsync();
        var m = await MemberAsync(db, rc, membershipId);
        if (m.UserId == rc.UserId) throw new DomainException("self_action", "لا يمكنك تعليق حسابك.", StatusCodes.Status403Forbidden);
        if (m.Status != MembershipStatus.Active) throw new ConflictException("not_active", "المستخدم ليس نشطاً.");
        if (m.Roles.Any(r => r.Role!.Key == SystemRoles.OrgAdmin)
            && !await db.Memberships.AnyAsync(x => x.OrganizationId == m.OrganizationId && x.Id != m.Id && x.Status == MembershipStatus.Active && x.Roles.Any(r => r.Role!.Key == SystemRoles.OrgAdmin)))
            throw new ConflictException("last_admin", "لا يمكن تعليق آخر مسؤول نشط للمنشأة.");
        m.Status = MembershipStatus.Suspended;
        var sessions = await db.Sessions.Where(s => s.MembershipId == m.Id && s.RevokedAt == null).ToListAsync();
        foreach (var s in sessions) { s.RevokedAt = clock.UtcNow; s.RevokedReason = "membership_suspended"; }
        await audit.RecordAsync(new AuditEntry("user.suspended", $"تعليق مستخدم: {m.User!.FullName}", Reason: req.Reason!.Trim(), Detail: $"إنهاء {sessions.Count} جلسة"));
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Results.Ok(new { status = "suspended", revokedSessions = sessions.Count });
    }

    private static async Task<IResult> Reactivate(Guid membershipId, ReasonRequest req, RahoonDbContext db, RequestContext rc, AuditLog audit)
    {
        new Validator().Require(!string.IsNullOrWhiteSpace(req.Reason), "reason", "اكتب سبب إعادة التفعيل.").ThrowIfInvalid();
        await using var tx = await db.Database.BeginTransactionAsync();
        var m = await MemberAsync(db, rc, membershipId);
        if (m.Status != MembershipStatus.Suspended) throw new ConflictException("not_suspended", "المستخدم ليس معلّقاً.");
        m.Status = MembershipStatus.Active;
        await audit.RecordAsync(new AuditEntry("user.reactivated", $"إعادة تفعيل مستخدم: {m.User!.FullName}", Reason: req.Reason!.Trim()));
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Results.Ok(new { status = "active" });
    }

    private static async Task<IResult> RequestRoleChange(Guid membershipId, RoleChangeCreateRequest req, RahoonDbContext db, RequestContext rc, AuditLog audit, Notifier notifier)
    {
        await using var tx = await db.Database.BeginTransactionAsync();
        var m = await MemberAsync(db, rc, membershipId);
        var to = await db.Roles.FirstOrDefaultAsync(r => r.OrganizationId == rc.OrganizationId && r.Key == req.RoleKey);
        new Validator()
            .Require(to is not null, "roleKey", "اختر دوراً من أدوار المنشأة.")
            .Require(to is null || m.Roles.All(r => r.RoleId != to.Id), "roleKey", "المستخدم يحمل هذا الدور بالفعل.")
            .Require(!string.IsNullOrWhiteSpace(req.Reason), "reason", "اكتب سبب تغيير الدور.")
            .ThrowIfInvalid();
        if (m.UserId == rc.UserId) throw new DomainException("self_action", "لا يمكنك طلب تغيير دورك.", StatusCodes.Status403Forbidden);
        if (m.Status != MembershipStatus.Active) throw new ConflictException("not_active", "المستخدم ليس نشطاً.");
        if (await db.RoleChangeRequests.AnyAsync(r => r.MembershipId == m.Id && r.Status == RoleChangeStatus.Pending))
            throw new ConflictException("pending_exists", "يوجد طلب تغيير دور معلق لهذا المستخدم.");
        var from = m.Roles.FirstOrDefault()?.Role;
        var request = new RoleChangeRequest
        {
            OrganizationId = m.OrganizationId, MembershipId = m.Id, FromRoleId = from?.Id, ToRoleId = to!.Id, Reason = req.Reason!.Trim(),
            Status = RoleChangeStatus.Pending, RequestedByUserId = rc.UserId, CreatedAt = DateTimeOffset.UtcNow,
        };
        db.RoleChangeRequests.Add(request);
        var approvers = await db.Memberships.Where(x => x.OrganizationId == rc.OrganizationId && x.Status == MembershipStatus.Active && x.UserId != rc.UserId
                                                        && x.Roles.Any(r => r.Role!.Permissions.Any(p => p.PermissionKey == P.RoleChangeApprove)))
            .Select(x => x.UserId).ToListAsync();
        foreach (var u in approvers)
            notifier.Notify(u, rc.OrganizationId, "admin", "طلب تغيير دور بانتظار موافقتك", $"{m.User!.FullName}: {from?.NameAr ?? "—"} ← {to.NameAr}", "/settings/users");
        await audit.RecordAsync(new AuditEntry("user.role_change_requested", $"طلب تغيير دور {m.User!.FullName}", Reason: request.Reason, Detail: $"{from?.NameAr ?? "—"} ← {to.NameAr} · بانتظار موافقة ثانية"));
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Results.Ok(new { request.Id, status = request.Status });
    }

    private static async Task<IResult> RoleChangeRequests(RahoonDbContext db, RequestContext rc)
    {
        var list = await db.RoleChangeRequests.AsNoTracking().Where(r => r.OrganizationId == rc.OrganizationId).OrderByDescending(r => r.CreatedAt).Take(100)
            .Select(r => new
            {
                r.Id, r.Status, r.Reason, r.CreatedAt, r.DecidedAt,
                user = db.Memberships.Where(m => m.Id == r.MembershipId).Select(m => m.User!.FullName).FirstOrDefault(),
                fromRole = db.Roles.Where(x => x.Id == r.FromRoleId).Select(x => x.NameAr).FirstOrDefault(),
                toRole = db.Roles.Where(x => x.Id == r.ToRoleId).Select(x => x.NameAr).FirstOrDefault(),
                requestedBy = db.Users.Where(u => u.Id == r.RequestedByUserId).Select(u => u.FullName).FirstOrDefault(),
                decidedBy = db.Users.Where(u => u.Id == r.DecidedByUserId).Select(u => u.FullName).FirstOrDefault(),
                canDecide = r.Status == RoleChangeStatus.Pending && r.RequestedByUserId != rc.UserId,
            }).ToListAsync();
        return Results.Ok(list);
    }

    private static async Task<IResult> ApproveRoleChange(Guid id, RahoonDbContext db, RequestContext rc, IClock clock, AuditLog audit, Notifier notifier)
    {
        EndpointAccess.EnsureStepUp(rc, clock);
        await using var tx = await db.Database.BeginTransactionAsync();
        var r = await db.RoleChangeRequests.FirstOrDefaultAsync(x => x.Id == id && x.OrganizationId == rc.OrganizationId) ?? throw new NotFoundException();
        if (r.Status != RoleChangeStatus.Pending) throw new ConflictException("already_decided", "قُرر هذا الطلب مسبقاً.");
        var m = await MemberAsync(db, rc, r.MembershipId);
        if (r.RequestedByUserId == rc.UserId)
            throw new DomainException("maker_checker", "لا يعتمد مقدم الطلب طلبه؛ تغيير الدور يتطلب موافقة مسؤول ثانٍ.", StatusCodes.Status403Forbidden);
        if (m.UserId == rc.UserId) throw new DomainException("self_action", "لا تعتمد تغيير دورك.", StatusCodes.Status403Forbidden);
        if (m.Status != MembershipStatus.Active) throw new ConflictException("not_active", "المستخدم لم يعد نشطاً.");
        var to = await db.Roles.FirstAsync(x => x.Id == r.ToRoleId);
        var from = m.Roles.Select(x => x.Role!.NameAr).FirstOrDefault();
        if (m.Roles.Count != 1 || m.Roles[0].RoleId != to.Id)
        {
            m.Roles.Clear();
            m.Roles.Add(new MembershipRole { MembershipId = m.Id, RoleId = to.Id });
        }
        m.Title = to.NameAr;
        r.Status = RoleChangeStatus.Approved;
        r.DecidedByUserId = rc.UserId;
        r.DecidedAt = clock.UtcNow;
        notifier.Notify(r.RequestedByUserId, rc.OrganizationId, "admin", "اعتُمد تغيير الدور", $"{m.User!.FullName}: {to.NameAr}", "/settings/users", tone: "ok");
        await audit.RecordAsync(new AuditEntry("user.role_changed", $"تغيير دور {m.User.FullName}", FromState: from, ToState: to.NameAr, Reason: r.Reason,
            Detail: "موافقة مسؤول ثانٍ", Evidence: [$"طلب {r.Id}"]));
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Results.Ok(new { status = r.Status, role = to.Key });
    }

    private static async Task<IResult> RejectRoleChange(Guid id, ReasonRequest req, RahoonDbContext db, RequestContext rc, IClock clock, AuditLog audit)
    {
        new Validator().Require(!string.IsNullOrWhiteSpace(req.Reason), "reason", "اكتب سبب الرفض.").ThrowIfInvalid();
        await using var tx = await db.Database.BeginTransactionAsync();
        var r = await db.RoleChangeRequests.FirstOrDefaultAsync(x => x.Id == id && x.OrganizationId == rc.OrganizationId) ?? throw new NotFoundException();
        if (r.Status != RoleChangeStatus.Pending) throw new ConflictException("already_decided", "قُرر هذا الطلب مسبقاً.");
        if (r.RequestedByUserId == rc.UserId)
            throw new DomainException("maker_checker", "لا يقرر مقدم الطلب طلبه.", StatusCodes.Status403Forbidden);
        r.Status = RoleChangeStatus.Rejected;
        r.DecidedByUserId = rc.UserId;
        r.DecidedAt = clock.UtcNow;
        await audit.RecordAsync(new AuditEntry("user.role_change_rejected", "رفض طلب تغيير دور", Reason: req.Reason!.Trim()));
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Results.Ok(new { status = r.Status });
    }

    /// <summary>A02 matrix: institution roles × catalog (institution/case scope), tri-state with an accessible label per cell.</summary>
    private static async Task<IResult> PermissionMatrix(RahoonDbContext db, RequestContext rc)
    {
        var roles = await db.Roles.AsNoTracking().Include(r => r.Permissions).Where(r => r.OrganizationId == rc.OrganizationId).ToListAsync();
        var order = SystemRoles.Templates.Select((t, i) => (t.Key, i)).ToDictionary(x => x.Key, x => x.i);
        roles = roles.OrderBy(r => order.GetValueOrDefault(r.Key, 99)).ToList();
        var rows = P.Catalog.Where(p => p.Scope != PermissionScope.Platform).Select(p => new
        {
            key = p.Key, name = p.NameAr, group = p.Group, sensitivity = p.Sensitivity.ToString().ToLowerInvariant(), condition = p.ConditionAr,
            cells = roles.Select(r =>
            {
                var grant = r.Permissions.FirstOrDefault(x => x.PermissionKey == p.Key)?.Grant;
                var value = grant switch { PermissionGrant.Allow => "allow", PermissionGrant.Conditional => "conditional", _ => "deny" };
                var label = value switch { "allow" => "مسموح", "conditional" => "بشرط", _ => "غير مسموح" };
                return new { role = r.Key, grant = value, ariaLabel = $"{r.NameAr}: {label}" };
            }),
        });
        return Results.Ok(new
        {
            roles = roles.Select(r => new { r.Key, name = r.NameAr }),
            rows,
            legend = "✓ مسموح · ◐ بشرط (حد أو ليس المُعِدّ) · — غير مسموح. تغيير الأدوار يتطلب موافقة ثانية ويُسجل.",
        });
    }
}
