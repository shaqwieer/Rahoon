using System.Text.Encodings.Web;
using System.Text.Json;
using Rahoon.Api.Modules.Cases;
using Rahoon.Api.Modules.Sale;

namespace Rahoon.Api.Tests;

/// <summary>B8 money rules and the disclosure policy against the design's sample figures.</summary>
public sealed class SaleUnitTests
{
    [Fact]
    public void Pre_offer_estimate_reproduces_L27_surplus()
    {
        var e = SaleCalculator.Estimate(2_850_000m, 2_240_000m, 0.02m, 8_000m);
        Assert.Equal(57_000.00m, e.BrokerCommission);
        Assert.Equal(545_000.00m, e.ExpectedSurplus);
        Assert.Equal(0m, e.ExpectedShortfall);
    }

    [Theory]
    [InlineData(2_780_000, 2_724_400)]
    [InlineData(2_800_000, 2_744_000)]
    [InlineData(2_820_000, 2_763_600)]
    public void Offer_net_after_commission_matches_L32(decimal price, decimal net)
    {
        var f = SaleCalculator.Offer(price, 2_240_000m, 0.02m, 2_700_000m);
        Assert.Equal(net, f.NetAfterCommission);
        Assert.True(f.MeetsMinimum);
    }

    [Fact]
    public void Distribution_of_accepted_offer_matches_L33()
    {
        var f = SaleCalculator.Offer(2_800_000m, 2_240_000m, 0.02m, 2_700_000m);
        Assert.Equal(56_000.00m, f.BrokerCommission);
        Assert.Equal(2_240_000.00m, f.LenderRecovery);
        Assert.Equal(504_000.00m, f.OwnerSurplus);
        Assert.Equal(0m, f.Shortfall);
    }

    [Fact]
    public void Minimum_is_compared_with_price_and_shortfall_is_explicit()
    {
        Assert.False(SaleCalculator.Offer(2_690_000m, 2_240_000m, 0.02m, 2_700_000m).MeetsMinimum);
        Assert.True(SaleCalculator.Offer(2_700_000m, 2_240_000m, 0.02m, 2_700_000m).MeetsMinimum);
        var low = SaleCalculator.Offer(2_000_000m, 2_240_000m, 0.025m, null);
        Assert.Equal(50_000.00m, low.BrokerCommission); // 2.5% keeps its precision
        Assert.Equal(1_950_000.00m, low.LenderRecovery);
        Assert.Equal(290_000.00m, low.Shortfall);
        Assert.Equal(0m, low.OwnerSurplus);
    }

    [Fact]
    public void Disclosure_matrix_has_the_eight_rows_and_no_public_column()
    {
        Assert.Equal(8, DisclosurePolicy.Matrix.Count);
        Assert.All(DisclosurePolicy.Matrix, r => Assert.Equal(Disclosure.No, r.Public));
        foreach (var key in new[] { "owner_identity", "owner_minimum", "debt_default" })
        {
            var row = DisclosurePolicy.Matrix.Single(r => r.Key == key);
            Assert.Equal(Disclosure.No, row.Broker);
            Assert.Equal(Disclosure.No, row.QualifiedBuyer);
        }
        Assert.Equal(Disclosure.AfterVisit, DisclosurePolicy.Matrix.Single(r => r.Key == "exact_location").QualifiedBuyer);
        Assert.Throws<InvalidOperationException>(() => DisclosurePolicy.Project(Source(), DisclosureAudience.Public));
    }

    [Theory]
    [InlineData(DisclosureAudience.Broker)]
    [InlineData(DisclosureAudience.QualifiedBuyer)]
    public void Projection_never_leaks_owner_case_lender_minimum_or_debt(DisclosureAudience audience)
    {
        var json = JsonSerializer.Serialize(DisclosurePolicy.Project(Source(), audience), new JsonSerializerOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
        foreach (var secret in new[] { "عائشة", "RH-2026-004012", "مصرف الأفق", "2700000", "2240000" })
            Assert.DoesNotContain(secret, json);
        Assert.Contains("VS-2026-0031", json);
        Assert.Contains("2850000", json);
        if (audience == DisclosureAudience.QualifiedBuyer)
        {
            Assert.DoesNotContain("حي الياسمين", json); // generalized area for buyers
            Assert.Contains("شمال الرياض", json);
        }
    }

    [Fact]
    public void Withdrawal_transition_returns_to_proposed_solution_never_referral()
    {
        var t = CaseWorkflow.Def("sale_withdrawn");
        Assert.Equal(CaseStatus.ProposedSolution, t.To);
        Assert.Equal([CaseStatus.VoluntarySale], t.From);
        Assert.True(t.RequiresReason);
    }

    private static ListingSource Source() => new("عائشة فهد القرشي", "RH-2026-004012", "مصرف الأفق", "VS-2026-0031", "فيلا سكنية", "الرياض", "حي الياسمين",
        "شمال الرياض", "حي الياسمين، شارع 12، الرياض", 6, 2_850_000m, 2_700_000m, 2_240_000m, 11, 60, "بموعد", 600, 680, 2017);
}
