using Rahoon.Api.Infrastructure.Security;
using Rahoon.Api.Infrastructure.Time;
using Rahoon.Api.Modules.Cases;
using Rahoon.Api.Modules.Solutions;

namespace Rahoon.Api.Tests;

public sealed class UnitTests
{
    [Fact]
    public void Solution_calculator_reproduces_design_figures_for_v2()
    {
        var f = SolutionCalculator.Compute(new SolutionInput(SolutionKind.Reschedule, 1_284_560.00m, 84, new DateOnly(2026, 11, 1), 18_300.00m, 0, 0, 32_400.00m, 0.55m));
        Assert.Equal(1_266_260.00m, f.RescheduledAmount);
        Assert.Equal(15_074.52m, f.InstallmentAmount);
        Assert.Equal(new DateOnly(2033, 10, 1), f.LastDueDate);
        Assert.Equal(0.0142m, Math.Round(f.WaiverPercent, 4));
        Assert.Equal(0.4653m, f.Dsr);
        Assert.True(f.DsrWithinLimit);
        // Σ installments equals the rescheduled amount exactly (rounding absorbed by the last one).
        Assert.Equal(f.RescheduledAmount, f.InstallmentAmount * 83 + f.FinalInstallmentAmount);
    }

    [Fact]
    public void Solution_calculator_reproduces_design_figures_for_v1()
    {
        var f = SolutionCalculator.Compute(new SolutionInput(SolutionKind.Reschedule, 1_284_560.00m, 60, new DateOnly(2026, 11, 1), 0, 0, 0, 32_400.00m, 0.55m));
        Assert.Equal(21_409.33m, f.InstallmentAmount);
        Assert.Equal(0.6608m, f.Dsr);
        Assert.False(f.DsrWithinLimit);
    }

    [Theory]
    [InlineData("1098734542", "1•••••••42")]
    [InlineData("2012345601", "2•••••••01")]
    public void National_id_mask_keeps_first_and_last_two(string id, string expected) => Assert.Equal(expected, Mask.NationalId(id));

    [Fact]
    public void Phone_and_name_masks_follow_A10()
    {
        Assert.Equal("+966 5• ••• ••81", Mask.Phone("0551234581"));
        Assert.Equal("عبدالله م.", Mask.PersonName("عبدالله محمد السبيعي"));
    }

    [Fact]
    public void Business_days_skip_friday_and_saturday()
    {
        // Wed 2026-09-23 + 3 business days = Mon 2026-09-28 (Thu, Sun, Mon).
        Assert.Equal(new DateOnly(2026, 9, 28), BusinessDays.Add(new DateOnly(2026, 9, 23), 3));
    }

    [Theory]
    [InlineData(1, "يوم واحد")]
    [InlineData(2, "يومان")]
    [InlineData(3, "3 أيام")]
    [InlineData(12, "12 يوماً")]
    public void Arabic_day_counts_agree_with_number(int n, string expected) => Assert.Equal(expected, CaseDisplay.Days(n));

    [Fact]
    public void Hijri_display_uses_umm_al_qura()
    {
        // Official Umm al-Qura: 2026-09-23 is 12 Rabi al-Akhir (the design sample says 11 — logged as a design conflict).
        Assert.Equal("12 ربيع الآخر 1448هـ", Hijri.Format(new DateOnly(2026, 9, 23)));
    }

    [Fact]
    public void Transition_table_never_auto_refers_on_decline()
    {
        var decline = CaseWorkflow.Def("owner_declined");
        Assert.Equal(CaseStatus.ProposedSolution, decline.To);
        Assert.DoesNotContain(CaseWorkflow.Transitions, t => t.To == CaseStatus.JudicialReferral && t.Permission.Length == 0);
    }
}
