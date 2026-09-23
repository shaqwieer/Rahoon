using System.Net.Mail;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Rahoon.Api.Infrastructure.Auth;
using Rahoon.Api.Infrastructure.Http;
using Rahoon.Api.Infrastructure.Integrations;
using Rahoon.Api.Infrastructure.Persistence;
using Rahoon.Api.Infrastructure.Security;
using Rahoon.Api.Infrastructure.Tenancy;
using Rahoon.Api.Infrastructure.Time;
using Rahoon.Api.Modules.Audit;
using Rahoon.Api.Modules.Cases;
using Rahoon.Api.Modules.Communications;

namespace Rahoon.Api.Modules.Identity;

public sealed record CreateInvitationRequest(string Email, string FullName, string? Phone, string RoleKey, Guid? TeamId);
public sealed record AcceptInvitationRequest(string FullName, string? Password, bool AckPolicy, string? Phone, string? CurrentPassword);
public sealed record PasswordCheckRequest(string? Password, string? FullName);

public sealed record IssuedInvitation(Invitation Invitation, string Token);

/// <summary>Staff invitations (A02 → S05): e-mail domain restricted, hashed single-use token, 7-day expiry.</summary>
public sealed class StaffInvitationService(RahoonDbContext db, IClock clock, ISmsGateway sms, AuditLog audit)
{
    public const int ValidDays = 7;
    public const string PolicyVersion = "aup-2026-01";

    public static string? NormalizeEmail(string? email)
    {
        var e = (email ?? "").Trim().ToLowerInvariant();
        return MailAddress.TryCreate(e, out var m) && m.Address == e && e.Contains('.', StringComparison.Ordinal) ? e : null;
    }

    public async Task<IssuedInvitation> CreateAsync(Organization org, CreateInvitationRequest req, Guid invitedBy, bool requirePhone = true)
    {
        var email = NormalizeEmail(req.Email);
        var domain = email?.Split('@')[1];
        var role = await db.Roles.FirstOrDefaultAsync(r => r.OrganizationId == org.Id && r.Key == req.RoleKey);
        var team = req.TeamId is { } tid ? await db.Teams.FirstOrDefaultAsync(t => t.Id == tid && t.OrganizationId == org.Id) : null;
        new Validator()
            .Require(email is not null, "email", "أدخل بريداً مؤسسياً صحيحاً.")
            .Require(email is null || org.AllowedEmailDomains.Contains(domain!, StringComparer.OrdinalIgnoreCase), "email",
                $"البريد يجب أن يكون ضمن نطاق المنشأة ({string.Join("، ", org.AllowedEmailDomains)}).")
            .Require(!string.IsNullOrWhiteSpace(req.FullName) && req.FullName.Trim().Length <= 120, "fullName", "أدخل الاسم الكامل.")
            .Require(CaseFactory.IsValidSaudiMobile(req.Phone) || (!requirePhone && string.IsNullOrWhiteSpace(req.Phone)), "phone", "أدخل رقم جوال سعودي يبدأ بـ 05 (يُستخدم للتحقق بخطوتين).")
            .Require(role is not null, "roleKey", "اختر دوراً من أدوار المنشأة.")
            .Require(req.TeamId is null || team is not null, "teamId", "الفريق لا يتبع المنشأة.")
            .ThrowIfInvalid();

        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email);
        if (user is not null && await db.Memberships.IgnoreQueryFilters()
                .AnyAsync(m => m.UserId == user.Id && m.OrganizationId == org.Id && (m.Status == MembershipStatus.Active || m.Status == MembershipStatus.Suspended)))
            throw new ConflictException("already_member", "هذا البريد لمستخدم في المنشأة بالفعل.");
        var now = clock.UtcNow;
        if (await db.Invitations.IgnoreQueryFilters().AnyAsync(i => i.OrganizationId == org.Id && i.Email == email && i.Status == InvitationStatus.Pending && i.ExpiresAt > now))
            throw new ConflictException("invitation_pending", "توجد دعوة معلقة لهذا البريد. أعد إرسالها أو ألغها أولاً.");

        var token = Tokens.NewToken();
        var inv = new Invitation
        {
            OrganizationId = org.Id, Email = email!, FullName = req.FullName.Trim(), Phone = CaseFactory.NormalizePhone(req.Phone), RoleId = role!.Id, TeamId = team?.Id,
            TokenHash = Tokens.Sha256(token), InvitedByUserId = invitedBy, CreatedAt = now, ExpiresAt = now.AddDays(ValidDays),
        };
        db.Invitations.Add(inv);
        await SendAsync(org, inv, role.NameAr);
        await audit.RecordAsync(new AuditEntry("user.invited", $"دعوة مستخدم بدور {role.NameAr}", Detail: $"{Mask.Email(inv.Email)} · تنتهي {inv.ExpiresAt:yyyy-MM-dd}", OrganizationId: org.Id));
        return new IssuedInvitation(inv, token);
    }

    public async Task<string> ReissueAsync(Organization org, Invitation inv)
    {
        var token = Tokens.NewToken();
        inv.TokenHash = Tokens.Sha256(token);
        inv.Status = InvitationStatus.Pending;
        inv.ExpiresAt = clock.UtcNow.AddDays(ValidDays);
        var role = await db.Roles.Where(r => r.Id == inv.RoleId).Select(r => r.NameAr).FirstOrDefaultAsync() ?? "";
        await SendAsync(org, inv, role);
        return token;
    }

    /// <summary>Sandbox e-mail + SMS. The link token is never stored: only a redacted body is recorded.</summary>
    private async Task SendAsync(Organization org, Invitation inv, string roleName)
    {
        SandboxEmail.Record(db, clock, org.Id, inv.Email,
            $"دعوة من {org.NameAr} للانضمام إلى رهون بدور {roleName}. افتح الرابط /invite/•••• قبل {inv.ExpiresAt.ToOffset(TimeSpan.FromHours(3)):yyyy-MM-dd}. إن لم تتوقع هذه الدعوة فتجاهلها أو أبلغنا من الصفحة نفسها.",
            "TPL-STAFF-INVITE");
        if (inv.Phone is { } phone)
            await sms.SendAsync(phone, $"رهون: لديك دعوة من {org.NameAr}. افتح الرابط المرسل إلى بريدك المؤسسي.", org.Id, null, "TPL-STAFF-INVITE");
    }
}

public static class StaffInvitationEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/settings/invitations").RequireOrg(OrganizationKind.Lender).RequirePermission(P.UserManage);
        g.MapGet("", List);
        g.MapPost("", Create).Idempotent();
        g.MapPost("/{id:guid}/resend", Resend).Idempotent();
        g.MapPost("/{id:guid}/cancel", Cancel).Idempotent();

        var p = app.MapGroup("/api/public/staff-invitations/{token}");
        p.MapGet("", Get);
        p.MapPost("/password-check", PasswordCheck).RequireRateLimiting("auth");
        p.MapPost("/accept", Accept).RequireRateLimiting("auth");
        p.MapPost("/report", Report).RequireRateLimiting("auth");
    }

    private static string StatusLabel(InvitationStatus status, DateTimeOffset expiresAt, DateTimeOffset now) => status switch
    {
        InvitationStatus.Pending when expiresAt <= now => "منتهية",
        InvitationStatus.Pending => "دعوة معلقة",
        InvitationStatus.Accepted => "مقبولة",
        InvitationStatus.Cancelled => "ملغاة",
        InvitationStatus.Reported => "أُبلغ عنها",
        _ => "منتهية",
    };

    private static async Task<IResult> List(RahoonDbContext db, RequestContext rc, IClock clock)
    {
        var now = clock.UtcNow;
        var list = await db.Invitations.AsNoTracking().Where(i => i.OrganizationId == rc.OrganizationId).OrderByDescending(i => i.CreatedAt)
            .Select(i => new
            {
                i.Id, i.Email, i.FullName, i.Status, i.ExpiresAt, i.CreatedAt, i.AcceptedAt,
                role = db.Roles.Where(r => r.Id == i.RoleId).Select(r => r.NameAr).FirstOrDefault(),
                team = db.Teams.Where(t => t.Id == i.TeamId).Select(t => t.NameAr).FirstOrDefault(),
            }).ToListAsync();
        return Results.Ok(list.Select(i => new
        {
            i.Id, i.Email, i.FullName, i.role, i.team, i.ExpiresAt, i.CreatedAt, i.AcceptedAt,
            status = i.Status == InvitationStatus.Pending && i.ExpiresAt <= now ? "expired" : i.Status.ToString().ToLowerInvariant(),
            statusLabel = StatusLabel(i.Status, i.ExpiresAt, now),
            canResend = i.Status is InvitationStatus.Pending or InvitationStatus.Expired,
            canCancel = i.Status == InvitationStatus.Pending,
        }));
    }

    private static async Task<IResult> Create(CreateInvitationRequest req, RahoonDbContext db, RequestContext rc, StaffInvitationService service, AuthOptions options)
    {
        await using var tx = await db.Database.BeginTransactionAsync();
        var org = await db.Organizations.FirstAsync(o => o.Id == rc.OrganizationId);
        var issued = await service.CreateAsync(org, req, rc.UserId);
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Results.Ok(new { issued.Invitation.Id, issued.Invitation.ExpiresAt, sandboxToken = options.ExposeSandboxOtp ? issued.Token : null });
    }

    private static async Task<IResult> Resend(Guid id, RahoonDbContext db, RequestContext rc, StaffInvitationService service, AuditLog audit, AuthOptions options)
    {
        await using var tx = await db.Database.BeginTransactionAsync();
        var inv = await db.Invitations.FirstOrDefaultAsync(i => i.Id == id && i.OrganizationId == rc.OrganizationId) ?? throw new NotFoundException();
        if (inv.Status is not (InvitationStatus.Pending or InvitationStatus.Expired))
            throw new ConflictException("invitation_closed", "لا يمكن إعادة إرسال دعوة مقبولة أو ملغاة.");
        var org = await db.Organizations.FirstAsync(o => o.Id == rc.OrganizationId);
        var token = await service.ReissueAsync(org, inv);
        await audit.RecordAsync(new AuditEntry("user.invitation_resent", "إعادة إرسال دعوة مستخدم", Detail: Mask.Email(inv.Email)));
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Results.Ok(new { inv.Id, inv.ExpiresAt, sandboxToken = options.ExposeSandboxOtp ? token : null });
    }

    private static async Task<IResult> Cancel(Guid id, RahoonDbContext db, RequestContext rc, AuditLog audit)
    {
        await using var tx = await db.Database.BeginTransactionAsync();
        var inv = await db.Invitations.FirstOrDefaultAsync(i => i.Id == id && i.OrganizationId == rc.OrganizationId) ?? throw new NotFoundException();
        if (inv.Status != InvitationStatus.Pending) throw new ConflictException("invitation_closed", "الدعوة ليست معلقة.");
        inv.Status = InvitationStatus.Cancelled;
        var invited = await db.Memberships.Where(m => m.OrganizationId == inv.OrganizationId && m.Status == MembershipStatus.Invited && m.User!.Email == inv.Email).ToListAsync();
        foreach (var m in invited) m.Status = MembershipStatus.Revoked;
        await audit.RecordAsync(new AuditEntry("user.invitation_cancelled", "إلغاء دعوة مستخدم", Detail: Mask.Email(inv.Email)));
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Results.Ok(new { status = "cancelled" });
    }

    // ───────── Public (S05) ─────────

    private static Task<Invitation?> FindAsync(RahoonDbContext db, string token) =>
        db.Invitations.IgnoreQueryFilters().FirstOrDefaultAsync(i => i.TokenHash == Tokens.Sha256(token ?? ""));

    private static async Task<IResult> Get(string token, RahoonDbContext db, RequestContext rc, IClock clock)
    {
        using var _ = rc.BeginSystemScope();
        var inv = await FindAsync(db, token);
        if (inv is null) return Results.Ok(new { status = "invalid" });
        var status = inv.Status switch
        {
            InvitationStatus.Accepted => "used",
            InvitationStatus.Cancelled or InvitationStatus.Reported => "revoked",
            InvitationStatus.Pending when inv.ExpiresAt > clock.UtcNow => "active",
            _ => "expired",
        };
        if (status != "active") return Results.Ok(new { status });
        var org = await db.Organizations.FirstAsync(o => o.Id == inv.OrganizationId);
        var role = await db.Roles.FirstAsync(r => r.Id == inv.RoleId);
        var team = inv.TeamId is { } t ? await db.Teams.Where(x => x.Id == t).Select(x => x.NameAr).FirstOrDefaultAsync() : null;
        var existing = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Email == inv.Email);
        return Results.Ok(new
        {
            status, organization = org.NameAr, initials = org.Initials, role = role.NameAr, team, expiresAt = inv.ExpiresAt,
            email = inv.Email, fullName = inv.FullName, phoneMasked = inv.Phone is { } ph ? Mask.Phone(ph) : null, needsPhone = inv.Phone is null,
            // Someone already holding a Rahoon account (another institution) confirms with their current password instead.
            existingAccount = existing?.PasswordHash is not null,
            policyVersion = StaffInvitationService.PolicyVersion,
            passwordRequirements = PasswordPolicy.Evaluate("", inv.Email, inv.FullName).Select(r => new { r.Key, r.Label }),
        });
    }

    private static async Task<IResult> PasswordCheck(string token, PasswordCheckRequest req, RahoonDbContext db, RequestContext rc, IClock clock)
    {
        using var _ = rc.BeginSystemScope();
        var inv = await FindAsync(db, token);
        if (inv is null || inv.Status != InvitationStatus.Pending || inv.ExpiresAt <= clock.UtcNow)
            throw new DomainException("invitation_invalid", "الدعوة منتهية أو غير صالحة. اطلب من مسؤول منشأتك دعوة جديدة.", StatusCodes.Status410Gone);
        var reqs = PasswordPolicy.Evaluate(req.Password, inv.Email, req.FullName ?? inv.FullName);
        return Results.Ok(new { valid = reqs.All(r => r.Met), requirements = reqs });
    }

    private static async Task<IResult> Accept(string token, AcceptInvitationRequest req, HttpContext http, RahoonDbContext db, RequestContext rc, IClock clock,
        IPasswordHasher<User> hasher, SessionService sessions, OtpService otp, AuditLog audit)
    {
        using var _ = rc.BeginSystemScope();
        await using var tx = await db.Database.BeginTransactionAsync();
        var inv = await FindAsync(db, token);
        var now = clock.UtcNow;
        if (inv is null || inv.Status != InvitationStatus.Pending || inv.ExpiresAt <= now)
        {
            if (inv is { Status: InvitationStatus.Pending }) { inv.Status = InvitationStatus.Expired; await db.SaveChangesAsync(); await tx.CommitAsync(); }
            throw new DomainException("invitation_invalid", "الدعوة منتهية أو مستخدمة أو ملغاة. اطلب من مسؤول منشأتك دعوة جديدة.", StatusCodes.Status410Gone);
        }

        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == inv.Email);
        var existingAccount = user?.PasswordHash is not null;
        var fullName = req.FullName?.Trim() ?? "";
        var phone = inv.Phone ?? CaseFactory.NormalizePhone(req.Phone);
        var v = new Validator()
            .Require(req.AckPolicy, "ackPolicy", "أقر بسياسة الاستخدام المقبول وسرية بيانات العملاء للمتابعة.")
            .Require(existingAccount || fullName.Length is > 2 and <= 120, "fullName", "أدخل الاسم الكامل.")
            .Require(phone is not null && CaseFactory.IsValidSaudiMobile(phone), "phone", "أدخل رقم جوال سعودي يبدأ بـ 05 للتحقق بخطوتين.");
        if (!existingAccount)
        {
            var reqs = PasswordPolicy.Evaluate(req.Password, inv.Email, fullName);
            foreach (var r in reqs.Where(r => !r.Met)) v.Require(false, "password", r.Label);
        }
        v.ThrowIfInvalid();

        if (existingAccount)
        {
            // Same generic refusal as login; never reveal more than needed.
            if (hasher.VerifyHashedPassword(user!, user!.PasswordHash!, req.CurrentPassword ?? "") == PasswordVerificationResult.Failed)
                throw new DomainException("invalid_credentials", "كلمة المرور الحالية غير صحيحة.", StatusCodes.Status401Unauthorized);
        }
        else
        {
            if (user is null)
            {
                user = new User { Email = inv.Email, FullName = fullName, Phone = phone };
                db.Users.Add(user);
            }
            user.FullName = fullName;
            user.Phone ??= phone;
            user.PasswordHash = hasher.HashPassword(user, req.Password!);
            user.PasswordChangedAt = now;
            user.MfaEnrolled = false;
            user.Status = UserStatus.Active;
        }

        var membership = await db.Memberships.Include(m => m.Roles).FirstOrDefaultAsync(m => m.UserId == user.Id && m.OrganizationId == inv.OrganizationId);
        if (membership is { Status: MembershipStatus.Active or MembershipStatus.Suspended })
            throw new ConflictException("already_member", "أنت عضو في هذه المنشأة بالفعل. سجّل الدخول.");
        if (membership is null)
        {
            membership = new Membership { OrganizationId = inv.OrganizationId, UserId = user.Id };
            db.Memberships.Add(membership);
        }
        membership.Status = MembershipStatus.Active;
        membership.TeamId = inv.TeamId ?? membership.TeamId;
        if (membership.Roles.Count != 1 || membership.Roles[0].RoleId != inv.RoleId)
        {
            membership.Roles.Clear();
            membership.Roles.Add(new MembershipRole { MembershipId = membership.Id, RoleId = inv.RoleId });
        }
        membership.Title ??= await db.Roles.Where(r => r.Id == inv.RoleId).Select(r => r.NameAr).FirstAsync();

        inv.Status = InvitationStatus.Accepted;
        inv.AcceptedAt = now;
        var org = await db.Organizations.FirstAsync(o => o.Id == inv.OrganizationId);
        if (org.Status == OrganizationStatus.Onboarding) org.Status = OrganizationStatus.Active; // first admin completes onboarding (PA02 «إعداد المنشأة»)
        await audit.RecordAsync(new AuditEntry("user.invitation_accepted", $"قبول دعوة وإنشاء حساب: {(existingAccount ? user.FullName : fullName)}",
            Detail: $"إقرار سياسة الاستخدام {StaffInvitationService.PolicyVersion} · {now.ToOffset(TimeSpan.FromHours(3)):yyyy-MM-dd HH:mm}", OrganizationId: inv.OrganizationId));
        await db.SaveChangesAsync();
        await tx.CommitAsync();

        // MFA step exactly as at login: pending session + SMS code, completed via /api/auth/mfa/verify.
        var session = await sessions.CreateAsync(http, user, SessionStage.MfaPending, SessionScope.None);
        var issued = await otp.IssueAsync(OtpPurpose.Login, user.Phone ?? phone!, user.Id, session.Id);
        return Results.Ok(new { mfaRequired = true, factor = "sms", destination = issued.DestinationMasked, issued.ResendInSeconds, sandboxCode = issued.SandboxCode });
    }

    private static async Task<IResult> Report(string token, RahoonDbContext db, RequestContext rc, Notifier notifier, AuditLog audit, IClock clock)
    {
        using var _ = rc.BeginSystemScope();
        var inv = await FindAsync(db, token);
        if (inv is null || inv.Status != InvitationStatus.Pending) return Results.Ok(new { status = "reported" }); // same answer, no oracle
        inv.Status = InvitationStatus.Reported;
        notifier.Notify(inv.InvitedByUserId, inv.OrganizationId, "security", "أُبلغ عن دعوة غير متوقعة",
            $"أبلغ مستلم الدعوة ({Mask.Email(inv.Email)}) أنه لا يعرفها، فأُبطلت.", "/settings/users", null, "warn");
        await audit.RecordAsync(new AuditEntry("user.invitation_reported", "إبلاغ عن دعوة غير متوقعة وإبطالها", Detail: Mask.Email(inv.Email), OrganizationId: inv.OrganizationId));
        await db.SaveChangesAsync();
        return Results.Ok(new { status = "reported" });
    }
}
