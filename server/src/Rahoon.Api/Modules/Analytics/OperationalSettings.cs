using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Rahoon.Api.Infrastructure.Persistence;

namespace Rahoon.Api.Modules.Analytics;

public sealed record SettingDef(string Key, string TitleAr, string Description, string DefaultJson);

/// <summary>
/// O03 operational settings: catalog, defaults and validation. Values are JSON; the effective version is the one
/// approved by a second person. Consumers read through <see cref="DaysAsync"/> / <see cref="EffectiveJsonAsync"/>.
/// </summary>
public static class OperationalSettings
{
    public const string ObjectionDays = "referral.objection_period_days";
    public const string OwnerReadOnlyDays = "owner.closed_read_only_days";
    public const string RoutingRules = "routing.rules";
    public const string TeamCapacity = "team.capacity";
    public const string ReminderCadence = "reminders.cadence";

    public static readonly IReadOnlyList<SettingDef> Catalog =
    [
        new(RoutingRules, "قواعد الإسناد", "كل قاعدة بحالتها؛ «تجريبي» يقترح ولا ينفذ.",
            """
            [{"key":"new_case_least_load","title":"حالات جديدة ← مدير حالات بأقل عبء","description":"توزيع آلي ضمن المنطقة؛ يمكن للمدير إعادة الإسناد بسبب.","status":"enabled"},
             {"key":"valuer_rotation","title":"تقييم ← مقيّم من الدليل بالتناوب","description":"يستبعد المنتهية تراخيصهم تلقائياً.","status":"enabled"},
             {"key":"large_case_senior_analyst","title":"حالات فوق 3M ← محلل أول","description":"إلزامي.","status":"enabled","amountThreshold":3000000},
             {"key":"bottleneck_priority_suggestion","title":"اقتراح أولوية بناءً على الاختناق","description":"يقترح فقط؛ لا يعيد الترتيب دون موافقة.","status":"experimental"}]
            """),
        new(TeamCapacity, "سعة الفرق", "الحد الأقصى للحالات المفتوحة لكل فريق؛ الحمل الحالي يُحسب من الحالات.",
            """[{"team":"التحصيل — الرياض","max":140},{"team":"التحصيل — جدة","max":100},{"team":"المخاطر","max":30}]"""),
        new(ReminderCadence, "إيقاع التذكيرات للمالك", "حد أقصى 2 تذكير أسبوعياً، وضمن أوقات التواصل المفضلة. لا تذكير أثناء شكوى مفتوحة.",
            """{"steps":[{"day":0,"action":"إرسال العرض"},{"day":3,"action":"تذكير لطيف"},{"day":7,"action":"عرض مكالمة"},{"day":9,"action":"تذكير أخير بالمهلة"}],"maxPerWeek":2,"respectContactWindows":true,"suppressDuringOpenComplaint":true}"""),
        new(ObjectionDays, "مهلة الاعتراض قبل الإحالة", "بالأيام من تاريخ الإشعار المسبق (افتراض 15 يوماً — يتطلب تأكيداً قانونياً).", """{"days":15}"""),
        new(OwnerReadOnlyDays, "وصول المالك بعد الإغلاق", "قراءة فقط بالأيام بعد الإغلاق (افتراض 90 يوماً).", """{"days":90}"""),
    ];

    public static SettingDef? Find(string key) => Catalog.FirstOrDefault(c => c.Key == key);

    public static async Task<string> EffectiveJsonAsync(RahoonDbContext db, Guid orgId, string key)
    {
        var json = await db.Set<OperationalSetting>().AsNoTracking()
            .Where(s => s.OrganizationId == orgId && s.Key == key && s.Status == SettingChangeStatus.Effective)
            .Select(s => s.ValueJson).FirstOrDefaultAsync();
        return json ?? Find(key)?.DefaultJson ?? "null";
    }

    public static async Task<int> DaysAsync(RahoonDbContext db, Guid orgId, string key, int fallback)
    {
        try
        {
            var node = JsonNode.Parse(await EffectiveJsonAsync(db, orgId, key));
            return node?["days"]?.GetValue<int>() ?? fallback;
        }
        catch (Exception e) when (e is JsonException or InvalidOperationException or FormatException)
        {
            return fallback;
        }
    }

    /// <summary>Server-side guards for a proposed value (specO3 «الحدود»). Returns Arabic problems; empty = valid.</summary>
    public static List<string> Validate(string key, JsonNode? value)
    {
        var problems = new List<string>();
        if (value is null) { problems.Add("القيمة مطلوبة."); return problems; }
        try
        {
            switch (key)
            {
                case ObjectionDays:
                {
                    var d = value["days"]?.GetValue<int>();
                    if (d is null or < 15 or > 60) problems.Add("مهلة الاعتراض بين 15 و60 يوماً.");
                    break;
                }
                case OwnerReadOnlyDays:
                {
                    var d = value["days"]?.GetValue<int>();
                    if (d is null or < 30 or > 365) problems.Add("وصول المالك بعد الإغلاق بين 30 و365 يوماً.");
                    break;
                }
                case ReminderCadence:
                {
                    var max = value["maxPerWeek"]?.GetValue<int>();
                    if (max is null or < 0 or > 2) problems.Add("الحد الأقصى تذكيران أسبوعياً.");
                    if (value["respectContactWindows"]?.GetValue<bool>() != true) problems.Add("التذكيرات تحترم أوقات التواصل المفضلة دائماً.");
                    if (value["suppressDuringOpenComplaint"]?.GetValue<bool>() != true) problems.Add("لا تذكير أثناء شكوى مفتوحة.");
                    if (value["steps"] is not JsonArray steps || steps.Count == 0) problems.Add("أضف خطوة واحدة على الأقل.");
                    else if (steps.Any(s => s?["day"]?.GetValue<int>() is null or < 0 || string.IsNullOrWhiteSpace(s?["action"]?.GetValue<string>())))
                        problems.Add("كل خطوة تحتاج يوماً وإجراءً.");
                    break;
                }
                case TeamCapacity:
                {
                    if (value is not JsonArray teams || teams.Count == 0) problems.Add("أضف فريقاً واحداً على الأقل.");
                    else if (teams.Any(t => string.IsNullOrWhiteSpace(t?["team"]?.GetValue<string>()) || t?["max"]?.GetValue<int>() is null or <= 0))
                        problems.Add("لكل فريق اسم وسعة قصوى أكبر من صفر.");
                    break;
                }
                case RoutingRules:
                {
                    if (value is not JsonArray rules || rules.Count == 0) problems.Add("أضف قاعدة واحدة على الأقل.");
                    else
                    {
                        foreach (var r in rules)
                        {
                            var status = r?["status"]?.GetValue<string>();
                            if (status is not ("enabled" or "experimental" or "disabled")) problems.Add("حالة القاعدة: مفعّل أو تجريبي أو معطّل.");
                            if (string.IsNullOrWhiteSpace(r?["key"]?.GetValue<string>()) || string.IsNullOrWhiteSpace(r?["title"]?.GetValue<string>()))
                                problems.Add("لكل قاعدة مفتاح وعنوان.");
                        }
                        // Mandatory safeguard: the large-case rule cannot be switched off (O03 «إلزامي»).
                        if (!rules.Any(r => r?["key"]?.GetValue<string>() == "large_case_senior_analyst" && r?["status"]?.GetValue<string>() == "enabled"))
                            problems.Add("قاعدة «حالات فوق 3M ← محلل أول» إلزامية ولا تُعطّل.");
                    }
                    break;
                }
                default:
                    problems.Add("إعداد غير معروف.");
                    break;
            }
        }
        catch (Exception e) when (e is InvalidOperationException or FormatException or JsonException)
        {
            problems.Add("صيغة القيمة غير صحيحة.");
        }
        return problems.Distinct().ToList();
    }
}
