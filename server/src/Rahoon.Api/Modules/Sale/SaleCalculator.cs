namespace Rahoon.Api.Modules.Sale;

/// <summary>Pre-offer estimate (L27). All figures are «تقديري» and carry their source.</summary>
public sealed record SaleEstimate(
    decimal MarketValue, decimal OutstandingDebt, decimal BrokerRate, decimal BrokerCommission, decimal OtherFees,
    decimal ExpectedSurplus, decimal ExpectedShortfall);

/// <summary>Per-offer figures (L32 comparison, L33 distribution).</summary>
public sealed record OfferFigures(
    decimal Price, decimal BrokerCommission, decimal NetAfterCommission, decimal OtherCosts, decimal NetProceeds,
    decimal LenderRecovery, decimal OwnerSurplus, decimal Shortfall, bool MeetsMinimum);

/// <summary>
/// Voluntary-sale money rules, server-side only (B8):
/// <list type="bullet">
/// <item>Before offers: surplus = valuation − debt − valuation × brokerRate − otherFees (2,850,000 → 545,000).</item>
/// <item>Per offer: net = price × (1 − brokerRate) (2,780,000 → 2,724,400).</item>
/// <item>Distribution: owner surplus = price − debt payoff − commission (2,800,000 → 504,000); a negative remainder is a shortfall.</item>
/// <item>Minimum check compares the offer price with the owner's consented minimum (conflict #4).</item>
/// </list>
/// Other fees are an assumption applied to the pre-offer estimate only; offer figures deduct only the
/// broker commission unless actual other costs are recorded (conflict #2).
/// </summary>
public static class SaleCalculator
{
    public static decimal Money(decimal v) => Math.Round(v, 2, MidpointRounding.AwayFromZero);

    public static SaleEstimate Estimate(decimal marketValue, decimal outstandingDebt, decimal brokerRate, decimal otherFees)
    {
        if (marketValue <= 0) throw new ArgumentOutOfRangeException(nameof(marketValue));
        if (brokerRate is < 0 or >= 0.2m) throw new ArgumentOutOfRangeException(nameof(brokerRate));
        var commission = Money(marketValue * brokerRate);
        var remainder = Money(marketValue - outstandingDebt - commission - otherFees);
        return new SaleEstimate(marketValue, outstandingDebt, brokerRate, commission, otherFees, Math.Max(0, remainder), Math.Max(0, -remainder));
    }

    public static OfferFigures Offer(decimal price, decimal outstandingDebt, decimal brokerRate, decimal? ownerMinimum, decimal otherCosts = 0)
    {
        if (price <= 0) throw new ArgumentOutOfRangeException(nameof(price));
        var commission = Money(price * brokerRate);
        var net = Money(price - commission);
        var proceeds = Money(net - otherCosts);
        var recovery = Math.Min(outstandingDebt, Math.Max(0, proceeds));
        var surplus = Math.Max(0, Money(proceeds - outstandingDebt));
        var shortfall = Math.Max(0, Money(outstandingDebt - proceeds));
        return new OfferFigures(price, commission, net, otherCosts, proceeds, recovery, surplus, shortfall,
            ownerMinimum is null || price >= ownerMinimum.Value);
    }
}
