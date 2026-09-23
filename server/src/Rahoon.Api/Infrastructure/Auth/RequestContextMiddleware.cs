using Microsoft.EntityFrameworkCore;
using Rahoon.Api.Infrastructure.Http;
using Rahoon.Api.Infrastructure.Persistence;
using Rahoon.Api.Infrastructure.Security;
using Rahoon.Api.Infrastructure.Tenancy;
using Rahoon.Api.Infrastructure.Time;
using Rahoon.Api.Modules.Administration;
using Rahoon.Api.Modules.Identity;
using Rahoon.Api.Modules.Providers;

namespace Rahoon.Api.Infrastructure.Auth;

/// <summary>
/// Resolves the session cookie into a <see cref="RequestContext"/>: user, active
/// membership, permissions and the set of organizations whose data this request may read.
/// Also enforces CSRF (double-submit verified against the server-side hash) and Origin checks.
/// </summary>
public sealed class RequestContextMiddleware(RequestDelegate next)
{
    private static readonly string[] CsrfExempt =
    [
        "/api/auth/login", "/api/auth/owner/", "/api/public/", "/api/invitations/", "/api/health",
    ];

    public async Task InvokeAsync(HttpContext http, RequestContext rc, RahoonDbContext db, SessionService sessions,
        IClock clock, IConfiguration config)
    {
        rc.IpMasked = Mask.Ip(http.Connection.RemoteIpAddress?.ToString());
        var unsafeMethod = !HttpMethods.IsGet(http.Request.Method) && !HttpMethods.IsHead(http.Request.Method) && !HttpMethods.IsOptions(http.Request.Method);

        if (unsafeMethod && !OriginAllowed(http, config))
        {
            await WriteProblem(http, 403, "origin", "مصدر الطلب غير مسموح.");
            return;
        }

        Session? session = null;
        if (http.Request.Cookies.TryGetValue(AuthCookies.Session, out var token) && !string.IsNullOrEmpty(token))
        {
            using (rc.BeginSystemScope())
            {
                session = await sessions.FindActiveAsync(token);
                if (session?.User is { Status: UserStatus.Active } user)
                {
                    rc.SetSession(user.Id, session.Id, user.FullName, session.Scope, session.Stage, session.StepUpUntil);
                    var idle = await ResolveScopeAsync(rc, db, session, clock);
                    if (idle is null)
                    {
                        // Membership revoked/suspended since login: kill the session.
                        session.RevokedAt = clock.UtcNow;
                        session.RevokedReason = "membership_inactive";
                        rc.SetAnonymous();
                    }
                    else
                    {
                        sessions.Touch(session, idle.Value);
                    }
                    await db.SaveChangesAsync();
                }
            }
        }

        if (unsafeMethod && http.Request.Path.StartsWithSegments("/api") && !CsrfExempt.Any(p => http.Request.Path.Value!.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
        {
            if (session is null || !rc.IsAuthenticated)
            {
                await WriteProblem(http, 401, "unauthenticated", "انتهت الجلسة. سجّل الدخول مرة أخرى.");
                return;
            }
            if (!SessionService.CsrfMatches(session, http.Request.Headers[AuthCookies.CsrfHeader]))
            {
                await WriteProblem(http, 403, "csrf", "تعذّر التحقق من الطلب. حدّث الصفحة وأعد المحاولة.");
                return;
            }
        }

        await next(http);
    }

    /// <returns>Idle timeout in minutes, or null if the session's scope is no longer valid.</returns>
    internal static async Task<int?> ResolveScopeAsync(RequestContext rc, RahoonDbContext db, Session session, IClock clock)
    {
        switch (session.Scope)
        {
            case SessionScope.Organization when session.MembershipId is { } membershipId:
            {
                var m = await db.Memberships.Include(x => x.Organization)
                    .Include(x => x.Roles).ThenInclude(r => r.Role!).ThenInclude(r => r.Permissions)
                    .FirstOrDefaultAsync(x => x.Id == membershipId);
                if (m is null || m.Status != MembershipStatus.Active || m.Organization is not { Status: OrganizationStatus.Active } org)
                    return null;

                var roles = m.Roles.Select(r => r.Role!).ToList();
                var permissions = roles.SelectMany(r => r.Permissions).Select(p => p.PermissionKey).ToHashSet();
                var dataOrgs = new List<Guid> { org.Id };
                var now = clock.UtcNow;

                if (org.Kind is OrganizationKind.ServiceProvider or OrganizationKind.JudicialAgent)
                {
                    var lenderOrgs = await db.Assignments
                        .Where(a => a.ProviderOrganizationId == org.Id && a.Status != AssignmentStatus.Cancelled
                                    && (a.AccessExpiresAt == null || a.AccessExpiresAt > now))
                        .Select(a => a.OrganizationId).Distinct().ToListAsync();
                    dataOrgs.AddRange(lenderOrgs);
                }
                else if (org.Kind == OrganizationKind.Platform)
                {
                    // Platform staff read tenant data only under an approved, unexpired temporary grant.
                    var granted = await db.TempAccessRequests
                        .Where(t => t.RequesterUserId == session.UserId && t.Status == TempAccessStatus.Active && t.ExpiresAt > now)
                        .Select(t => t.OrganizationId).Distinct().ToListAsync();
                    dataOrgs.AddRange(granted);
                }

                rc.SetOrganization(org.Id, org.Kind, org.NameAr, m.Id, roles.Select(r => r.Key).ToHashSet(), permissions,
                    roles.FirstOrDefault()?.NameAr, dataOrgs);
                return org.IdleTimeoutMinutes;
            }
            case SessionScope.Owner when session.OwnerAccessId is { } accessId:
            {
                var access = await db.OwnerAccesses.FirstOrDefaultAsync(a => a.Id == accessId);
                if (access is null || access.RevokedAt != null || access.UserId != session.UserId) return null;
                var org = await db.Organizations.FirstAsync(o => o.Id == access.OrganizationId);
                rc.SetOwner(org.Id, org.NameAr, access.CaseId, access.PartyId, access.Id);
                return org.IdleTimeoutMinutes;
            }
            case SessionScope.None:
                return 30;
            default:
                return null;
        }
    }

    private static bool OriginAllowed(HttpContext http, IConfiguration config)
    {
        var allowed = config.GetSection("Web:AllowedOrigins").Get<string[]>() ?? [];
        var origin = http.Request.Headers.Origin.ToString();
        if (string.IsNullOrEmpty(origin))
        {
            var referer = http.Request.Headers.Referer.ToString();
            if (string.IsNullOrEmpty(referer)) return !http.Request.Cookies.ContainsKey(AuthCookies.Session); // non-browser, no ambient credentials
            if (!Uri.TryCreate(referer, UriKind.Absolute, out var r)) return false;
            origin = r.GetLeftPart(UriPartial.Authority);
        }
        return allowed.Contains(origin, StringComparer.OrdinalIgnoreCase);
    }

    private static Task WriteProblem(HttpContext http, int status, string code, string message)
    {
        http.Response.StatusCode = status;
        return http.Response.WriteAsJsonAsync(new { status, title = message, code }, (System.Text.Json.JsonSerializerOptions?)null, "application/problem+json");
    }
}
