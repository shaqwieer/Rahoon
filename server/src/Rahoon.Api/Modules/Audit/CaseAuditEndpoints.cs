using System.Linq.Expressions;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Rahoon.Api.Infrastructure.Http;
using Rahoon.Api.Infrastructure.Persistence;
using Rahoon.Api.Infrastructure.Tenancy;
using Rahoon.Api.Infrastructure.Time;
using Rahoon.Api.Modules.Cases;
using Rahoon.Api.Modules.Identity;

namespace Rahoon.Api.Modules.Audit;

/// <summary>
/// L24 case audit log: filtered, paged history including blocked attempts (Gregorian + Hijri),
/// hash-chain verification for the case's organization, and a signed CSV export (trailing SHA-256 line).
/// </summary>
public static class CaseAuditEndpoints
{
    public const string ExportHashPrefix = "# sha256:";

    /// <summary>Filter categories → event-type prefixes.</summary>
    public static readonly IReadOnlyDictionary<string, (string Label, string[] Prefixes)> Categories = new Dictionary<string, (string, string[])>
    {
        ["transitions"] = ("انتقالات", ["case.transition", "case.draft_created"]),
        ["approvals"] = ("موافقات", ["approval.", "solution.", "cancellation.", "agreement."]),
        ["reveal"] = ("كشف بيانات", ["pii."]),
        ["documents"] = ("مستندات", ["document."]),
        ["owner"] = ("المالك والموافقات", ["owner.", "consent.", "offer."]),
        ["messages"] = ("تواصل", ["message.", "comms."]),
        ["valuation"] = ("التقييم والتحليل", ["valuation.", "assignment.", "analysis."]),
        ["payments"] = ("مدفوعات", ["payment.", "breach."]),
        ["complaints"] = ("شكاوى", ["complaint."]),
        ["data"] = ("تعديل بيانات", ["party.", "property.", "mortgage.", "finance.", "import."]),
        ["audit"] = ("السجل", ["audit."]),
    };

    public static void Map(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/cases/{reference}/audit").RequirePermission(P.CaseView, P.AuditView);
        g.MapGet("", List);
        g.MapGet("/verify", Verify);
        g.MapGet("/export", Export);
    }

    private static (string Icon, string Tone) Visual(string type, bool blocked) => type switch
    {
        _ when blocked => ("block", "err"),
        "case.transition" or "case.draft_created" => ("swap_horiz", "info"),
        "pii.reveal" => ("visibility", "warn"),
        _ when type.StartsWith("approval.") || type.StartsWith("cancellation.decision") || type == "valuation.accepted" => ("approval", "ok"),
        "owner.invitation_accepted" or "consent.recorded" => ("task_alt", "ok"),
        _ when type.StartsWith("consent.") || type.StartsWith("offer.") => ("task_alt", "ok"),
        _ when type.StartsWith("document.") => ("description", "neutral"),
        _ when type.StartsWith("message.") => ("forum", "neutral"),
        _ when type.StartsWith("payment.") => ("payments", "neutral"),
        _ when type.StartsWith("complaint.") => ("report", "warn"),
        _ when type.StartsWith("audit.") => ("download", "neutral"),
        _ => ("history", "neutral"),
    };

    private static string? CategoryOf(string type) =>
        Categories.FirstOrDefault(kv => kv.Value.Prefixes.Any(p => type.StartsWith(p, StringComparison.Ordinal))).Key;

    private sealed record Filter(string? Type, string? Actor, bool? Blocked, DateOnly? From, DateOnly? To);

    private static IQueryable<AuditEvent> Apply(IQueryable<AuditEvent> q, Filter f)
    {
        if (!string.IsNullOrWhiteSpace(f.Type))
        {
            var prefixes = f.Type.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .SelectMany(t => Categories.TryGetValue(t, out var cat) ? cat.Prefixes : [t]).Distinct().ToList();
            if (prefixes.Count > 0)
            {
                // OR of StartsWith, translated to LIKE per prefix.
                var e = Expression.Parameter(typeof(AuditEvent), "e");
                var typeProp = Expression.Property(e, nameof(AuditEvent.Type));
                var startsWith = typeof(string).GetMethod(nameof(string.StartsWith), [typeof(string)])!;
                Expression body = prefixes.Select(p => (Expression)Expression.Call(typeProp, startsWith, Expression.Constant(p)))
                    .Aggregate(Expression.OrElse);
                q = q.Where(Expression.Lambda<Func<AuditEvent, bool>>(body, e));
            }
        }
        if (!string.IsNullOrWhiteSpace(f.Actor))
        {
            if (Guid.TryParse(f.Actor, out var uid)) q = q.Where(x => x.ActorUserId == uid);
            else { var type = f.Actor.Trim().ToLowerInvariant(); q = q.Where(x => x.ActorType == type); }
        }
        if (f.Blocked is { } b) q = q.Where(x => x.Blocked == b);
        var riyadh = TimeSpan.FromHours(3);
        if (f.From is { } from) { var start = new DateTimeOffset(from.ToDateTime(TimeOnly.MinValue), riyadh).ToUniversalTime(); q = q.Where(x => x.OccurredAt >= start); }
        if (f.To is { } to) { var end = new DateTimeOffset(to.AddDays(1).ToDateTime(TimeOnly.MinValue), riyadh).ToUniversalTime(); q = q.Where(x => x.OccurredAt < end); }
        return q;
    }

    private static object Row(AuditEvent e)
    {
        var local = e.OccurredAt.ToOffset(TimeSpan.FromHours(3));
        var (icon, tone) = Visual(e.Type, e.Blocked);
        return new
        {
            e.Seq, e.Type, category = CategoryOf(e.Type), icon, tone, e.Title,
            occurredAt = e.OccurredAt, time = local.ToString("yyyy-MM-dd HH:mm"), hijri = Hijri.Format(DateOnly.FromDateTime(local.DateTime)),
            actor = new { type = e.ActorType, id = e.ActorUserId, label = e.ActorLabel, role = e.ActorRole, display = e.ActorRole is null ? e.ActorLabel : $"{e.ActorLabel} · {e.ActorRole}" },
            e.FromState, e.ToState, e.Reason, e.Detail, e.Blocked, e.Evidence, ipMasked = e.IpMasked, e.Hash,
        };
    }

    private static async Task<IResult> List(string reference, string? type, string? actor, bool? blocked, DateOnly? from, DateOnly? to, int? page, int? pageSize,
        CaseAccess access, RahoonDbContext db, RequestContext rc)
    {
        var c = await access.GetAsync(reference, track: false);
        var size = Math.Clamp(pageSize ?? 50, 1, 200);
        var p = Math.Max(1, page ?? 1);
        var q = Apply(db.AuditEvents.AsNoTracking().Where(e => e.CaseId == c.Id), new Filter(type, actor, blocked, from, to));
        var total = await q.CountAsync();
        var rows = await q.OrderByDescending(e => e.Seq).Skip((p - 1) * size).Take(size).ToListAsync();
        var actors = await db.AuditEvents.AsNoTracking().Where(e => e.CaseId == c.Id)
            .GroupBy(e => new { e.ActorType, e.ActorUserId, e.ActorLabel })
            .Select(g => new { g.Key.ActorType, g.Key.ActorUserId, g.Key.ActorLabel, Count = g.Count() }).ToListAsync();
        return Results.Ok(new
        {
            items = rows.Select(Row), page = p, pageSize = size, total,
            filters = new
            {
                categories = Categories.Select(kv => new { key = kv.Key, label = kv.Value.Label }),
                actors = actors.OrderByDescending(a => a.Count).Select(a => new
                {
                    key = a.ActorUserId?.ToString() ?? a.ActorType, label = a.ActorLabel ?? (a.ActorType == "system" ? "النظام" : a.ActorType), type = a.ActorType,
                }).DistinctBy(a => a.key),
                applied = new { type, actor, blocked, from, to },
            },
            canExport = rc.Has(P.AuditView),
            appendOnlyNote = "السجل للإضافة فقط؛ تُسجَّل المحاولات المحجوبة مع مانعها.",
        });
    }

    private static async Task<IResult> Verify(string reference, CaseAccess access, RahoonDbContext db, IClock clock)
    {
        var c = await access.GetAsync(reference, track: false);
        var broken = await AuditLog.VerifyChainAsync(db, c.OrganizationId);
        var lastOrg = await db.AuditEvents.AsNoTracking().Where(e => e.OrganizationId == c.OrganizationId).OrderByDescending(e => e.Seq)
            .Select(e => new { e.Seq, e.OccurredAt }).FirstOrDefaultAsync();
        var lastCase = await db.AuditEvents.AsNoTracking().Where(e => e.CaseId == c.Id).OrderByDescending(e => e.Seq)
            .Select(e => new { e.Seq, e.OccurredAt }).FirstOrDefaultAsync();
        var caseTime = lastCase is null ? null : lastCase.OccurredAt.ToOffset(TimeSpan.FromHours(3)).ToString("yyyy-MM-dd HH:mm");
        return Results.Ok(new
        {
            ok = broken is null, firstBrokenSeq = broken, chainLastSeq = lastOrg?.Seq,
            caseLastSeq = lastCase?.Seq, caseLastAt = lastCase?.OccurredAt, checkedAt = clock.UtcNow,
            text = broken is null
                ? (lastCase is null ? "السجل للإضافة فقط. لا أحداث لهذه الحالة بعد." : $"السجل للإضافة فقط. سلسلة البصمة سليمة حتى {caseTime} · الحدث #{lastCase.Seq}")
                : $"تحذير: انقطاع في سلسلة البصمة عند الحدث #{broken}. لا يُعتمد السجل بعد هذه النقطة حتى تحقيق الامتثال.",
            tone = broken is null ? "ok" : "err",
        });
    }

    private static string Csv(string? value)
    {
        var v = value ?? "";
        // Neutralize spreadsheet formula injection.
        if (v.Length > 0 && v[0] is '=' or '+' or '-' or '@' or '\t' or '\r') v = "'" + v;
        return v.IndexOfAny([',', '"', '\n', '\r']) >= 0 ? "\"" + v.Replace("\"", "\"\"") + "\"" : v;
    }

    /// <summary>
    /// CSV (UTF-8 with BOM) of the filtered case events, oldest first, followed by one line
    /// «# sha256:&lt;hex&gt;» = SHA-256 over every byte before that line. The export itself is audited.
    /// </summary>
    private static async Task<IResult> Export(string reference, string? type, string? actor, bool? blocked, DateOnly? from, DateOnly? to,
        CaseAccess access, RahoonDbContext db, IClock clock, AuditLog audit)
    {
        var c = await access.GetAsync(reference, track: false);
        var rows = await Apply(db.AuditEvents.AsNoTracking().Where(e => e.CaseId == c.Id), new Filter(type, actor, blocked, from, to))
            .OrderBy(e => e.Seq).Take(50_000).ToListAsync();
        var sb = new StringBuilder();
        sb.Append("seq,occurred_at_utc,time_riyadh,hijri,type,event,actor,actor_role,from_state,to_state,reason,detail,blocked,evidence,prev_hash,hash\n");
        foreach (var e in rows)
        {
            var local = e.OccurredAt.ToOffset(TimeSpan.FromHours(3));
            sb.AppendJoin(',', [
                e.Seq.ToString(), e.OccurredAt.UtcDateTime.ToString("O"), local.ToString("yyyy-MM-dd HH:mm"), Csv(Hijri.Format(DateOnly.FromDateTime(local.DateTime))),
                Csv(e.Type), Csv(e.Title), Csv(e.ActorLabel), Csv(e.ActorRole), Csv(e.FromState), Csv(e.ToState), Csv(e.Reason), Csv(e.Detail),
                e.Blocked ? "true" : "false", Csv(string.Join(" | ", e.Evidence)), e.PrevHash, e.Hash,
            ]);
            sb.Append('\n');
        }
        var content = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
        var hash = Convert.ToHexStringLower(SHA256.HashData(content));
        var file = content.Concat(Encoding.UTF8.GetBytes($"{ExportHashPrefix}{hash}\n")).ToArray();

        await audit.RecordAsync(new AuditEntry("audit.exported", "تصدير سجل التدقيق (CSV موقّع)", c.Id, c.Reference,
            Detail: $"{rows.Count} حدثاً · sha256:{hash}" + (string.IsNullOrWhiteSpace(type) ? "" : $" · النوع: {type}"), OrganizationId: c.OrganizationId));
        await db.SaveChangesAsync();
        return Results.File(file, "text/csv; charset=utf-8", $"audit-{c.Reference}-{clock.UtcNow.ToOffset(TimeSpan.FromHours(3)):yyyyMMdd-HHmm}.csv");
    }
}
