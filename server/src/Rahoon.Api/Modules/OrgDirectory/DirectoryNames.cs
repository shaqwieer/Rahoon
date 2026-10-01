using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Rahoon.Api.Modules.OrgDirectory;

/// <summary>Name normalization used to match and deduplicate organizations across sources and manual entries.</summary>
public static partial class DirectoryNames
{
    // Legal-form words that do not distinguish one organization from another.
    private static readonly string[] ArabicNoise =
        ["شركة", "شركه", "مساهمة", "مساهمه", "المساهمة", "المساهمه", "مقفلة", "مقفله", "المقفلة", "المقفله", "محدودة", "محدوده", "المحدودة", "المحدوده",
         "ذات", "المسؤولية", "المسئولية", "مسؤولية", "سعودية", "سعوديه", "عامة", "عامه"];
    private static readonly string[] EnglishNoise =
        ["company", "co", "ltd", "limited", "llc", "plc", "inc", "the", "jsc", "cjsc", "closed", "joint", "stock", "saudi", "sjsc", "csjc"];

    /// <summary>Arabic: strips diacritics and tatweel, unifies alef/hamza, taa marbuta, alef maqsura, digits and spacing, drops legal-form words.</summary>
    public static string Normalize(string? arabic)
    {
        if (string.IsNullOrWhiteSpace(arabic)) return "";
        var sb = new StringBuilder(arabic.Length);
        // NFKC first: some sources use Arabic presentation forms (U+FExx) that look identical but compare differently.
        foreach (var ch in arabic.Normalize(NormalizationForm.FormKC))
        {
            var c = ch switch
            {
                'أ' or 'إ' or 'آ' or 'ٱ' => 'ا',
                'ة' => 'ه',
                'ى' => 'ي',
                'ؤ' => 'و',
                'ئ' => 'ي',
                'ـ' => '\0',
                >= 'ً' and <= 'ٟ' or 'ٰ' => '\0', // harakat, superscript alef
                >= '٠' and <= '٩' => (char)('0' + (ch - '٠')),
                _ => char.ToLowerInvariant(ch),
            };
            if (c == '\0') continue;
            sb.Append(char.IsLetterOrDigit(c) ? c : ' ');
        }
        var noise = ArabicNoise.Select(w => Normalize1(w)).ToHashSet();
        var words = sb.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries).Where(w => !noise.Contains(w)).ToList();
        return string.Join(' ', words);
    }

    private static string Normalize1(string w) => w.Replace('ة', 'ه').Replace('أ', 'ا').Replace('إ', 'ا').Replace('آ', 'ا').Replace('ى', 'ي').Replace('ئ', 'ي').Replace('ؤ', 'و');

    /// <summary>English: lower case, letters/digits only, drops legal-form words.</summary>
    public static string NormalizeEnglish(string? english)
    {
        if (string.IsNullOrWhiteSpace(english)) return "";
        var decomposed = english.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposed.Length);
        foreach (var ch in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark) continue;
            sb.Append(char.IsLetterOrDigit(ch) ? char.ToLowerInvariant(ch) : ' ');
        }
        var words = sb.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries).Where(w => !EnglishNoise.Contains(w));
        return string.Join(' ', words);
    }

    /// <summary>Collapses inner whitespace and trims; returns null for blank input.</summary>
    public static string? Clean(string? value, int max = 200)
    {
        if (value is null) return null;
        var v = Spaces().Replace(value.Normalize(NormalizationForm.FormKC), " ").Trim();
        if (v.Length == 0) return null;
        return v.Length > max ? v[..max] : v;
    }

    /// <summary>An http(s) URL with a host, in its absolute form (path and query kept); null when the input is not one.</summary>
    public static string? CleanUrl(string? value)
    {
        var v = Clean(value, 500);
        if (v is null) return null;
        if (!v.Contains("://")) v = "https://" + v;
        if (!Uri.TryCreate(v, UriKind.Absolute, out var u) || (u.Scheme != Uri.UriSchemeHttps && u.Scheme != Uri.UriSchemeHttp) || !u.Host.Contains('.'))
            return null;
        var abs = u.AbsoluteUri;
        return u.AbsolutePath == "/" && u.Query.Length == 0 ? abs.TrimEnd('/') : abs;
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex Spaces();
}
