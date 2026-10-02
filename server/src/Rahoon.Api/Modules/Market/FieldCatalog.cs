using System.Globalization;
using System.Text.RegularExpressions;

namespace Rahoon.Api.Modules.Market;

public enum FieldType { Integer, Decimal, Money, Text, LongText, Select, MultiSelect, Boolean, Date, Month }

public sealed record FieldOption(string Value, string Label, string[]? PropertyTypes = null);

/// <summary>The field applies only when another answer has one of these values (e.g. arrears_amount when arrears_state = has).</summary>
public sealed record FieldCondition(string Key, string[] Values);

/// <param name="Scope">property (the request) or obligation (one obligation of the request).</param>
/// <param name="SubmitAnswer">Must be answered — a value or «لا أعرف» — before the first request is sent.</param>
/// <param name="PublishKnown">Must be a known value (not «لا أعرف») before an opportunity can be published.</param>
/// <param name="Initial">Asked in step 2 of the first request («الأرقام الأساسية»); every other field is in the completion file.</param>
public sealed record FieldDef(
    string Key, string Scope, string Label, FieldType Type,
    string[]? PropertyTypes = null, string[]? ObligationKinds = null,
    bool SubmitAnswer = false, bool PublishKnown = false, bool Initial = false, bool AllowUnknown = false,
    decimal? Min = null, decimal? Max = null, string? Unit = null, string? Help = null,
    FieldOption[]? Options = null, FieldCondition? When = null);

public sealed record DocumentDef(string Key, string Label, string[] ObligationKinds, bool PublishRequired, string? Help = null);

public sealed record CityDef(string Key, string Label, double Lat, double Lng, string[] Districts);

/// <summary>
/// The single, extensible rule set for sale-request fields (docs/product/product-definition.md §4). The API serves it to the
/// web (GET /api/market/catalog) and applies the same rules on every save: fields of an inactive branch (another property
/// type, obligation kind or unmet condition) are dropped, never validated and never block a submit.
/// </summary>
public static class FieldCatalog
{
    public const string Unknown = "__unknown";

    public static readonly FieldOption[] PropertyTypes =
    [
        new("apartment", "شقة"), new("duplex", "دوبلكس"), new("villa", "فيلا"), new("townhouse", "تاون هاوس"),
        new("land", "أرض"), new("office", "مكتب"), new("shop", "محل"),
    ];

    public static readonly FieldOption[] ObligationModes =
    [
        new("developer", "مطور عقاري"), new("financier", "بنك أو جهة تمويل"),
    ];

    public static readonly FieldOption[] ObligationKinds = [new("developer", "مطور عقاري"), new("financier", "بنك أو جهة تمويل")];

    public static readonly FieldOption[] Frequencies =
    [
        new("monthly", "شهري"), new("quarterly", "ربع سنوي"), new("semiannual", "نصف سنوي"), new("annual", "سنوي"),
    ];

    private static readonly string[] Residential = ["apartment", "duplex", "villa", "townhouse"];
    private static readonly string[] Houses = ["villa", "townhouse"];
    private static readonly string[] NotLand = ["apartment", "duplex", "villa", "townhouse", "office", "shop"];
    private static readonly string[] Dev = ["developer"];
    private static readonly string[] Fin = ["financier"];
    private static readonly string[] Both = ["developer", "financier"];
    private static readonly FieldOption[] YesNoUnknown = [new("yes", "نعم"), new("no", "لا"), new("unknown", "لا أعرف")];
    private static readonly FieldCondition ArrearsHas = new("arrears_state", ["has"]);

    public static readonly FieldDef[] Fields =
    [
        // ── Property (the request): details and location ──
        new("area", "property", "المساحة", FieldType.Decimal, PublishKnown: true, Min: 10, Max: 1_000_000, Unit: "م²",
            Help: "مساحة الوحدة كما في العقد أو الصك."),
        new("land_area", "property", "مساحة الأرض", FieldType.Decimal, PropertyTypes: Houses, Min: 10, Max: 1_000_000, Unit: "م²", AllowUnknown: true),
        new("built_area", "property", "مساحة البناء", FieldType.Decimal, PropertyTypes: Houses, Min: 10, Max: 1_000_000, Unit: "م²", AllowUnknown: true),
        new("bedrooms", "property", "غرف النوم", FieldType.Integer, PropertyTypes: Residential, PublishKnown: true, Min: 0, Max: 20),
        new("bathrooms", "property", "دورات المياه", FieldType.Integer, PropertyTypes: Residential, Min: 0, Max: 20),
        new("floor", "property", "الدور", FieldType.Integer, PropertyTypes: ["apartment", "duplex", "office", "shop"], Min: -2, Max: 100,
            Help: "الدور الأرضي = 0."),
        new("floors_count", "property", "عدد الأدوار", FieldType.Integer, PropertyTypes: Houses, Min: 1, Max: 10),
        new("elevator", "property", "يوجد مصعد", FieldType.Boolean, PropertyTypes: ["apartment", "duplex", "office"]),
        new("parking", "property", "الموقف", FieldType.Select, PropertyTypes: ["apartment", "duplex", "villa", "townhouse", "office"],
            Options: [new("none", "لا يوجد"), new("one", "موقف واحد"), new("two_plus", "موقفان أو أكثر")]),
        new("garden", "property", "يوجد فناء أو حديقة", FieldType.Boolean, PropertyTypes: Houses),
        new("finishing", "property", "التشطيب", FieldType.Select, PropertyTypes: NotLand,
            Options: [new("none", "بدون تشطيب"), new("partial", "تشطيب جزئي"), new("full", "تشطيب كامل")]),
        new("land_use", "property", "الاستخدام", FieldType.Select, PropertyTypes: ["land"], PublishKnown: true,
            Options: [new("residential", "سكني"), new("commercial", "تجاري"), new("mixed", "سكني تجاري"), new("agricultural", "زراعي"), new("industrial", "صناعي")]),
        new("unit_use", "property", "الاستخدام", FieldType.Select, PropertyTypes: ["office", "shop"],
            Options: [new("office", "مكتبي"), new("retail", "تجاري"), new("clinic", "عيادة"), new("restaurant", "مطعم أو مقهى"), new("other", "أخرى")]),
        new("frontages", "property", "عدد الواجهات", FieldType.Integer, PropertyTypes: ["land"], Min: 1, Max: 4, AllowUnknown: true),
        new("street_width", "property", "عرض الشارع", FieldType.Decimal, PropertyTypes: ["land"], Min: 1, Max: 200, Unit: "م", AllowUnknown: true),
        new("readiness", "property", "حالة العقار", FieldType.Select, PropertyTypes: NotLand, PublishKnown: true,
            Options: [new("ready", "جاهز"), new("under_construction", "تحت الإنشاء")]),
        new("delivery_month", "property", "موعد التسليم المتوقع", FieldType.Month, PropertyTypes: NotLand, AllowUnknown: true,
            When: new("readiness", ["under_construction"]), Help: "كما في عقدك أو بحسب إعلان المطور."),
        new("description", "property", "وصف قصير", FieldType.LongText, PublishKnown: true, Max: 1500,
            Help: "ما يساعد المشتري: الإطلالة، الحالة، القرب من الخدمات. لا تكتب بيانات شخصية أو رقم جوال."),
        new("features", "property", "المميزات", FieldType.MultiSelect,
            Options:
            [
                new("maid_room", "غرفة خادمة", Residential), new("driver_room", "غرفة سائق", Houses), new("roof", "سطح خاص", ["apartment", "duplex", "villa", "townhouse"]),
                new("private_entrance", "مدخل خاص", Residential), new("central_ac", "تكييف مركزي", NotLand), new("kitchen", "مطبخ مجهز", Residential),
                new("annex", "ملحق", Houses), new("pool", "مسبح", Houses), new("storage", "مستودع", NotLand),
                new("storefront", "واجهة على شارع رئيسي", ["shop", "office"]), new("meeting_room", "غرفة اجتماعات", ["office"]),
                new("corner", "أرض زاوية", ["land"]), new("paved", "شارع مسفلت", ["land"]), new("utilities", "خدمات متوفرة", ["land"]),
            ]),

        // ── Obligation: developer ──
        new("contract_date", "obligation", "تاريخ التعاقد", FieldType.Date, ObligationKinds: Both, AllowUnknown: true),
        new("original_price", "obligation", "سعر الوحدة في العقد", FieldType.Money, ObligationKinds: Dev, Initial: true, AllowUnknown: true, Min: 1000),
        new("paid_approved", "obligation", "المدفوع المعتمد من ثمن الوحدة", FieldType.Money, ObligationKinds: Dev, Initial: true, SubmitAnswer: true,
            PublishKnown: true, AllowUnknown: true, Min: 0,
            Help: "ما دفعته من ثمن الوحدة نفسها كما يظهر في كشف المطور، دون الرسوم أو الغرامات."),
        new("remaining_balance", "obligation", "الرصيد المتبقي للمطور", FieldType.Money, ObligationKinds: Dev, Initial: true, SubmitAnswer: true,
            PublishKnown: true, AllowUnknown: true, Min: 0),
        new("installment_amount", "obligation", "قيمة القسط", FieldType.Money, ObligationKinds: Dev, Initial: true, SubmitAnswer: true, AllowUnknown: true, Min: 0,
            Help: "القسط الدوري فقط. الدفعات الإضافية تُسجل منفصلة."),
        new("current_installment", "obligation", "القسط الحالي", FieldType.Money, ObligationKinds: Fin, Initial: true, AllowUnknown: true, Min: 0,
            Help: "للمعلومة فقط؛ لا يعني أن المشتري سيكمل هذا القسط."),
        new("installment_frequency", "obligation", "دورية القسط", FieldType.Select, ObligationKinds: Both, Initial: true, AllowUnknown: true, Options: Frequencies),
        new("remaining_installments", "obligation", "عدد الأقساط المتبقية", FieldType.Integer, ObligationKinds: Dev, AllowUnknown: true, Min: 0, Max: 600),
        new("remaining_months", "obligation", "المدة المتبقية", FieldType.Integer, ObligationKinds: Fin, AllowUnknown: true, Min: 0, Max: 480, Unit: "شهر"),
        new("extra_payments", "obligation", "دفعات إضافية", FieldType.Select, ObligationKinds: Dev, Initial: true, SubmitAnswer: true,
            Options: [new("none", "لا توجد"), new("has", "توجد دفعة إضافية"), new("unknown", "لا أعرف")],
            Help: "مثل دفعة سنوية أو دفعة عند الاستلام، خارج القسط الدوري."),
        new("extra_payment_amount", "obligation", "قيمة الدفعة الإضافية", FieldType.Money, ObligationKinds: Dev, Initial: true, AllowUnknown: true, Min: 0,
            When: new("extra_payments", ["has"]), Help: "لا تضفها إلى قيمة القسط."),
        new("extra_payment_date", "obligation", "موعد الدفعة الإضافية القادمة", FieldType.Date, ObligationKinds: Dev, AllowUnknown: true, When: new("extra_payments", ["has"])),
        new("extra_payment_recurrence", "obligation", "تكرار الدفعة الإضافية", FieldType.Select, ObligationKinds: Dev, When: new("extra_payments", ["has"]),
            Options: [new("once", "مرة واحدة"), new("annual", "سنويًا")]),
        new("arrears_state", "obligation", "هل توجد متأخرات؟", FieldType.Select, ObligationKinds: Both, Initial: true, SubmitAnswer: true, PublishKnown: true,
            Options: [new("none", "لا توجد متأخرات"), new("has", "توجد متأخرات"), new("unknown", "لا أعرف")]),
        new("arrears_amount", "obligation", "إجمالي المتأخرات", FieldType.Money, ObligationKinds: Both, Initial: true, PublishKnown: true, AllowUnknown: true, Min: 0,
            When: ArrearsHas),
        new("arrears_count", "obligation", "عدد الأقساط المتأخرة", FieldType.Integer, ObligationKinds: Both, Min: 1, Max: 600, When: ArrearsHas),
        new("arrears_in_balance", "obligation", "هل المتأخرات ضمن الرصيد المتبقي؟", FieldType.Select, ObligationKinds: Dev, PublishKnown: true,
            Options: YesNoUnknown, When: ArrearsHas, Help: "كما يظهر في كشف المطور. لا نحسبها مرتين."),
        new("payoff_amount", "obligation", "مبلغ السداد المطلوب من الجهة", FieldType.Money, ObligationKinds: Fin, Initial: true, SubmitAnswer: true,
            PublishKnown: true, AllowUnknown: true, Min: 0,
            Help: "كما في خطاب الجهة إن توفر. لا تحسبه من مجموع الأقساط؛ اختر «لا أعرف» ويطلبه الفريق."),
        new("payoff_valid_until", "obligation", "صلاحية مبلغ السداد", FieldType.Date, ObligationKinds: Fin, AllowUnknown: true),
        new("payoff_includes_arrears", "obligation", "هل يشمل مبلغ السداد المتأخرات؟", FieldType.Select, ObligationKinds: Fin, PublishKnown: true,
            Options: YesNoUnknown, When: ArrearsHas),
        new("purchase_price", "obligation", "سعر شراء العقار عند التمويل", FieldType.Money, ObligationKinds: Fin, Initial: true, AllowUnknown: true, Min: 1000),
        new("asking_price", "obligation", "السعر المطلوب للعقار", FieldType.Money, ObligationKinds: Fin, Initial: true, SubmitAnswer: true, PublishKnown: true,
            AllowUnknown: true, Min: 1000),
        new("target_net", "obligation", "صافي المبلغ الذي تستهدفه بعد سداد الجهة", FieldType.Money, ObligationKinds: Fin, AllowUnknown: true, Min: 0),
        new("owner_target", "obligation", "المبلغ الذي تطلبه لنفسك", FieldType.Money, ObligationKinds: Dev, Initial: true, AllowUnknown: true, Min: 0,
            Help: "يمكن اقتراح خروج دون زيادة فوق المدفوع المعتمد. هذا عرض مستهدف، وليس وعدًا بأنك ستحصل على كامل ما دفعته."),
        new("reduction_max", "obligation", "أقصى تخفيض تقبله من مدفوعك", FieldType.Money, ObligationKinds: Dev, AllowUnknown: true, Min: 0),
        new("transfer_allowed", "obligation", "هل يسمح المطور بنقل العقد؟", FieldType.Select, ObligationKinds: Dev, Options: YesNoUnknown),
    ];

    public static readonly DocumentDef[] Documents =
    [
        new("developer_contract", "عقد الشراء مع المطور", Dev, true),
        new("payment_proof", "إثباتات السداد أو كشف المطور", Dev, true),
        new("payment_schedule", "جدول الدفعات", Dev, true),
        new("transfer_terms", "شروط أو موافقة النقل", Dev, false, "عند توفرها."),
        new("financing_contract", "عقد التمويل", Fin, true),
        new("financier_statement", "كشف حديث من الجهة", Fin, true),
        new("payoff_letter", "خطاب مبلغ السداد", Fin, true, "عند توفره. يمكنك الإرسال بدونه، ويطلبه الفريق أثناء المراجعة."),
        new("ownership_proof", "مستند الملكية أو العلاقة بالعقار", Fin, true),
        new("other", "مستند آخر", [], false),
    ];

    public static readonly CityDef[] Cities =
    [
        new("riyadh", "الرياض", 24.7136, 46.6753,
            ["الملقا", "النرجس", "حطين", "الياسمين", "العارض", "القيروان", "الصحافة", "الربيع", "النخيل", "العليا", "الملز", "الروضة", "قرطبة",
             "اليرموك", "المونسية", "الرمال", "طويق", "ظهرة لبن", "الشفا", "العزيزية", "النسيم", "السويدي", "المروج", "الوادي", "الغدير", "العقيق"]),
        new("jeddah", "جدة", 21.4858, 39.1925,
            ["الشاطئ", "الروضة", "الزهراء", "السلامة", "النعيم", "الحمراء", "البساتين", "المحمدية", "أبحر الشمالية", "الفيصلية", "الصفا", "المروة",
             "الربوة", "السامر", "الأجواد"]),
        new("makkah", "مكة المكرمة", 21.3891, 39.8579, ["العزيزية", "الشوقية", "العوالي", "النسيم", "الزاهر", "الشرائع", "بطحاء قريش"]),
        new("madinah", "المدينة المنورة", 24.5247, 39.5692, ["العزيزية", "قباء", "الخالدية", "شوران", "الدفاع", "العاقول"]),
        new("dammam", "الدمام", 26.4207, 50.0888, ["الشاطئ", "الفيصلية", "النور", "الفردوس", "الشعلة", "الضاحية", "طيبة", "الروضة"]),
        new("khobar", "الخبر", 26.2172, 50.1971, ["العليا", "الراكة", "الحزام الذهبي", "اليرموك", "الجسر", "الخزامى", "صبيخة"]),
        new("dhahran", "الظهران", 26.2361, 50.0393, ["الدوحة", "القصور", "هجر"]),
        new("ahsa", "الأحساء", 25.3830, 49.5863, []),
        new("qatif", "القطيف", 26.5196, 50.0115, []),
        new("jubail", "الجبيل", 27.0046, 49.6460, []),
        new("taif", "الطائف", 21.2703, 40.4158, []),
        new("abha", "أبها", 18.2164, 42.5053, []),
        new("khamis", "خميس مشيط", 18.3000, 42.7333, []),
        new("buraydah", "بريدة", 26.3592, 43.9818, []),
        new("unaizah", "عنيزة", 26.0840, 43.9930, []),
        new("tabuk", "تبوك", 28.3838, 36.5550, []),
        new("hail", "حائل", 27.5114, 41.7208, []),
        new("jazan", "جازان", 16.8892, 42.5511, []),
        new("najran", "نجران", 17.5650, 44.2289, []),
        new("yanbu", "ينبع", 24.0890, 38.0618, []),
    ];

    public static readonly IReadOnlyDictionary<string, FieldDef> ByKey = Fields.GroupBy(f => f.Key).ToDictionary(g => g.Key, g => g.First());

    public static CityDef? City(string? key) => Cities.FirstOrDefault(c => c.Key == key);
    public static string Label(FieldOption[] options, string? value) => options.FirstOrDefault(o => o.Value == value)?.Label ?? value ?? "";

    // ── Applicability ──

    public static bool Applies(FieldDef f, string? propertyType, string? obligationKind, IReadOnlyDictionary<string, string> answers)
    {
        if (f.Scope == "property" && f.PropertyTypes is { } pts && (propertyType is null || !pts.Contains(propertyType))) return false;
        if (f.Scope == "obligation" && f.ObligationKinds is { } oks && (obligationKind is null || !oks.Contains(obligationKind))) return false;
        if (f.When is { } w && (!answers.TryGetValue(w.Key, out var v) || !w.Values.Contains(v))) return false;
        return true;
    }

    public static IEnumerable<FieldDef> PropertyFields(string? propertyType, IReadOnlyDictionary<string, string> answers) =>
        Fields.Where(f => f.Scope == "property" && Applies(f, propertyType, null, answers));

    public static IEnumerable<FieldDef> ObligationFields(string kind, IReadOnlyDictionary<string, string> answers) =>
        Fields.Where(f => f.Scope == "obligation" && Applies(f, null, kind, answers));

    public static IEnumerable<DocumentDef> DocumentsFor(IEnumerable<string> obligationKinds)
    {
        var kinds = obligationKinds.ToHashSet();
        return Documents.Where(d => d.ObligationKinds.Length == 0 || d.ObligationKinds.Any(kinds.Contains));
    }

    // ── Normalisation and validation ──

    /// <summary>Arabic-Indic and Eastern Arabic digits, the Arabic decimal and thousands separators → invariant form.</summary>
    public static string NormalizeDigits(string s)
    {
        var chars = s.Select(c => c switch
        {
            >= '٠' and <= '٩' => (char)('0' + (c - '٠')),
            >= '۰' and <= '۹' => (char)('0' + (c - '۰')),
            '٫' => '.',
            _ => c,
        }).Where(c => c is not ('٬' or '،' or ',')).ToArray();
        return new string(chars).Trim();
    }

    public sealed record Applied(Dictionary<string, string> Answers, Dictionary<string, string> Errors);

    /// <summary>
    /// Keeps only the fields that apply (scope, type, kind, conditions — evaluated on the incoming answers, so a branch switched
    /// off is dropped with its dependants), normalises them and validates each. Invalid values are reported and not kept;
    /// the rest is kept, so an autosave never loses valid input. Empty values clear the field.
    /// </summary>
    public static Applied Apply(string scope, IReadOnlyDictionary<string, string?>? input, string? propertyType, string? obligationKind, string errorPrefix = "")
    {
        var raw = (input ?? new Dictionary<string, string?>())
            .Where(kv => !string.IsNullOrWhiteSpace(kv.Value))
            .ToDictionary(kv => kv.Key, kv => kv.Value!.Trim());
        var kept = new Dictionary<string, string>();
        var errors = new Dictionary<string, string>();
        // Two passes: conditions read the normalised value of their controlling field.
        foreach (var pass in new[] { 0, 1 })
        {
            foreach (var f in Fields.Where(f => f.Scope == scope && (pass == 0 ? f.When is null : f.When is not null)))
            {
                if (!raw.TryGetValue(f.Key, out var value)) continue;
                if (!Applies(f, propertyType, obligationKind, kept)) continue;
                var (ok, normalized, error) = Normalize(f, value, propertyType);
                if (ok && normalized != "") kept[f.Key] = normalized!;
                else errors[errorPrefix + f.Key] = error!;
            }
        }
        return new Applied(kept, errors);
    }

    public static (bool Ok, string? Value, string? Error) Normalize(FieldDef f, string value, string? propertyType)
    {
        if (value == Unknown)
            return f.AllowUnknown ? (true, Unknown, null) : (false, null, $"اختر قيمة لـ«{f.Label}».");
        // Digits are normalised for numbers and dates only; lists keep their commas.
        var v = f.Type is FieldType.Text or FieldType.LongText or FieldType.MultiSelect or FieldType.Select or FieldType.Boolean ? value.Trim() : NormalizeDigits(value);
        switch (f.Type)
        {
            case FieldType.Integer:
                if (!int.TryParse(v, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var i)) return (false, null, $"أدخل رقمًا صحيحًا في «{f.Label}».");
                if (f.Min is { } imin && i < imin || f.Max is { } imax && i > imax) return (false, null, $"«{f.Label}» يجب أن يكون بين {f.Min} و{f.Max}.");
                return (true, i.ToString(CultureInfo.InvariantCulture), null);
            case FieldType.Decimal or FieldType.Money:
                if (!Regex.IsMatch(v, @"^\d+(\.\d{1,2})?$") || !decimal.TryParse(v, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var d))
                    return (false, null, f.Type == FieldType.Money ? $"أدخل مبلغ «{f.Label}» بالريال، دون رموز." : $"أدخل رقمًا صحيحًا في «{f.Label}».");
                if (f.Min is { } dmin && d < dmin) return (false, null, $"«{f.Label}» لا يمكن أن يقل عن {dmin:#,0}.");
                var dmax = f.Max ?? 10_000_000_000m;
                if (d > dmax) return (false, null, $"«{f.Label}» أكبر من المتوقع. راجع الرقم.");
                return (true, d.ToString("0.##", CultureInfo.InvariantCulture), null);
            case FieldType.Boolean:
                return v is "true" or "false" ? (true, v, null) : (false, null, $"اختر نعم أو لا في «{f.Label}».");
            case FieldType.Select:
                return Options(f, propertyType).Any(o => o.Value == v) ? (true, v, null) : (false, null, $"اختر من القائمة في «{f.Label}».");
            case FieldType.MultiSelect:
                var parts = v.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct().ToList();
                var allowed = Options(f, propertyType).Select(o => o.Value).ToHashSet();
                // Options of another property type are dropped silently with the branch.
                var keptParts = parts.Where(allowed.Contains).ToList();
                return keptParts.Count == 0 ? (true, "", null) : (true, string.Join(',', keptParts), null);
            case FieldType.Date:
                if (!DateOnly.TryParseExact(v, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) || date.Year < 1980 || date.Year > 2100)
                    return (false, null, $"أدخل تاريخًا صحيحًا في «{f.Label}».");
                return (true, date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), null);
            case FieldType.Month:
                if (!Regex.IsMatch(v, @"^(19|20|21)\d{2}-(0[1-9]|1[0-2])$")) return (false, null, $"اختر الشهر والسنة في «{f.Label}».");
                return (true, v, null);
            case FieldType.Text:
                return v.Length <= (int)(f.Max ?? 200) ? (true, v, null) : (false, null, $"«{f.Label}» أطول من المسموح.");
            case FieldType.LongText:
                return v.Length <= (int)(f.Max ?? 2000) ? (true, v, null) : (false, null, $"«{f.Label}» أطول من المسموح ({f.Max ?? 2000} حرف).");
            default:
                return (false, null, "قيمة غير مدعومة.");
        }
    }

    public static IEnumerable<FieldOption> Options(FieldDef f, string? propertyType) =>
        (f.Options ?? []).Where(o => o.PropertyTypes is null || (propertyType is not null && o.PropertyTypes.Contains(propertyType)));

    /// <summary>Cross-field consistency for the developer track (not applied to bank installments, which include finance cost).</summary>
    public static Dictionary<string, string> CrossCheck(string kind, IReadOnlyDictionary<string, string> a, string errorPrefix = "")
    {
        var errors = new Dictionary<string, string>();
        if (kind == "developer" && Num(a, "paid_approved") is { } paid && Num(a, "original_price") is { } price && paid > price)
            errors[errorPrefix + "paid_approved"] = "المدفوع المعتمد أكبر من سعر الوحدة في العقد. راجع الرقمين بعد فصل الرسوم والتعديلات.";
        if (kind == "developer" && Num(a, "reduction_max") is { } red && Num(a, "paid_approved") is { } p2 && red > p2)
            errors[errorPrefix + "reduction_max"] = "التخفيض لا يمكن أن يتجاوز المدفوع المعتمد.";
        return errors;
    }

    /// <summary>A known numeric answer, or null when missing or «لا أعرف» (never 0).</summary>
    public static decimal? Num(IReadOnlyDictionary<string, string> a, string key) =>
        a.TryGetValue(key, out var v) && v != Unknown && decimal.TryParse(v, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var d) ? d : null;

    public static bool IsAnswered(IReadOnlyDictionary<string, string> a, string key) => a.TryGetValue(key, out var v) && v.Length > 0;
    public static bool IsKnown(IReadOnlyDictionary<string, string> a, string key) =>
        a.TryGetValue(key, out var v) && v.Length > 0 && v != Unknown && v != "unknown";
}
