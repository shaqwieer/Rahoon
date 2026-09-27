using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Rahoon.Api.Infrastructure.Http;
using Rahoon.Api.Infrastructure.Persistence;
using Rahoon.Api.Infrastructure.Tenancy;
using Rahoon.Api.Infrastructure.Time;
using Rahoon.Api.Modules.Audit;
using Rahoon.Api.Modules.Cases;
using Rahoon.Api.Modules.Communications;
using Rahoon.Api.Modules.Communications;
using Rahoon.Api.Modules.Identity;

namespace Rahoon.Api.Modules.Analytics;

public sealed record SettingChangeRequest(string Key, JsonElement Value, string Reason);
public sealed record SettingDecisionRequest(string Decision, string Reason);

/// <summary>
/// O03 configurable operations: versioned settings; every change is a proposal that becomes effective only when a
/// second person with operations.approve accepts it. «تجريبي» rules only suggest; nothing here executes automatically.
/// </summary>
public static class OperationsEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/settings/operations").RequireOrg(OrganizationKind.Lender);
        g.MapGet("", Get).RequireAnyPermission(P.OrgSettings, P.OperationsApprove);
        g.MapPost("/change-requests", Propose).RequirePermission(P.OrgSettings).Idempotent();
        g.MapPost("/change-requests/{id:guid}/decision", Decide).RequirePermission(P.OperationsApprove).Idempotent();
    }

    private static async Task<IResult> Get(RahoonDbContext db, RequestContext rc)
    {
        var org = rc.OrganizationId!.Value;
        var all = await db.Set<OperationalSetting>().AsNoTracking().Where(s => s.OrganizationId == org).OrderByDescending(s => s.VersionNo).ToListAsync();
        var userIds = all.SelectMany(s => new[] { s.ProposedByUserId, s.DecidedByUserId ?? Guid.Empty }).Distinct().ToList();
        var users = await db.Users.AsNoTracking().Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName);
        string? Name(Guid? id) => id is { } v && users.TryGetValue(v, out var n) ? n : null;

        // Current team load for the capacity view (open, non-terminal cases per manager's team).
        var load = await db.Cases.AsNoTracking().Where(c => c.OrganizationId == org && c.Status != CaseStatus.Closed && c.Status != CaseStatus.Cancelled && c.Status != CaseStatus.Draft && c.AssignedManagerId != null)
            .Join(db.Memberships, c => c.AssignedManagerId, m => m.Id, (c, m) => m.TeamId)
            .Join(db.Teams, t => t, team => (Guid?)team.Id, (t, team) => team.NameAr)
            .GroupBy(x => x).Select(g => new { team = g.Key, count = g.Count() }).ToDictionaryAsync(x => x.team, x => x.count);

        var settings = OperationalSettings.Catalog.Select(def =>
        {
            var effective = all.FirstOrDefault(s => s.Key == def.Key && s.Status == SettingChangeStatus.Effective);
            var pending = all.FirstOrDefault(s => s.Key == def.Key && s.Status == SettingChangeStatus.PendingApproval);
            var value = JsonNode.Parse(effective?.ValueJson ?? def.DefaultJson);
            object? capacity = null;
            if (def.Key == OperationalSettings.TeamCapacity && value is JsonArray teams)
                capacity = teams.Select(t =>
                {
                    var name = t?["team"]?.GetValue<string>() ?? "";
                    var max = t?["max"]?.GetValue<int>() ?? 0;
                    var n = load.GetValueOrDefault(name);
                    return new { team = name, current = n, max, percent = max == 0 ? 0 : (int)Math.Round(n * 100.0 / max), overLimit = max > 0 && n > max * 0.9, aria = $"{name}: {n} من {max}" };
                }).ToList();
            return new
            {
                key = def.Key, title = def.TitleAr, description = def.Description,
                effective = new
                {
                    version = effective?.VersionNo ?? 0, value, approvedBy = Name(effective?.DecidedByUserId) ?? (effective is null ? "القيمة الافتراضية للمنصة" : "الإعداد الأولي"),
                    approvedAt = effective?.DecidedAt,
                },
                pending = pending is null ? null : new { pending.Id, version = pending.VersionNo, value = JsonNode.Parse(pending.ValueJson), proposedBy = Name(pending.ProposedByUserId), pending.ProposedAt, pending.Reason },
                history = all.Where(s => s.Key == def.Key && s.Status != SettingChangeStatus.PendingApproval).Take(5)
                    .Select(s => new { version = s.VersionNo, status = s.Status.ToString(), proposedBy = Name(s.ProposedByUserId), decidedBy = Name(s.DecidedByUserId), s.DecidedAt, s.Reason, s.DecisionReason }),
                capacity,
            };
        });
        return Results.Ok(new { settings, rule = "تعديلات تُعتمد وتُسجّل: كل تعديل مقترح يحتاج اعتماد شخص ثانٍ قبل أن يسري.", experimentalRule = "«تجريبي» يقترح ولا ينفذ." });
    }

    private static async Task<IResult> Propose(SettingChangeRequest req, RahoonDbContext db, RequestContext rc, IClock clock, AuditLog audit, Notifier notifier)
    {
        var def = OperationalSettings.Find(req.Key);
        var v = new Validator().Require(def is not null, "key", "إعداد غير معروف.")
            .Require(!string.IsNullOrWhiteSpace(req.Reason) && req.Reason.Trim().Length >= 10, "reason", "سبب التعديل إلزامي (10 أحرف على الأقل).");
        v.ThrowIfInvalid();
        JsonNode? value;
        try { value = JsonNode.Parse(req.Value.GetRawText()); } catch (JsonException) { value = null; }
        var problems = OperationalSettings.Validate(req.Key, value);
        if (problems.Count > 0) throw new ValidationFailedException(new Dictionary<string, string[]> { ["value"] = problems.ToArray() });

        await using var tx = await db.Database.BeginTransactionAsync();
        var org = rc.OrganizationId!.Value;
        var set = db.Set<OperationalSetting>();
        if (await set.AnyAsync(s => s.OrganizationId == org && s.Key == req.Key && s.Status == SettingChangeStatus.PendingApproval))
            throw new ConflictException("change_pending", "يوجد تعديل مقترح بانتظار الاعتماد لهذا الإعداد.");
        var version = (await set.Where(s => s.OrganizationId == org && s.Key == req.Key).Select(s => (int?)s.VersionNo).MaxAsync() ?? 0) + 1;
        var change = new OperationalSetting
        {
            OrganizationId = org, Key = req.Key, VersionNo = version, ValueJson = value!.ToJsonString(), Status = SettingChangeStatus.PendingApproval,
            ProposedByUserId = rc.UserId, ProposedAt = clock.UtcNow, Reason = req.Reason.Trim(),
        };
        set.Add(change);
        var approvers = await db.Memberships.Where(m => m.OrganizationId == org && m.Status == MembershipStatus.Active && m.UserId != rc.UserId
            && m.Roles.Any(x => x.Role!.Permissions.Any(p => p.PermissionKey == P.OperationsApprove))).Select(m => m.UserId).ToListAsync();
        foreach (var u in approvers)
            notifier.Notify(u, org, "settings", $"تعديل مقترح بانتظار اعتمادك: {def!.TitleAr}", change.Reason, "/settings/operations");
        await audit.RecordAsync(new AuditEntry("settings.change_proposed", $"اقتراح تعديل «{def!.TitleAr}» v{version}", Reason: change.Reason, Detail: change.ValueJson));
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Results.Ok(new { change.Id, version, status = change.Status.ToString() });
    }

    private static async Task<IResult> Decide(Guid id, SettingDecisionRequest req, RahoonDbContext db, RequestContext rc, IClock clock, AuditLog audit, Notifier notifier)
    {
        var decision = req.Decision?.ToLowerInvariant();
        new Validator().Require(decision is "approve" or "reject", "decision", "اختر: اعتماد أو رفض.")
            .Require(!string.IsNullOrWhiteSpace(req.Reason) && req.Reason.Trim().Length >= 5, "reason", "السبب إلزامي.").ThrowIfInvalid();
        var org = rc.OrganizationId!.Value;
        var change = await db.Set<OperationalSetting>().FirstOrDefaultAsync(s => s.Id == id && s.OrganizationId == org) ?? throw new NotFoundException();
        if (change.Status != SettingChangeStatus.PendingApproval) throw new ConflictException("already_decided", "تم القرار على هذا التعديل.");
        var title = OperationalSettings.Find(change.Key)?.TitleAr ?? change.Key;
        if (change.ProposedByUserId == rc.UserId)
        {
            await audit.RecordBlockedAsync(new AuditEntry("settings.change_blocked", $"محاولة اعتماد تعديل «{title}» من مقترحه", Detail: "المانع: مقترح التعديل ≠ معتمده.", Blocked: true));
            throw new DomainException("separation_of_duties", "اقترحت هذا التعديل؛ الاعتماد لشخص آخر.", StatusCodes.Status403Forbidden);
        }
        await using var tx = await db.Database.BeginTransactionAsync();
        var now = clock.UtcNow;
        if (decision == "approve")
        {
            foreach (var old in await db.Set<OperationalSetting>().Where(s => s.OrganizationId == org && s.Key == change.Key && s.Status == SettingChangeStatus.Effective).ToListAsync())
                old.Status = SettingChangeStatus.Superseded;
            await db.SaveChangesAsync(); // release the «one effective» unique slot before promoting
            change.Status = SettingChangeStatus.Effective;
        }
        else change.Status = SettingChangeStatus.Rejected;
        change.DecidedByUserId = rc.UserId;
        change.DecidedAt = now;
        change.DecisionReason = req.Reason.Trim();
        notifier.Notify(change.ProposedByUserId, org, "settings", decision == "approve" ? $"اعتُمد تعديل «{title}»" : $"رُفض تعديل «{title}»", change.DecisionReason, "/settings/operations");
        await audit.RecordAsync(new AuditEntry("settings.change_decided", decision == "approve" ? $"اعتماد تعديل «{title}» v{change.VersionNo}" : $"رفض تعديل «{title}» v{change.VersionNo}",
            Reason: change.DecisionReason, Detail: change.ValueJson));
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Results.Ok(new { status = change.Status.ToString(), version = change.VersionNo });
    }
}
