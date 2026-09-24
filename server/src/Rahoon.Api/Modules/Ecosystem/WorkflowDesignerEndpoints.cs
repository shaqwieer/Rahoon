using Microsoft.EntityFrameworkCore;
using Rahoon.Api.Infrastructure.Http;
using Rahoon.Api.Infrastructure.Persistence;
using Rahoon.Api.Infrastructure.Tenancy;
using Rahoon.Api.Infrastructure.Time;
using Rahoon.Api.Modules.Audit;
using Rahoon.Api.Modules.Cases;
using Rahoon.Api.Modules.Identity;
using Rahoon.Api.Modules.Solutions;

namespace Rahoon.Api.Modules.Ecosystem;

public sealed record StageSlaBody(int? SlaDays);
public sealed record AddRuleBody(string StageKey, string Field, string Operator, decimal Value, string Action, string? Label);
public sealed record SubmitVersionBody(string ChangeSummary);
public sealed record ApproveVersionBody(string? Note);
public sealed record ReturnVersionBody(string Reason);
public sealed record SimulateBody(SimulationCase? Case, int? SampleSize);

/// <summary>
/// Workflow designer & approval rules (PA14). Platform operations (platform.defaults) edit any institution; an
/// institution admin (org.settings) may view and draft its own. Every edit happens on a draft version; publishing
/// requires a second authorised platform user (≠ submitter, step-up) — the spec leaves the approver open; this is
/// the decision recorded in docs/progress/backend-B8-B9.md. New versions apply to new cases only.
/// </summary>
public static class WorkflowDesignerEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/workflows/{institutionId:guid}").RequireAnyPermission(P.PlatformDefaults, P.OrgSettings);
        g.MapGet("", Get);
        g.MapPost("/versions", CreateDraft).Idempotent();
        g.MapPut("/versions/{v:int}/stages/{key}", UpdateStage).Idempotent();
        g.MapPost("/versions/{v:int}/rules", AddRule).Idempotent();
        g.MapDelete("/versions/{v:int}/rules/{ruleId}", RemoveRule).Idempotent();
        // Simulation is read-only by design: no Idempotent filter (it would persist a record), no audit, no SaveChanges.
        g.MapPost("/versions/{v:int}/simulate", Simulate);
        g.MapPost("/versions/{v:int}/submit", Submit).Idempotent();
        g.MapPost("/versions/{v:int}/approve", Approve).RequirePermission(P.PlatformDefaults).Idempotent();
        g.MapPost("/versions/{v:int}/return", Return).RequirePermission(P.PlatformDefaults).Idempotent();
    }

    /// <summary>Platform staff may act on any lender; institution admins only on their own organization.</summary>
    private static async Task EnsureScopeAsync(RahoonDbContext db, RequestContext rc, Guid institutionId)
    {
        if (rc.IsPlatform && rc.Has(P.PlatformDefaults))
        {
            if (!await db.Organizations.AnyAsync(o => o.Id == institutionId && o.Kind == OrganizationKind.Lender)) throw new NotFoundException();
            return;
        }
        if (rc.IsLenderStaff && rc.Has(P.OrgSettings) && rc.OrganizationId == institutionId) return;
        throw new NotFoundException();
    }

    private static async Task<WorkflowVersion> DraftAsync(RahoonDbContext db, Guid institutionId, int v)
    {
        var version = await db.Set<WorkflowVersion>().FirstOrDefaultAsync(x => x.InstitutionOrganizationId == institutionId && x.VersionNo == v) ?? throw new NotFoundException();
        if (version.Status != WorkflowVersionStatus.Draft) throw new ConflictException("not_draft", "التعديل على مسودة فقط؛ الإصدار المُرسل أو النافذ لا يُعدّل.");
        return version;
    }

    private static object View(WorkflowVersion v, IReadOnlyList<WorkflowStage> stages, IReadOnlyDictionary<Guid, string> names) => new
    {
        version = v.VersionNo, status = v.Status.ToString(), v.EffectiveFrom, v.ChangeSummary, basedOn = v.BasedOnVersionNo,
        createdBy = names.GetValueOrDefault(v.CreatedByUserId), submittedBy = v.SubmittedByUserId is { } s ? names.GetValueOrDefault(s) : null, v.SubmittedAt,
        approvedBy = v.ApprovedByUserId is { } a ? names.GetValueOrDefault(a) : null, v.ApprovedAt, v.ApprovalNote,
        stages = stages.Select(s => new
        {
            s.Key, s.Icon, s.Title, s.SlaDays, slaText = s.SlaDays is { } d ? CaseDisplay.Days(d) : "—", s.Locked,
            lockLabel = s.Locked ? "قاعدة منصة ثابتة" : null, changed = s.ChangedInVersion is { } cv ? $"معدّل في v{cv}" : null,
            rules = s.Rules.Select(r => new
            {
                r.Id, r.Label, locked = r.PlatformLocked, r.AddedInVersion,
                condition = r.Condition is null ? null : new { r.Condition.Field, r.Condition.Operator, r.Condition.Value }, r.Action,
            }),
        }),
    };

    private static async Task<IResult> Get(Guid institutionId, RahoonDbContext db, RequestContext rc)
    {
        await EnsureScopeAsync(db, rc, institutionId);
        var versions = await db.Set<WorkflowVersion>().AsNoTracking().Where(x => x.InstitutionOrganizationId == institutionId).OrderByDescending(x => x.VersionNo).ToListAsync();
        var org = await db.Organizations.AsNoTracking().Where(o => o.Id == institutionId).Select(o => o.NameAr).FirstAsync();
        var ids = versions.SelectMany(v => new[] { v.CreatedByUserId, v.SubmittedByUserId ?? Guid.Empty, v.ApprovedByUserId ?? Guid.Empty }).Distinct().ToList();
        var names = await db.Users.AsNoTracking().Where(u => ids.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName);
        var active = versions.FirstOrDefault(v => v.Status == WorkflowVersionStatus.Active);
        var open = versions.FirstOrDefault(v => v.Status is WorkflowVersionStatus.Draft or WorkflowVersionStatus.PendingApproval);
        return Results.Ok(new
        {
            title = $"مصمم سير العمل · {org}",
            meta = string.Join(" · ", new[] { open is null ? null : $"{(open.Status == WorkflowVersionStatus.Draft ? "مسودة" : "بانتظار الاعتماد")} v{open.VersionNo}", active is null ? null : $"النافذ v{active.VersionNo} منذ {active.EffectiveFrom:yyyy-MM-dd}" }.Where(x => x is not null)),
            active = active is null ? null : View(active, WorkflowModel.Parse(active.StagesJson), names),
            draft = open is null ? null : View(open, WorkflowModel.Parse(open.StagesJson), names),
            history = versions.Select(v => new { version = v.VersionNo, status = v.Status.ToString(), v.EffectiveFrom, v.ChangeSummary }),
            builder = new
            {
                fields = WorkflowModel.Fields.Select(f => new { key = f.Key, label = f.Value }), operators = WorkflowModel.Operators,
                actions = WorkflowModel.Actions.Select(a => new { key = a.Key, label = a.Value.Label, addedDays = a.Value.AddedDays }),
            },
            lockNote = "لا يمكن إزالة فصل المهام، أو الموافقتين للإحالة والإغلاق، أو حق المالك في الاعتراض. التعديلات تنطبق على الحالات الجديدة فقط.",
            canApprove = rc.IsPlatform && rc.Has(P.PlatformDefaults) && open is { Status: WorkflowVersionStatus.PendingApproval } && open.SubmittedByUserId != rc.UserId,
        });
    }

    private static async Task<IResult> CreateDraft(Guid institutionId, RahoonDbContext db, RequestContext rc, AuditLog audit)
    {
        await EnsureScopeAsync(db, rc, institutionId);
        if (await db.Set<WorkflowVersion>().AnyAsync(x => x.InstitutionOrganizationId == institutionId && (x.Status == WorkflowVersionStatus.Draft || x.Status == WorkflowVersionStatus.PendingApproval)))
            throw new ConflictException("draft_exists", "توجد مسودة مفتوحة؛ أكملها أو أعدها قبل إنشاء أخرى.");
        var active = await db.Set<WorkflowVersion>().AsNoTracking().FirstOrDefaultAsync(x => x.InstitutionOrganizationId == institutionId && x.Status == WorkflowVersionStatus.Active);
        var max = await db.Set<WorkflowVersion>().Where(x => x.InstitutionOrganizationId == institutionId).MaxAsync(x => (int?)x.VersionNo) ?? 0;
        var draft = new WorkflowVersion
        {
            InstitutionOrganizationId = institutionId, VersionNo = max + 1, StagesJson = active?.StagesJson ?? WorkflowModel.Serialize(WorkflowModel.DefaultStages()),
            BasedOnVersionNo = active?.VersionNo, CreatedByUserId = rc.UserId,
        };
        db.Set<WorkflowVersion>().Add(draft);
        await audit.RecordAsync(new AuditEntry("workflow.draft_created", $"إنشاء مسودة سير العمل v{draft.VersionNo}", OrganizationId: institutionId));
        await db.SaveChangesAsync();
        return Results.Ok(new { version = draft.VersionNo });
    }

    private static async Task<IResult> UpdateStage(Guid institutionId, int v, string key, StageSlaBody req, RahoonDbContext db, RequestContext rc, AuditLog audit)
    {
        await EnsureScopeAsync(db, rc, institutionId);
        new Validator().Require(req.SlaDays is null or (>= 1 and <= 60), "slaDays", "المهلة بين 1 و60 يوم عمل.").ThrowIfInvalid();
        var draft = await DraftAsync(db, institutionId, v);
        var stages = WorkflowModel.Parse(draft.StagesJson);
        var stage = stages.FirstOrDefault(s => s.Key == key) ?? throw new NotFoundException();
        if (stage.SlaDays is null && req.SlaDays is not null && stage.Locked) throw new ConflictException("locked_stage", "مهلة هذه المرحلة تحددها جهة خارجية ولا تُضبط هنا.");
        var before = stage.SlaDays;
        stage.SlaDays = req.SlaDays;
        stage.ChangedInVersion = draft.VersionNo;
        draft.StagesJson = WorkflowModel.Serialize(stages);
        await audit.RecordAsync(new AuditEntry("workflow.stage_sla", $"تعديل مهلة «{stage.Title}» في v{draft.VersionNo}", Detail: $"{before?.ToString() ?? "—"} ← {req.SlaDays?.ToString() ?? "—"} يوم عمل", OrganizationId: institutionId));
        await db.SaveChangesAsync();
        return Results.Ok(new { stage.Key, stage.SlaDays });
    }

    private static async Task<IResult> AddRule(Guid institutionId, int v, AddRuleBody req, RahoonDbContext db, RequestContext rc, AuditLog audit)
    {
        await EnsureScopeAsync(db, rc, institutionId);
        new Validator()
            .Require(WorkflowModel.Fields.ContainsKey(req.Field ?? ""), "field", "اختر الحقل.")
            .Require(WorkflowModel.Operators.Contains(req.Operator), "operator", "اختر المقارنة.")
            .Require(WorkflowModel.Actions.ContainsKey(req.Action ?? ""), "action", "اختر الإجراء.")
            .Require(req.Value >= 0, "value", "القيمة لا تكون سالبة.")
            .ThrowIfInvalid();
        var draft = await DraftAsync(db, institutionId, v);
        var stages = WorkflowModel.Parse(draft.StagesJson);
        var stage = stages.FirstOrDefault(s => s.Key == req.StageKey) ?? throw new NotFoundException();
        var condition = new RuleCondition { Field = req.Field, Operator = req.Operator, Value = req.Value };
        var rule = new WorkflowRule
        {
            Label = string.IsNullOrWhiteSpace(req.Label) ? "جديد: " + WorkflowModel.Describe(condition, req.Action) : req.Label.Trim(),
            Condition = condition, Action = req.Action, AddedInVersion = draft.VersionNo,
        };
        stage.Rules.Add(rule);
        stage.ChangedInVersion = draft.VersionNo;
        draft.StagesJson = WorkflowModel.Serialize(stages);
        await audit.RecordAsync(new AuditEntry("workflow.rule_added", $"إضافة قاعدة إلى «{stage.Title}» في v{draft.VersionNo}", Detail: rule.Label, OrganizationId: institutionId));
        await db.SaveChangesAsync();
        return Results.Ok(new { rule.Id, rule.Label });
    }

    private static async Task<IResult> RemoveRule(Guid institutionId, int v, string ruleId, RahoonDbContext db, RequestContext rc, AuditLog audit)
    {
        await EnsureScopeAsync(db, rc, institutionId);
        var draft = await DraftAsync(db, institutionId, v);
        var stages = WorkflowModel.Parse(draft.StagesJson);
        var stage = stages.FirstOrDefault(s => s.Rules.Any(r => r.Id == ruleId)) ?? throw new NotFoundException();
        var rule = stage.Rules.First(r => r.Id == ruleId);
        if (rule.PlatformLocked) throw new ConflictException("locked_rule", "قاعدة منصة ثابتة لا يمكن إزالتها (فصل المهام، الموافقتان للإحالة والإغلاق، حق الاعتراض).");
        stage.Rules.Remove(rule);
        stage.ChangedInVersion = draft.VersionNo;
        draft.StagesJson = WorkflowModel.Serialize(stages);
        await audit.RecordAsync(new AuditEntry("workflow.rule_removed", $"إزالة قاعدة من «{stage.Title}» في v{draft.VersionNo}", Detail: rule.Label, OrganizationId: institutionId));
        await db.SaveChangesAsync();
        return Results.Ok(new { removed = rule.Id });
    }

    /// <summary>
    /// Evaluates a hypothetical case against the version (single-case mode) and/or replays the rule change over up to
    /// 50 recent solution versions of the institution (aggregates only). Performs no writes of any kind.
    /// </summary>
    private static async Task<IResult> Simulate(Guid institutionId, int v, SimulateBody req, RahoonDbContext db, RequestContext rc)
    {
        await EnsureScopeAsync(db, rc, institutionId);
        var version = await db.Set<WorkflowVersion>().AsNoTracking().FirstOrDefaultAsync(x => x.InstitutionOrganizationId == institutionId && x.VersionNo == v) ?? throw new NotFoundException();
        var stages = WorkflowModel.Parse(version.StagesJson);
        var active = await db.Set<WorkflowVersion>().AsNoTracking().FirstOrDefaultAsync(x => x.InstitutionOrganizationId == institutionId && x.Status == WorkflowVersionStatus.Active);
        var activeStages = active is null ? [] : WorkflowModel.Parse(active.StagesJson);
        var policyId = await db.ApprovalLimitPolicies.IgnoreQueryFilters().AsNoTracking().Where(p => p.OrganizationId == institutionId && p.Status == "effective")
            .OrderByDescending(p => p.VersionNo).Select(p => (Guid?)p.Id).FirstOrDefaultAsync();
        var tiers = policyId is null ? null : await db.ApprovalLimitTiers.AsNoTracking().Where(t => t.PolicyId == policyId).ToListAsync();

        object? single = null;
        if (req.Case is { } x)
        {
            var draftOutcome = WorkflowModel.Evaluate(stages, x, tiers);
            var activeOutcome = WorkflowModel.Evaluate(activeStages, x, tiers);
            single = new
            {
                baseTier = draftOutcome.BaseTier, requiredApprovals = draftOutcome.RequiredApprovals,
                triggeredRules = draftOutcome.Hits.Select(h => new { h.StageKey, h.RuleId, h.Label, h.ActionLabel }),
                addedDays = draftOutcome.AddedDays, comparedToActive = new { activeOutcome.RequiredApprovals, activeOutcome.AddedDays },
                totalSlaDays = stages.Sum(s => s.SlaDays ?? 0) + draftOutcome.AddedDays,
            };
        }

        var sampleSize = Math.Clamp(req.SampleSize ?? 50, 1, 200);
        // Aggregate replay over the institution's own recent solutions (read-only; platform staff see counts only).
        var sample = await db.Solutions.IgnoreQueryFilters().AsNoTracking().Where(s => s.OrganizationId == institutionId && s.Status != SolutionStatus.Draft)
            .OrderByDescending(s => s.PreparedAt).Take(sampleSize)
            .Select(s => new { s.OutstandingAtPreparation, s.WaiverPercent, s.Dsr, s.TermMonths, s.Kind }).ToListAsync();
        var affected = 0;
        var addedTotal = 0;
        foreach (var s in sample)
        {
            var sc = new SimulationCase(s.OutstandingAtPreparation, s.WaiverPercent * 100m, s.Dsr is { } d ? d * 100m : null, s.TermMonths, s.Kind.ToString(), null);
            var draftHits = WorkflowModel.Evaluate(stages, sc, null);
            var activeHits = WorkflowModel.Evaluate(activeStages, sc, null);
            var extra = draftHits.AddedDays - activeHits.AddedDays;
            if (extra > 0 || draftHits.Hits.Count > activeHits.Hits.Count) { affected++; addedTotal += Math.Max(0, extra); }
        }
        var avg = sample.Count == 0 ? 0 : Math.Round((decimal)addedTotal / sample.Count, 1);
        return Results.Ok(new
        {
            version = v, sideEffects = "none",
            @case = single,
            sample = new
            {
                size = sample.Count, affected, avgDelayDays = avg,
                text = sample.Count == 0 ? "لا توجد حالات سابقة كافية للمحاكاة." : $"المحاكاة: كانت ستضيف مراجعة لـ {affected} من {sample.Count} حالة، ومتوسط تأخير +{avg} يوم.",
            },
        });
    }

    private static async Task<IResult> Submit(Guid institutionId, int v, SubmitVersionBody req, RahoonDbContext db, RequestContext rc, IClock clock, AuditLog audit)
    {
        await EnsureScopeAsync(db, rc, institutionId);
        new Validator().Require(!string.IsNullOrWhiteSpace(req.ChangeSummary) && req.ChangeSummary.Trim().Length >= 5, "changeSummary", "ملخص التغيير إلزامي.").ThrowIfInvalid();
        var draft = await DraftAsync(db, institutionId, v);
        var stages = WorkflowModel.Parse(draft.StagesJson);
        // Platform-mandated rules must all still be present.
        var required = WorkflowModel.DefaultStages().SelectMany(s => s.Rules.Where(r => r.PlatformLocked).Select(r => (s.Key, r.Id))).ToList();
        var missing = required.Where(r => !stages.Any(s => s.Key == r.Key && s.Rules.Any(x => x.Id == r.Id && x.PlatformLocked))).ToList();
        if (missing.Count > 0) throw new DomainException("locked_rules_missing", "قواعد المنصة الثابتة ناقصة في المسودة.", 422, missing.Select(m => $"{m.Key}:{m.Id}").ToList());
        draft.Status = WorkflowVersionStatus.PendingApproval;
        draft.ChangeSummary = req.ChangeSummary.Trim();
        draft.SubmittedByUserId = rc.UserId;
        draft.SubmittedAt = clock.UtcNow;
        await audit.RecordAsync(new AuditEntry("workflow.submitted", $"إرسال سير العمل v{draft.VersionNo} للاعتماد", Reason: draft.ChangeSummary, OrganizationId: institutionId));
        await db.SaveChangesAsync();
        return Results.Ok(new { status = draft.Status.ToString() });
    }

    /// <summary>Second authorised user (platform.defaults, ≠ submitter, ≠ draft author) publishes with a fresh step-up.</summary>
    private static async Task<IResult> Approve(Guid institutionId, int v, ApproveVersionBody req, RahoonDbContext db, RequestContext rc, IClock clock, AuditLog audit)
    {
        await EnsureScopeAsync(db, rc, institutionId);
        if (!rc.IsPlatform) throw new ForbiddenException();
        EndpointAccess.EnsureStepUp(rc, clock);
        var version = await db.Set<WorkflowVersion>().FirstOrDefaultAsync(x => x.InstitutionOrganizationId == institutionId && x.VersionNo == v) ?? throw new NotFoundException();
        if (version.Status != WorkflowVersionStatus.PendingApproval) throw new ConflictException("not_pending", "الإصدار ليس بانتظار الاعتماد.");
        if (version.SubmittedByUserId == rc.UserId || version.CreatedByUserId == rc.UserId)
            throw new ForbiddenException("اعتماد الإصدار لمستخدم ثانٍ غير من أعدّه أو أرسله.");
        var now = clock.UtcNow;
        await using var tx = await db.Database.BeginTransactionAsync();
        foreach (var old in await db.Set<WorkflowVersion>().Where(x => x.InstitutionOrganizationId == institutionId && x.Status == WorkflowVersionStatus.Active).ToListAsync())
            old.Status = WorkflowVersionStatus.Superseded;
        await db.SaveChangesAsync(); // release the single-active index before activating the new version
        version.Status = WorkflowVersionStatus.Active;
        version.EffectiveFrom = clock.TodayRiyadh;
        version.ApprovedByUserId = rc.UserId;
        version.ApprovedAt = now;
        version.ApprovalNote = req.Note?.Trim();
        await audit.RecordAsync(new AuditEntry("workflow.published", $"اعتماد ونشر سير العمل v{version.VersionNo}", Reason: version.ApprovalNote,
            Detail: $"أعدّه وأرسله مستخدم آخر · اعتماد برمز التحقق · يسري على الحالات الجديدة من {version.EffectiveFrom:yyyy-MM-dd}", OrganizationId: institutionId));
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Results.Ok(new { status = version.Status.ToString(), version.EffectiveFrom, approvedBy = rc.UserName });
    }

    private static async Task<IResult> Return(Guid institutionId, int v, ReturnVersionBody req, RahoonDbContext db, RequestContext rc, AuditLog audit)
    {
        await EnsureScopeAsync(db, rc, institutionId);
        new Validator().Require(!string.IsNullOrWhiteSpace(req.Reason) && req.Reason.Trim().Length >= 5, "reason", "السبب إلزامي.").ThrowIfInvalid();
        var version = await db.Set<WorkflowVersion>().FirstOrDefaultAsync(x => x.InstitutionOrganizationId == institutionId && x.VersionNo == v) ?? throw new NotFoundException();
        if (version.Status != WorkflowVersionStatus.PendingApproval) throw new ConflictException("not_pending", "الإصدار ليس بانتظار الاعتماد.");
        if (version.SubmittedByUserId == rc.UserId) throw new ForbiddenException("الإعادة لمستخدم ثانٍ غير من أرسل الإصدار.");
        version.Status = WorkflowVersionStatus.Draft;
        version.SubmittedAt = null;
        version.SubmittedByUserId = null;
        await audit.RecordAsync(new AuditEntry("workflow.returned", $"إعادة سير العمل v{version.VersionNo} للتعديل", Reason: req.Reason.Trim(), OrganizationId: institutionId));
        await db.SaveChangesAsync();
        return Results.Ok(new { status = version.Status.ToString() });
    }
}
