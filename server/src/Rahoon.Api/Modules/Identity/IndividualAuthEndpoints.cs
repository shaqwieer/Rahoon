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

public sealed record IndividualStartRequest(string? NationalId, string? Phone);
public sealed record IndividualVerifyRequest(string? Code, bool AcceptTerms, bool AwarenessOptIn);

/// <summary>
/// Self-registration and sign-in for individuals (OR01/OR02, ADR 0001 §4.1). One flow for both: national ID/iqama +
/// mobile → SMS code → terms. Interim rules (open product questions): no password (Q15), mobile code only — the
/// national digital identity slot is «unavailable» (Q10), identity is self-declared until verified by the Rahoon team.
/// Anti-enumeration: /start answers identically whether or not the ID is registered; if the ID is registered with a
/// different mobile, no code is sent (a decoy challenge runs out like a wrong code) and nothing is locked.
/// </summary>
public static class IndividualAuthEndpoints
{
    /// <summary>Version of the terms and privacy text shown at registration. The text itself is a draft pending legal approval.</summary>
    public const string TermsVersion = "terms-draft-2026-09";
    private const string Decoy = "decoy";

    public static void Map(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/auth/individual");
        g.MapPost("/start", Start).RequireRateLimiting("auth");
        g.MapPost("/resend", Resend).RequireRateLimiting("auth");
        g.MapPost("/verify", Verify).RequireRateLimiting("auth");
        app.MapGet("/api/individual/account", Account).RequireIndividual();
    }

    internal static string NormalizeId(string? id) =>
        new string((id ?? "").Select(c => c is >= '٠' and <= '٩' ? (char)('0' + (c - '٠')) : c).Where(char.IsDigit).ToArray());

    private static async Task<IResult> Start(IndividualStartRequest req, HttpContext http, RahoonDbContext db, RequestContext rc,
        PiiProtector pii, SessionService sessions, OtpService otp, AuthOptions options, IClock clock, AuditLog audit)
    {
        var nationalId = NormalizeId(req.NationalId);
        var phone = CaseFactory.NormalizePhone(NormalizeId(req.Phone));
        new Validator()
            .Require(CaseFactory.IsValidNationalId(nationalId), "nationalId", "أدخل رقم هوية وطنية أو إقامة صحيحاً من 10 أرقام يبدأ بـ 1 أو 2.")
            .Require(CaseFactory.IsValidSaudiMobile(phone), "phone", "أدخل رقم جوال سعودي صحيحاً يبدأ بـ 05.")
            .ThrowIfInvalid();

        var now = clock.UtcNow;
        using var _ = rc.BeginSystemScope();
        var idHash = pii.LookupHash(nationalId);
        var phoneHash = pii.LookupHash(phone!);
        var profile = await db.IndividualProfiles.Include(p => p.User).FirstOrDefaultAsync(p => p.NationalIdHash == idHash);

        if (profile?.User?.LockedUntil is { } until && until > now)
            throw new DomainException("locked", "أُوقف الدخول مؤقتاً بعد محاولات كثيرة. حاول بعد 15 دقيقة، أو تواصل مع فريق رهون.", StatusCodes.Status423Locked);

        User user;
        var decoy = false;
        if (profile is null)
        {
            user = NewPendingUser(nationalId, now);
            db.Users.Add(user);
            profile = NewProfile(user, nationalId, phone!, idHash, phoneHash, pii, now);
            db.IndividualProfiles.Add(profile);
        }
        else
        {
            user = profile.User!;
            var registered = profile.PhoneVerifiedAt is not null;
            if (profile.PhoneHash != phoneHash)
            {
                // A pending (never verified) registration from another mobile expires with its code; after that it may be replaced.
                if (!registered && now - profile.CreatedAt > TimeSpan.FromMinutes(options.OtpMinutes + 5))
                {
                    profile.PhoneEnc = pii.Protect(phone!);
                    profile.PhoneHash = phoneHash;
                    profile.PhoneMasked = Mask.Phone(phone!);
                    profile.CreatedAt = now;
                }
                else decoy = true;
            }
        }
        await db.SaveChangesAsync();

        var session = await sessions.CreateAsync(http, user, SessionStage.MfaPending, SessionScope.None);
        var issued = await otp.IssueAsync(OtpPurpose.IndividualAccess, phone!, user.Id, session.Id, context: decoy ? Decoy : null, send: !decoy);
        if (decoy)
        {
            await audit.RecordAsync(new AuditEntry("individual.start_mismatch", "محاولة دخول برقم جوال لا يطابق الحساب", Detail: profile.NationalIdMasked));
            await db.SaveChangesAsync();
        }
        // Same shape in every branch; the destination is always the number the person typed.
        return Results.Ok(new { destination = Mask.Phone(phone!), issued.ResendInSeconds, sandboxCode = issued.SandboxCode });
    }

    private static async Task<IResult> Resend(HttpContext http, RahoonDbContext db, RequestContext rc, SessionService sessions, OtpService otp, PiiProtector pii)
    {
        var session = await PendingSessionAsync(http, sessions, rc);
        using var _ = rc.BeginSystemScope();
        var latest = await LatestChallengeAsync(db, session.Id);
        var profile = await db.IndividualProfiles.FirstAsync(p => p.UserId == session.UserId);
        var decoy = latest?.Context == Decoy;
        // A decoy is re-issued against the mask of the number originally typed (masking a mask yields the same mask),
        // so neither the response nor the stored challenge ever carries the registered number.
        var phone = decoy ? latest!.Destination ?? "" : pii.Unprotect(profile.PhoneEnc);
        var issued = await otp.IssueAsync(OtpPurpose.IndividualAccess, phone, session.UserId, session.Id, context: decoy ? Decoy : null, send: !decoy);
        return Results.Ok(new { destination = Mask.Phone(phone), issued.ResendInSeconds, sandboxCode = issued.SandboxCode });
    }

    private static async Task<IResult> Verify(IndividualVerifyRequest req, HttpContext http, RahoonDbContext db, RequestContext rc,
        SessionService sessions, OtpService otp, AuthOptions options, IClock clock, AuditLog audit)
    {
        var session = await PendingSessionAsync(http, sessions, rc);
        // Checked before the code so a missing tick never costs an attempt.
        if (!req.AcceptTerms) Validate.Throw("acceptTerms", "للمتابعة، وافق على شروط الاستخدام وسياسة الخصوصية.");

        using var _ = rc.BeginSystemScope();
        var user = await db.Users.FirstAsync(u => u.Id == session.UserId);
        if (user.AccountKind != AccountKind.Individual) throw new ForbiddenException();
        var profile = await db.IndividualProfiles.FirstAsync(p => p.UserId == user.Id);
        var latest = await LatestChallengeAsync(db, session.Id);
        var decoy = latest?.Context == Decoy;
        var now = clock.UtcNow;

        if (!await otp.VerifyAsync(OtpPurpose.IndividualAccess, session.Id, req.Code ?? "", decoy ? Decoy : null))
        {
            // Exhausted. Lock only a real, already-registered account whose own mobile received the codes.
            session.RevokedAt = now;
            session.RevokedReason = "otp_exhausted";
            if (!decoy && profile.PhoneVerifiedAt is not null)
            {
                user.LockedUntil = now.AddMinutes(options.LockoutMinutes);
                await audit.RecordAsync(new AuditEntry("individual.login_locked", "إيقاف دخول الفرد مؤقتاً بعد رموز خاطئة", Detail: profile.NationalIdMasked));
            }
            await db.SaveChangesAsync();
            sessions.ClearCookies(http);
            throw new DomainException("otp_exhausted", "تجاوزت عدد المحاولات. انتظر قليلاً ثم ابدأ من جديد.", StatusCodes.Status423Locked);
        }

        var firstTime = profile.PhoneVerifiedAt is null;
        if (firstTime)
        {
            user.Status = UserStatus.Active;
            profile.PhoneVerifiedAt = now;
        }
        profile.TermsVersion = TermsVersion;
        profile.TermsAcceptedAt = now;
        profile.AwarenessOptIn = req.AwarenessOptIn;
        db.TermsAcceptances.Add(new TermsAcceptance { UserId = user.Id, Version = TermsVersion, AcceptedAt = now, IpMasked = rc.IpMasked });
        user.LastLoginAt = now;
        user.FailedLoginCount = 0;
        session.MfaVerifiedAt = now;
        var active = await sessions.RotateAsync(http, session, user, SessionScope.Individual, null, null, null, 30, firstTime ? "individual_registered" : "individual_login");
        rc.SetSession(user.Id, active.Id, user.FullName, SessionScope.Individual, SessionStage.Active, null);
        rc.SetIndividual();
        await audit.RecordAsync(new AuditEntry(firstTime ? "individual.registered" : "individual.login",
            firstTime ? "تسجيل فرد جديد والتحقق من جواله" : "دخول فرد", Detail: profile.NationalIdMasked));
        await db.SaveChangesAsync();
        return Results.Ok(new { next = "/my", firstTime });
    }

    private static async Task<IResult> Account(RequestContext rc, RahoonDbContext db)
    {
        using var _ = rc.BeginSystemScope();
        var p = await db.IndividualProfiles.AsNoTracking().FirstAsync(x => x.UserId == rc.UserId);
        return Results.Ok(new
        {
            idMasked = p.NationalIdMasked,
            idType = p.IdType,
            phoneMasked = p.PhoneMasked,
            identityAssurance = p.IdentityAssurance,
            p.PhoneVerifiedAt,
            p.TermsVersion,
            p.TermsAcceptedAt,
            p.AwarenessOptIn,
            nationalIdProvider = "unavailable",
        });
    }

    // ── helpers ──

    private static async Task<Session> PendingSessionAsync(HttpContext http, SessionService sessions, RequestContext rc)
    {
        if (!http.Request.Cookies.TryGetValue(AuthCookies.Session, out var token) || string.IsNullOrEmpty(token))
            throw new DomainException("unauthenticated", "انتهت خطوة التحقق. ابدأ من جديد.", StatusCodes.Status401Unauthorized);
        using var _ = rc.BeginSystemScope();
        var session = await sessions.FindActiveAsync(token);
        if (session is null || session.Stage != SessionStage.MfaPending || session.User?.AccountKind != AccountKind.Individual)
            throw new DomainException("unauthenticated", "انتهت خطوة التحقق. ابدأ من جديد.", StatusCodes.Status401Unauthorized);
        return session;
    }

    private static Task<OtpChallenge?> LatestChallengeAsync(RahoonDbContext db, Guid sessionId) =>
        db.OtpChallenges.Where(o => o.SessionId == sessionId && o.Purpose == OtpPurpose.IndividualAccess && o.ConsumedAt == null)
            .OrderByDescending(o => o.CreatedAt).FirstOrDefaultAsync();

    private static User NewPendingUser(string nationalId, DateTimeOffset now)
    {
        var id = Guid.CreateVersion7();
        return new User
        {
            Id = id,
            // Reserved, non-routable placeholder: individuals sign in with ID + mobile, never by e-mail (ADR 0001 amendment).
            Email = $"individual+{id:N}@individuals.rahoon.local",
            // Display label until the person's name is collected (Q5); never the full ID.
            FullName = Mask.NationalId(nationalId),
            AccountKind = AccountKind.Individual,
            Status = UserStatus.Pending,
            MfaEnrolled = true,
            CreatedAt = now,
            UpdatedAt = now,
        };
    }

    private static IndividualProfile NewProfile(User user, string nationalId, string phone, string idHash, string phoneHash, PiiProtector pii, DateTimeOffset now) => new()
    {
        UserId = user.Id,
        NationalIdEnc = pii.Protect(nationalId),
        NationalIdHash = idHash,
        NationalIdMasked = Mask.NationalId(nationalId),
        IdType = nationalId[0] == '1' ? "citizen" : "resident",
        PhoneEnc = pii.Protect(phone),
        PhoneHash = phoneHash,
        PhoneMasked = Mask.Phone(phone),
        CreatedAt = now,
        UpdatedAt = now,
    };
}
