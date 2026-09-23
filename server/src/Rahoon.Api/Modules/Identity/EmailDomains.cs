using System.Text.RegularExpressions;

namespace Rahoon.Api.Modules.Identity;

/// <summary>Institutional e-mail rules: personal/free domains are refused for demo requests (S02) and institution domains (A01).</summary>
public static partial class EmailDomains
{
    public static readonly IReadOnlySet<string> Personal = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "gmail.com", "googlemail.com", "hotmail.com", "hotmail.sa", "outlook.com", "outlook.sa", "live.com", "msn.com", "yahoo.com", "yahoo.co.uk",
        "ymail.com", "icloud.com", "me.com", "mac.com", "aol.com", "proton.me", "protonmail.com", "gmx.com", "gmx.net", "mail.com", "yandex.com",
        "yandex.ru", "zoho.com", "tutanota.com", "fastmail.com", "hey.com", "mail.ru", "qq.com", "163.com", "inbox.com",
    };

    [GeneratedRegex(@"^(?=.{4,253}$)([a-z0-9]([a-z0-9-]{0,61}[a-z0-9])?\.)+[a-z]{2,24}$")]
    private static partial Regex DomainRegex();

    public static bool IsValidDomain(string? domain) => domain is not null && DomainRegex().IsMatch(domain.Trim().ToLowerInvariant());

    public static bool IsPersonal(string? emailOrDomain)
    {
        var d = (emailOrDomain ?? "").Trim().ToLowerInvariant();
        if (d.Contains('@')) d = d[(d.IndexOf('@') + 1)..];
        return Personal.Contains(d);
    }
}
