using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Rahoon.Api.Infrastructure.Auth;
using Rahoon.Api.Infrastructure.Http;
using Rahoon.Api.Infrastructure.Persistence;
using Rahoon.Api.Infrastructure.Security;
using Rahoon.Api.Infrastructure.Tenancy;
using Rahoon.Api.Infrastructure.Time;
using Rahoon.Api.Modules.Audit;

namespace Rahoon.Api.Modules.Identity;

public sealed record LoginRequest(string Email, string Password);
public sealed record CodeRequest(string Code);

public static class AuthEndpoints
{
    private const string GenericLoginError = "البريد أو كلمة المرور غير صحيحة.";

    /// <summary>Rahoon team sign-in (e-mail, password, SMS code), the session view and sign-out. Owners and buyers sign in by mobile (PhoneAuthEndpoints).</summary>
    public static void Map(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/auth");

        g.MapPost("/login", Login).RequireRateLimiting("auth");
        g.MapPost("/mfa/verify", VerifyMfa).RequireRateLimiting("auth");
        g.MapPost("/mfa/resend", ResendMfa).RequireRateLimiting("auth");
        g.MapGet("/me", Me);
        g.MapPost("/logout", Logout);
        g.MapGet("/sessions", ListSessions).RequireSession();
        g.MapPost("/sessions/{id:guid}/revoke", RevokeSession).RequireSession();
        g.MapPost("/sessions/revoke-others", RevokeOthers).RequireSession();

    }

    private static async Task<IResult> Login(LoginRequest req, HttpContext http, RahoonDbContext db, IPasswordHasher<User> hasher,
        SessionService sessions, OtpService otp, AuthOptions options, IClock clock, AuditLog audit, RequestContext rc)
    {
        var email = (req.Email ?? "").Trim().ToLowerInvariant();
        var now = clock.UtcNow;
        using var _ = rc.BeginSystemScope();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email);

        if (user?.LockedUntil is { } until && until > now)
            return Locked(until, now);

        var ok = user is { Status: UserStatus.Active, PasswordHash: not null }
                 && hasher.VerifyHashedPassword(user, user.PasswordHash, req.Password ?? "") != PasswordVerificationResult.Failed;
        if (!ok)
        {
            if (user is null)
                // Same shape for unknown emails: never reveal whether an account exists.
                return Results.Problem(title: GenericLoginError, statusCode: 401,
                    extensions: new Dictionary<string, object?> { ["code"] = "invalid_credentials", ["remainingAttempts"] = options.MaxFailedLogins - 1 });

            user.FailedLoginCount++;
            var remaining = options.MaxFailedLogins - user.FailedLoginCount;
            if (remaining <= 0)
            {
                user.LockedUntil = now.AddMinutes(options.LockoutMinutes);
                user.FailedLoginCount = 0;
                await audit.RecordAsync(new AuditEntry("auth.login_locked", "إيقاف الدخول مؤقتاً بعد محاولات فاشلة", Detail: user.Email));
                await db.SaveChangesAsync();
                return Locked(user.LockedUntil.Value, now);
            }
            await audit.RecordAsync(new AuditEntry("auth.login_failed", "محاولة دخول فاشلة", Detail: Mask.Email(user.Email)));
            await db.SaveChangesAsync();
            return Results.Problem(title: $"{GenericLoginError} تبقّى {remaining} محاولات قبل إيقاف مؤقت لمدة {options.LockoutMinutes} دقيقة.",
                statusCode: 401, extensions: new Dictionary<string, object?> { ["code"] = "invalid_credentials", ["remainingAttempts"] = remaining });
        }

        user!.FailedLoginCount = 0;
        // Only members of «فريق رهون» sign in here.
        if (user.AccountKind != AccountKind.Staff || await ActiveMembershipAsync(db, user.Id) is null)
        {
            await db.SaveChangesAsync();
            return Results.Problem(title: "هذا الحساب ليس من فريق رهون. للدخول كمالك أو مشترٍ استخدم الدخول برقم الجوال.",
                statusCode: 403, extensions: new Dictionary<string, object?> { ["code"] = "not_team_member" });
        }
        var session = await sessions.CreateAsync(http, user, SessionStage.MfaPending, SessionScope.None);
        var issued = await otp.IssueAsync(OtpPurpose.Login, user.Phone ?? "", user.Id, session.Id);
        return Results.Ok(new { mfaRequired = true, factor = "sms", destination = issued.DestinationMasked, issued.ResendInSeconds, sandboxCode = issued.SandboxCode, otpRequired = issued.Required });
    }

    private static IResult Locked(DateTimeOffset until, DateTimeOffset now) =>
        Results.Problem(title: "أُوقف الدخول مؤقتاً. تجاوزت عدد المحاولات المسموح. يمكنك المحاولة بعد 15 دقيقة، أو التواصل مع قائد الفريق. لم يتغير أي شيء في حسابك.",
            statusCode: 423, extensions: new Dictionary<string, object?> { ["code"] = "locked", ["lockedUntil"] = until, ["minutes"] = (int)Math.Ceiling((until - now).TotalMinutes) });

    private static async Task<Session?> CurrentSessionAsync(HttpContext http, SessionService sessions, RequestContext rc)
    {
        if (!http.Request.Cookies.TryGetValue(AuthCookies.Session, out var token) || string.IsNullOrEmpty(token)) return null;
        using var _ = rc.BeginSystemScope();
        return await sessions.FindActiveAsync(token);
    }

    private static async Task<IResult> VerifyMfa(CodeRequest req, HttpContext http, RahoonDbContext db, SessionService sessions,
        OtpService otp, AuthOptions options, IClock clock, RequestContext rc, AuditLog audit)
    {
        var session = await CurrentSessionAsync(http, sessions, rc);
        if (session is null || session.Stage != SessionStage.MfaPending)
            return Results.Problem(title: "انتهت خطوة التحقق. سجّل الدخول مرة أخرى.", statusCode: 401, extensions: new Dictionary<string, object?> { ["code"] = "unauthenticated" });

        using var _ = rc.BeginSystemScope();
        var user = await db.Users.FirstAsync(u => u.Id == session.UserId);
        var passed = await otp.VerifyAsync(OtpPurpose.Login, session.Id, req.Code);
        if (!passed)
        {
            user.LockedUntil = clock.UtcNow.AddMinutes(options.LockoutMinutes);
            session.RevokedAt = clock.UtcNow;
            session.RevokedReason = "mfa_locked";
            await audit.RecordAsync(new AuditEntry("auth.login_locked", "إيقاف الدخول مؤقتاً بعد رموز تحقق خاطئة", Detail: Mask.Email(user.Email)));
            await db.SaveChangesAsync();
            sessions.ClearCookies(http);
            return Locked(user.LockedUntil.Value, clock.UtcNow);
        }

        session.Stage = SessionStage.Active;
        session.MfaVerifiedAt = clock.UtcNow;
        user.LastLoginAt = clock.UtcNow;
        user.MfaEnrolled = true;

        var m = await ActiveMembershipAsync(db, user.Id);
        await db.SaveChangesAsync();
        if (m is null) return Results.Ok(new { next = "/access-denied" });
        var fresh = await sessions.RotateAsync(http, session, user, SessionScope.Organization, m.OrganizationId, m.Id, m.Organization!.IdleTimeoutMinutes, "mfa_complete");
        await audit.RecordAsync(new AuditEntry("auth.login", "تسجيل دخول", OrganizationId: m.OrganizationId));
        await db.SaveChangesAsync();
        // Land on the first area this member's grants open.
        rc.SetSession(user.Id, fresh.Id, user.FullName, SessionScope.Organization, SessionStage.Active);
        await Infrastructure.Auth.RequestContextMiddleware.ResolveScopeAsync(rc, db, fresh);
        return Results.Ok(new { next = TeamHome.For(rc) });
    }

    private static async Task<IResult> ResendMfa(HttpContext http, RahoonDbContext db, SessionService sessions, OtpService otp, RequestContext rc)
    {
        var session = await CurrentSessionAsync(http, sessions, rc);
        if (session is null || session.Stage != SessionStage.MfaPending)
            return Results.Problem(title: "انتهت خطوة التحقق. سجّل الدخول مرة أخرى.", statusCode: 401);
        using var _ = rc.BeginSystemScope();
        var user = await db.Users.FirstAsync(u => u.Id == session.UserId);
        var issued = await otp.IssueAsync(OtpPurpose.Login, user.Phone ?? "", user.Id, session.Id);
        return Results.Ok(new { destination = issued.DestinationMasked, issued.ResendInSeconds, sandboxCode = issued.SandboxCode, otpRequired = issued.Required });
    }

    /// <summary>The user's active membership of «فريق رهون», if any.</summary>
    internal static async Task<Membership?> ActiveMembershipAsync(RahoonDbContext db, Guid userId) =>
        await db.Memberships.IgnoreQueryFilters().Include(m => m.Organization)
            .Include(m => m.Roles).ThenInclude(r => r.Role)
            .Where(m => m.UserId == userId && m.Status == MembershipStatus.Active && m.Organization!.Status == OrganizationStatus.Active
                        && m.Organization.Kind == OrganizationKind.Operator)
            .OrderBy(m => m.CreatedAt).FirstOrDefaultAsync();

    private static async Task<IResult> Me(RequestContext rc, RahoonDbContext db, AuthOptions options)
    {
        if (!rc.IsAuthenticated) return Results.Ok(new { authenticated = false, smsConfirmation = options.SmsConfirmation });
        using var _ = rc.BeginSystemScope();
        var user = await db.Users.AsNoTracking().FirstAsync(u => u.Id == rc.UserId);
        object? individual = null;
        if (rc.IsIndividual)
        {
            var p = await db.IndividualProfiles.AsNoTracking().FirstAsync(x => x.UserId == rc.UserId);
            individual = new { phoneMasked = p.PhoneMasked };
        }
        var org = rc.OrganizationId is { } oid ? await db.Organizations.AsNoTracking().FirstAsync(o => o.Id == oid) : null;
        return Results.Ok(new
        {
            authenticated = true,
            stage = rc.Stage.ToString().ToLowerInvariant(),
            scope = rc.Scope.ToString().ToLowerInvariant(),
            user = new { id = user.Id, name = user.FullName, email = user.AccountKind == AccountKind.Staff ? user.Email : "", locale = user.PreferredLocale, numerals = user.NumeralStyle, initials = Initials(user.FullName) },
            organization = org is null ? null : new { id = org.Id, name = org.NameAr, kind = org.Kind.ToString().ToLowerInvariant(), initials = org.Initials },
            roles = rc.RoleKeys, roleName = rc.PrimaryRoleNameAr,
            permissions = rc.Permissions,
            individual,
            smsConfirmation = options.SmsConfirmation,
            scopes = rc.Grants.ToDictionary(g => g.Key, g => g.Value == GrantScope.All ? "all" : "assigned"),
            home = rc.IsOperator ? TeamHome.For(rc) : rc.IsIndividual ? "/account" : "/",
        });
    }

    internal static string Initials(string name)
    {
        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2 ? $"{parts[0][0]} {parts[^1][0]}" : name.Length > 0 ? name[..1] : "";
    }

    private static async Task<IResult> Logout(HttpContext http, SessionService sessions, RahoonDbContext db, RequestContext rc, IClock clock)
    {
        var session = await CurrentSessionAsync(http, sessions, rc);
        if (session is not null)
        {
            using var _ = rc.BeginSystemScope();
            db.Attach(session);
            session.RevokedAt = clock.UtcNow;
            session.RevokedReason = "logout";
            await db.SaveChangesAsync();
        }
        sessions.ClearCookies(http);
        // Individuals return to the public landing; staff to their sign-in page.
        return Results.Ok(new { next = session?.User?.AccountKind == AccountKind.Individual ? "/" : "/login" });
    }

    private static async Task<IResult> ListSessions(RequestContext rc, RahoonDbContext db, IClock clock)
    {
        var now = clock.UtcNow;
        var list = await db.Sessions.AsNoTracking()
            .Where(s => s.UserId == rc.UserId && s.RevokedAt == null && s.IdleExpiresAt > now && s.Stage == SessionStage.Active)
            .OrderByDescending(s => s.LastSeenAt)
            .Select(s => new { s.Id, s.UserAgent, s.City, s.CreatedAt, s.LastSeenAt, current = s.Id == rc.SessionId })
            .ToListAsync();
        return Results.Ok(list);
    }

    private static async Task<IResult> RevokeSession(Guid id, RequestContext rc, RahoonDbContext db, IClock clock, AuditLog audit)
    {
        if (id == rc.SessionId) throw new DomainException("current_session", "لا يمكن إنهاء الجلسة الحالية من هنا؛ استخدم تسجيل الخروج.");
        var s = await db.Sessions.FirstOrDefaultAsync(x => x.Id == id && x.UserId == rc.UserId && x.RevokedAt == null) ?? throw new NotFoundException();
        s.RevokedAt = clock.UtcNow;
        s.RevokedReason = "user_revoked";
        await audit.RecordAsync(new AuditEntry("auth.session_revoked", "إنهاء جلسة"));
        await db.SaveChangesAsync();
        return Results.Ok(new { revoked = 1 });
    }

    private static async Task<IResult> RevokeOthers(RequestContext rc, RahoonDbContext db, IClock clock, AuditLog audit)
    {
        var others = await db.Sessions.Where(x => x.UserId == rc.UserId && x.Id != rc.SessionId && x.RevokedAt == null).ToListAsync();
        foreach (var s in others) { s.RevokedAt = clock.UtcNow; s.RevokedReason = "user_revoked_all"; }
        await audit.RecordAsync(new AuditEntry("auth.sessions_revoked", $"إنهاء كل الجلسات الأخرى ({others.Count})"));
        await db.SaveChangesAsync();
        return Results.Ok(new { revoked = others.Count });
    }
}
