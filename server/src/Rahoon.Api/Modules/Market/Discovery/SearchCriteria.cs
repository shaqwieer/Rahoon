using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Rahoon.Api.Infrastructure.Http;

namespace Rahoon.Api.Modules.Market.Discovery;

/// <summary>A validated public map window (south, west, north, east) in degrees. No antimeridian crossing (Saudi Arabia).</summary>
public sealed record GeoBounds(double South, double West, double North, double East)
{
    public const double MaxSpanDegrees = 40;

    public string ToParam() => string.Join(",", new[] { South, West, North, East }.Select(v => Math.Round(v, 5).ToString("0.#####", CultureInfo.InvariantCulture)));
}

/// <summary>What the buyer can pay: cash now, a comfortable installment and its frequency, an optional ceiling and term.</summary>
public sealed record CapacityProfile(decimal? AvailableNow, decimal? InstallmentComfort, string? InstallmentFrequency, decimal? MaxTotal, int? MaxTermMonths = null)
{
    public bool Any => AvailableNow is not null || InstallmentComfort is not null || MaxTotal is not null || MaxTermMonths is not null;
    public decimal? ComfortMonthly => MarketCalculator.MonthlyEquivalent(InstallmentComfort, InstallmentFrequency ?? "monthly");
}

/// <summary>Ranking-only preferences (never exclude): from the buyer's own request.</summary>
public sealed record RankingPreferences(decimal? AreaMin, decimal? AreaMax, int? BedroomsMin, string? Readiness, string? DeliveryBy, IReadOnlyList<string> Districts)
{
    public bool Any => AreaMin is not null || AreaMax is not null || BedroomsMin is not null || Readiness is "ready" or "under_construction" || DeliveryBy is not null || Districts.Count > 0;
}

/// <summary>
/// The only parser and validator of search state (Phase 2). The public list, the map, facets, saved searches and the alert job
/// all run a <see cref="SearchCriteria"/> through <see cref="DiscoveryQuery"/>, so they always agree. The canonical query string
/// (sorted keys) is what URLs, saved searches and their hash carry. Unknown option values are dropped (old links keep working);
/// malformed numbers and map bounds are refused with a reason.
/// </summary>
public sealed record SearchCriteria
{
    public const int DefaultPageSize = 12;
    public const int MaxPageSize = 24;
    public static readonly string[] Sorts = ["relevance", "now", "total", "newest"];
    private static readonly string[] Frequencies = ["monthly", "quarterly", "semiannual", "annual"];
    private static readonly Regex Month = new(@"^(19|20|21)\d{2}-(0[1-9]|1[0-2])$", RegexOptions.Compiled);

    public IReadOnlyList<string> Cities { get; init; } = [];
    public string? District { get; init; }
    /// <summary>Free text matched against the project and the developer's name.</summary>
    public string? Project { get; init; }
    /// <summary>A developer from the organization directory (exact).</summary>
    public Guid? Developer { get; init; }
    public IReadOnlyList<string> Types { get; init; } = [];
    public decimal? MinArea { get; init; }
    public decimal? MaxArea { get; init; }
    public int? Bedrooms { get; init; }
    public int? Bathrooms { get; init; }
    public string? Readiness { get; init; }
    public string? DeliveryFrom { get; init; }
    public string? DeliveryTo { get; init; }
    public decimal? MaxNow { get; init; }
    public decimal? MaxTotal { get; init; }
    public decimal? MaxInstallment { get; init; }
    public string Freq { get; init; } = "monthly";
    public int? MaxTerm { get; init; }
    public IReadOnlyList<string> Features { get; init; } = [];
    public string? Track { get; init; }
    public GeoBounds? Bounds { get; init; }
    /// <summary>Apply the signed-in person's own buyer profile (never another person's: no reference is accepted).</summary>
    public bool MatchMe { get; init; }
    /// <summary>Explicit sort, or null for the default (relevance when a budget is given, newest otherwise).</summary>
    public string? Sort { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = DefaultPageSize;

    public CapacityProfile Capacity => new(MaxNow, MaxInstallment, MaxInstallment is null ? null : Freq, MaxTotal, MaxTerm);
    public string EffectiveSort => Sort ?? (Capacity.Any || MatchMe ? "relevance" : "newest");

    public static SearchCriteria Parse(IQueryCollection query) =>
        Parse(query.Select(kv => new KeyValuePair<string, string?>(kv.Key, kv.Value.ToString())));

    public static SearchCriteria Parse(string canonical) =>
        Parse(canonical.Split('&', StringSplitOptions.RemoveEmptyEntries).Select(p =>
        {
            var i = p.IndexOf('=');
            return i < 0 ? new KeyValuePair<string, string?>(Uri.UnescapeDataString(p), "")
                : new KeyValuePair<string, string?>(Uri.UnescapeDataString(p[..i]), Uri.UnescapeDataString(p[(i + 1)..].Replace('+', ' ')));
        }));

    public static SearchCriteria Parse(IEnumerable<KeyValuePair<string, string?>> pairs)
    {
        var q = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (k, raw0) in pairs)
            if (raw0?.Trim() is { Length: > 0 } t) q[k] = t;
        string? S(string k) => q.GetValueOrDefault(k);
        var v = new Validator();

        decimal? Money(string key, decimal max)
        {
            if (S(key) is not { } raw) return null;
            var ok = decimal.TryParse(ToLatinDigits(raw), NumberStyles.Number, CultureInfo.InvariantCulture, out var d) && d >= 0 && d <= max;
            v.Require(ok, key, "أدخل مبلغًا صحيحًا بالريال.");
            return ok ? Math.Round(d, 2) : null;
        }
        int? Int(string key, int min, int max, string message)
        {
            if (S(key) is not { } raw) return null;
            var ok = int.TryParse(ToLatinDigits(raw), NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) && n >= min && n <= max;
            v.Require(ok, key, message);
            return ok ? n : null;
        }
        string? MonthParam(string key)
        {
            if (S(key) is not { } raw) return null;
            var ok = Month.IsMatch(raw);
            v.Require(ok, key, "اختر الشهر والسنة.");
            return ok ? raw : null;
        }
        List<string> Csv(string key, Func<string, bool> allowed) =>
            (S(key) ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Where(allowed).Distinct().Order(StringComparer.Ordinal).ToList();
        string? Text(string key, int max) => S(key) is { } t ? (t.Length > max ? t[..max] : t) : null;

        var freq = S("freq") is { } f && Frequencies.Contains(f) ? f : "monthly";
        var minArea = Money("minArea", 1_000_000);
        var maxArea = Money("maxArea", 1_000_000);
        v.Require(minArea is null || maxArea is null || minArea <= maxArea, "maxArea", "الحد الأعلى للمساحة أقل من الحد الأدنى.");
        var deliveryFrom = MonthParam("deliveryFrom");
        var deliveryTo = MonthParam("deliveryTo");
        v.Require(deliveryFrom is null || deliveryTo is null || string.CompareOrdinal(deliveryFrom, deliveryTo) <= 0, "deliveryTo", "نهاية فترة التسليم قبل بدايتها.");
        Guid? developer = null;
        if (S("developer") is { } dev)
        {
            var ok = Guid.TryParse(dev, out var g);
            v.Require(ok, "developer", "اختر المطور من الدليل.");
            if (ok) developer = g;
        }
        var bounds = ParseBounds(S("bbox"), v);
        var sort = S("sort") switch
        {
            "fit" => "relevance", // Phase 1 links
            "price" => "total",
            { } x when Sorts.Contains(x) => x,
            _ => null,
        };
        var page = Int("page", 1, 10_000, "رقم الصفحة غير صحيح.") ?? 1;
        var size = Int("pageSize", 1, 200, "حجم الصفحة غير صحيح.") ?? DefaultPageSize;

        var c = new SearchCriteria
        {
            Cities = Csv("city", x => FieldCatalog.City(x) is not null),
            District = Text("district", 100),
            Project = Text("project", 150),
            Developer = developer,
            Types = Csv("types", x => FieldCatalog.PropertyTypes.Any(p => p.Value == x)),
            MinArea = minArea,
            MaxArea = maxArea,
            Bedrooms = Int("bedrooms", 0, 20, "عدد الغرف غير منطقي."),
            Bathrooms = Int("bathrooms", 0, 20, "عدد دورات المياه غير منطقي."),
            Readiness = S("readiness") is "ready" or "under_construction" ? S("readiness") : null,
            DeliveryFrom = deliveryFrom,
            DeliveryTo = deliveryTo,
            MaxNow = Money("maxNow", 1_000_000_000),
            MaxTotal = Money("maxTotal", 1_000_000_000),
            MaxInstallment = Money("maxInstallment", 10_000_000),
            Freq = freq,
            MaxTerm = Int("maxTerm", 1, 600, "المدة المتبقية بين شهر و600 شهر."),
            Features = Csv("features", x => FieldCatalog.ByKey["features"].Options?.Any(o => o.Value == x) == true),
            Track = S("track") is "developer" or "financier" or "mixed" ? S("track") : null,
            Bounds = bounds,
            MatchMe = S("match") == "me",
            Sort = sort,
            Page = page,
            PageSize = Math.Clamp(size, 1, MaxPageSize),
        };
        v.ThrowIfInvalid();
        return c;
    }

    public static GeoBounds? ParseBounds(string? raw, Validator v)
    {
        if (raw is null) return null;
        var parts = raw.Split(',');
        var nums = parts.Select(p => double.TryParse(p, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) && double.IsFinite(d) ? d : (double?)null).ToList();
        if (parts.Length != 4 || nums.Any(n => n is null))
        {
            v.Require(false, "bbox", "حدود الخريطة غير صحيحة.");
            return null;
        }
        var (s, w, n, e) = (nums[0]!.Value, nums[1]!.Value, nums[2]!.Value, nums[3]!.Value);
        var ok = s is >= -90 and <= 90 && n is >= -90 and <= 90 && w is >= -180 and <= 180 && e is >= -180 and <= 180 && s < n && w < e
                 && n - s <= GeoBounds.MaxSpanDegrees && e - w <= GeoBounds.MaxSpanDegrees;
        v.Require(ok, "bbox", "حدود الخريطة غير صحيحة أو واسعة جدًا؛ قرّب الخريطة ثم ابحث في هذه المنطقة.");
        return ok ? new GeoBounds(s, w, n, e) : null;
    }

    /// <summary>
    /// The canonical query string: sorted keys, sorted list values, defaults left out. <paramref name="view"/> adds sort and page
    /// (a saved search keeps the sort but never the page). The criteria part alone identifies a saved search.
    /// </summary>
    public string ToQuery(bool sortAndPage = false, bool includePage = false)
    {
        var p = new SortedDictionary<string, string>(StringComparer.Ordinal);
        void Put(string k, string? val) { if (!string.IsNullOrEmpty(val)) p[k] = val; }
        static string M(decimal? d) => d is { } x ? x.ToString("0.##", CultureInfo.InvariantCulture) : "";
        Put("city", string.Join(",", Cities));
        Put("district", District);
        Put("project", Project);
        Put("developer", Developer?.ToString());
        Put("types", string.Join(",", Types));
        Put("minArea", M(MinArea));
        Put("maxArea", M(MaxArea));
        Put("bedrooms", Bedrooms?.ToString(CultureInfo.InvariantCulture));
        Put("bathrooms", Bathrooms?.ToString(CultureInfo.InvariantCulture));
        Put("readiness", Readiness);
        Put("deliveryFrom", DeliveryFrom);
        Put("deliveryTo", DeliveryTo);
        Put("maxNow", M(MaxNow));
        Put("maxTotal", M(MaxTotal));
        Put("maxInstallment", M(MaxInstallment));
        if (MaxInstallment is not null && Freq != "monthly") Put("freq", Freq);
        Put("maxTerm", MaxTerm?.ToString(CultureInfo.InvariantCulture));
        Put("features", string.Join(",", Features));
        Put("track", Track);
        Put("bbox", Bounds?.ToParam());
        if (MatchMe) Put("match", "me");
        if (sortAndPage)
        {
            Put("sort", Sort);
            if (includePage && Page > 1) Put("page", Page.ToString(CultureInfo.InvariantCulture));
        }
        return string.Join("&", p.Select(kv => $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value)}"));
    }

    /// <summary>Identifies the filters of a saved search (sort and page excluded).</summary>
    public string Hash() => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(ToQuery()))).ToLowerInvariant();

    /// <summary>Plain Arabic summary of the criteria, for saved searches and alert messages.</summary>
    public List<string> Summary()
    {
        var s = new List<string>();
        static string Sar(decimal d) => d.ToString("#,0", CultureInfo.InvariantCulture);
        if (Cities.Count > 0) s.Add(string.Join("، ", Cities.Select(c => FieldCatalog.City(c)?.Label ?? c)));
        if (District is not null) s.Add($"حي {District}");
        if (Project is not null) s.Add($"مشروع أو مطور: {Project}");
        if (Types.Count > 0) s.Add(string.Join("، ", Types.Select(t => FieldCatalog.Label(FieldCatalog.PropertyTypes, t))));
        if (MaxNow is { } n) s.Add($"الآن حتى {Sar(n)} ر.س");
        if (MaxInstallment is { } i) s.Add($"قسط حتى {Sar(i)} ر.س {FieldCatalog.Label(FieldCatalog.Frequencies, Freq)}");
        if (MaxTotal is { } t) s.Add($"الإجمالي حتى {Sar(t)} ر.س");
        if (MinArea is not null || MaxArea is not null) s.Add($"المساحة {(MinArea is { } a ? $"من {a:0} " : "")}{(MaxArea is { } b ? $"إلى {b:0} " : "")}م²");
        if (Bedrooms is { } bd) s.Add($"{bd}+ غرف");
        if (Bathrooms is { } bt) s.Add($"{bt}+ دورات مياه");
        if (Readiness is { } r) s.Add(r == "ready" ? "جاهز" : "تحت الإنشاء");
        if (DeliveryFrom is not null || DeliveryTo is not null) s.Add($"التسليم {(DeliveryFrom is { } df ? $"من {df} " : "")}{(DeliveryTo is { } dt ? $"حتى {dt}" : "")}".Trim());
        if (MaxTerm is { } m) s.Add($"مدة متبقية حتى {m} شهرًا");
        if (Features.Count > 0) s.Add(string.Join("، ", Features.Select(f => FieldCatalog.Label(FieldCatalog.ByKey["features"].Options ?? [], f))));
        if (Track is { } tr) s.Add(FieldCatalog.Label(OpportunityProjection.Tracks, tr));
        if (Bounds is not null) s.Add("منطقة محددة على الخريطة");
        if (MatchMe) s.Add("وفق قدرتي الشرائية المسجلة");
        if (s.Count == 0) s.Add("كل الفرص المنشورة");
        return s;
    }

    private static string ToLatinDigits(string s)
    {
        var sb = new StringBuilder(s.Length);
        foreach (var ch in s)
        {
            if (ch is >= '٠' and <= '٩') sb.Append((char)('0' + (ch - '٠')));
            else if (ch is >= '۰' and <= '۹') sb.Append((char)('0' + (ch - '۰')));
            else if (ch is ',' or '٬' or ' ') continue;
            else if (ch == '٫') sb.Append('.');
            else sb.Append(ch);
        }
        return sb.ToString();
    }
}
