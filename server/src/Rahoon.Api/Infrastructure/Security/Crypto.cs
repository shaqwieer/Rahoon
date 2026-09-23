using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.DataProtection;

namespace Rahoon.Api.Infrastructure.Security;

public static class Tokens
{
    public static string NewToken(int bytes = 32) => Base64Url(RandomNumberGenerator.GetBytes(bytes));

    public static byte[] Sha256(string value) => SHA256.HashData(Encoding.UTF8.GetBytes(value));

    public static string Sha256Hex(string value) => Convert.ToHexStringLower(Sha256(value));

    public static bool FixedEquals(byte[] a, byte[] b) => CryptographicOperations.FixedTimeEquals(a, b);

    public static string NumericCode(int digits = 6)
    {
        var max = (int)Math.Pow(10, digits);
        return RandomNumberGenerator.GetInt32(0, max).ToString($"D{digits}");
    }

    private static string Base64Url(byte[] data) => Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}

/// <summary>
/// Encrypts personal data at rest (national id, phone, deed number) with ASP.NET Core
/// Data Protection and produces keyed lookup hashes. In production the key ring must
/// be protected by a KMS/HSM — see docs/architecture.md.
/// </summary>
public sealed class PiiProtector
{
    private readonly IDataProtector _protector;
    private readonly byte[] _lookupKey;

    public PiiProtector(IDataProtectionProvider provider, IConfiguration config)
    {
        _protector = provider.CreateProtector("Rahoon.Pii.v1");
        var key = config["Security:PiiLookupKey"];
        _lookupKey = Encoding.UTF8.GetBytes(string.IsNullOrWhiteSpace(key) ? "dev-only-lookup-key-change-me" : key);
    }

    public string Protect(string plain) => _protector.Protect(plain);

    public string Unprotect(string cipher) => _protector.Unprotect(cipher);

    public string LookupHash(string normalized) =>
        Convert.ToHexStringLower(HMACSHA256.HashData(_lookupKey, Encoding.UTF8.GetBytes(normalized)));
}

/// <summary>Server-side masking rules (A-10). The UI never receives unmasked values except via an audited reveal.</summary>
public static class Mask
{
    private const char Dot = '•';

    /// <summary>First digit + last two: 1•••••••42.</summary>
    public static string NationalId(string id)
    {
        var d = new string(id.Where(char.IsDigit).ToArray());
        if (d.Length < 4) return new string(Dot, d.Length);
        return d[0] + new string(Dot, d.Length - 3) + d[^2..];
    }

    /// <summary>Saudi mobile as +966 5• ••• ••81.</summary>
    public static string Phone(string phone)
    {
        var d = new string(phone.Where(char.IsDigit).ToArray());
        if (d.StartsWith("966")) d = d[3..];
        if (d.StartsWith('0')) d = d[1..];
        if (d.Length < 3) return "+966 •••";
        return $"+966 {d[0]}{Dot} {Dot}{Dot}{Dot} {Dot}{Dot}{d[^2..]}";
    }

    /// <summary>First name + initial of the last name: «عبدالله م.».</summary>
    public static string PersonName(string fullName)
    {
        var parts = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return fullName;
        if (parts.Length == 1) return parts[0];
        return $"{parts[0]} {parts[1][0]}.";
    }

    /// <summary>Keep the first seven characters of a contract/deed number: MF-88-3317•••.</summary>
    public static string Reference(string value, int keep = 10)
    {
        if (value.Length <= keep) return value;
        return value[..keep] + new string(Dot, 3);
    }

    public static string Deed(string deed)
    {
        var d = new string(deed.Where(char.IsDigit).ToArray());
        if (d.Length < 4) return new string(Dot, d.Length);
        return d[0] + new string(Dot, d.Length - 3) + d[^2..];
    }

    public static string Email(string email)
    {
        var at = email.IndexOf('@');
        if (at <= 1) return email;
        return email[0] + new string(Dot, 3) + email[at..];
    }

    public static string Ip(string? ip)
    {
        if (string.IsNullOrEmpty(ip)) return "";
        var parts = ip.Split('.');
        return parts.Length == 4 ? $"{parts[0]}.{parts[1]}.•.•" : "•";
    }

    /// <summary>Opaque id for platform monitoring (PA06): C-7F3A…91.</summary>
    public static string OpaqueCaseId(Guid caseId, string salt)
    {
        var h = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(salt + caseId)));
        return $"C-{h[..4]}…{h[^2..]}";
    }
}
