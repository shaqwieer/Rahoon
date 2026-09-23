using Microsoft.EntityFrameworkCore;
using Rahoon.Api.Infrastructure.Persistence;
using Rahoon.Api.Infrastructure.Security;
using Rahoon.Api.Infrastructure.Time;
using Rahoon.Api.Modules.Identity;

namespace Rahoon.Api.Infrastructure.Auth;

public static class AuthCookies
{
    public const string Session = "rahoon_sid";
    public const string Csrf = "rahoon_csrf";
    public const string CsrfHeader = "X-CSRF-Token";
}

public sealed class AuthOptions
{
    public bool SecureCookies { get; set; } = true;
    public int AbsoluteSessionHours { get; set; } = 12;
    public int DefaultIdleMinutes { get; set; } = 30;
    public int MaxFailedLogins { get; set; } = 5;
    public int LockoutMinutes { get; set; } = 15;
    public int OtpMinutes { get; set; } = 5;
    public int StepUpMinutes { get; set; } = 5;
    /// <summary>Development only: echo sandbox OTP codes in API responses so demos and E2E tests can proceed.</summary>
    public bool ExposeSandboxOtp { get; set; }
}

/// <summary>Creates, rotates and revokes server-side sessions and their cookies.</summary>
public sealed class SessionService(RahoonDbContext db, IClock clock, AuthOptions options)
{
    public async Task<Session> CreateAsync(HttpContext http, User user, SessionStage stage, SessionScope scope,
        Guid? orgId = null, Guid? membershipId = null, Guid? ownerAccessId = null, int? idleMinutes = null)
    {
        var token = Tokens.NewToken();
        var csrf = Tokens.NewToken(24);
        var now = clock.UtcNow;
        var idle = idleMinutes ?? options.DefaultIdleMinutes;
        var session = new Session
        {
            UserId = user.Id,
            TokenHash = Tokens.Sha256(token),
            CsrfHash = Tokens.Sha256(csrf),
            Stage = stage,
            Scope = scope,
            OrganizationId = orgId,
            MembershipId = membershipId,
            OwnerAccessId = ownerAccessId,
            CreatedAt = now,
            LastSeenAt = now,
            IdleExpiresAt = now.AddMinutes(stage == SessionStage.MfaPending ? 10 : idle),
            AbsoluteExpiresAt = now.AddHours(options.AbsoluteSessionHours),
            Ip = http.Connection.RemoteIpAddress?.ToString(),
            UserAgent = Truncate(http.Request.Headers.UserAgent.ToString(), 300),
            City = "الرياض",
        };
        db.Sessions.Add(session);
        await db.SaveChangesAsync();
        WriteCookies(http, token, csrf);
        return session;
    }

    /// <summary>Organization switch or privilege change: revoke and issue a fresh session (C12).</summary>
    public async Task<Session> RotateAsync(HttpContext http, Session current, User user, SessionScope scope,
        Guid? orgId, Guid? membershipId, Guid? ownerAccessId, int? idleMinutes, string reason)
    {
        current.RevokedAt = clock.UtcNow;
        current.RevokedReason = reason;
        var next = await CreateAsync(http, user, SessionStage.Active, scope, orgId, membershipId, ownerAccessId, idleMinutes);
        next.MfaVerifiedAt = current.MfaVerifiedAt ?? clock.UtcNow;
        await db.SaveChangesAsync();
        return next;
    }

    public async Task<Session?> FindActiveAsync(string token)
    {
        var hash = Tokens.Sha256(token);
        var now = clock.UtcNow;
        return await db.Sessions.Include(s => s.User)
            .FirstOrDefaultAsync(s => s.TokenHash == hash && s.RevokedAt == null && s.IdleExpiresAt > now && s.AbsoluteExpiresAt > now);
    }

    public void Touch(Session s, int idleMinutes)
    {
        var now = clock.UtcNow;
        if (now - s.LastSeenAt < TimeSpan.FromSeconds(60)) return;
        s.LastSeenAt = now;
        if (s.Stage == SessionStage.Active) s.IdleExpiresAt = now.AddMinutes(idleMinutes);
    }

    public static bool CsrfMatches(Session s, string? header) =>
        !string.IsNullOrEmpty(header) && Tokens.FixedEquals(s.CsrfHash, Tokens.Sha256(header));

    public void ClearCookies(HttpContext http)
    {
        http.Response.Cookies.Delete(AuthCookies.Session, new CookieOptions { Path = "/" });
        http.Response.Cookies.Delete(AuthCookies.Csrf, new CookieOptions { Path = "/" });
    }

    private void WriteCookies(HttpContext http, string token, string csrf)
    {
        var secure = options.SecureCookies || http.Request.IsHttps;
        http.Response.Cookies.Append(AuthCookies.Session, token, new CookieOptions
        {
            HttpOnly = true, Secure = secure, SameSite = SameSiteMode.Lax, Path = "/", IsEssential = true,
        });
        // Readable by the web app so it can echo it in X-CSRF-Token (double-submit, verified against the server-side hash).
        http.Response.Cookies.Append(AuthCookies.Csrf, csrf, new CookieOptions
        {
            HttpOnly = false, Secure = secure, SameSite = SameSiteMode.Strict, Path = "/", IsEssential = true,
        });
    }

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max];
}
