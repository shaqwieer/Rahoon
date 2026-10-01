using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Rahoon.Api.Infrastructure.Persistence;
using Rahoon.Api.Infrastructure.Tenancy;

namespace Rahoon.Api.Modules.Market.Discovery;

/// <summary>Why an opportunity is in a buyer's list: what made it eligible, the preferences it meets and the ones it doesn't.</summary>
public sealed record MatchExplanation(List<string> Eligibility, List<string> Preferences, List<string> NotMet);

/// <summary>
/// Explained matching (Phase 2) on top of the discovery spine. Mandatory eligibility = published + the buyer's cities and
/// property types; affordability = <see cref="Affordability"/> on the buyer's declared capacity; ranking preferences (area,
/// bedrooms, readiness, delivery, districts named by the buyer) only order the list. No percentage is ever produced: the reasons
/// are the explanation. The buyer profile always comes from the signed-in person's own request, never from a reference.
/// </summary>
public static class Matching
{
    /// <summary>The signed-in individual's live buyer request (any state but withdrawn or rejected), or null.</summary>
    public static Task<BuyerRequest?> OwnLiveRequestAsync(RahoonDbContext db, RequestContext rc) =>
        !rc.IsIndividual ? Task.FromResult<BuyerRequest?>(null)
            : db.BuyerRequests.Where(b => b.ApplicantUserId == rc.UserId && b.Status != BuyerRequestStatus.Withdrawn && b.Status != BuyerRequestStatus.Rejected)
                .OrderByDescending(b => b.CreatedAt).FirstOrDefaultAsync();

    public static CapacityProfile Capacity(BuyerRequest r) => new(r.AvailableNow, r.InstallmentComfort, r.InstallmentFrequency, r.MaxPrice);

    public static RankingPreferences Preferences(BuyerRequest r)
    {
        // Districts the buyer named in their own words, recognised against the catalog's districts of their cities.
        var districts = new List<string>();
        if (r.AreasText is { Length: > 0 } text)
        {
            var cities = r.Cities.Count > 0 ? FieldCatalog.Cities.Where(c => r.Cities.Contains(c.Key)) : FieldCatalog.Cities;
            districts = cities.SelectMany(c => c.Districts).Distinct().Where(d => text.Contains(d, StringComparison.Ordinal)).ToList();
        }
        return new RankingPreferences(r.AreaMin, r.AreaMax, r.BedroomsMin, r.Readiness, r.DeliveryBy, districts);
    }

    /// <summary>Fills what the URL leaves out from the buyer's own request. Explicit URL values win.</summary>
    public static SearchCriteria WithProfile(SearchCriteria c, BuyerRequest r) => c with
    {
        Cities = c.Cities.Count > 0 ? c.Cities : r.Cities.Order(StringComparer.Ordinal).ToList(),
        Types = c.Types.Count > 0 ? c.Types : r.PropertyTypes.Order(StringComparer.Ordinal).ToList(),
        MaxNow = c.MaxNow ?? r.AvailableNow,
        MaxInstallment = c.MaxInstallment ?? r.InstallmentComfort,
        Freq = c.MaxInstallment is not null ? c.Freq : r.InstallmentFrequency ?? "monthly",
        MaxTotal = c.MaxTotal ?? r.MaxPrice,
    };

    public static MatchExplanation Explain(Opportunity o, SearchCriteria c, RankingPreferences? p)
    {
        var eligibility = new List<string>();
        if (c.Cities.Count > 0) eligibility.Add($"في مدينة اخترتها: {FieldCatalog.City(o.City)?.Label ?? o.City}.");
        if (c.Types.Count > 0) eligibility.Add($"نوع العقار ضمن ما اخترته: {FieldCatalog.Label(FieldCatalog.PropertyTypes, o.PropertyType)}.");
        if (p is null) return new MatchExplanation(eligibility, [], []);
        var (met, notMet) = PreferenceReasons(o, p);
        return new MatchExplanation(eligibility, met, notMet);
    }

    /// <summary>The C# explanation of <see cref="DiscoveryQuery.PreferenceScore"/>: one line per preference met or not met.</summary>
    public static (List<string> Met, List<string> NotMet) PreferenceReasons(Opportunity o, RankingPreferences p)
    {
        var met = new List<string>();
        var notMet = new List<string>();
        static string N(decimal d) => d.ToString("0.##", CultureInfo.InvariantCulture);
        if (p.AreaMin is not null || p.AreaMax is not null)
        {
            if (o.Area is not { } area) notMet.Add("المساحة غير مسجلة للمقارنة بما تفضله.");
            else if ((p.AreaMin is null || area >= p.AreaMin) && (p.AreaMax is null || area <= p.AreaMax)) met.Add($"المساحة ({N(area)} م²) ضمن ما تفضله.");
            else notMet.Add($"المساحة ({N(area)} م²) خارج ما تفضله.");
        }
        if (p.BedroomsMin is { } beds)
        {
            if (o.Bedrooms is not { } b) notMet.Add("عدد الغرف غير مسجل.");
            else if (b >= beds) met.Add($"{b} غرف نوم، ويناسب ما تفضله.");
            else notMet.Add($"{b} غرف نوم، أقل مما تفضله.");
        }
        if (p.Readiness is "ready" or "under_construction")
        {
            if (o.Readiness == p.Readiness) met.Add(p.Readiness == "ready" ? "جاهز كما تفضل." : "تحت الإنشاء كما تفضل.");
            else notMet.Add(p.Readiness == "ready" ? "ليس جاهزًا بعد." : "ليس تحت الإنشاء.");
        }
        if (p.DeliveryBy is { } by)
        {
            if (o.Readiness == "ready") met.Add("جاهز الآن، قبل موعد التسليم الذي تفضله.");
            else if (o.DeliveryMonth is not { } dm) notMet.Add("موعد التسليم غير مسجل.");
            else if (string.CompareOrdinal(dm, by) <= 0) met.Add($"التسليم المتوقع ({dm}) قبل موعدك ({by}).");
            else notMet.Add($"التسليم المتوقع ({dm}) بعد موعدك ({by}).");
        }
        if (p.Districts.Count > 0)
        {
            if (o.District is { } d && p.Districts.Contains(d)) met.Add($"في حي ذكرته: {d}.");
            else notMet.Add("ليس في الأحياء التي ذكرتها.");
        }
        return (met, notMet);
    }
}
