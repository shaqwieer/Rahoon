using Microsoft.EntityFrameworkCore;
using Rahoon.Api.Infrastructure.Auth;
using Rahoon.Api.Infrastructure.Http;
using Rahoon.Api.Infrastructure.Integrations;
using Rahoon.Api.Infrastructure.Persistence;
using Rahoon.Api.Infrastructure.Security;
using Rahoon.Api.Infrastructure.Time;

namespace Rahoon.Api.Modules.Identity;

/// <param name="Required">false when SMS confirmation is off: the web confirms with <paramref name="SandboxCode"/> without showing a code step.</param>
public sealed record OtpIssued(Guid ChallengeId, string DestinationMasked, int ExpiresInSeconds, int ResendInSeconds, string? SandboxCode, bool Required);

/// <summary>One-time codes over the (sandboxed) SMS channel: 6 digits, 3 attempts, 5-minute expiry, 60-second resend cooldown.
/// With <see cref="AuthOptions.SmsConfirmation"/> off, nothing is sent and the code is handed back for automatic confirmation.</summary>
public sealed class OtpService(RahoonDbContext db, IClock clock, ISmsGateway sms, AuthOptions options)
{
    public const int MaxAttempts = 3;
    public const int ResendCooldownSeconds = 60;

    /// <param name="send">false issues a decoy challenge that is never delivered (anti-enumeration); its code is never echoed.</param>
    public async Task<OtpIssued> IssueAsync(OtpPurpose purpose, string phone, Guid? userId, Guid? sessionId, string? context = null,
        bool send = true)
    {
        var now = clock.UtcNow;
        var recent = await db.OtpChallenges
            .Where(o => o.SessionId == sessionId && o.Purpose == purpose && o.ConsumedAt == null && o.Context == context)
            .OrderByDescending(o => o.CreatedAt).FirstOrDefaultAsync();
        if (recent is not null && (now - recent.CreatedAt).TotalSeconds < ResendCooldownSeconds)
        {
            var wait = ResendCooldownSeconds - (int)(now - recent.CreatedAt).TotalSeconds;
            throw new DomainException("otp_cooldown", $"يمكنك طلب رمز جديد بعد {wait} ثانية.", StatusCodes.Status429TooManyRequests);
        }
        if (recent is not null) recent.ConsumedAt = now; // supersede

        var code = Tokens.NumericCode();
        var challenge = new OtpChallenge
        {
            UserId = userId, SessionId = sessionId, Purpose = purpose, CodeHash = Tokens.Sha256(code + ":" + purpose),
            Destination = Mask.Phone(phone), Context = context, CreatedAt = now, ExpiresAt = now.AddMinutes(options.OtpMinutes),
        };
        db.OtpChallenges.Add(challenge);
        var text = $"رمز الدخول إلى رهون: {code}. صالح 5 دقائق. لا تشاركه مع أحد.";
        if (send && options.SmsConfirmation) await sms.SendAsync(phone, text);
        await db.SaveChangesAsync();
        // A decoy never returns its code and still asks for one, so it looks like a real challenge that was not delivered.
        var autoConfirm = send && !options.SmsConfirmation;
        return new OtpIssued(challenge.Id, challenge.Destination!, options.OtpMinutes * 60, ResendCooldownSeconds,
            send && (autoConfirm || options.ExposeSandboxOtp) ? code : null, Required: !autoConfirm);
    }

    /// <summary>Verifies and consumes. Throws with remaining attempts; returns false when attempts are exhausted (caller locks).</summary>
    public async Task<bool> VerifyAsync(OtpPurpose purpose, Guid? sessionId, string code, string? context = null)
    {
        var now = clock.UtcNow;
        var ch = await db.OtpChallenges
            .Where(o => o.SessionId == sessionId && o.Purpose == purpose && o.ConsumedAt == null && o.Context == context)
            .OrderByDescending(o => o.CreatedAt).FirstOrDefaultAsync();
        if (ch is null || ch.ExpiresAt < now)
            throw new DomainException("otp_expired", "انتهت صلاحية الرمز. اطلب رمزاً جديداً.", StatusCodes.Status410Gone);

        var normalized = new string((code ?? "").Where(char.IsDigit).ToArray());
        if (normalized.Length == 6 && Tokens.FixedEquals(ch.CodeHash, Tokens.Sha256(normalized + ":" + purpose)))
        {
            ch.ConsumedAt = now;
            await db.SaveChangesAsync();
            return true;
        }

        ch.Attempts++;
        var left = MaxAttempts - ch.Attempts;
        if (left <= 0) ch.ConsumedAt = now;
        await db.SaveChangesAsync();
        if (left <= 0) return false;
        var msg = left switch { 1 => "الرمز غير صحيح. تبقّت محاولة واحدة.", 2 => "الرمز غير صحيح. تبقّت محاولتان.", _ => $"الرمز غير صحيح. تبقّت {left} محاولات." };
        throw new DomainException("otp_invalid", msg, StatusCodes.Status401Unauthorized, [left.ToString()]);
    }
}
