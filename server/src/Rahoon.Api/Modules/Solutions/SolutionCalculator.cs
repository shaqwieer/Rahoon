namespace Rahoon.Api.Modules.Solutions;

public sealed record SolutionInput(SolutionKind Kind, decimal Outstanding, int TermMonths, DateOnly FirstDueDate,
    decimal WaiverAmount, decimal DownPayment, int GraceMonths, decimal? NetIncome, decimal DsrLimit, decimal? DiscountedPayoff = null);

public sealed record SolutionFigures(decimal RescheduledAmount, decimal InstallmentAmount, decimal FinalInstallmentAmount,
    DateOnly LastDueDate, decimal WaiverPercent, decimal? Dsr, bool DsrWithinLimit);

/// <summary>
/// Financial calculations for solution versions — server-side only, decimal arithmetic.
/// Verified against the design's sample figures (RH-2026-004172):
/// rescheduled = outstanding − waiver − down payment (1,266,260.00);
/// installment = rescheduled ÷ term rounded to 2 dp (15,074.52 for 84 months);
/// the final installment absorbs the rounding remainder so Σ installments = rescheduled;
/// last due = first due + (term − 1) months (2033-10-01);
/// waiver % = waiver ÷ outstanding (1.42%); DSR = installment ÷ verified net income (46.5%).
/// No profit is added (the design shows none) — flagged as an assumption for product confirmation.
/// </summary>
public static class SolutionCalculator
{
    public static SolutionFigures Compute(SolutionInput i)
    {
        if (i.Outstanding <= 0) throw new ArgumentOutOfRangeException(nameof(i.Outstanding));
        var waiverPct = Math.Round(i.WaiverAmount / i.Outstanding, 6);

        if (i.Kind == SolutionKind.ReducedPayoff)
        {
            var payoff = i.DiscountedPayoff ?? (i.Outstanding - i.WaiverAmount);
            var discountPct = Math.Round((i.Outstanding - payoff) / i.Outstanding, 6);
            return new SolutionFigures(payoff, payoff, payoff, i.FirstDueDate, discountPct, null, true);
        }

        var term = Math.Max(1, i.TermMonths);
        var rescheduled = i.Outstanding - i.WaiverAmount - i.DownPayment;
        if (rescheduled <= 0) throw new ArgumentOutOfRangeException(nameof(i.WaiverAmount), "Waiver and down payment exceed the outstanding amount.");

        var installment = Math.Round(rescheduled / term, 2, MidpointRounding.AwayFromZero);
        var final = rescheduled - installment * (term - 1);
        var first = i.FirstDueDate.AddMonths(i.GraceMonths);
        var last = first.AddMonths(term - 1);

        decimal? dsr = i.NetIncome is > 0 ? Math.Round(installment / i.NetIncome.Value, 4) : null;
        return new SolutionFigures(rescheduled, installment, final, last, waiverPct, dsr, dsr is null || dsr <= i.DsrLimit);
    }
}
