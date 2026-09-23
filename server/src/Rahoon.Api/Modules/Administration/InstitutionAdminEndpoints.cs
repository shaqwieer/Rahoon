using System.Text;
using Microsoft.EntityFrameworkCore;
using Rahoon.Api.Infrastructure.Http;
using Rahoon.Api.Infrastructure.Persistence;
using Rahoon.Api.Infrastructure.Tenancy;
using Rahoon.Api.Infrastructure.Time;
using Rahoon.Api.Modules.Audit;
using Rahoon.Api.Modules.Cases;
using Rahoon.Api.Modules.Complaints;
using Rahoon.Api.Modules.Identity;

namespace Rahoon.Api.Modules.Administration;

public sealed record SlaRuleInput(string Status, int BusinessDays, string? RuleNote, bool PausesOnOpenComplaint);
public sealed record SlaRulesRequest(List<SlaRuleInput>? Rules);

/// <summary>A06 stage SLAs (business days) and A07 operational reports with audited CSV exports.</summary>
public static class InstitutionAdminEndpoints
{
    public sealed record ReportDef(string Key, string Title, string Cadence, string Format, string? RestrictedToRole);

    public static readonly ReportDef[] Reports =
    [
        new("cases_by_status", "الحالات حسب الحالة والمرحلة", "أسبوعي", "CSV", null),
        new("sla_compliance", "الالتزام بمستوى الخدمة", "شهري", "CSV", null),
        new("complaints_outcomes", "الشكاوى ونتائجها", "شهري", "CSV", null),
        new("sensitive_user_activity", "نشاط المستخدمين الحساس", "عند الطلب", "CSV", SystemRoles.Auditor),
    ];

    private static readonly string[] SensitiveEventTypes =
    [
        "pii.reveal", "document.downloaded", "assignment.document_downloaded", "user.suspended", "user.reactivated", "user.role_changed",
        "user.role_change_requested", "user.invited", "auth.login_locked", "auth.login_failed", "limits.approved", "limits.proposed",
        "template.published", "report.exported", "temp_access.requested", "temp_access.approved", "temp_access.viewed", "temp_access.expired",
        "org.settings_updated", "config.sla_updated",
    ];

    public static void Map(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/settings").RequireOrg(OrganizationKind.Lender);
        g.MapGet("/sla-rules", GetSla).RequireAnyPermission(P.OrgSettings, P.ReportsView);
        g.MapPut("/sla-rules", PutSla).RequirePermission(P.OrgSettings).Idempotent();
        g.MapGet("/reports", ListReports).RequirePermission(P.ReportsView);
        g.MapPost("/reports/{key}/export", Export).RequirePermission(P.ReportsView);
    }

    private static async Task<IResult> GetSla(RahoonDbContext db, RequestContext rc, PlatformMinima minima)
    {
        var rules = await db.SlaRules.AsNoTracking().Where(r => r.OrganizationId == rc.OrganizationId).ToListAsync();
        return Results.Ok(new
        {
            unit = "business_days", ownerResponseMinDays = await minima.OwnerResponseMinDaysAsync(),
            rules = rules.Select(r => (Rule: r, Status: Enum.Parse<CaseStatus>(r.Status))).OrderBy(x => x.Status).Select(x => new
            {
                status = CaseStatusInfo.Key(x.Status), stage = CaseStatusInfo.Of(x.Status).LabelAr, x.Rule.BusinessDays, rule = x.Rule.RuleNote, x.Rule.PausesOnOpenComplaint,
            }),
        });
    }

    private static async Task<IResult> PutSla(SlaRulesRequest req, RahoonDbContext db, RequestContext rc, PlatformMinima minima, AuditLog audit)
    {
        var rules = req.Rules ?? [];
        var minOwner = await minima.OwnerResponseMinDaysAsync();
        var parsed = rules.Select(r => (Input: r, Status: CaseStatusInfo.Parse(r.Status) ?? (Enum.TryParse<CaseStatus>(r.Status, true, out var s) ? s : null))).ToList();
        new Validator()
            .Require(rules.Count > 0, "rules", "حدد مهل المراحل.")
            .Require(parsed.All(p => p.Status is { } s && !CaseStatusInfo.IsTerminal(s) && s is not (CaseStatus.Draft or CaseStatus.Paused)), "rules", "مرحلة غير صالحة.")
            .Require(parsed.Select(p => p.Status).Distinct().Count() == parsed.Count, "rules", "مرحلة مكررة.")
            .Require(rules.All(r => r.BusinessDays is >= 1 and <= 60), "rules", "المهلة بين يوم و60 يوم عمل.")
            .Require(rules.All(r => (r.RuleNote?.Length ?? 0) <= 200), "rules", "نص القاعدة حتى 200 حرف.")
            .Require(parsed.Where(p => p.Status is CaseStatus.AwaitingCustomer or CaseStatus.Negotiation).All(p => p.Input.BusinessDays >= minOwner),
                "rules", $"مهلة رد المالك لا تقل عن {minOwner} أيام (حد المنصة).")
            .ThrowIfInvalid();

        await using var tx = await db.Database.BeginTransactionAsync();
        var existing = await db.SlaRules.Where(r => r.OrganizationId == rc.OrganizationId).ToListAsync();
        var changes = new List<string>();
        foreach (var (input, status) in parsed)
        {
            var key = status!.Value.ToString();
            var rule = existing.FirstOrDefault(r => r.Status == key);
            if (rule is null)
            {
                rule = new SlaRule { OrganizationId = rc.OrganizationId!.Value, Status = key, RuleNote = "" };
                db.SlaRules.Add(rule);
                changes.Add($"{CaseStatusInfo.Of(status.Value).LabelAr}: جديد {input.BusinessDays}");
            }
            else if (rule.BusinessDays != input.BusinessDays)
                changes.Add($"{CaseStatusInfo.Of(status.Value).LabelAr}: {rule.BusinessDays}→{input.BusinessDays}");
            rule.BusinessDays = input.BusinessDays;
            rule.RuleNote = input.RuleNote?.Trim() ?? "";
            rule.PausesOnOpenComplaint = input.PausesOnOpenComplaint;
        }
        foreach (var removed in existing.Where(r => parsed.All(p => p.Status!.Value.ToString() != r.Status)))
        {
            db.SlaRules.Remove(removed);
            changes.Add($"حذف {removed.Status}");
        }
        await audit.RecordAsync(new AuditEntry("config.sla_updated", "تعديل مهل المراحل", Detail: changes.Count == 0 ? "دون تغيير في المهل" : string.Join(" · ", changes)));
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Results.Ok(new { saved = true, changes });
    }

    private static bool CanRun(ReportDef r, RequestContext rc) => r.RestrictedToRole is null || rc.RoleKeys.Contains(r.RestrictedToRole);

    private static IResult ListReports(RequestContext rc) => Results.Ok(Reports.Select(r => new
    {
        r.Key, r.Title, meta = r.RestrictedToRole is null ? $"{r.Cadence} · {r.Format}" : $"{r.Cadence} · للمدقق", r.Format,
        available = CanRun(r, rc), reason = CanRun(r, rc) ? null : "متاح لدور المدقق فقط.",
    }));

    private static string Csv(IEnumerable<IEnumerable<object?>> rows)
    {
        var sb = new StringBuilder("﻿"); // BOM so spreadsheet tools read Arabic correctly
        foreach (var row in rows)
            sb.AppendLine(string.Join(",", row.Select(c =>
            {
                var s = c switch { null => "", DateTimeOffset d => d.ToOffset(TimeSpan.FromHours(3)).ToString("yyyy-MM-dd HH:mm"), decimal m => m.ToString("0.##"), _ => c.ToString() ?? "" };
                // Neutralise spreadsheet formula injection and quote every cell.
                if (s.Length > 0 && "=+-@".Contains(s[0])) s = "'" + s;
                return "\"" + s.Replace("\"", "\"\"") + "\"";
            })));
        return sb.ToString();
    }

    private static async Task<IResult> Export(string key, RahoonDbContext db, RequestContext rc, IClock clock, AuditLog audit)
    {
        var def = Reports.FirstOrDefault(r => r.Key == key) ?? throw new NotFoundException();
        if (!CanRun(def, rc)) throw new ForbiddenException("هذا التقرير متاح لدور المدقق فقط.");
        var org = rc.OrganizationId!.Value;
        var today = clock.TodayRiyadh;
        List<List<object?>> rows = [];
        switch (key)
        {
            case "cases_by_status":
            {
                var data = await db.Cases.AsNoTracking().Where(c => c.OrganizationId == org)
                    .GroupBy(c => c.Status).Select(g => new
                    {
                        g.Key, N = g.Count(),
                        Overdue = g.Count(c => c.StageDueOn != null && c.StageDueOn < today && c.SlaPausedAt == null),
                    }).ToListAsync();
                rows.Add(["الحالة", "المرحلة", "عدد الحالات", "متأخرة عن المهلة"]);
                foreach (var d in data.OrderBy(d => d.Key))
                {
                    var meta = CaseStatusInfo.Of(d.Key);
                    rows.Add([meta.LabelAr, meta.Stage >= 0 ? CaseStatusInfo.StageNames[meta.Stage] : "—", d.N, CaseStatusInfo.IsTerminal(d.Key) ? 0 : d.Overdue]);
                }
                break;
            }
            case "sla_compliance":
            {
                var sla = await db.SlaRules.AsNoTracking().Where(r => r.OrganizationId == org).ToListAsync();
                var data = await db.Cases.AsNoTracking().Where(c => c.OrganizationId == org)
                    .GroupBy(c => c.Status).Select(g => new
                    {
                        g.Key, N = g.Count(), Paused = g.Count(c => c.SlaPausedAt != null),
                        Overdue = g.Count(c => c.StageDueOn != null && c.StageDueOn < today && c.SlaPausedAt == null),
                    }).ToListAsync();
                rows.Add(["المرحلة", "المهلة (أيام عمل)", "حالات نشطة", "ضمن المهلة", "متأخرة", "موقوفة المهلة", "نسبة الالتزام"]);
                foreach (var rule in sla.Select(r => (Rule: r, Status: Enum.Parse<CaseStatus>(r.Status))).OrderBy(x => x.Status))
                {
                    var d = data.FirstOrDefault(x => x.Key == rule.Status);
                    var n = d?.N ?? 0;
                    var within = n - (d?.Overdue ?? 0) - (d?.Paused ?? 0);
                    rows.Add([CaseStatusInfo.Of(rule.Status).LabelAr, rule.Rule.BusinessDays, n, within, d?.Overdue ?? 0, d?.Paused ?? 0,
                        n - (d?.Paused ?? 0) == 0 ? "—" : $"{Math.Round(100m * within / (n - (d?.Paused ?? 0)), 1)}%"]);
                }
                break;
            }
            case "complaints_outcomes":
            {
                var list = await db.Complaints.AsNoTracking().Where(c => c.OrganizationId == org)
                    .Select(c => new { c.Status, c.Decision, c.Type, c.SubmittedAt, c.RespondedAt }).ToListAsync();
                rows.Add(["النوع", "الحالة", "القرار", "العدد", "متوسط أيام المعالجة"]);
                foreach (var g in list.GroupBy(c => (c.Type, c.Status, c.Decision)).OrderBy(g => g.Key.Status))
                {
                    var done = g.Where(c => c.RespondedAt != null).Select(c => (c.RespondedAt!.Value - c.SubmittedAt).TotalDays).ToList();
                    rows.Add([g.Key.Type == ComplaintType.Objection ? "اعتراض" : "شكوى", g.Key.Status.ToString(), g.Key.Decision?.ToString() ?? "—", g.Count(),
                        done.Count == 0 ? "—" : Math.Round(done.Average(), 1).ToString("0.0")]);
                }
                break;
            }
            case "sensitive_user_activity":
            {
                var since = clock.UtcNow.AddDays(-90);
                var events = await db.AuditEvents.AsNoTracking()
                    .Where(e => e.OrganizationId == org && e.OccurredAt >= since && SensitiveEventTypes.Contains(e.Type))
                    .OrderByDescending(e => e.Seq).Take(5000)
                    .Select(e => new { e.OccurredAt, e.ActorLabel, e.ActorRole, e.Type, e.Title, e.CaseReference, e.Reason, e.Blocked }).ToListAsync();
                rows.Add(["الوقت", "الفاعل", "الدور", "الحدث", "الوصف", "الحالة", "السبب", "محجوب"]);
                rows.AddRange(events.Select(e => new List<object?> { e.OccurredAt, e.ActorLabel, e.ActorRole, e.Type, e.Title, e.CaseReference, e.Reason, e.Blocked ? "نعم" : "لا" }));
                break;
            }
        }
        var csv = Csv(rows);
        var bytes = Encoding.UTF8.GetBytes(csv);
        var sha = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(bytes));
        await using var tx = await db.Database.BeginTransactionAsync();
        await audit.RecordAsync(new AuditEntry("report.exported", $"تصدير تقرير: {def.Title}", Detail: $"{rows.Count - 1} صف · sha256:{sha[..16]}…"));
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Results.File(bytes, "text/csv; charset=utf-8", $"{key}-{today:yyyyMMdd}.csv");
    }
}
