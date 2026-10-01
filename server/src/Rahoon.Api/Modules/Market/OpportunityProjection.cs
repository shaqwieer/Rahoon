using System.Globalization;
using System.Text.Json;
using Rahoon.Api.Infrastructure.Http;

namespace Rahoon.Api.Modules.Market;

/// <summary>
/// Shapes of an opportunity. The public shapes carry only what may be published: no owner identity, no private documents,
/// no exact coordinates (only <see cref="Opportunity.PublicLatitude"/>), and the figures of the published terms version.
/// </summary>
public static class OpportunityProjection
{
    public static readonly FieldOption[] Readiness = [new("ready", "جاهز"), new("under_construction", "تحت الإنشاء")];
    public static readonly FieldOption[] Tracks = [new("developer", "التزام لدى مطور"), new("financier", "عقار مموَّل"), new("mixed", "مطور وجهة تمويل")];

    /// <summary>
    /// Public coordinates: the exact point only when the team set precision «exact» with the owner's agreement; otherwise the
    /// centre of a ~1 km grid cell, so neither the map nor the Google Maps link can reveal more than allowed.
    /// </summary>
    public static (double? Lat, double? Lng) PublicPoint(double? lat, double? lng, string precision) =>
        lat is null || lng is null ? (null, null)
        : precision == "exact" ? (lat, lng)
        : (Math.Round(Math.Floor(lat.Value * 100) / 100 + 0.005, 4), Math.Round(Math.Floor(lng.Value * 100) / 100 + 0.005, 4));

    public static string GoogleMapsUrl(double lat, double lng) =>
        $"https://www.google.com/maps/search/?api=1&query={lat.ToString("0.######", CultureInfo.InvariantCulture)},{lng.ToString("0.######", CultureInfo.InvariantCulture)}";

    public static object Content(Opportunity o) => new
    {
        o.Title, o.Description, o.PropertyType, propertyTypeLabel = FieldCatalog.Label(FieldCatalog.PropertyTypes, o.PropertyType),
        o.City, cityLabel = FieldCatalog.City(o.City)?.Label ?? o.City, o.District, o.Project, o.Track, trackLabel = FieldCatalog.Label(Tracks, o.Track),
        o.Area, o.Bedrooms, o.Bathrooms, o.Readiness, readinessLabel = o.Readiness is null ? null : FieldCatalog.Label(Readiness, o.Readiness), o.DeliveryMonth,
        specs = Specs(o), features = Features(o),
    };

    public static IEnumerable<object> Specs(Opportunity o) => o.Specs
        .Where(kv => FieldCatalog.ByKey.ContainsKey(kv.Key) && kv.Value != FieldCatalog.Unknown)
        .Select(kv =>
        {
            var f = FieldCatalog.ByKey[kv.Key];
            var text = f.Type switch
            {
                FieldType.Boolean => kv.Value == "true" ? "نعم" : "لا",
                FieldType.Select => FieldCatalog.Label(f.Options ?? [], kv.Value),
                _ => f.Unit is null ? kv.Value : $"{kv.Value} {f.Unit}",
            };
            return (object)new { key = kv.Key, label = f.Label, value = text };
        });

    public static IEnumerable<string> Features(Opportunity o)
    {
        var options = FieldCatalog.ByKey["features"].Options ?? [];
        return o.Features.Select(k => options.FirstOrDefault(x => x.Value == k)?.Label).OfType<string>();
    }

    public static TermsResult Result(OpportunityTerms t) => JsonSerializer.Deserialize<TermsResult>(t.ResultJson, JsonOptions.Web)!;
    public static TermsInput Input(OpportunityTerms t) => JsonSerializer.Deserialize<TermsInput>(t.InputJson, JsonOptions.Web)!;

    public static object Terms(OpportunityTerms t)
    {
        var r = Result(t);
        return new
        {
            id = t.Id, versionNo = t.VersionNo, status = t.Status, track = t.Track, t.TransferConditions, t.VerificationScope, t.VerifiedOn,
            r.Complete, r.Quality, r.QualityText, r.Missing, r.Lines, r.Notes, r.Gap, r.OwnerAmount, r.SellerNet, r.DueNow, r.FutureBalance, r.BuyerTotal,
            r.Installment, r.InstallmentFrequency, installmentFrequencyLabel = r.InstallmentFrequency is null ? null : FieldCatalog.Label(FieldCatalog.Frequencies, r.InstallmentFrequency),
            r.InstallmentMonthlyEquivalent, r.LargestExtraPayment, r.RemainingMonths, r.NeedsNewFinancing, r.Commission,
            t.SentToOwnerAt, t.OwnerDecidedAt, t.OwnerNote,
        };
    }

    public static string PhotoUrl(Guid id) => $"/api/market/photos/{id}";

    /// <summary>A listing card: the amount due now is the headline; price/total and future obligations are labelled separately.</summary>
    public static object Card(Opportunity o, OpportunityTerms t, bool saved)
    {
        var r = Result(t);
        return new
        {
            reference = o.Reference, title = o.Title, city = o.City, cityLabel = FieldCatalog.City(o.City)?.Label ?? o.City, district = o.District, project = o.Project,
            developerName = o.Track == "financier" ? null : o.DeveloperName,
            propertyType = o.PropertyType, propertyTypeLabel = FieldCatalog.Label(FieldCatalog.PropertyTypes, o.PropertyType),
            track = o.Track, readiness = o.Readiness, readinessLabel = o.Readiness is null ? null : FieldCatalog.Label(Readiness, o.Readiness), deliveryMonth = o.DeliveryMonth,
            area = o.Area, bedrooms = o.Bedrooms, bathrooms = o.Bathrooms, specs = Specs(o).Take(3),
            coverUrl = o.PhotoIds.Count > 0 ? PhotoUrl(o.PhotoIds[0]) : null, photoCount = o.PhotoIds.Count,
            dueNow = r.DueNow, buyerTotal = r.BuyerTotal, futureBalance = o.Track == "financier" ? null : r.FutureBalance,
            installment = r.Installment, installmentFrequency = r.InstallmentFrequency,
            installmentFrequencyLabel = r.InstallmentFrequency is null ? null : FieldCatalog.Label(FieldCatalog.Frequencies, r.InstallmentFrequency),
            installmentMonthlyEquivalent = r.InstallmentMonthlyEquivalent, largestExtraPayment = r.LargestExtraPayment,
            extraPaymentRecurrence = t.ExtraPaymentRecurrence, remainingMonths = r.RemainingMonths,
            needsNewFinancing = r.NeedsNewFinancing, quality = r.Quality, complete = r.Complete,
            verifiedOn = t.VerifiedOn, publishedAt = o.PublishedAt, saved, isDemo = o.IsDemo,
            location = o.PublicLatitude is null ? null : new { lat = o.PublicLatitude, lng = o.PublicLongitude, precision = o.LocationPrecision },
        };
    }

    public static object Detail(Opportunity o, OpportunityTerms t, bool saved, IEnumerable<object> approvals, object? fit, object? myInterest, object? schedule = null)
    {
        var maps = o.PublicLatitude is { } lat && o.PublicLongitude is { } lng ? GoogleMapsUrl(lat, lng) : null;
        return new
        {
            card = Card(o, t, saved),
            content = Content(o),
            photos = o.PhotoIds.Select(id => new { id, url = PhotoUrl(id) }),
            terms = Terms(t),
            location = o.PublicLatitude is null ? null : new { lat = o.PublicLatitude, lng = o.PublicLongitude, precision = o.LocationPrecision, googleMapsUrl = maps },
            approvals,
            fit,
            myInterest,
            schedule,
            status = o.Status,
            statusLabel = OpportunityFlow.Labels[o.Status],
        };
    }
}
