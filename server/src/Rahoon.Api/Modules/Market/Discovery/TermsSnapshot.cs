namespace Rahoon.Api.Modules.Market.Discovery;

/// <summary>
/// The typed copy of a terms version's computed figures that search, the map, matching and alerts filter on. Written once when
/// the version is computed; a version sent to the owner is immutable, so this is never recomputed for it (the Phase 2 migration
/// backfilled older rows from their stored JSON, not by running the calculator again).
/// </summary>
public static class TermsSnapshot
{
    public static void Apply(OpportunityTerms t, TermsInput input, TermsResult result)
    {
        t.DueNow = result.DueNow;
        t.PurchaseTotal = result.BuyerTotal;
        t.FutureBalance = result.FutureBalance;
        t.Installment = result.Installment;
        t.InstallmentFrequency = result.InstallmentFrequency;
        t.InstallmentMonthlyEquivalent = result.InstallmentMonthlyEquivalent;
        t.LargestExtraPayment = result.LargestExtraPayment;
        t.RemainingMonths = result.RemainingMonths;
        t.NeedsNewFinancing = result.NeedsNewFinancing;
        t.Complete = result.Complete;
        t.Quality = result.Quality;

        var dev = input.Developer;
        var extra = dev?.ExtraPayment;
        var recurrence = extra is null ? null : dev!.ExtraPaymentRecurrence is "once" or "annual" ? dev.ExtraPaymentRecurrence : null;
        t.ExtraPaymentRecurrence = recurrence;
        t.AnnualExtraPayment = recurrence == "annual" ? extra : null;
        t.OneOffExtraPayment = recurrence == "once" ? extra : null;
        t.NextExtraPaymentDate = extra is null ? null : dev!.ExtraPaymentDate;
        t.ScheduleKnown = IsScheduleKnown(result.FutureBalance, result.Installment, result.InstallmentFrequency, extra, recurrence);
    }

    /// <summary>No future balance; or installment, frequency and any extra payment's recurrence are all known.</summary>
    public static bool IsScheduleKnown(decimal? futureBalance, decimal? installment, string? frequency, decimal? extra, string? recurrence) =>
        futureBalance == 0
        || (installment is not null && MarketCalculator.MonthsPerPeriod(frequency) is not null && (extra is null || recurrence is "once" or "annual"));
}
