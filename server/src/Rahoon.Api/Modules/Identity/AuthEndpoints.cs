using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Rahoon.Api.Infrastructure.Auth;
using Rahoon.Api.Infrastructure.Http;
using Rahoon.Api.Infrastructure.Persistence;
using Rahoon.Api.Infrastructure.Security;
using Rahoon.Api.Infrastructure.Tenancy;
using Rahoon.Api.Infrastructure.Time;
using Rahoon.Api.Modules.Audit;
using Rahoon.Api.Modules.Cases;

namespace Rahoon.Api.Modules.Identity;

public sealed record LoginRequest(string Email, string Password);
public sealed record CodeRequest(string Code);
public sealed record ContextRequest(Guid MembershipId);
public sealed record OwnerVerifyRequest(string Token, string IdLast4);
public sealed record OwnerOtpRequest(string Token, string Code);

public static class AuthEndpoints
{
    private const string GenericLoginError = "البريد أو كلمة المرور غير صحيحة.";

    public static void Map(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/auth");

        g.MapPost("/login", Login).RequireRateLimiting("auth");
        g.MapPost("/mfa/verify", VerifyMfa).RequireRateLimiting("auth");
        g.MapPost("/mfa/resend", ResendMfa).RequireRateLimiting("auth");
        g.MapGet("/me", Me);
        g.MapPost("/logout", Logout);
        g.MapPost("/context", SwitchContext).RequireSession();
        g.MapPost("/step-up/start", StartStepUp).RequireSession();
        g.MapPost("/step-up/verify", VerifyStepUp).RequireSession();
        g.MapGet("/sessions", ListSessions).RequireSession();
        g.MapPost("/sessions/{id:guid}/revoke", RevokeSession).RequireSession();
        g.MapPost("/sessions/revoke-others", RevokeOthers).RequireSession();

        // Owner (debtor) sign-in: invitation link + last 4 of national ID + SMS code. No passwords.
        app.MapGet("/api/public/invitations/{token}", GetOwnerInvitation);
        g.MapPost("/owner/verify-id", OwnerVerifyId).RequireRateLimiting("auth");
        g.MapPost("/owner/verify-otp", OwnerVerifyOtp).RequireRateLimiting("auth");
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
        var session = await sessions.CreateAsync(http, user, SessionStage.MfaPending, SessionScope.None);
        var issued = await otp.IssueAsync(OtpPurpose.Login, user.Phone ?? "", user.Id, session.Id);
        return Results.Ok(new { mfaRequired = true, factor = "sms", destination = issued.DestinationMasked, issued.ResendInSeconds, sandboxCode = issued.SandboxCode });
    }

    private static IResult Locked(DateTimeOffset until, DateTimeOffset now) =>
        Results.Problem(title: "أُوقف الدخول مؤقتاً. تجاوزت عدد المحاولات المسموح. يمكنك المحاولة بعد 15 دقيقة، أو التواصل مع مسؤول منشأتك. لم يتغير أي شيء في حسابك.",
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

        var memberships = await ActiveMembershipsAsync(db, user.Id);
        await db.SaveChangesAsync();
        if (memberships.Count == 1)
        {
            var m = memberships[0];
            await sessions.RotateAsync(http, session, user, SessionScope.Organization, m.OrganizationId, m.Id, null, m.Organization!.IdleTimeoutMinutes, "mfa_complete");
            await audit.RecordAsync(new AuditEntry("auth.login", "تسجيل دخول", OrganizationId: m.OrganizationId));
            await db.SaveChangesAsync();
            return Results.Ok(new { next = HomeFor(m) });
        }
        return Results.Ok(new { next = memberships.Count == 0 ? "/access-denied" : "/select-context" });
    }

    private static async Task<IResult> ResendMfa(HttpContext http, RahoonDbContext db, SessionService sessions, OtpService otp, RequestContext rc)
    {
        var session = await CurrentSessionAsync(http, sessions, rc);
        if (session is null || session.Stage != SessionStage.MfaPending)
            return Results.Problem(title: "انتهت خطوة التحقق. سجّل الدخول مرة أخرى.", statusCode: 401);
        using var _ = rc.BeginSystemScope();
        var user = await db.Users.FirstAsync(u => u.Id == session.UserId);
        var issued = await otp.IssueAsync(OtpPurpose.Login, user.Phone ?? "", user.Id, session.Id);
        return Results.Ok(new { destination = issued.DestinationMasked, issued.ResendInSeconds, sandboxCode = issued.SandboxCode });
    }

    internal static async Task<List<Membership>> ActiveMembershipsAsync(RahoonDbContext db, Guid userId) =>
        await db.Memberships.IgnoreQueryFilters().Include(m => m.Organization)
            .Include(m => m.Roles).ThenInclude(r => r.Role)
            .Where(m => m.UserId == userId && m.Status == MembershipStatus.Active && m.Organization!.Status == OrganizationStatus.Active)
            .OrderBy(m => m.CreatedAt).ToListAsync();

    internal static string HomeFor(Membership m)
    {
        var roles = m.Roles.Select(r => r.Role?.Key).ToHashSet();
        return m.Organization!.Kind switch
        {
            OrganizationKind.ServiceProvider => "/provider",
            OrganizationKind.JudicialAgent => "/agent",
            OrganizationKind.Platform => "/platform",
            _ when roles.Count > 0 && roles.All(r => r is SystemRoles.Approver or SystemRoles.SeniorApprover or SystemRoles.RiskCommittee) => "/approvals",
            _ => "/portfolio",
        };
    }

    private static async Task<IResult> Me(RequestContext rc, RahoonDbContext db, IClock clock)
    {
        if (!rc.IsAuthenticated) return Results.Ok(new { authenticated = false });
        using var _ = rc.BeginSystemScope();
        var user = await db.Users.AsNoTracking().FirstAsync(u => u.Id == rc.UserId);
        var memberships = rc.IsOwner || rc.IsIndividual ? [] : await ActiveMembershipsAsync(db, rc.UserId);
        object? owner = null;
        if (rc.IsOwner)
        {
            var c = await db.Cases.AsNoTracking().FirstAsync(x => x.Id == rc.OwnerCaseId);
            var party = await db.Parties.AsNoTracking().FirstAsync(p => p.Id == rc.OwnerPartyId);
            owner = new { caseRef = c.Reference, firstName = party.FullName.Split(' ')[0], lenderName = rc.OrganizationName };
        }
        object? individual = null;
        if (rc.IsIndividual)
        {
            var p = await db.IndividualProfiles.AsNoTracking().FirstAsync(x => x.UserId == rc.UserId);
            individual = new { idMasked = p.NationalIdMasked, phoneMasked = p.PhoneMasked, identityAssurance = p.IdentityAssurance };
        }
        var org = rc.OrganizationId is { } oid ? await db.Organizations.AsNoTracking().FirstAsync(o => o.Id == oid) : null;
        var unread = await db.Notifications.CountAsync(n => n.UserId == rc.UserId && n.ReadAt == null);
        return Results.Ok(new
        {
            authenticated = true,
            stage = rc.Stage.ToString().ToLowerInvariant(),
            scope = rc.Scope.ToString().ToLowerInvariant(),
            user = new { id = user.Id, name = user.FullName, email = user.AccountKind == AccountKind.Staff ? user.Email : "", locale = user.PreferredLocale, numerals = user.NumeralStyle, initials = Initials(user.FullName) },
            organization = org is null ? null : new { id = org.Id, name = org.NameAr, kind = org.Kind.ToString().ToLowerInvariant(), initials = org.Initials },
            roles = rc.RoleKeys, roleName = rc.PrimaryRoleNameAr,
            permissions = rc.Permissions,
            owner,
            individual,
            memberships = memberships.Select(m => new
            {
                id = m.Id, organization = m.Organization!.NameAr, initials = m.Organization.Initials, kind = m.Organization.Kind.ToString().ToLowerInvariant(),
                role = m.Roles.Select(r => r.Role!.NameAr).FirstOrDefault(), current = m.Id == rc.MembershipId,
            }),
            stepUpActive = rc.StepUpUntil > clock.UtcNow,
            unreadNotifications = unread,
            home = memberships.FirstOrDefault(m => m.Id == rc.MembershipId) is { } cur ? HomeFor(cur) : rc.IsOwner ? "/owner" : rc.IsIndividual ? "/my" : "/select-context",
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

    /// <summary>Organization switch rotates the session so no cached data crosses tenants (C12).</summary>
    private static async Task<IResult> SwitchContext(ContextRequest req, HttpContext http, RahoonDbContext db, SessionService sessions, RequestContext rc, AuditLog audit)
    {
        using var _ = rc.BeginSystemScope();
        var memberships = await ActiveMembershipsAsync(db, rc.UserId);
        var m = memberships.FirstOrDefault(x => x.Id == req.MembershipId) ?? throw new NotFoundException();
        var session = await db.Sessions.FirstAsync(s => s.Id == rc.SessionId);
        var user = await db.Users.FirstAsync(u => u.Id == rc.UserId);
        await sessions.RotateAsync(http, session, user, SessionScope.Organization, m.OrganizationId, m.Id, null, m.Organization!.IdleTimeoutMinutes, "context_switch");
        await audit.RecordAsync(new AuditEntry("auth.context_switch", $"تبديل المنشأة إلى {m.Organization.NameAr}", OrganizationId: m.OrganizationId));
        await db.SaveChangesAsync();
        return Results.Ok(new { next = HomeFor(m) });
    }

    private static async Task<IResult> StartStepUp(RequestContext rc, RahoonDbContext db, OtpService otp, PiiProtector pii)
    {
        using var _ = rc.BeginSystemScope();
        var user = await db.Users.FirstAsync(u => u.Id == rc.UserId);
        var phone = user.Phone;
        if (rc.IsOwner)
        {
            var party = await db.Parties.FirstAsync(p => p.Id == rc.OwnerPartyId);
            phone = party.PhoneEnc is null ? null : pii.Unprotect(party.PhoneEnc);
        }
        var issued = await otp.IssueAsync(OtpPurpose.StepUp, phone ?? "", rc.UserId, rc.SessionId);
        return Results.Ok(new { destination = issued.DestinationMasked, issued.ResendInSeconds, sandboxCode = issued.SandboxCode });
    }

    private static async Task<IResult> VerifyStepUp(CodeRequest req, RequestContext rc, RahoonDbContext db, OtpService otp, IClock clock, AuthOptions options)
    {
        using var _ = rc.BeginSystemScope();
        if (!await otp.VerifyAsync(OtpPurpose.StepUp, rc.SessionId, req.Code))
            throw new DomainException("otp_exhausted", "تجاوزت عدد المحاولات. اطلب رمزاً جديداً.", StatusCodes.Status401Unauthorized);
        var session = await db.Sessions.FirstAsync(s => s.Id == rc.SessionId);
        session.StepUpUntil = clock.UtcNow.AddMinutes(options.StepUpMinutes);
        await db.SaveChangesAsync();
        return Results.Ok(new { stepUpUntil = session.StepUpUntil });
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
        EndpointAccess.EnsureStepUp(rc, clock);
        var others = await db.Sessions.Where(x => x.UserId == rc.UserId && x.Id != rc.SessionId && x.RevokedAt == null).ToListAsync();
        foreach (var s in others) { s.RevokedAt = clock.UtcNow; s.RevokedReason = "user_revoked_all"; }
        await audit.RecordAsync(new AuditEntry("auth.sessions_revoked", $"إنهاء كل الجلسات الأخرى ({others.Count})"));
        await db.SaveChangesAsync();
        return Results.Ok(new { revoked = others.Count });
    }

    // ───────── Owner (debtor) ─────────

    private static async Task<OwnerAccess?> FindInvitationAsync(RahoonDbContext db, string token) =>
        await db.OwnerAccesses.IgnoreQueryFilters().FirstOrDefaultAsync(a => a.InvitationTokenHash == Tokens.Sha256(token ?? ""));

    private static async Task<IResult> GetOwnerInvitation(string token, RahoonDbContext db, IClock clock, RequestContext rc)
    {
        using var _ = rc.BeginSystemScope();
        var access = await FindInvitationAsync(db, token);
        if (access is null || access.RevokedAt != null)
            return Results.Ok(new { status = "invalid" });
        if (access.InvitationStatus == OwnerInvitationStatus.Sent && access.InvitationExpiresAt < clock.UtcNow)
            return Results.Ok(new { status = "expired" });
        var org = await db.Organizations.FirstAsync(o => o.Id == access.OrganizationId);
        var party = await db.Parties.FirstAsync(p => p.Id == access.PartyId);
        return Results.Ok(new
        {
            status = access.InvitationStatus == OwnerInvitationStatus.Accepted ? "used" : "active",
            lenderName = org.NameAr,
            invitationCode = "AF-" + Convert.ToHexString(Tokens.Sha256(token))[..4],
            phoneMasked = party.PhoneMasked,
            nationalIdProvider = "unavailable",
        });
    }

    private static async Task<IResult> OwnerVerifyId(OwnerVerifyRequest req, HttpContext http, RahoonDbContext db, IClock clock,
        RequestContext rc, PiiProtector pii, SessionService sessions, OtpService otp, AuditLog audit)
    {
        using var _ = rc.BeginSystemScope();
        var access = await FindInvitationAsync(db, req.Token);
        if (access is null || access.RevokedAt != null || (access.InvitationStatus == OwnerInvitationStatus.Sent && access.InvitationExpiresAt < clock.UtcNow))
            throw new DomainException("invitation_invalid", "الدعوة منتهية أو غير صالحة. تواصل مع المصرف لإرسال دعوة جديدة إلى جوالك المسجل.", StatusCodes.Status410Gone);

        var party = await db.Parties.FirstAsync(p => p.Id == access.PartyId);
        var last4 = new string((req.IdLast4 ?? "").Where(char.IsDigit).ToArray());
        if (last4.Length != 4) Validate.Throw("idLast4", "أدخل آخر 4 أرقام من هويتك الوطنية.");
        var id = party.NationalIdEnc is null ? "" : pii.Unprotect(party.NationalIdEnc);
        if (!id.EndsWith(last4))
        {
            await audit.RecordAsync(new AuditEntry("owner.verify_failed", "فشل التحقق من هوية المالك", access.CaseId, OrganizationId: access.OrganizationId));
            await db.SaveChangesAsync();
            throw new DomainException("id_mismatch", "الأرقام لا تطابق الهوية المسجلة لدى المصرف. تحقق وأعد المحاولة، أو تواصل مع المصرف.");
        }

        // Owner user exists per party; created on first verification.
        var user = access.UserId is { } uid ? await db.Users.FirstAsync(u => u.Id == uid) : null;
        if (user is null)
        {
            // Owners have no password; their phone stays encrypted on the party record only.
            user = new User { Email = $"owner+{access.Id:N}@owners.rahoon.local", FullName = party.FullName, PreferredLocale = party.PreferredLanguage, AccountKind = AccountKind.Owner };
            db.Users.Add(user);
            access.UserId = user.Id;
            await db.SaveChangesAsync();
        }
        var session = await sessions.CreateAsync(http, user, SessionStage.MfaPending, SessionScope.None, ownerAccessId: access.Id);
        var phone = party.PhoneEnc is null ? "" : pii.Unprotect(party.PhoneEnc);
        var issued = await otp.IssueAsync(OtpPurpose.OwnerLogin, phone, user.Id, session.Id, orgId: access.OrganizationId, caseId: access.CaseId);
        return Results.Ok(new { destination = issued.DestinationMasked, issued.ResendInSeconds, sandboxCode = issued.SandboxCode });
    }

    private static async Task<IResult> OwnerVerifyOtp(OwnerOtpRequest req, HttpContext http, RahoonDbContext db, IClock clock,
        RequestContext rc, SessionService sessions, OtpService otp, AuditLog audit)
    {
        var session = await CurrentSessionAsync(http, sessions, rc);
        using var _ = rc.BeginSystemScope();
        var access = await FindInvitationAsync(db, req.Token);
        if (session is null || access is null || session.OwnerAccessId != access.Id)
            return Results.Problem(title: "انتهت خطوة التحقق. ابدأ من رابط الدعوة مرة أخرى.", statusCode: 401);

        if (!await otp.VerifyAsync(OtpPurpose.OwnerLogin, session.Id, req.Code))
        {
            session.RevokedAt = clock.UtcNow;
            await db.SaveChangesAsync();
            sessions.ClearCookies(http);
            throw new DomainException("otp_exhausted", "تجاوزت عدد المحاولات. انتظر 15 دقيقة ثم ابدأ من رابط الدعوة، أو تواصل مع المصرف.", StatusCodes.Status423Locked);
        }

        var firstTime = access.InvitationStatus != OwnerInvitationStatus.Accepted;
        access.InvitationStatus = OwnerInvitationStatus.Accepted;
        access.AcceptedAt ??= clock.UtcNow;
        access.IdentityVerifiedAt = clock.UtcNow;
        access.IdentityMethod = "رابط الدعوة + آخر 4 أرقام من الهوية + رمز جوال";
        var party = await db.Parties.FirstAsync(p => p.Id == access.PartyId);
        party.IdentityVerifiedAt ??= clock.UtcNow;
        party.IdentityVerifiedVia ??= "الدعوة ورمز الجوال";
        var user = await db.Users.FirstAsync(u => u.Id == session.UserId);
        session.MfaVerifiedAt = clock.UtcNow;
        await sessions.RotateAsync(http, session, user, SessionScope.Owner, access.OrganizationId, null, access.Id, 30, "owner_verified");
        var c = await db.Cases.FirstAsync(x => x.Id == access.CaseId);
        await audit.RecordAsync(new AuditEntry(firstTime ? "owner.invitation_accepted" : "owner.login",
            firstTime ? "قبول الدعوة والتحقق من هوية المالك" : "دخول المالك", c.Id, c.Reference, OrganizationId: access.OrganizationId));
        await db.SaveChangesAsync();
        return Results.Ok(new { next = "/owner" });
    }
}
