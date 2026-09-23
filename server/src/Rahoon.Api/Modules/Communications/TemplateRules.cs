using System.Text.RegularExpressions;

namespace Rahoon.Api.Modules.Communications;

public sealed record ToneCheck(string Key, string Label, bool Ok, string Level); // level: block | warn | info

/// <summary>
/// A05 template validation: variables must be declared for the template (Arabic/SMS) or be the English alias of a
/// declared variable; the tone check flags threatening/coercive wording (blocking), missing deadline or «how to ask»
/// and length ≥ 60 words (warnings), and whether the English variant was updated with the Arabic one (info).
/// </summary>
public static partial class TemplateRules
{
    public const int MaxWords = 60;

    /// <summary>Known variables: Arabic placeholder → English alias used in the English body.</summary>
    public static readonly IReadOnlyDictionary<string, string> EnglishAlias = new Dictionary<string, string>
    {
        ["{القسط}"] = "{installment}", ["{المدة}"] = "{term}", ["{تاريخ_الصلاحية}"] = "{expiry}", ["{اسم_المسؤول}"] = "{officer_name}",
        ["{المستند}"] = "{document}", ["{المهلة}"] = "{deadline}", ["{السبب}"] = "{reason}", ["{الموعد}"] = "{appointment}",
    };

    private static readonly string[] ThreatAr =
    [
        "إجراءات قانونية", "الإجراءات القانونية", "المحكمة", "القضاء", "سنضطر", "إنذار", "فوراً", "فورا", "عقوبة", "ملزم", "يجب عليك", "وإلا",
        "الحجز", "مصادرة", "آخر فرصة", "نهائي وأخير", "التشهير", "الشرطة",
    ];

    private static readonly string[] ThreatEn =
    [
        "legal action", "court", "immediately", "final warning", "you must", "otherwise", "seize", "penalty", "last chance", "police",
    ];

    private static readonly string[] DeadlineMarkers = ["{تاريخ_الصلاحية}", "{المهلة}", "{الموعد}", "حتى", "قبل"];
    private static readonly string[] AskMarkers = ["اكتب لنا", "سؤال", "استفسار", "تواصل", "راسلنا", "اتصل"];

    [GeneratedRegex(@"\{[^{}\s]+\}")]
    private static partial Regex VariableRegex();

    public static IReadOnlyList<string> Variables(string? body) =>
        body is null ? [] : VariableRegex().Matches(body).Select(m => m.Value).Distinct().ToList();

    /// <summary>Unknown variables per field; empty when valid.</summary>
    public static Dictionary<string, string[]> UnknownVariables(IReadOnlyCollection<string> declared, string? bodyAr, string? bodyEn, string? bodySms)
    {
        var errors = new Dictionary<string, string[]>();
        var allowedEn = declared.Select(d => EnglishAlias.GetValueOrDefault(d)).Where(x => x is not null).ToHashSet();
        void Check(string field, string? body, Func<string, bool> ok)
        {
            var bad = Variables(body).Where(v => !ok(v)).ToList();
            if (bad.Count > 0) errors[field] = [$"متغيرات غير معروفة: {string.Join("، ", bad)}"];
        }
        Check("bodyAr", bodyAr, declared.Contains);
        Check("bodySms", bodySms, declared.Contains);
        Check("bodyEn", bodyEn, v => allowedEn.Contains(v));
        return errors;
    }

    public static int WordCount(string? text) => (text ?? "").Split([' ', '\n', '\t', '\r'], StringSplitOptions.RemoveEmptyEntries).Length;

    public static IReadOnlyList<ToneCheck> Tone(string bodyAr, string? bodyEn, string? publishedAr, string? publishedEn)
    {
        var threat = ThreatAr.Any(bodyAr.Contains) || (bodyEn is not null && ThreatEn.Any(w => bodyEn.Contains(w, StringComparison.OrdinalIgnoreCase)));
        var deadline = DeadlineMarkers.Any(bodyAr.Contains);
        var ask = AskMarkers.Any(bodyAr.Contains);
        var words = WordCount(bodyAr);
        var arChanged = publishedAr is null || publishedAr != bodyAr;
        var enUpdated = !string.IsNullOrWhiteSpace(bodyEn) && (!arChanged || publishedEn is null || publishedEn != bodyEn);
        return
        [
            new("no_threat", threat ? "توجد كلمات تهديد أو إلزام" : "لا كلمات تهديد أو إلزام", !threat, "block"),
            new("deadline_and_help", deadline && ask ? "يذكر المهلة وطريقة السؤال" : !deadline ? "لا يذكر المهلة" : "لا يذكر طريقة السؤال", deadline && ask, "warn"),
            new("under_60_words", words < MaxWords ? $"أقل من {MaxWords} كلمة" : $"{words} كلمة — أطول من {MaxWords}", words < MaxWords, "warn"),
            new("english_updated", enUpdated ? "النسخة الإنجليزية محدثة؟ نعم" : "النسخة الإنجليزية محدثة؟ لا", enUpdated, "info"),
        ];
    }
}
