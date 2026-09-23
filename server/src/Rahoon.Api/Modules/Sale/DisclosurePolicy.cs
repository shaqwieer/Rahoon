namespace Rahoon.Api.Modules.Sale;

public enum DisclosureAudience { Broker, QualifiedBuyer, Public }
public enum Disclosure { No, Yes, AfterVisit }

public sealed record DisclosureRow(string Key, string LabelAr, Disclosure Broker, Disclosure QualifiedBuyer, Disclosure Public);

/// <summary>Everything the lender knows about the listing. Never serialised directly — only through <see cref="DisclosurePolicy.Project"/>.</summary>
public sealed record ListingSource(
    string OwnerName, string CaseReference, string LenderName, string BuyerReference, string PropertyType,
    string City, string? District, string AreaLabel, string? ExactLocation, int ApprovedPhotoCount,
    decimal? AskingPrice, decimal? OwnerMinimum, decimal? OutstandingDebt, int? ArrearsInstallments,
    int EvacuationDays, string? VisitTerms, decimal? LandArea, decimal? BuiltArea, int? YearBuilt);

/// <summary>
/// Controlled disclosure (L30) as a server-enforced, field-level policy. The public column is «لا»
/// for every row: there is no public listing and no endpoint serves one. Owner identity, the owner's
/// minimum and the debt/default situation are never disclosed to brokers or buyers.
/// </summary>
public static class DisclosurePolicy
{
    public static readonly IReadOnlyList<DisclosureRow> Matrix =
    [
        new("owner_identity", "اسم المالكة وهويتها", Disclosure.No, Disclosure.No, Disclosure.No),
        new("district_city", "الحي والمدينة", Disclosure.Yes, Disclosure.Yes, Disclosure.No),
        new("exact_location", "الموقع الدقيق", Disclosure.Yes, Disclosure.AfterVisit, Disclosure.No),
        new("approved_photos", "الصور المعتمدة", Disclosure.Yes, Disclosure.Yes, Disclosure.No),
        new("asking_price", "السعر المطلوب", Disclosure.Yes, Disclosure.Yes, Disclosure.No),
        new("owner_minimum", "الحد الأدنى للمالكة", Disclosure.No, Disclosure.No, Disclosure.No),
        new("debt_default", "المديونية والتعثر", Disclosure.No, Disclosure.No, Disclosure.No),
        new("evacuation_terms", "مهلة الإخلاء", Disclosure.Yes, Disclosure.Yes, Disclosure.No),
    ];

    public static Disclosure For(DisclosureRow row, DisclosureAudience audience) => audience switch
    {
        DisclosureAudience.Broker => row.Broker,
        DisclosureAudience.QualifiedBuyer => row.QualifiedBuyer,
        _ => Disclosure.No,
    };

    public static string Label(Disclosure d) => d switch { Disclosure.Yes => "نعم", Disclosure.AfterVisit => "بعد الزيارة", _ => "لا" };

    /// <summary>The matrix as displayed (L30), columns الوسيط · مشترٍ مؤهل · العامة.</summary>
    public static object MatrixView() => Matrix.Select(r => new
    {
        key = r.Key, label = r.LabelAr,
        broker = Label(r.Broker), brokerTone = Tone(r.Broker),
        qualifiedBuyer = Label(r.QualifiedBuyer), qualifiedBuyerTone = Tone(r.QualifiedBuyer),
        @public = Label(r.Public), publicTone = Tone(r.Public),
    }).ToList();

    private static string Tone(Disclosure d) => d switch { Disclosure.Yes => "ok", Disclosure.AfterVisit => "warn", _ => "neutral" };

    /// <summary>
    /// Builds the audience's view field by field from the matrix. A field is emitted only if the
    /// matrix allows it; fields with no «نعم» anywhere (owner identity, minimum, debt) have no
    /// projection at all. The case reference and lender name are never part of any projection.
    /// </summary>
    public static Dictionary<string, object?> Project(ListingSource s, DisclosureAudience audience)
    {
        if (audience == DisclosureAudience.Public)
            throw new InvalidOperationException("No public listing exists.");
        var view = new Dictionary<string, object?>
        {
            ["reference"] = s.BuyerReference,
            ["referenceNote"] = "المرجع للمشتري — لا يكشف رقم الحالة أو الجهة الممولة",
            ["title"] = $"{s.PropertyType} · {(audience == DisclosureAudience.Broker && s.District is not null ? $"{s.District}، {s.City}" : s.AreaLabel)}",
            ["facts"] = Facts(s),
        };
        foreach (var row in Matrix)
        {
            var d = For(row, audience);
            if (d == Disclosure.No) continue;
            switch (row.Key)
            {
                case "district_city":
                    view["area"] = audience == DisclosureAudience.Broker && s.District is not null ? $"{s.District}، {s.City}" : s.AreaLabel;
                    break;
                case "exact_location":
                    view["exactLocation"] = d == Disclosure.Yes ? s.ExactLocation : null;
                    view["exactLocationNote"] = d == Disclosure.AfterVisit ? "يُشارك الموقع الدقيق بعد الزيارة" : null;
                    break;
                case "approved_photos":
                    view["approvedPhotos"] = s.ApprovedPhotoCount;
                    break;
                case "asking_price":
                    view["askingPrice"] = s.AskingPrice;
                    break;
                case "evacuation_terms":
                    view["evacuation"] = $"الإخلاء: {s.EvacuationDays} يوماً بعد نقل الملكية" + (string.IsNullOrWhiteSpace(s.VisitTerms) ? "" : $" · الزيارة {s.VisitTerms}");
                    break;
            }
        }
        return view;
    }

    private static string Facts(ListingSource s)
    {
        var parts = new List<string>();
        if (s.LandArea is { } l) parts.Add($"أرض {l:0.#} م²");
        if (s.BuiltArea is { } b) parts.Add($"بناء {b:0.#} م²");
        if (s.YearBuilt is { } y) parts.Add(y.ToString());
        return string.Join(" · ", parts);
    }
}
