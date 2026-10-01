using System.Text.Json;
using Rahoon.Api.Infrastructure.Http;
using Rahoon.Api.Modules.Market;
using Rahoon.Api.Modules.Market.Discovery;

namespace Rahoon.Api.Tests;

/// <summary>Phase 2: the affordability classifier, the calculator's fee allocation and the bank-track rule (no database).</summary>
public sealed class AffordabilityTests
{
    private static readonly CommissionPolicy NoPolicy = new();

    private static TermsInput DeveloperExample(List<FeeItem>? fees = null, decimal buyerCostsNow = 15_000) => new()
    {
        Developer = new DeveloperTerms
        {
            PaidApproved = 300_000, RemainingBalance = 700_000, ArrearsState = "has", Arrears = 20_000, ArrearsInBalance = "yes", ArrearsPayer = "buyer", Reduction = 10_000,
        },
        SellerCosts = 0, BuyerCostsNow = buyerCostsNow, BuyerCostsLater = 0, Fees = fees,
    };

    private static OpportunityTerms Snapshot(TermsInput input, string track = "developer")
    {
        var t = new OpportunityTerms { Track = track, InputJson = "{}", ResultJson = "{}", PreparedByLabel = "x" };
        TermsSnapshot.Apply(t, input, MarketCalculator.Compute(input, NoPolicy));
        return t;
    }

    [Fact]
    public void Developer_example_with_the_buyer_paying_the_fees_matches_the_brief()
    {
        // Fees given as an allocation instead of a bucket: the result must be the brief's example exactly.
        var r = MarketCalculator.Compute(DeveloperExample([new FeeItem("رسوم نقل لدى المطور", 15_000, "buyer", null, "now")], buyerCostsNow: 0), NoPolicy);
        Assert.Equal(290_000, r.OwnerAmount);
        Assert.Equal(325_000, r.DueNow);
        Assert.Equal(680_000, r.FutureBalance);
        Assert.Equal(1_005_000, r.BuyerTotal);
        Assert.Single(r.FeeLines!);
        Assert.Contains(r.Assumptions!, a => a.Contains("ليست رسومًا معتمدة"));
    }

    [Fact]
    public void Changing_who_pays_the_fee_moves_it_coherently_between_seller_and_buyer()
    {
        var seller = MarketCalculator.Compute(DeveloperExample([new FeeItem("رسوم نقل", 15_000, "seller", null, "now")], buyerCostsNow: 0), NoPolicy);
        Assert.Equal(310_000, seller.DueNow);   // 290,000 + 20,000 arrears; the fee leaves the buyer
        Assert.Equal(275_000, seller.SellerNet); // 290,000 − 15,000
        Assert.Equal(990_000, seller.BuyerTotal);

        var split = MarketCalculator.Compute(DeveloperExample([new FeeItem("رسوم نقل", 15_000, "split", 50, "now")], buyerCostsNow: 0), NoPolicy);
        Assert.Equal(317_500, split.DueNow);
        Assert.Equal(282_500, split.SellerNet);
        // Whatever the allocation, the buyer's extra cash plus the seller's lost net is the fee, counted once.
        Assert.Equal(15_000, (seller.DueNow - 310_000) + (290_000 - seller.SellerNet));
        Assert.Equal(15_000, (split.DueNow - 310_000) + (290_000 - split.SellerNet));

        var later = MarketCalculator.Compute(DeveloperExample([new FeeItem("رسوم لاحقة", 15_000, "buyer", null, "later")], buyerCostsNow: 0), NoPolicy);
        Assert.Equal(310_000, later.DueNow);
        Assert.Equal(1_005_000, later.BuyerTotal);
    }

    [Fact]
    public void Unknown_fee_makes_an_incomplete_estimate_not_zero()
    {
        var r = MarketCalculator.Compute(DeveloperExample([new FeeItem("رسوم المطور", null, "buyer", null, "now")]), NoPolicy);
        Assert.False(r.Complete);
        Assert.Null(r.DueNow);
        Assert.Contains(r.Missing, m => m.Contains("رسوم المطور"));
    }

    [Fact]
    public void Stored_terms_without_fees_compute_exactly_as_before()
    {
        // A Phase 1 version's input JSON has no Fees/AsOf: it deserializes and its result is unchanged (no Assumptions/FeeLines).
        var input = DeveloperExample();
        var json = JsonSerializer.Serialize(input with { Fees = null }, JsonOptions.Web).Replace(",\"fees\":null", "").Replace(",\"asOf\":null", "");
        var back = JsonSerializer.Deserialize<TermsInput>(json, JsonOptions.Web)!;
        var r = MarketCalculator.Compute(back, NoPolicy);
        Assert.Equal(325_000, r.DueNow);
        Assert.Null(r.FeeLines);
        Assert.Null(r.Assumptions);
    }

    [Fact]
    public void Bank_example_and_the_buyer_never_inherits_the_sellers_installment()
    {
        var input = new TermsInput
        {
            Financier = new FinancierTerms { SalePrice = 1_100_000, PayoffAmount = 800_000, ArrearsState = "none" },
            SellerCosts = 10_000, BuyerCostsNow = 0, BuyerCostsLater = 0, NeedsNewFinancing = true,
        };
        var r = MarketCalculator.Compute(input, NoPolicy);
        Assert.Equal(290_000, r.SellerNet);
        Assert.Equal(1_100_000, r.DueNow); // not 290,000
        var t = Snapshot(input, "financier");
        Assert.Null(t.Installment);
        Assert.Equal(0, t.FutureBalance);
        var payments = Affordability.NextPayments(t);
        Assert.DoesNotContain(payments, p => p.Key == "installment");
        Assert.Contains(payments, p => p.Key == "buyer_financing" && p.Amount is null);
        Assert.Contains(Affordability.Caveats(t), c => c.Contains("لا ينتقل إليك"));
        // A comfortable-installment search doesn't need an installment here (cash purchase): it fits on the cash rule alone.
        var fit = Affordability.Classify(new CapacityProfile(1_200_000, 5_000, "monthly", null), t);
        Assert.Equal(AffordabilityOutcome.Fits, fit.Outcome);
    }

    [Fact]
    public void A_large_annual_payment_invalidates_a_comfortable_monthly_average()
    {
        var t = Snapshot(new TermsInput
        {
            Developer = new DeveloperTerms
            {
                PaidApproved = 260_000, RemainingBalance = 840_000, ArrearsState = "none", Installment = 21_000, InstallmentFrequency = "quarterly",
                RemainingInstallments = 32, ExtraPayment = 50_000, ExtraPaymentRecurrence = "annual", ExtraPaymentDate = "2027-01-15",
            },
            SellerCosts = 0, BuyerCostsNow = 10_000, BuyerCostsLater = 0,
        });
        Assert.Equal(7_000, t.InstallmentMonthlyEquivalent);
        var tight = Affordability.Classify(new CapacityProfile(null, 7_000, "monthly", null), t);
        Assert.Equal(AffordabilityOutcome.DoesNotFit, tight.Outcome);
        Assert.Contains(tight.Limits, l => l.Contains("الدفعة السنوية"));
        Assert.Equal(134_000, tight.AnnualCommitment);
        var roomy = Affordability.Classify(new CapacityProfile(null, 11_200, "monthly", null), t);
        Assert.Equal(AffordabilityOutcome.Fits, roomy.Outcome);
        Assert.Contains(roomy.NextPayments, p => p.Key == "extra_payment" && p.Note!.Contains("2027-01-15"));
        Assert.Contains(roomy.NextPayments, p => p.Key == "installment" && p.Note!.Contains("غير مسجل"));
    }

    [Fact]
    public void Missing_schedule_or_cash_is_incomplete_never_a_pass()
    {
        // Installment unknown with a balance still owed.
        var noInstallment = Snapshot(new TermsInput
        {
            Developer = new DeveloperTerms { PaidApproved = 200_000, RemainingBalance = 500_000, ArrearsState = "none" },
            SellerCosts = 0, BuyerCostsNow = 0, BuyerCostsLater = 0,
        });
        Assert.Equal(AffordabilityOutcome.Incomplete, Affordability.Classify(new CapacityProfile(null, 9_000, "monthly", null), noInstallment).Outcome);

        // Extra payment without its recurrence.
        var noRecurrence = Snapshot(new TermsInput
        {
            Developer = new DeveloperTerms { PaidApproved = 200_000, RemainingBalance = 500_000, ArrearsState = "none", Installment = 4_000, InstallmentFrequency = "monthly", ExtraPayment = 30_000 },
            SellerCosts = 0, BuyerCostsNow = 0, BuyerCostsLater = 0,
        });
        Assert.False(noRecurrence.ScheduleKnown);
        Assert.Equal(AffordabilityOutcome.Incomplete, Affordability.Classify(new CapacityProfile(null, 9_000, "monthly", null), noRecurrence).Outcome);

        // Unknown cash due now against a budget.
        var unknownCash = Snapshot(DeveloperExample(buyerCostsNow: 0) with { BuyerCostsNow = null });
        var fit = Affordability.Classify(new CapacityProfile(1_000_000, null, null, null), unknownCash);
        Assert.Equal(AffordabilityOutcome.Incomplete, fit.Outcome);
        Assert.False(fit.Comparable);
    }

    [Fact]
    public void A_known_failure_wins_over_an_unknown()
    {
        var t = Snapshot(new TermsInput
        {
            Developer = new DeveloperTerms { PaidApproved = 200_000, RemainingBalance = 500_000, ArrearsState = "none", Installment = 4_000, InstallmentFrequency = "monthly" },
            SellerCosts = 0, BuyerCostsNow = 0, BuyerCostsLater = null, // total unknown
        });
        var fit = Affordability.Classify(new CapacityProfile(100_000, null, null, 900_000), t);
        Assert.Equal(AffordabilityOutcome.DoesNotFit, fit.Outcome); // 200,000 now > 100,000
    }
}
