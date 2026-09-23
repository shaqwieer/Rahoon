using Rahoon.Api.Modules.Ecosystem;
using Rahoon.Api.Modules.Solutions;

namespace Rahoon.Api.Tests;

/// <summary>B9 pure rules: k-anonymity, workflow rule evaluation and licence thresholds.</summary>
public sealed class EcosystemUnitTests
{
    [Fact]
    public void Cells_with_fewer_than_ten_cases_are_suppressed_value_and_count()
    {
        var facts = Enumerable.Range(0, 9).Select(i => new ReportFact("إعادة جدولة", "Q3-2026", 40 + i))
            .Concat(Enumerable.Range(0, 12).Select(i => new ReportFact("إعادة جدولة", "Q2-2026", 50)))
            .Concat(Enumerable.Range(0, 10).Select(i => new ReportFact("سداد مخفض", "Q2-2026", i % 2)));
        string[] rows = ["إعادة جدولة", "سداد مخفض"];
        string[] cols = ["Q2-2026", "Q3-2026"];
        var cells = ReportAggregator.Aggregate(facts, "avg", rows, cols);
        var small = cells.Single(c => c.Row == "إعادة جدولة" && c.Column == "Q3-2026");
        Assert.True(small.Suppressed);
        Assert.Null(small.Value);
        Assert.Null(small.N);
        var ok = cells.Single(c => c.Row == "إعادة جدولة" && c.Column == "Q2-2026");
        Assert.Equal(50m, ok.Value);
        Assert.Equal(12, ok.N);
        Assert.True(cells.Single(c => c.Row == "سداد مخفض" && c.Column == "Q3-2026").Suppressed); // empty cell
        Assert.Equal(0.5m, ReportAggregator.Aggregate(facts, "rate", rows, cols).Single(c => c.Row == "سداد مخفض" && c.Column == "Q2-2026").Value);
        Assert.Equal(12m, ReportAggregator.Aggregate(facts, "count", rows, cols).Single(c => c.Row == "إعادة جدولة" && c.Column == "Q2-2026").Value);

        var csv = ReportAggregator.Csv("نوع الحل", rows, cols, cells);
        Assert.Contains("إعادة جدولة,50,—", csv);
        Assert.DoesNotContain("44", csv); // nothing from the suppressed cell leaks
    }

    [Fact]
    public void Quarters_cover_the_period_in_order()
    {
        Assert.Equal(["Q4-2025", "Q1-2026", "Q2-2026", "Q3-2026"], ReportAggregator.Quarters(new DateOnly(2025, 10, 1), new DateOnly(2026, 9, 30)));
    }

    [Fact]
    public void Workflow_rule_adds_compliance_review_above_three_percent_waiver()
    {
        var stages = WorkflowModel.DefaultStages();
        stages.First(s => s.Key == "internal_approval").Rules.Add(new WorkflowRule
        {
            Label = "التنازل > 3% ← مراجعة الامتثال", Action = "add_compliance_review", Condition = new RuleCondition { Field = "waiver_percent", Operator = ">", Value = 3 },
        });
        List<ApprovalLimitTier> tiers =
        [
            new() { Rank = 1, RoleKey = "approver", LevelLabel = "معتمد", SolutionKinds = ["Reschedule"], MaxAmount = 2_000_000m, MaxWaiverPercent = 0.05m },
            new() { Rank = 2, RoleKey = "senior_approver", LevelLabel = "معتمد أول", SolutionKinds = ["Reschedule"], MaxAmount = 5_000_000m, MaxWaiverPercent = 0.10m },
        ];
        var over = WorkflowModel.Evaluate(stages, new SimulationCase(1_120_450m, 4m, 45m, 96, "Reschedule", 6), tiers);
        Assert.Equal("معتمد", over.BaseTier);
        Assert.Equal(["مراجعة الامتثال قبل المعتمد", "معتمد"], over.RequiredApprovals);
        Assert.Equal(1, over.AddedDays);
        var under = WorkflowModel.Evaluate(stages, new SimulationCase(1_120_450m, 2m, 45m, 96, "Reschedule", 6), tiers);
        Assert.Equal(["معتمد"], under.RequiredApprovals);
        var big = WorkflowModel.Evaluate(stages, new SimulationCase(3_000_000m, 1m, null, 96, "Reschedule", 6), tiers);
        Assert.Equal("معتمد أول", big.BaseTier);
        Assert.All(WorkflowModel.DefaultStages().Where(s => s.Locked), s => Assert.All(s.Rules, r => Assert.True(r.PlatformLocked)));
    }

    [Theory]
    [InlineData(90, "valid")]
    [InlineData(45, "expiring_soon")]
    [InlineData(-1, "expired")]
    public void Licence_state_thresholds(int daysLeft, string expected)
    {
        var today = new DateOnly(2026, 9, 23);
        Assert.Equal(expected, ProviderDirectory.Snake(LicenseRules.State(today.AddDays(daysLeft), today)));
    }

    [Fact]
    public void Directory_status_warns_at_thirty_days_and_suspends_expired()
    {
        var today = new DateOnly(2026, 9, 23);
        Assert.Equal("approved", LicenseRules.DirectoryStatus(ProviderRegistrationStatus.Accepted, today.AddDays(38), today).Key);
        var warn = LicenseRules.DirectoryStatus(ProviderRegistrationStatus.Accepted, today.AddDays(20), today);
        Assert.Equal(("warning", true), (warn.Key, warn.Assignable));
        var expired = LicenseRules.DirectoryStatus(ProviderRegistrationStatus.Accepted, today.AddDays(-5), today);
        Assert.Equal(("suspended", false), (expired.Key, expired.Assignable));
        Assert.False(LicenseRules.DirectoryStatus(ProviderRegistrationStatus.NeedsInfo, today.AddDays(300), today).Assignable);
        Assert.Equal("ينتهي خلال 20 يوماً", LicenseRules.Text(today.AddDays(20), today));
    }
}
