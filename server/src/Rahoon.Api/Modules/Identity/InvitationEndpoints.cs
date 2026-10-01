using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Rahoon.Api.Infrastructure.Http;
using Rahoon.Api.Infrastructure.Persistence;
using Rahoon.Api.Infrastructure.Security;
using Rahoon.Api.Infrastructure.Tenancy;
using Rahoon.Api.Infrastructure.Time;
using Rahoon.Api.Modules.Audit;

namespace Rahoon.Api.Modules.Identity;

public sealed record InvitationTokenInput(string? Token);
public sealed record AcceptInvitationInput(string? Token, string? Password, string? CurrentPassword);

/// <summary>
/// Joining «فريق رهون» from an invitation link (`/join#token`; the fragment never reaches server logs). Both calls are
/// POSTs without a session (CSRF-exempt, Origin still checked, rate-limited). The invitation fixes the e-mail, mobile and
/// roles: the person only sets a password (or proves an existing staff account's password). Expired, revoked and used
/// links fail with the same answer.
/// </summary>
public static class InvitationEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/auth/invitations");
        g.MapPost("/lookup", Lookup).RequireRateLimiting("auth");
        g.MapPost("/accept", Accept).RequireRateLimiting("auth");
    }

    private static IResult Invalid() => Results.Problem(
        title: "رابط الدعوة غير صالح: ربما انتهت مدته أو استُخدم أو أُلغي. اطلب من مسؤول الفريق رابطًا جديدًا.",
        statusCode: StatusCodes.Status410Gone, extensions: new Dictionary<string, object?> { ["code"] = "invitation_invalid" });

    private static async Task<StaffInvitation?> FindAsync(RahoonDbContext db, string? token, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > 100) return null;
        var hash = Tokens.Sha256(token.Trim());
        var inv = await db.StaffInvitations.FirstOrDefaultAsync(i => i.TokenHash == hash);
        return inv is { Status: InvitationStatus.Pending } && inv.ExpiresAt > now ? inv : null;
    }

    private static async Task<IResult> Lookup(InvitationTokenInput req, RahoonDbContext db, RequestContext rc, IClock clock)
    {
        using var _ = rc.BeginSystemScope();
        var inv = await FindAsync(db, req.Token, clock.UtcNow);
        if (inv is null) return Invalid();
        var roles = await db.Roles.Where(r => inv.RoleIds.Contains(r.Id)).Select(r => r.NameAr).ToListAsync();
        var org = await db.Organizations.Where(o => o.Id == inv.OrganizationId).Select(o => o.NameAr).FirstAsync();
        var existing = await db.Users.AnyAsync(u => u.Email == inv.Email && u.AccountKind == AccountKind.Staff && u.PasswordHash != null);
        return Results.Ok(new
        {
            organization = org, inv.FullName, inv.Email, phoneMasked = Mask.Phone(inv.Phone), inv.Title, roles, inv.ExpiresAt, inv.InvitedByLabel,
            existingAccount = existing,
        });
    }

    internal static string? PasswordProblem(string? password) =>
        password is null || password.Length < 10 ? "كلمة المرور 10 أحرف على الأقل."
        : password.Length > 128 ? "كلمة المرور أطول من المسموح."
        : !password.Any(char.IsLetter) || !password.Any(char.IsDigit) ? "استخدم حروفًا وأرقامًا معًا في كلمة المرور."
        : null;

    private static async Task<IResult> Accept(AcceptInvitationInput req, RahoonDbContext db, RequestContext rc, IClock clock, IPasswordHasher<User> hasher, AuditLog audit)
    {
        using var _ = rc.BeginSystemScope();
        var now = clock.UtcNow;
        await using var tx = await db.Database.BeginTransactionAsync();
        var inv = await FindAsync(db, req.Token, now);
        if (inv is null) return Invalid();
        var key = "team-admin:" + inv.OrganizationId;
        await db.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock(hashtext({key}))");
        await db.Entry(inv).ReloadAsync();
        if (inv.Status != InvitationStatus.Pending || inv.ExpiresAt <= now) return Invalid();

        // The roles are the inviter's choice, re-checked now: still present, and still within what the inviter may grant.
        var roles = await db.Roles.Include(r => r.Permissions).Where(r => inv.RoleIds.Contains(r.Id)).ToListAsync();
        if (roles.Count != inv.RoleIds.Count || roles.Any(r => r.ArchivedAt is not null))
            throw new ConflictException("invitation_stale", "تغيّرت أدوار هذه الدعوة منذ إرسالها. اطلب دعوة جديدة.");
        var bootstrap = inv.InvitedByUserId == Guid.Empty;
        if (bootstrap)
        {
            if (await TeamBootstrap.HasActiveOwnerAsync(db, inv.OrganizationId))
                throw new ConflictException("invitation_stale", "للفريق مالك نشط الآن؛ اطلب منه دعوة.");
        }
        else
        {
            var inviter = await db.Memberships.Include(m => m.Roles).ThenInclude(r => r.Role!).ThenInclude(r => r.Permissions)
                .FirstOrDefaultAsync(m => m.OrganizationId == inv.OrganizationId && m.UserId == inv.InvitedByUserId && m.Status == MembershipStatus.Active);
            var inviterGrants = inviter is null ? new Dictionary<string, GrantScope>() : EffectiveAccess.Grants(inviter.Roles.Select(r => r.Role!));
            if (!inviterGrants.ContainsKey(P.TeamManage) || !EffectiveAccess.Covers(inviterGrants, EffectiveAccess.Grants(roles)))
                throw new ConflictException("invitation_stale", "لم يعد لمن أرسل الدعوة صلاحية منح هذه الأدوار. اطلب دعوة جديدة.");
        }

        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == inv.Email);
        if (user is { AccountKind: AccountKind.Individual }) return Invalid();
        if (user is null)
        {
            if (PasswordProblem(req.Password) is { } problem) Validate.Throw("password", problem);
            user = new User { Email = inv.Email, FullName = inv.FullName, Phone = inv.Phone, AccountKind = AccountKind.Staff, CreatedAt = now };
            user.PasswordHash = hasher.HashPassword(user, req.Password!);
            user.PasswordChangedAt = now;
            db.Users.Add(user);
        }
        else
        {
            // A returning staff account proves it is theirs; the invitation never resets someone else's password.
            var ok = user.PasswordHash is not null && hasher.VerifyHashedPassword(user, user.PasswordHash, req.CurrentPassword ?? "") != PasswordVerificationResult.Failed;
            if (!ok) Validate.Throw("currentPassword", "كلمة المرور الحالية لحسابك غير صحيحة.");
            if (user.Status != UserStatus.Active) throw new ConflictException("account_disabled", "حسابك موقوف. تواصل مع مسؤول الفريق.");
            user.Phone ??= inv.Phone;
        }

        var m = await db.Memberships.Include(x => x.Roles).FirstOrDefaultAsync(x => x.OrganizationId == inv.OrganizationId && x.UserId == user.Id);
        if (m is { Status: MembershipStatus.Active or MembershipStatus.Suspended })
            throw new ConflictException("already_member", "أنت عضو في الفريق مسبقًا. سجّل الدخول.");
        if (m is null)
        {
            m = new Membership { OrganizationId = inv.OrganizationId, UserId = user.Id, CreatedAt = now };
            db.Memberships.Add(m);
        }
        m.Status = MembershipStatus.Active;
        m.Title = inv.Title;
        m.StatusReason = null;
        m.StatusChangedAt = now;
        m.Roles.Clear();
        foreach (var r in roles) m.Roles.Add(new MembershipRole { MembershipId = m.Id, RoleId = r.Id });

        inv.Status = InvitationStatus.Accepted;
        inv.AcceptedAt = now;
        inv.AcceptedUserId = user.Id;
        await audit.RecordAsync(new AuditEntry("team.invitation_accepted", $"انضمام {inv.FullName} إلى الفريق", OrganizationId: inv.OrganizationId,
            Data: new { roles = roles.Select(r => new { r.Key, r.NameAr }) }, SubjectType: "team_member", SubjectReference: m.Id.ToString()));
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Results.Ok(new { joined = true, email = inv.Email, next = "/login" });
    }
}
