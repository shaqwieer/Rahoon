using System.Text.Json;
using Rahoon.Api.Modules.Solutions;

namespace Rahoon.Api.Modules.Ecosystem;

public sealed class RuleCondition
{
    public string Field { get; set; } = "";
    public string Operator { get; set; } = ">";
    public decimal Value { get; set; }
}

public sealed class WorkflowRule
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string Label { get; set; } = "";
    /// <summary>Platform-mandated (segregation of duties, dual approval for referral/closure, owner's right to object): never removable.</summary>
    public bool PlatformLocked { get; set; }
    public RuleCondition? Condition { get; set; }
    public string? Action { get; set; }
    public int? AddedInVersion { get; set; }
}

public sealed class WorkflowStage
{
    public string Key { get; set; } = "";
    public string Icon { get; set; } = "";
    public string Title { get; set; } = "";
    public int? SlaDays { get; set; }
    /// <summary>Platform stage: cannot be removed or reordered; institutions may still add rules and adjust the SLA.</summary>
    public bool Locked { get; set; }
    public int? ChangedInVersion { get; set; }
    public List<WorkflowRule> Rules { get; set; } = [];
}

/// <summary>A hypothetical case for simulation (never persisted).</summary>
public sealed record SimulationCase(decimal Amount, decimal WaiverPercent, decimal? DsrPercent, int? TermMonths, string? SolutionKind, int? ArrearsInstallments);

public sealed record RuleHit(string StageKey, string RuleId, string Label, string Action, string ActionLabel);

public sealed record SimulationOutcome(string? BaseTier, IReadOnlyList<string> RequiredApprovals, IReadOnlyList<RuleHit> Hits, int AddedDays);

/// <summary>
/// Workflow designer model (PA14): a fixed vertical list of stages (not a free-form canvas) with rules
/// «conditions → required approval tier». Evaluation is pure — used by simulation without side effects.
/// </summary>
public static class WorkflowModel
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static readonly IReadOnlyDictionary<string, string> Fields = new Dictionary<string, string>
    {
        ["waiver_percent"] = "التنازل (%)",
        ["amount"] = "المبلغ (ر.س)",
        ["dsr_percent"] = "نسبة الاستقطاع (%)",
        ["term_months"] = "المدة (شهر)",
        ["arrears_installments"] = "الأقساط المتأخرة",
    };

    public static readonly string[] Operators = [">", ">=", "<", "<=", "="];

    /// <summary>Actions add a required approval step (the «required approval tier»). Added days are a stated assumption.</summary>
    public static readonly IReadOnlyDictionary<string, (string Label, int AddedDays)> Actions = new Dictionary<string, (string, int)>
    {
        ["add_compliance_review"] = ("مراجعة الامتثال قبل المعتمد", 1),
        ["add_legal_review"] = ("مراجعة القانونية قبل المعتمد", 1),
        ["require_senior_approver"] = ("اعتماد معتمد أول", 1),
        ["require_risk_committee"] = ("اعتماد لجنة المخاطر", 3),
    };

    public static List<WorkflowStage> Parse(string json) => JsonSerializer.Deserialize<List<WorkflowStage>>(json, Json) ?? [];
    public static string Serialize(List<WorkflowStage> stages) => JsonSerializer.Serialize(stages, Json);

    /// <summary>The platform baseline for a new institution (v1) — matches the designer frame's stage list.</summary>
    public static List<WorkflowStage> DefaultStages() =>
    [
        new() { Key = "intake", Icon = "edit_note", Title = "الإنشاء والاستلام", SlaDays = 5, Rules =
            [new() { Id = "dup-check", Label = "فحص التكرار إلزامي", PlatformLocked = true }, new() { Id = "import-draft", Label = "الاستيراد يبدأ مسودة", PlatformLocked = true }] },
        new() { Key = "verification", Icon = "fact_check", Title = "التحقق", SlaDays = 5, Rules =
            [new() { Id = "core-docs", Label = "صك + هوية + عقد", PlatformLocked = true }, new() { Id = "escalate-day4", Label = "تصعيد لمدير الفريق يوم 4" }] },
        new() { Key = "valuation", Icon = "query_stats", Title = "التقييم", SlaDays = 10, Rules =
            [new() { Id = "valuer-directory", Label = "مقيّم من الدليل", PlatformLocked = true }, new() { Id = "valuation-90", Label = "صلاحية ≤ 90 يوماً", PlatformLocked = true }] },
        new() { Key = "internal_approval", Icon = "approval", Title = "الموافقة الداخلية", SlaDays = 3, Locked = true, Rules =
            [new() { Id = "sod", Label = "المُعِدّ ≠ المعتمد", PlatformLocked = true }, new() { Id = "limits", Label = "حسب جدول الحدود v4", PlatformLocked = true }] },
        new() { Key = "awaiting_customer", Icon = "hourglass_empty", Title = "بانتظار العميل", SlaDays = 10, Locked = true, Rules =
            [new() { Id = "pause-on-complaint", Label = "تتوقف المهلة عند شكوى", PlatformLocked = true }, new() { Id = "decline-to-proposed", Label = "الرفض ← حل مقترح", PlatformLocked = true }] },
        new() { Key = "judicial_referral", Icon = "outbound", Title = "الإحالة القضائية", SlaDays = null, Locked = true, Rules =
            [new() { Id = "referral-dual", Label = "القانونية + معتمد", PlatformLocked = true }, new() { Id = "owner-objection", Label = "إشعار مسبق ومهلة اعتراض", PlatformLocked = true }] },
        new() { Key = "closure", Icon = "calculate", Title = "التسوية والإغلاق", SlaDays = 5, Locked = true, Rules =
            [new() { Id = "zero-diff", Label = "مطابقة صفرية الفرق", PlatformLocked = true }, new() { Id = "closure-dual", Label = "المالية + معتمد", PlatformLocked = true }] },
    ];

    public static string Describe(RuleCondition c, string action) =>
        $"{Fields.GetValueOrDefault(c.Field, c.Field)} {c.Operator} {c.Value:0.##}{(c.Field.EndsWith("_percent") ? "%" : "")} ← {Actions.GetValueOrDefault(action).Label ?? action}";

    public static bool Matches(RuleCondition c, SimulationCase x)
    {
        decimal? v = c.Field switch
        {
            "waiver_percent" => x.WaiverPercent,
            "amount" => x.Amount,
            "dsr_percent" => x.DsrPercent,
            "term_months" => x.TermMonths,
            "arrears_installments" => x.ArrearsInstallments,
            _ => null,
        };
        if (v is not { } val) return false;
        return c.Operator switch
        {
            ">" => val > c.Value, ">=" => val >= c.Value, "<" => val < c.Value, "<=" => val <= c.Value, "=" => val == c.Value, _ => false,
        };
    }

    /// <summary>
    /// Evaluates a hypothetical case: the base approver tier from the institution's effective approval-limit policy,
    /// then every matching rule's added approval step. Pure function — no persistence, no audit.
    /// </summary>
    public static SimulationOutcome Evaluate(IReadOnlyList<WorkflowStage> stages, SimulationCase x, IReadOnlyList<ApprovalLimitTier>? tiers)
    {
        string? baseTier = null;
        if (tiers is not null)
        {
            var kind = string.IsNullOrWhiteSpace(x.SolutionKind) ? "Reschedule" : x.SolutionKind;
            baseTier = tiers.Where(t => t.CanApprove && t.SolutionKinds.Contains(kind)
                                        && (t.MaxAmount is null || x.Amount <= t.MaxAmount)
                                        && (t.MaxWaiverPercent is null || x.WaiverPercent / 100m <= t.MaxWaiverPercent))
                .OrderBy(t => t.Rank).Select(t => t.LevelLabel).FirstOrDefault() ?? "خارج جدول الحدود — تصعيد";
        }
        var hits = new List<RuleHit>();
        foreach (var s in stages)
            foreach (var r in s.Rules.Where(r => r.Condition is not null && r.Action is not null))
                if (Matches(r.Condition!, x))
                    hits.Add(new RuleHit(s.Key, r.Id, r.Label, r.Action!, Actions.GetValueOrDefault(r.Action!).Label ?? r.Action!));
        var approvals = new List<string>();
        approvals.AddRange(hits.Where(h => h.Action is "add_compliance_review" or "add_legal_review").Select(h => h.ActionLabel).Distinct());
        var escalation = hits.Any(h => h.Action == "require_risk_committee") ? "لجنة المخاطر" : hits.Any(h => h.Action == "require_senior_approver") ? "معتمد أول" : null;
        approvals.Add(escalation ?? baseTier ?? "المعتمد المختص");
        var added = hits.Select(h => h.Action).Distinct().Sum(a => Actions.GetValueOrDefault(a).AddedDays);
        return new SimulationOutcome(baseTier, approvals, hits, added);
    }
}
