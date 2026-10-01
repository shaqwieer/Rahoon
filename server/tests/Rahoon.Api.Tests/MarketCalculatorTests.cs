using Rahoon.Api.Modules.Market;

namespace Rahoon.Api.Tests;

/// <summary>
/// The calculation rules of docs/product/product-definition.md §7, including the three verification examples of the brief
/// (they are examples, not real prices or fees). Pure unit tests: no database.
/// </summary>
public sealed class MarketCalculatorTests
{
    private static readonly CommissionPolicy NoPolicy = new();

    [Fact]
    public void Example_1_developer_arrears_carried_by_the_buyer_are_counted_once()
    {
        // Contract 1,000,000 · paid 300,000 · balance 700,000 including arrears 20,000 · reduction 10,000 · buyer costs 15,000 now.
        var r = MarketCalculator.Compute(new TermsInput
        {
            Developer = new DeveloperTerms
            {
                PaidApproved = 300_000, RemainingBalance = 700_000, ArrearsState = "has", Arrears = 20_000, ArrearsInBalance = "yes",
                ArrearsPayer = "buyer", Reduction = 10_000,
            },
            BuyerCostsNow = 15_000, BuyerCostsLater = 0, SellerCosts = 0,
        }, NoPolicy);

        Assert.True(r.Complete);
        Assert.Equal(290_000m, r.OwnerAmount);
        Assert.Equal(325_000m, r.DueNow);
        Assert.Equal(680_000m, r.FutureBalance);
        Assert.Equal(1_005_000m, r.BuyerTotal);
    }

    [Fact]
    public void Example_1_variant_arrears_outside_the_balance_are_added_once()
    {
        // Same deal, but the statement shows the 20,000 outside a 680,000 balance: the developer is still owed 700,000 in total.
        var r = MarketCalculator.Compute(new TermsInput
        {
            Developer = new DeveloperTerms
            {
                PaidApproved = 300_000, RemainingBalance = 680_000, ArrearsState = "has", Arrears = 20_000, ArrearsInBalance = "no",
                ArrearsPayer = "buyer", Reduction = 10_000,
            },
            BuyerCostsNow = 15_000, BuyerCostsLater = 0, SellerCosts = 0,
        }, NoPolicy);

        Assert.Equal(325_000m, r.DueNow);
        Assert.Equal(680_000m, r.FutureBalance);
        Assert.Equal(1_005_000m, r.BuyerTotal);
    }

    [Fact]
    public void Developer_arrears_carried_by_the_seller_come_out_of_the_owner_amount_not_from_both()
    {
        var r = MarketCalculator.Compute(new TermsInput
        {
            Developer = new DeveloperTerms
            {
                PaidApproved = 300_000, RemainingBalance = 700_000, ArrearsState = "has", Arrears = 20_000, ArrearsInBalance = "yes",
                ArrearsPayer = "seller", Reduction = 10_000,
            },
            BuyerCostsNow = 15_000, BuyerCostsLater = 0, SellerCosts = 0,
        }, NoPolicy);

        Assert.Equal(290_000m, r.OwnerAmount);
        Assert.Equal(270_000m, r.SellerNet);       // 290,000 − 20,000 paid to the developer from the owner's amount
        Assert.Equal(305_000m, r.DueNow);          // 290,000 + 15,000; the arrears are not added on the buyer as well
        Assert.Equal(680_000m, r.FutureBalance);
        Assert.Equal(985_000m, r.BuyerTotal);
    }

    [Fact]
    public void Example_2_bank_seller_net_ignores_past_installments()
    {
        // Sale 1,100,000 · official payoff 800,000 · seller costs 10,000 → net 290,000.
        var r = MarketCalculator.Compute(new TermsInput
        {
            Financier = new FinancierTerms { SalePrice = 1_100_000, PayoffAmount = 800_000, ArrearsState = "none" },
            SellerCosts = 10_000, BuyerCostsNow = 0, BuyerCostsLater = 0,
        }, NoPolicy);

        Assert.True(r.Complete);
        Assert.Equal(290_000m, r.SellerNet);
        // Not «the buyer pays 290,000» and not «the buyer continues the old installment».
        Assert.Equal(1_100_000m, r.DueNow);
        Assert.Null(r.Installment);
        Assert.Equal(0m, r.FutureBalance);
        Assert.Contains(r.Notes, n => n.Contains("لا نفترض أن المشتري سيكمل قسط التمويل الحالي"));
    }

    [Fact]
    public void Bank_payoff_that_includes_arrears_is_not_increased()
    {
        var r = MarketCalculator.Compute(new TermsInput
        {
            Financier = new FinancierTerms { SalePrice = 1_100_000, PayoffAmount = 800_000, ArrearsState = "has", Arrears = 30_000, PayoffIncludesArrears = "yes" },
            SellerCosts = 10_000, BuyerCostsNow = 0, BuyerCostsLater = 0,
        }, NoPolicy);
        Assert.Equal(290_000m, r.SellerNet);

        var excluded = MarketCalculator.Compute(new TermsInput
        {
            Financier = new FinancierTerms { SalePrice = 1_100_000, PayoffAmount = 800_000, ArrearsState = "has", Arrears = 30_000, PayoffIncludesArrears = "no" },
            SellerCosts = 10_000, BuyerCostsNow = 0, BuyerCostsLater = 0,
        }, NoPolicy);
        Assert.Equal(260_000m, excluded.SellerNet);
    }

    [Fact]
    public void Example_3_unknown_balance_or_fees_is_an_incomplete_estimate_not_zero()
    {
        var r = MarketCalculator.Compute(new TermsInput
        {
            Developer = new DeveloperTerms { PaidApproved = 300_000, RemainingBalance = null, ArrearsState = "none" },
            BuyerCostsNow = null, BuyerCostsLater = 0, SellerCosts = 0,
        }, NoPolicy);

        Assert.False(r.Complete);
        Assert.Equal("incomplete", r.Quality);
        Assert.Contains("الرصيد المتبقي للمطور", r.Missing);
        Assert.Contains("تكاليف المشتري المستحقة الآن", r.Missing);
        Assert.Null(r.DueNow);
        Assert.Null(r.BuyerTotal);
        Assert.StartsWith("تقدير غير مكتمل", r.QualityText);
    }

    [Fact]
    public void Unknown_arrears_state_is_missing_not_none()
    {
        var r = MarketCalculator.Compute(new TermsInput
        {
            Developer = new DeveloperTerms { PaidApproved = 300_000, RemainingBalance = 700_000, ArrearsState = "unknown" },
        }, NoPolicy);
        Assert.False(r.Complete);
        Assert.Contains("وجود متأخرات لدى المطور", r.Missing);
        Assert.Null(r.DueNow);
    }

    [Fact]
    public void Negative_bank_result_is_a_gap_to_review_not_zero()
    {
        var r = MarketCalculator.Compute(new TermsInput
        {
            Financier = new FinancierTerms { SalePrice = 700_000, PayoffAmount = 800_000, ArrearsState = "none" },
            SellerCosts = 10_000, BuyerCostsNow = 0, BuyerCostsLater = 0,
        }, NoPolicy);
        Assert.True(r.Gap);
        Assert.Equal(-110_000m, r.SellerNet);
        Assert.Contains(r.Notes, n => n.StartsWith("توجد فجوة"));
    }

    [Fact]
    public void Commission_is_never_zero_while_the_policy_is_not_approved()
    {
        var r = MarketCalculator.Compute(new TermsInput
        {
            Financier = new FinancierTerms { SalePrice = 1_000_000, PayoffAmount = 500_000, ArrearsState = "none" },
        }, NoPolicy);
        Assert.False(r.Commission.PolicyApproved);
        Assert.Null(r.Commission.Amount);
        Assert.Contains("لم تُعتمد", r.Commission.Text);

        var approved = MarketCalculator.Compute(new TermsInput
        {
            Financier = new FinancierTerms { SalePrice = 1_000_000, PayoffAmount = 500_000, ArrearsState = "none" },
        }, new CommissionPolicy { Approved = true, Rate = 0.01m, Basis = "purchase_total" });
        Assert.Equal(10_000m, approved.Commission.Amount);
    }

    [Fact]
    public void Quarterly_installment_has_a_monthly_equivalent_for_comparison_only()
    {
        var r = MarketCalculator.Compute(new TermsInput
        {
            Developer = new DeveloperTerms
            {
                PaidApproved = 200_000, RemainingBalance = 600_000, ArrearsState = "none", Installment = 30_000, InstallmentFrequency = "quarterly",
                RemainingInstallments = 20,
            },
        }, NoPolicy);
        Assert.Equal(30_000m, r.Installment);
        Assert.Equal("quarterly", r.InstallmentFrequency);
        Assert.Equal(10_000m, r.InstallmentMonthlyEquivalent);
        Assert.Equal(60, r.RemainingMonths);
    }

    [Fact]
    public void A_low_monthly_average_does_not_fit_when_an_extra_payment_exceeds_what_is_left()
    {
        var cap = new CapacityInput(AvailableNow: 400_000, InstallmentComfort: 12_000, InstallmentFrequency: "monthly", MaxPrice: null);
        var fit = MarketCalculator.Fit(cap, dueNow: 350_000, purchaseTotal: 1_200_000, installmentMonthly: 8_000, largestExtra: 150_000, needsNewFinancing: false);
        Assert.False(fit.Fits);
        Assert.Contains(fit.Limits, l => l.Contains("دفعة إضافية"));

        var ok = MarketCalculator.Fit(cap, dueNow: 350_000, purchaseTotal: 1_200_000, installmentMonthly: 8_000, largestExtra: 40_000, needsNewFinancing: false);
        Assert.True(ok.Fits);
    }

    [Fact]
    public void Unknown_due_now_never_matches_a_budget()
    {
        var fit = MarketCalculator.Fit(new CapacityInput(1_000_000, null, null, null), dueNow: null, purchaseTotal: null, null, null, false);
        Assert.False(fit.Fits);
        Assert.False(fit.Comparable);
    }
}
