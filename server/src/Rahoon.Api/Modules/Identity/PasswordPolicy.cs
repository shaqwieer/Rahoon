namespace Rahoon.Api.Modules.Identity;

public sealed record PasswordRequirement(string Key, string Label, bool Met);

/// <summary>
/// Staff password policy (S05): at least 12 characters, not a known breached/common password, and not
/// containing the user's name or e-mail. The breached check is a local list; production should add a
/// k-anonymity breached-password service (integration not contracted).
/// </summary>
public static class PasswordPolicy
{
    public const int MinLength = 12;

    private static readonly HashSet<string> Common = new(StringComparer.OrdinalIgnoreCase)
    {
        "123456789012", "1234567890123", "12345678901234", "111111111111", "000000000000", "123123123123",
        "password1234", "password123!", "passw0rd1234", "p@ssw0rd1234", "p@ssword1234", "qwertyuiop12", "qwerty123456",
        "qwertyuiopas", "iloveyou1234", "welcome12345", "welcome@2026", "admin1234567", "administrator", "letmein12345",
        "abc123456789", "aa1234567890", "1q2w3e4r5t6y", "zaq12wsxcde3", "changeme1234", "football1234", "baseball1234",
        "summer2026!!", "ramadan12345", "saudi1234567", "riyadh123456", "rahoon123456", "rahoon@12345",
    };

    public static IReadOnlyList<PasswordRequirement> Evaluate(string? password, string email, string? fullName)
    {
        var p = password ?? "";
        var lower = p.ToLowerInvariant();
        var local = email.Split('@')[0].ToLowerInvariant();
        var emailParts = local.Split(['.', '_', '-', '+'], StringSplitOptions.RemoveEmptyEntries).Where(x => x.Length >= 3).Append(local);
        var nameParts = (fullName ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries).Where(x => x.Length >= 3);
        var personal = emailParts.Concat(nameParts).Any(part => lower.Contains(part.ToLowerInvariant())) || lower.Contains(email.ToLowerInvariant());
        return
        [
            new("length", $"{MinLength} حرفاً على الأقل", p.Length >= MinLength),
            new("not_breached", "ليست ضمن كلمات مسرّبة", p.Length > 0 && !Common.Contains(p) && p.Distinct().Count() > 3),
            new("no_personal", "لا تحتوي اسمك أو بريدك", p.Length > 0 && !personal),
        ];
    }

    public static bool IsValid(string? password, string email, string? fullName) => Evaluate(password, email, fullName).All(r => r.Met);
}
