namespace Rahoon.Api.Modules.Closure;

public sealed record FeeInput(string Label, decimal Amount, string BasisRef);

public sealed record WaterfallResult(
    decimal SalePrice, decimal ProcedureCosts, decimal ExpectedNet, decimal Received, decimal Difference,
    decimal LenderShare, IReadOnlyList<FeeInput> FeesApplied, decimal OtherFees, decimal OwnerSurplus, decimal Shortfall)
{
    public bool Balanced => Difference == 0m;
}

/// <summary>
/// F02 distribution waterfall (server-side only):
/// <list type="number">
/// <item>expectedNet = officialSalePrice − procedureCosts (costs are already netted by the authority);</item>
/// <item>difference = received − expectedNet, which must be 0.00 before a distribution can proceed;</item>
/// <item>lenderShare = min(net, approved debt statement);</item>
/// <item>other approved fees next, in the order given, capped by what remains (B10 conflict 8 — decision: costs → debt → other fees → surplus);</item>
/// <item>ownerSurplus = net − lenderShare − otherFees; shortfall = max(0, debt − net) stays with the lender and is never charged to the surplus.</item>
/// </list>
/// </summary>
public static class Waterfall
{
    public static WaterfallResult Compute(decimal salePrice, decimal procedureCosts, decimal received, decimal debt, IReadOnlyList<FeeInput>? fees = null)
    {
        if (salePrice <= 0) throw new ArgumentOutOfRangeException(nameof(salePrice), "Sale price must be positive.");
        if (procedureCosts < 0 || procedureCosts > salePrice) throw new ArgumentOutOfRangeException(nameof(procedureCosts));
        if (debt < 0) throw new ArgumentOutOfRangeException(nameof(debt));

        var expectedNet = salePrice - procedureCosts;
        var difference = received - expectedNet;
        var net = Math.Min(received, expectedNet); // never distribute more than both the minutes and the bank support
        var lender = Math.Min(net, debt);
        var remaining = net - lender;
        var applied = new List<FeeInput>();
        foreach (var f in fees ?? [])
        {
            if (f.Amount < 0) throw new ArgumentOutOfRangeException(nameof(fees), "Fees cannot be negative.");
            var take = Math.Min(remaining, f.Amount);
            applied.Add(f with { Amount = take });
            remaining -= take;
        }
        var otherFees = applied.Sum(a => a.Amount);
        return new WaterfallResult(salePrice, procedureCosts, expectedNet, received, difference, lender, applied, otherFees, remaining, Math.Max(0, debt - net));
    }
}
