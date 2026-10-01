using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Rahoon.Api.Infrastructure.Http;
using Rahoon.Api.Modules.Identity;
using Rahoon.Api.Modules.Market;
using Rahoon.Api.Modules.Market.Discovery;

namespace Rahoon.Api.Tests.Infrastructure;

/// <summary>
/// Inserts opportunities with an exact terms snapshot straight into the database (one sale request each), so discovery tests can
/// shape schedules precisely. Each test uses its own city key for isolation from seeded and other tests' rows.
/// </summary>
public static class DiscoveryRows
{
    private static int _seq;

    public sealed record Shape
    {
        public decimal? DueNow { get; init; }
        public decimal? Total { get; init; }
        public decimal? FutureBalance { get; init; }
        public decimal? Installment { get; init; }
        public string? Frequency { get; init; }
        public decimal? ExtraPayment { get; init; }
        /// <summary>once | annual | null (recurrence not recorded)</summary>
        public string? ExtraRecurrence { get; init; }
        public int? RemainingMonths { get; init; }
        public string Track { get; init; } = "developer";
        public string Quality { get; init; } = "complete_estimate";
        public bool NeedsNewFinancing { get; init; }
    }

    public sealed record Inserted(Guid Id, string Reference, Guid TermsId, Guid SaleRequestId);

    public static async Task<Inserted> InsertAsync(ApiFixture api, string city, Shape shape, string propertyType = "apartment",
        OpportunityStatus status = OpportunityStatus.Published, double? lat = null, double? lng = null, string precision = "approximate",
        decimal? area = 120, int? bedrooms = 3, string? readiness = "ready", string? deliveryMonth = null, string? district = null, Guid? ownerId = null)
    {
        var n = Interlocked.Increment(ref _seq);
        return await api.WithDbAsync(async db =>
        {
            var org = await db.Organizations.Where(o => o.Kind == OrganizationKind.Operator).Select(o => o.Id).FirstAsync();
            var owner = ownerId ?? await db.Users.Where(u => u.AccountKind == AccountKind.Individual).OrderBy(u => u.CreatedAt).Select(u => u.Id).FirstAsync();
            var sr = new SaleRequest
            {
                OrganizationId = org, ApplicantUserId = owner, Reference = $"SRT-{n:D6}-{Random.Shared.Next(1000, 9999)}", Status = SaleRequestStatus.ApprovedForListing,
                PropertyType = propertyType, City = city, ObligationMode = shape.Track == "financier" ? "financier" : "developer", IsDemo = true,
            };
            db.SaleRequests.Add(sr);
            var (plat, plng) = OpportunityProjection.PublicPoint(lat, lng, precision);
            var o = new Opportunity
            {
                OrganizationId = org, SaleRequestId = sr.Id, ApplicantUserId = owner, Reference = $"OPT-{n:D5}-{Random.Shared.Next(100, 999)}", Status = status,
                Title = $"فرصة اختبار {n}", PropertyType = propertyType, City = city, District = district, Track = shape.Track, Area = area, Bedrooms = bedrooms,
                Readiness = readiness, DeliveryMonth = deliveryMonth, ExactLatitude = lat, ExactLongitude = lng, LocationPrecision = precision,
                PublicLatitude = plat, PublicLongitude = plng, PreparedByLabel = "اختبار", IsDemo = true,
                PublishedAt = status == OpportunityStatus.Published ? DateTimeOffset.UtcNow.AddMinutes(-n) : null,
            };
            db.Opportunities.Add(o);
            var annual = shape.ExtraRecurrence == "annual" ? shape.ExtraPayment : null;
            var t = new OpportunityTerms
            {
                OrganizationId = org, OpportunityId = o.Id, ApplicantUserId = owner, VersionNo = 1, Status = TermsStatus.OwnerConfirmed, Track = shape.Track,
                InputJson = "{}", ResultJson = ResultJson(shape.DueNow, shape.Total, shape.FutureBalance, shape.Installment, shape.Frequency, shape.ExtraPayment,
                    shape.RemainingMonths, shape.NeedsNewFinancing, shape.Quality), PreparedByLabel = "اختبار",
                DueNow = shape.DueNow, PurchaseTotal = shape.Total, FutureBalance = shape.FutureBalance, Installment = shape.Installment,
                InstallmentFrequency = shape.Frequency, InstallmentMonthlyEquivalent = MarketCalculator.MonthlyEquivalent(shape.Installment, shape.Frequency),
                LargestExtraPayment = shape.ExtraPayment, RemainingMonths = shape.RemainingMonths, NeedsNewFinancing = shape.NeedsNewFinancing,
                Complete = shape.Quality != "incomplete", Quality = shape.Quality, ExtraPaymentRecurrence = shape.ExtraPayment is null ? null : shape.ExtraRecurrence,
                AnnualExtraPayment = annual, OneOffExtraPayment = shape.ExtraRecurrence == "once" ? shape.ExtraPayment : null, NextExtraPaymentDate = shape.ExtraPayment is null ? null : "2027-01-15",
                ScheduleKnown = TermsSnapshot.IsScheduleKnown(shape.FutureBalance, shape.Installment, shape.Frequency, shape.ExtraPayment, shape.ExtraRecurrence),
            };
            db.OpportunityTerms.Add(t);
            o.PublishedTermsId = status is OpportunityStatus.Published or OpportunityStatus.Paused or OpportunityStatus.Withdrawn ? t.Id : null;
            await db.SaveChangesAsync();
            return new Inserted(o.Id, o.Reference, t.Id, sr.Id);
        });
    }

    /// <summary>A stored result consistent with the typed snapshot (cards and details read it).</summary>
    private static string ResultJson(decimal? due, decimal? total, decimal? future, decimal? inst, string? freq, decimal? extra, int? months, bool financing, string quality) =>
        JsonSerializer.Serialize(new TermsResult
        {
            Complete = quality != "incomplete", Quality = quality, QualityText = "", Missing = [], Lines = [], Notes = [], Commission = new CommissionLine(false, null, ""),
            DueNow = due, BuyerTotal = total, FutureBalance = future, Installment = inst, InstallmentFrequency = freq,
            InstallmentMonthlyEquivalent = MarketCalculator.MonthlyEquivalent(inst, freq), LargestExtraPayment = extra, RemainingMonths = months, NeedsNewFinancing = financing,
        }, JsonOptions.Web);

    /// <summary>Changes an inserted opportunity's status (withdraw / pause / publish) directly.</summary>
    public static Task SetStatusAsync(ApiFixture api, Guid id, OpportunityStatus status) =>
        api.WithDbAsync(db => db.Opportunities.Where(o => o.Id == id).ExecuteUpdateAsync(u => u.SetProperty(o => o.Status, status)));

    /// <summary>Publishes a new terms version (VersionNo+1) with the given cash due now; returns its id.</summary>
    public static Task<Guid> NewVersionAsync(ApiFixture api, Guid opportunityId, decimal? dueNow) => api.WithDbAsync(async db =>
    {
        var o = await db.Opportunities.FirstAsync(x => x.Id == opportunityId);
        var old = await db.OpportunityTerms.FirstAsync(x => x.Id == o.PublishedTermsId);
        var t = new OpportunityTerms
        {
            OrganizationId = old.OrganizationId, OpportunityId = o.Id, ApplicantUserId = old.ApplicantUserId, VersionNo = old.VersionNo + 1, Status = TermsStatus.OwnerConfirmed,
            Track = old.Track, InputJson = "{}", PreparedByLabel = "اختبار", DueNow = dueNow,
            ResultJson = ResultJson(dueNow, old.PurchaseTotal, old.FutureBalance, old.Installment, old.InstallmentFrequency, old.LargestExtraPayment, old.RemainingMonths,
                old.NeedsNewFinancing, old.Quality ?? "complete_estimate"), PurchaseTotal = old.PurchaseTotal,
            FutureBalance = old.FutureBalance, Installment = old.Installment, InstallmentFrequency = old.InstallmentFrequency,
            InstallmentMonthlyEquivalent = old.InstallmentMonthlyEquivalent, LargestExtraPayment = old.LargestExtraPayment, RemainingMonths = old.RemainingMonths,
            Complete = old.Complete, Quality = old.Quality, ScheduleKnown = old.ScheduleKnown,
        };
        old.Status = TermsStatus.Superseded;
        db.OpportunityTerms.Add(t);
        o.PublishedTermsId = t.Id;
        await db.SaveChangesAsync();
        return t.Id;
    });
}
