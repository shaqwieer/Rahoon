namespace Rahoon.Api.Modules.Cases;

public sealed record StatusMeta(string Key, string LabelAr, string LabelEn, string Icon, string Tone, int Stage);

/// <summary>Display metadata for the 16 case states (C02) and the 7-stage progress (C03).</summary>
public static class CaseStatusInfo
{
    public static readonly string[] StageNames =
        ["الاستلام", "التحقق", "التقييم", "إعداد الحل", "الموافقة الداخلية", "رد المالك", "التنفيذ والإغلاق"];

    private static readonly Dictionary<CaseStatus, StatusMeta> Map = new()
    {
        [CaseStatus.Draft] = new("draft", "مسودة", "Draft", "edit_note", "neutral", 0),
        [CaseStatus.AwaitingData] = new("awaiting_data", "بانتظار البيانات", "Awaiting data", "hourglass_top", "info", 0),
        [CaseStatus.Verification] = new("verification", "تحقق", "Verification", "fact_check", "info", 1),
        [CaseStatus.Valuation] = new("valuation", "تقييم", "Valuation", "query_stats", "info", 2),
        [CaseStatus.ProposedSolution] = new("proposed_solution", "حل مقترح", "Proposed solution", "tips_and_updates", "sel", 3),
        [CaseStatus.InternalApproval] = new("internal_approval", "موافقة داخلية", "Internal approval", "approval", "warn", 4),
        [CaseStatus.AwaitingCustomer] = new("awaiting_customer", "بانتظار العميل", "Awaiting customer", "hourglass_empty", "info", 5),
        [CaseStatus.Negotiation] = new("negotiation", "تفاوض", "Negotiation", "forum", "sel", 5),
        [CaseStatus.ActiveSettlement] = new("active_settlement", "تسوية معتمدة / نشطة", "Active settlement", "handshake", "ok", 6),
        [CaseStatus.VoluntarySale] = new("voluntary_sale", "بيع طوعي", "Voluntary sale", "sell", "ok", 6),
        [CaseStatus.JudicialReferral] = new("judicial_referral", "إحالة قضائية", "Judicial referral", "outbound", "ext", 6),
        [CaseStatus.ExternalJudicialSale] = new("external_judicial_sale", "بيع قضائي خارجي", "External judicial sale", "open_in_new", "ext", 6),
        [CaseStatus.AwaitingReconciliation] = new("awaiting_reconciliation", "بانتظار التسوية المالية", "Awaiting reconciliation", "calculate", "warn", 6),
        [CaseStatus.Closed] = new("closed", "مغلقة", "Closed", "check_circle", "dark", 6),
        [CaseStatus.Paused] = new("paused", "موقوفة", "Paused", "pause_circle", "neutral", -1),
        [CaseStatus.Cancelled] = new("cancelled", "ملغاة", "Cancelled", "cancel", "neutral", -1),
    };

    public static StatusMeta Of(CaseStatus s) => Map[s];

    public static string Key(CaseStatus s) => Map[s].Key;

    public static CaseStatus? Parse(string key) => Map.FirstOrDefault(kv => kv.Value.Key == key).Value is { } m
        ? Map.First(kv => kv.Value.Key == key).Key : null;

    public static int StageOf(Case c) =>
        c.Status is CaseStatus.Paused or CaseStatus.Cancelled
            ? (c.StatusBeforePause is { } prev ? Map[prev].Stage : 0)
            : Map[c.Status].Stage;

    public static bool IsTerminal(CaseStatus s) => s is CaseStatus.Closed or CaseStatus.Cancelled;

    /// <summary>States in which a solution path decision (settlement / sale / referral) can be taken.</summary>
    public static readonly CaseStatus[] SolutionStates =
        [CaseStatus.ProposedSolution, CaseStatus.InternalApproval, CaseStatus.AwaitingCustomer, CaseStatus.Negotiation, CaseStatus.ActiveSettlement];
}
