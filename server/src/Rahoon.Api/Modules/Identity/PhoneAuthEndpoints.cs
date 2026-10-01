using Microsoft.EntityFrameworkCore;
using Rahoon.Api.Infrastructure.Auth;
using Rahoon.Api.Infrastructure.Http;
using Rahoon.Api.Infrastructure.Persistence;
using Rahoon.Api.Infrastructure.Security;
using Rahoon.Api.Infrastructure.Tenancy;
using Rahoon.Api.Infrastructure.Time;
using Rahoon.Api.Modules.Audit;
using Rahoon.Api.Modules.Market;

namespace Rahoon.Api.Modules.Identity;

public sealed record PhoneStartRequest(string? Phone);
public sealed record PhoneVerifyRequest(string? Code, bool AcceptTerms, string? Name);
public sealed record AccountNameRequest(string? Name);

/// <summary>
/// Mobile-first sign-in for owners and buyers (2026-10-01). One account per mobile number: an existing account with the same
/// mobile — including one created by the withdrawn mortgage-help sign-in — is reused, never duplicated. The same person can be
/// a seller and a buyer. The code goes through the SMS gateway when Auth:SmsConfirmation is on; when it is off (no provider
/// yet), the response says so (otpRequired = false) and the UI must not present the sign-in as verified.
/// </summary>
public static class PhoneAuthEndpoints
{
    public const string TermsVersion = "terms-draft-2026-10";

    public static void Map(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/auth/phone");
        g.MapPost("/start", Start).RequireRateLimiting("auth");
        g.MapPost("/resend", Resend).RequireRateLimiting("auth");
        g.MapPost("/verify", Verify).RequireRateLimiting("auth");
        app.MapGet("/api/account", Account).RequireIndividual();
        app.MapPut("/api/account/name", SetName).RequireIndividual().Idempotent();
    }

    public static string? NormalizeMobile(string? input)
    {
        var s = new string(FieldCatalog.NormalizeDigits(input ?? "").Where(c => char.IsDigit(c) || c == '+').ToArray());
        if (s.StartsWith('+')) s = s[1..];
        if (s.StartsWith("00")) s = s[2..];
        if (s.StartsWith("966")) s = "0" + s[3..];
        if (s.StartsWith('5') && s.Length == 9) s = "0" + s;
        return s.Length == 10 && s.StartsWith("05") && s.All(char.IsDigit) ? s : null;
    }

    public static string? CleanName(string? name)
    {
        var n = string.Join(' ', (name ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        return n.Length is >= 2 and <= 120 ? n : null;
    }

    private static async Task<IResult> Start(PhoneStartRequest req, HttpContext http, RahoonDbContext db, RequestContext rc, PiiProtector pii,
        SessionService sessions, OtpService otp, IClock clock)
    {
        var phone = NormalizeMobile(req.Phone);
        if (phone is null) Validate.Throw("phone", "أدخل رقم جوال سعودي صحيحًا يبدأ بـ 05.");
        var now = clock.UtcNow;
        using var _ = rc.BeginSystemScope();
        var phoneHash = pii.LookupHash(phone!);

        // Serialise first-time creation per mobile so two tabs can never create two accounts.
        await using var tx = await db.Database.BeginTransactionAsync();
        await db.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock(hashtext({phoneHash}))");
        var profile = await FindByPhoneAsync(db, phoneHash);
        if (profile?.User?.LockedUntil is { } until && until > now)
            throw new DomainException("locked", "أُوقف الدخول مؤقتًا بعد محاولات كثيرة. حاول بعد 15 دقيقة.", StatusCodes.Status423Locked);

        if (profile is null)
        {
            var id = Guid.CreateVersion7();
            var user = new User
            {
                Id = id,
                // Reserved, non-routable placeholder: individuals sign in by mobile, never by e-mail.
                Email = $"individual+{id:N}@individuals.rahoon.local",
                FullName = "عميل رهون",
                AccountKind = AccountKind.Individual,
                Status = UserStatus.Pending,
                MfaEnrolled = true,
                CreatedAt = now,
                UpdatedAt = now,
            };
            db.Users.Add(user);
            profile = new IndividualProfile
            {
                UserId = id, User = user, PhoneEnc = pii.Protect(phone!), PhoneHash = phoneHash, PhoneMasked = Mask.Phone(phone!),
                CreatedAt = now, UpdatedAt = now,
            };
            db.IndividualProfiles.Add(profile);
            await db.SaveChangesAsync();
        }
        await tx.CommitAsync();

        var session = await sessions.CreateAsync(http, profile.User!, SessionStage.MfaPending, SessionScope.None);
        var issued = await otp.IssueAsync(OtpPurpose.IndividualAccess, phone!, profile.UserId, session.Id);
        // Same shape whether or not the mobile was known; the destination is the number typed.
        return Results.Ok(new { destination = Mask.Phone(phone!), issued.ResendInSeconds, sandboxCode = issued.SandboxCode, otpRequired = issued.Required });
    }

    /// <summary>Oldest verified account for this mobile, else the oldest pending one (legacy data may hold more than one).</summary>
    internal static Task<IndividualProfile?> FindByPhoneAsync(RahoonDbContext db, string phoneHash) =>
        db.IndividualProfiles.Include(p => p.User).Where(p => p.PhoneHash == phoneHash)
            .OrderBy(p => p.PhoneVerifiedAt == null).ThenBy(p => p.CreatedAt).FirstOrDefaultAsync();

    private static async Task<IResult> Resend(HttpContext http, RahoonDbContext db, RequestContext rc, SessionService sessions, OtpService otp, PiiProtector pii)
    {
        var session = await PendingSessionAsync(http, sessions, rc);
        using var _ = rc.BeginSystemScope();
        var profile = await db.IndividualProfiles.FirstAsync(p => p.UserId == session.UserId);
        var phone = pii.Unprotect(profile.PhoneEnc);
        var issued = await otp.IssueAsync(OtpPurpose.IndividualAccess, phone, session.UserId, session.Id);
        return Results.Ok(new { destination = Mask.Phone(phone), issued.ResendInSeconds, sandboxCode = issued.SandboxCode, otpRequired = issued.Required });
    }

    private static async Task<IResult> Verify(PhoneVerifyRequest req, HttpContext http, RahoonDbContext db, RequestContext rc,
        SessionService sessions, OtpService otp, AuthOptions options, IClock clock, AuditLog audit)
    {
        var session = await PendingSessionAsync(http, sessions, rc);
        if (!req.AcceptTerms) Validate.Throw("acceptTerms", "للمتابعة، وافق على شروط الاستخدام وسياسة الخصوصية.");
        var name = req.Name is { Length: > 0 } ? CleanName(req.Name) : null;
        if (req.Name is { Length: > 0 } && name is null) Validate.Throw("name", "اكتب اسمك (حرفان على الأقل).");

        using var _ = rc.BeginSystemScope();
        var user = await db.Users.FirstAsync(u => u.Id == session.UserId);
        if (user.AccountKind != AccountKind.Individual) throw new ForbiddenException();
        var profile = await db.IndividualProfiles.FirstAsync(p => p.UserId == user.Id);
        var now = clock.UtcNow;

        if (!await otp.VerifyAsync(OtpPurpose.IndividualAccess, session.Id, req.Code ?? ""))
        {
            session.RevokedAt = now;
            session.RevokedReason = "otp_exhausted";
            if (profile.PhoneVerifiedAt is not null)
            {
                user.LockedUntil = now.AddMinutes(options.LockoutMinutes);
                await audit.RecordAsync(new AuditEntry("individual.login_locked", "إيقاف الدخول مؤقتًا بعد رموز خاطئة", Detail: profile.PhoneMasked));
            }
            await db.SaveChangesAsync();
            sessions.ClearCookies(http);
            throw new DomainException("otp_exhausted", "تجاوزت عدد المحاولات. انتظر قليلًا ثم ابدأ من جديد.", StatusCodes.Status423Locked);
        }

        var firstTime = profile.PhoneVerifiedAt is null;
        if (firstTime)
        {
            user.Status = UserStatus.Active;
            profile.PhoneVerifiedAt = now;
        }
        // A name given now replaces only the placeholder; an existing name is changed from the account page.
        if (name is not null && (firstTime || user.FullName == "عميل رهون" || user.FullName.Contains('•'))) user.FullName = name;
        profile.TermsVersion = TermsVersion;
        profile.TermsAcceptedAt = now;
        db.TermsAcceptances.Add(new TermsAcceptance { UserId = user.Id, Version = TermsVersion, AcceptedAt = now, IpMasked = rc.IpMasked });
        user.LastLoginAt = now;
        user.FailedLoginCount = 0;
        session.MfaVerifiedAt = now;
        var active = await sessions.RotateAsync(http, session, user, SessionScope.Individual, null, null, null, 30, firstTime ? "individual_registered" : "individual_login");
        rc.SetSession(user.Id, active.Id, user.FullName, SessionScope.Individual, SessionStage.Active, null);
        rc.SetIndividual();
        await audit.RecordAsync(new AuditEntry(firstTime ? "individual.registered" : "individual.login",
            firstTime ? "إنشاء حساب بالجوال" : "دخول بالجوال", Detail: profile.PhoneMasked,
            Data: new { codeConfirmed = options.SmsConfirmation ? "sms" : "not_sent_sms_disabled" }));
        await db.SaveChangesAsync();
        return Results.Ok(new { next = "/account", firstTime, needsName = user.FullName == "عميل رهون", smsVerified = options.SmsConfirmation });
    }

    private static async Task<IResult> Account(RequestContext rc, RahoonDbContext db, AuthOptions options)
    {
        using var _ = rc.BeginSystemScope();
        var user = await db.Users.AsNoTracking().FirstAsync(u => u.Id == rc.UserId);
        var p = await db.IndividualProfiles.AsNoTracking().FirstAsync(x => x.UserId == rc.UserId);
        return Results.Ok(new
        {
            name = user.FullName == "عميل رهون" ? null : user.FullName,
            phoneMasked = p.PhoneMasked,
            p.PhoneVerifiedAt,
            // Honest wording: with the SMS flag off no code was ever sent to this mobile.
            phoneVerification = options.SmsConfirmation ? "sms_code" : "not_verified_sms_disabled",
            p.TermsVersion,
            p.TermsAcceptedAt,
        });
    }

    private static async Task<IResult> SetName(AccountNameRequest req, RequestContext rc, RahoonDbContext db)
    {
        var name = CleanName(req.Name);
        if (name is null) Validate.Throw("name", "اكتب اسمك (حرفان على الأقل).");
        using var _ = rc.BeginSystemScope();
        var user = await db.Users.FirstAsync(u => u.Id == rc.UserId);
        user.FullName = name!;
        await db.SaveChangesAsync();
        return Results.Ok(new { name });
    }

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
}
