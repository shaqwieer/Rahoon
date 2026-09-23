using System.Text;
using Microsoft.EntityFrameworkCore;
using Rahoon.Api.Infrastructure.Http;
using Rahoon.Api.Infrastructure.Persistence;
using Rahoon.Api.Infrastructure.Tenancy;
using Rahoon.Api.Infrastructure.Time;
using Rahoon.Api.Modules.Audit;
using Rahoon.Api.Modules.Cases;
using Rahoon.Api.Modules.Identity;
using Rahoon.Api.Modules.Sale;
using Rahoon.Api.Modules.Solutions;

namespace Rahoon.Api.Modules.Ecosystem;

public sealed record ReportQuery(string Metric, string Breakdown, string From, string To);
public sealed record ReportScheduleBody(string Metric, string Breakdown, string From, string To, List<Guid>? RecipientUserIds);

/// <summary>One aggregated cell. Cells built from fewer than <see cref="ReportAggregator.MinCell"/> cases carry neither value nor count.</summary>
public sealed record ReportCell(string Row, string Column, decimal? Value, int? N, bool Suppressed);

public sealed record ReportFact(string Row, string Column, decimal Value);

/// <summary>
/// Aggregation with k-anonymity (PA15): aggregates only; any cell with n &lt; 10 is suppressed on the server (value
/// and count both withheld, rendered «—»). No row/column totals are emitted, so a suppressed cell cannot be
/// back-computed. The same function feeds the JSON query and the CSV export.
/// </summary>
public static class ReportAggregator
{
    public const int MinCell = 10;

    public static List<ReportCell> Aggregate(IEnumerable<ReportFact> facts, string measure, IReadOnlyList<string> rows, IReadOnlyList<string> columns)
    {
        var grouped = facts.GroupBy(f => (f.Row, f.Column)).ToDictionary(g => g.Key, g => g.ToList());
        var cells = new List<ReportCell>();
        foreach (var r in rows)
            foreach (var c in columns)
            {
                var list = grouped.GetValueOrDefault((r, c)) ?? [];
                var n = list.Count;
                if (n < MinCell) { cells.Add(new ReportCell(r, c, null, null, true)); continue; }
                decimal value = measure switch
                {
                    "count" => n,
                    "rate" => Math.Round(list.Average(f => f.Value), 4),
                    _ => Math.Round(list.Average(f => f.Value), 1),
                };
                cells.Add(new ReportCell(r, c, value, n, false));
            }
        return cells;
    }

    public static string Csv(string rowHeader, IReadOnlyList<string> rows, IReadOnlyList<string> columns, IReadOnlyList<ReportCell> cells)
    {
        static string Esc(string s) => s.Contains(',') || s.Contains('"') ? $"\"{s.Replace("\"", "\"\"")}\"" : s;
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(',', new[] { rowHeader }.Concat(columns).Select(Esc)));
        foreach (var r in rows)
            sb.AppendLine(string.Join(',', new[] { Esc(r) }.Concat(columns.Select(c =>
            {
                var cell = cells.First(x => x.Row == r && x.Column == c);
                return cell.Suppressed ? "—" : cell.Value!.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
            }))));
        sb.AppendLine($"# «—» = أقل من {MinCell} حالات (مخفي لحماية الخصوصية)");
        return sb.ToString();
    }

    public static string Quarter(DateOnly d) => $"Q{(d.Month - 1) / 3 + 1}-{d.Year}";

    public static List<string> Quarters(DateOnly from, DateOnly to)
    {
        var list = new List<string>();
        var q = new DateOnly(from.Year, (from.Month - 1) / 3 * 3 + 1, 1);
        while (q <= to && list.Count < 40) { list.Add(Quarter(q)); q = q.AddMonths(3); }
        return list;
    }
}

/// <summary>Advanced reporting (PA15): measure × breakdown × period over the institution's own cases, CSV export audited.</summary>
public static class ReportingEndpoints
{
    public static readonly IReadOnlyDictionary<string, (string Label, string Measure)> Metrics = new Dictionary<string, (string, string)>
    {
        ["avg_resolution_days"] = ("متوسط أيام الحل", "avg"),
        ["case_count"] = ("عدد الحالات", "count"),
        ["amicable_rate"] = ("نسبة الحل الودي", "rate"),
    };

    public static readonly IReadOnlyDictionary<string, string> Breakdowns = new Dictionary<string, string>
    {
        ["solution_kind_quarter"] = "نوع الحل × الربع",
        ["region_quarter"] = "المنطقة × الربع",
        ["status"] = "الحالة",
        ["quarter"] = "الربع",
    };

    private static readonly CaseStatus[] Resolved = [CaseStatus.ActiveSettlement, CaseStatus.VoluntarySale, CaseStatus.AwaitingReconciliation, CaseStatus.Closed, CaseStatus.JudicialReferral, CaseStatus.ExternalJudicialSale];

    public static void Map(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/reports").RequireOrg(OrganizationKind.Lender).RequirePermission(P.ReportsView);
        g.MapGet("/definitions", Definitions);
        g.MapPost("/query", Query);
        g.MapPost("/export", Export);
        g.MapGet("/kpis", Kpis);
        g.MapGet("/schedules", Schedules);
        g.MapPost("/schedules", Schedule).Idempotent();
    }

    private static IResult Definitions() => Results.Ok(new
    {
        metrics = Metrics.Select(m => new { key = m.Key, label = m.Value.Label }),
        breakdowns = Breakdowns.Select(b => new { key = b.Key, label = b.Value }),
        privacyNote = $"بيانات مجمعة فقط. الخلايا الأقل من {ReportAggregator.MinCell} حالات تُخفى لحماية الخصوصية.",
    });

    private sealed record CaseFact(CaseStatus Status, string? Region, DateOnly Opened, DateOnly? Resolved, string? Kind, bool Amicable);

    private static async Task<List<CaseFact>> FactsAsync(RahoonDbContext db, Guid orgId)
    {
        var cases = await db.Cases.AsNoTracking().Where(c => c.OrganizationId == orgId && c.Status != CaseStatus.Draft)
            .Select(c => new { c.Id, c.Status, c.Region, c.OpenedOn, c.CreatedAt, c.StatusChangedAt }).ToListAsync();
        var resolvedKeys = Resolved.Select(CaseStatusInfo.Key).ToList();
        var firstResolution = await db.AuditEvents.AsNoTracking().Where(e => e.OrganizationId == orgId && e.Type == "case.transition" && e.CaseId != null && resolvedKeys.Contains(e.ToState!))
            .GroupBy(e => e.CaseId).Select(g => new { CaseId = g.Key!.Value, At = g.Min(e => e.OccurredAt) }).ToDictionaryAsync(x => x.CaseId, x => x.At);
        var kinds = await db.Solutions.AsNoTracking().Where(s => s.OrganizationId == orgId && (s.Status == SolutionStatus.Accepted || s.Status == SolutionStatus.Offered || s.Status == SolutionStatus.Approved))
            .GroupBy(s => s.CaseId).Select(g => new { CaseId = g.Key, Kind = g.OrderByDescending(s => s.VersionNo).Select(s => s.Kind).First() }).ToDictionaryAsync(x => x.CaseId, x => x.Kind);
        var sales = await db.Set<VoluntarySale>().AsNoTracking().Where(s => s.OrganizationId == orgId && (s.Status == SaleStatus.Completed || s.Status == SaleStatus.OfferApproved || s.Status == SaleStatus.Active || s.Status == SaleStatus.AwaitingConsent))
            .Select(s => s.CaseId).Distinct().ToListAsync();
        var saleSet = sales.ToHashSet();
        return cases.Select(c =>
        {
            var opened = c.OpenedOn ?? DateOnly.FromDateTime(c.CreatedAt.UtcDateTime);
            DateOnly? resolved = firstResolution.TryGetValue(c.Id, out var at) ? DateOnly.FromDateTime(at.ToOffset(TimeSpan.FromHours(3)).DateTime)
                : Resolved.Contains(c.Status) ? DateOnly.FromDateTime(c.StatusChangedAt.ToOffset(TimeSpan.FromHours(3)).DateTime) : null;
            string? kind = saleSet.Contains(c.Id) ? ApprovalEndpoints.KindLabel(SolutionKind.VoluntarySale)
                : kinds.TryGetValue(c.Id, out var k) ? ApprovalEndpoints.KindLabel(k)
                : c.Status is CaseStatus.JudicialReferral or CaseStatus.ExternalJudicialSale ? "إحالة قضائية" : null;
            var amicable = kind is not null && kind != "إحالة قضائية";
            return new CaseFact(c.Status, c.Region, opened, resolved, kind, amicable);
        }).ToList();
    }

    private static (DateOnly From, DateOnly To) Period(string from, string to)
    {
        var v = new Validator();
        var okF = DateOnly.TryParseExact((from ?? "") + "-01", "yyyy-MM-dd", out var f);
        var okT = DateOnly.TryParseExact((to ?? "") + "-01", "yyyy-MM-dd", out var t);
        v.Require(okF, "from", "الفترة بصيغة yyyy-MM.").Require(okT, "to", "الفترة بصيغة yyyy-MM.");
        v.Require(!okF || !okT || (t >= f && t <= f.AddMonths(36)), "to", "الفترة حتى 36 شهراً ونهايتها بعد بدايتها.");
        v.ThrowIfInvalid();
        // Snap to whole quarters so overlapping month ranges cannot be subtracted to isolate a suppressed cell.
        var qFrom = new DateOnly(f.Year, (f.Month - 1) / 3 * 3 + 1, 1);
        var qTo = new DateOnly(t.Year, (t.Month - 1) / 3 * 3 + 1, 1).AddMonths(3).AddDays(-1);
        return (qFrom, qTo);
    }

    private static readonly string[] Regions = ["الرياض", "مكة المكرمة", "الشرقية", "المدينة المنورة", "عسير", "أخرى"];

    internal static async Task<(string RowHeader, List<string> Rows, List<string> Columns, List<ReportCell> Cells, string MetricLabel)> RunAsync(ReportQuery q, RahoonDbContext db, Guid orgId)
    {
        new Validator().Require(Metrics.ContainsKey(q.Metric ?? ""), "metric", "اختر المقياس.").Require(Breakdowns.ContainsKey(q.Breakdown ?? ""), "breakdown", "اختر التقسيم.").ThrowIfInvalid();
        var (from, to) = Period(q.From, q.To);
        var (label, measure) = Metrics[q.Metric];
        var facts = await FactsAsync(db, orgId);
        // Resolution metrics are dated by resolution; counts by opening date.
        var dated = q.Metric == "case_count"
            ? facts.Select(f => (f, Date: (DateOnly?)f.Opened))
            : facts.Where(f => f.Resolved is not null && f.Kind is not null).Select(f => (f, Date: f.Resolved));
        var inPeriod = dated.Where(x => x.Date is { } d && d >= from && d <= to).ToList();
        decimal Value((CaseFact f, DateOnly? Date) x) => q.Metric switch
        {
            "avg_resolution_days" => x.f.Resolved!.Value.DayNumber - x.f.Opened.DayNumber,
            "amicable_rate" => x.f.Amicable ? 1 : 0,
            _ => 1,
        };
        var quarters = ReportAggregator.Quarters(from, to);
        List<ReportFact> rowsFacts;
        List<string> rows;
        List<string> columns;
        string header;
        switch (q.Breakdown)
        {
            case "solution_kind_quarter":
                header = "نوع الحل";
                rows = ["إعادة جدولة", "فترة سماح", "سداد مخفض", "بيع طوعي"];
                columns = quarters;
                rowsFacts = inPeriod.Where(x => x.f.Kind is not null).Select(x => new ReportFact(x.f.Kind!, ReportAggregator.Quarter(x.Date!.Value), Value(x))).ToList();
                break;
            case "region_quarter":
                header = "المنطقة";
                // Fixed row sets: a row's presence must not reveal that a case exists in it.
                rowsFacts = inPeriod.Select(x => new ReportFact(Regions.Contains(x.f.Region) ? x.f.Region! : "أخرى", ReportAggregator.Quarter(x.Date!.Value), Value(x))).ToList();
                rows = [.. Regions];
                columns = quarters;
                break;
            case "status":
                header = "الحالة";
                rowsFacts = inPeriod.Select(x => new ReportFact(CaseStatusInfo.Of(x.f.Status).LabelAr, "الكل", Value(x))).ToList();
                rows = Enum.GetValues<CaseStatus>().Where(s => s != CaseStatus.Draft).Select(s => CaseStatusInfo.Of(s).LabelAr).ToList();
                columns = ["الكل"];
                break;
            default:
                header = "الفترة";
                rowsFacts = inPeriod.Select(x => new ReportFact("الكل", ReportAggregator.Quarter(x.Date!.Value), Value(x))).ToList();
                rows = ["الكل"];
                columns = quarters;
                break;
        }
        return (header, rows, columns, ReportAggregator.Aggregate(rowsFacts, measure, rows, columns), label);
    }

    private static async Task<IResult> Query(ReportQuery q, RahoonDbContext db, RequestContext rc)
    {
        var (header, rows, columns, cells, label) = await RunAsync(q, db, rc.OrganizationId!.Value);
        return Results.Ok(new
        {
            metric = q.Metric, metricLabel = label, breakdown = q.Breakdown, breakdownLabel = Breakdowns[q.Breakdown], rowHeader = header, rows, columns,
            cells = cells.Select(c => new { c.Row, c.Column, c.Value, c.N, c.Suppressed, display = c.Suppressed ? "—" : null }),
            suppressedCount = cells.Count(c => c.Suppressed), minCell = ReportAggregator.MinCell,
            legend = $"«—» = أقل من {ReportAggregator.MinCell} حالات",
            privacyNote = $"بيانات مجمعة فقط. الخلايا الأقل من {ReportAggregator.MinCell} حالات تُخفى لحماية الخصوصية.",
        });
    }

    private static async Task<IResult> Export(ReportQuery q, RahoonDbContext db, RequestContext rc, AuditLog audit)
    {
        var (header, rows, columns, cells, label) = await RunAsync(q, db, rc.OrganizationId!.Value);
        var csv = ReportAggregator.Csv(header, rows, columns, cells);
        await audit.RecordAsync(new AuditEntry("report.exported", $"تصدير تقرير مخصص CSV: {label} · {Breakdowns[q.Breakdown]}",
            Detail: $"الفترة {q.From} → {q.To} · {cells.Count(c => c.Suppressed)} خلية مخفية (أقل من {ReportAggregator.MinCell})",
            Data: new { q.Metric, q.Breakdown, q.From, q.To, rows = rows.Count }, OrganizationId: rc.OrganizationId));
        await db.SaveChangesAsync();
        return Results.File(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv)).ToArray(), "text/csv; charset=utf-8", $"rahoon-report-{q.Metric}.csv");
    }

    private static async Task<IResult> Kpis(RahoonDbContext db, RequestContext rc, IClock clock)
    {
        var orgId = rc.OrganizationId!.Value;
        var facts = await FactsAsync(db, orgId);
        var since = clock.TodayRiyadh.AddMonths(-12);
        var closed = facts.Where(f => f.Status == CaseStatus.Closed && f.Resolved >= since).ToList();
        var resolved = facts.Where(f => f.Resolved >= since && f.Kind is not null).ToList();
        var complaints = await db.Complaints.CountAsync(c => c.OrganizationId == orgId);
        var total = facts.Count;
        object Tile(string key, string label, int n, Func<decimal> value, string sub) => n < ReportAggregator.MinCell
            ? new { key, label, value = (decimal?)null, suppressed = true, sub = $"أقل من {ReportAggregator.MinCell} حالات" }
            : new { key, label, value = (decimal?)value(), suppressed = false, sub };
        return Results.Ok(new
        {
            tiles = new[]
            {
                Tile("amicable_closure", "حالات أُغلقت بحل ودي", closed.Count, () => Math.Round((decimal)closed.Count(f => f.Amicable) / closed.Count, 4), "آخر 12 شهراً"),
                Tile("avg_resolution_days", "متوسط أيام الحل", resolved.Count, () => Math.Round((decimal)resolved.Average(f => f.Resolved!.Value.DayNumber - f.Opened.DayNumber), 0), "آخر 12 شهراً"),
                Tile("complaints_per_100", "شكاوى لكل 100 حالة", total, () => Math.Round(complaints * 100m / total, 1), "ضمن الهدف"),
            },
        });
    }

    private static async Task<IResult> Schedules(RahoonDbContext db, RequestContext rc) =>
        Results.Ok(await db.Set<ReportSchedule>().AsNoTracking().Where(s => s.OrganizationId == rc.OrganizationId).OrderByDescending(s => s.CreatedAt)
            .Select(s => new { s.Id, s.Metric, s.Breakdown, s.PeriodFrom, s.PeriodTo, s.Frequency, recipients = s.RecipientUserIds.Count }).ToListAsync());

    /// <summary>Monthly send to authorised users only: every recipient must hold reports.view in this institution.</summary>
    private static async Task<IResult> Schedule(ReportScheduleBody req, RahoonDbContext db, RequestContext rc, AuditLog audit)
    {
        new Validator().Require(Metrics.ContainsKey(req.Metric ?? ""), "metric", "اختر المقياس.").Require(Breakdowns.ContainsKey(req.Breakdown ?? ""), "breakdown", "اختر التقسيم.")
            .Require(req.RecipientUserIds is { Count: > 0 and <= 20 }, "recipientUserIds", "اختر مستلماً واحداً على الأقل.").ThrowIfInvalid();
        Period(req.From, req.To);
        var ids = req.RecipientUserIds!.Distinct().ToList();
        var authorised = await db.Memberships.Where(m => m.OrganizationId == rc.OrganizationId && m.Status == MembershipStatus.Active && ids.Contains(m.UserId)
                                                         && m.Roles.Any(r => r.Role!.Permissions.Any(p => p.PermissionKey == P.ReportsView)))
            .Select(m => m.UserId).ToListAsync();
        var refused = ids.Except(authorised).ToList();
        if (refused.Count > 0) throw new DomainException("recipient_not_authorised", "بعض المستلمين لا يملكون صلاحية التقارير في المنشأة.", 422);
        var s = new ReportSchedule { Metric = req.Metric, Breakdown = req.Breakdown, PeriodFrom = req.From, PeriodTo = req.To, RecipientUserIds = ids, CreatedByUserId = rc.UserId };
        db.Set<ReportSchedule>().Add(s);
        await audit.RecordAsync(new AuditEntry("report.scheduled", $"جدولة إرسال شهري: {Metrics[req.Metric].Label}", Detail: $"{ids.Count} مستلم مخوّل", OrganizationId: rc.OrganizationId));
        await db.SaveChangesAsync();
        return Results.Ok(new { s.Id, note = "يُسجَّل الإرسال الشهري؛ قناة البريد في وضع المحاكاة." });
    }
}
