using Microsoft.EntityFrameworkCore;
using Rahoon.Api.Infrastructure.Http;
using Rahoon.Api.Infrastructure.Persistence;
using Rahoon.Api.Infrastructure.Tenancy;
using Rahoon.Api.Infrastructure.Time;
using Rahoon.Api.Modules.Administration;
using Rahoon.Api.Modules.Audit;
using Rahoon.Api.Modules.Communications;
using Rahoon.Api.Modules.Identity;

namespace Rahoon.Api.Modules.Solutions;

public sealed record LimitTierInput(int Rank, string RoleKey, string LevelLabel, List<string>? SolutionKinds, string? SolutionKindsLabel,
    decimal? MaxAmount, decimal? MaxWaiverPercent, bool CanApprove, string? EscalateToLabel);
public sealed record LimitProposalRequest(DateOnly EffectiveFrom, string ChangeSummary, List<LimitTierInput>? Tiers,
    bool? SeparationOfDuties, bool? DualApprovals, bool Submit);
public sealed record LimitDecisionRequest(string? Reason);

/// <summary>
/// A04 approval limits: versioned policies. A change is proposed as vN+1 (draft → pending_approval) and becomes
/// effective only when a different admin approves it (maker-checker, MFA step-up). Platform minima (PA07) are
/// enforced: separation of duties and dual approvals cannot be disabled; non-committee waiver ≤ platform max.
/// </summary>
public static class ApprovalLimitEndpoints
{
    public static readonly string[] FixedRules =
    [
        "المُعِدّ والمراجِع لا يعتمدان طلبهما.",
        "الإحالة والإلغاء والإغلاق: موافقتان دائماً.",
        "أي تعديل على الحدود يتطلب موافقة المسؤول الثاني.",
    ];

    public static void Map(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/settings/approval-limits").RequireOrg(OrganizationKind.Lender).RequirePermission(P.LimitsManage);
        g.MapGet("", Current);
        g.MapGet("/versions", Versions);
        g.MapPost("/proposals", Propose).Idempotent();
        g.MapPost("/proposals/{version:int}/submit", Submit).Idempotent();
        g.MapPost("/proposals/{version:int}/approve", Approve).Idempotent();
        g.MapPost("/proposals/{version:int}/reject", Reject).Idempotent();
    }

    private static object TierDto(ApprovalLimitTier t) => new
    {
        t.Rank, t.RoleKey, level = t.LevelLabel, t.SolutionKinds, kindsLabel = t.SolutionKindsLabel, t.MaxAmount, t.MaxWaiverPercent, t.CanApprove,
        amountLabel = !t.CanApprove ? "—" : t.MaxAmount is { } m ? m.ToString("N0") : "بلا حد",
        waiverLabel = !t.CanApprove ? "—" : t.MaxWaiverPercent is { } w ? $"{w * 100:0.##}%" : "حسب القرار",
        escalateTo = t.EscalateToLabel,
    };

    private static async Task<IResult> Current(RahoonDbContext db, RequestContext rc, PlatformMinima minima)
    {
        var policies = await db.ApprovalLimitPolicies.AsNoTracking().Include(p => p.Tiers).Where(p => p.OrganizationId == rc.OrganizationId).OrderByDescending(p => p.VersionNo).ToListAsync();
        var effective = policies.FirstOrDefault(p => p.Status == "effective");
        var names = await UserNamesAsync(db, policies.Select(p => p.ProposedByUserId));
        return Results.Ok(new
        {
            effective = effective is null ? null : new
            {
                version = effective.VersionNo, effective.EffectiveFrom, header = $"الإصدار النافذ v{effective.VersionNo} · منذ {effective.EffectiveFrom:yyyy-MM-dd} · القيم أمثلة (افتراض)",
                tiers = effective.Tiers.OrderBy(t => t.Rank).Select(TierDto),
            },
            fixedRows = new[]
            {
                new { level = "القانونية + معتمد", kinds = "إحالة قضائية", rule = "موافقتان دائماً" },
                new { level = "المالية + معتمد", kinds = "الإغلاق والتوزيع", rule = "موافقتان دائماً" },
            },
            fixedRules = FixedRules,
            proposals = policies.Where(p => p.Status is "draft" or "pending_approval").Select(p => new
            {
                version = p.VersionNo, p.Status, p.ChangeSummary, p.EffectiveFrom, proposedBy = names.GetValueOrDefault(p.ProposedByUserId),
                proposedBySelf = p.ProposedByUserId == rc.UserId,
                canApprove = p.Status == "pending_approval" && p.ProposedByUserId != rc.UserId,
                tiers = p.Tiers.OrderBy(t => t.Rank).Select(TierDto),
            }),
            platformMinima = new { maxWaiverWithoutCommittee = await minima.MaxWaiverWithoutCommitteeAsync(), separationOfDuties = "إلزامي", dualApprovals = "إلزامي" },
        });
    }

    private static async Task<Dictionary<Guid, string>> UserNamesAsync(RahoonDbContext db, IEnumerable<Guid?> ids)
    {
        var list = ids.Where(i => i is not null).Select(i => i!.Value).Distinct().ToList();
        return await db.Users.AsNoTracking().Where(u => list.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName);
    }

    private static Task<Dictionary<Guid, string>> UserNamesAsync(RahoonDbContext db, IEnumerable<Guid> ids) => UserNamesAsync(db, ids.Select(i => (Guid?)i));

    private static async Task<IResult> Versions(RahoonDbContext db, RequestContext rc)
    {
        var policies = await db.ApprovalLimitPolicies.AsNoTracking().Where(p => p.OrganizationId == rc.OrganizationId).OrderByDescending(p => p.VersionNo).ToListAsync();
        var names = await UserNamesAsync(db, policies.Select(p => (Guid?)p.ProposedByUserId).Concat(policies.Select(p => p.ApprovedByUserId)));
        return Results.Ok(policies.Select(p => new
        {
            version = p.VersionNo, p.Status, p.EffectiveFrom, p.ChangeSummary, proposedBy = names.GetValueOrDefault(p.ProposedByUserId),
            approvedBy = p.ApprovedByUserId is { } a ? names.GetValueOrDefault(a) : null, p.ApprovedAt, p.CreatedAt,
        }));
    }

    private static async Task<IResult> Propose(LimitProposalRequest req, RahoonDbContext db, RequestContext rc, IClock clock, PlatformMinima minima, AuditLog audit, Notifier notifier)
    {
        var tiers = req.Tiers ?? [];
        var roleKeys = await db.Roles.Where(r => r.OrganizationId == rc.OrganizationId).Select(r => r.Key).ToListAsync();
        var maxWaiver = await minima.MaxWaiverWithoutCommitteeAsync();
        var kinds = Enum.GetNames<SolutionKind>();
        var v = new Validator()
            .Require(req.SeparationOfDuties != false, "separationOfDuties", "فصل المُعِدّ والمعتمد حد أدنى للمنصة ولا يمكن تعطيله.")
            .Require(req.DualApprovals != false, "dualApprovals", "الموافقتان للإحالة والإلغاء والإغلاق حد أدنى للمنصة ولا يمكن تعطيلهما.")
            .Require(!string.IsNullOrWhiteSpace(req.ChangeSummary) && req.ChangeSummary.Trim().Length <= 500, "changeSummary", "اكتب ملخص التعديل.")
            .Require(req.EffectiveFrom >= clock.TodayRiyadh, "effectiveFrom", "تاريخ النفاذ اليوم أو بعده.")
            .Require(tiers.Count is > 0 and <= 10, "tiers", "حدد مستويات الموافقة.")
            .Require(tiers.Select(t => t.Rank).Distinct().Count() == tiers.Count, "tiers", "ترتيب المستويات مكرر.")
            .Require(tiers.Any(t => t.CanApprove), "tiers", "يجب أن يوجد مستوى واحد على الأقل يعتمد.")
            .Require(tiers.All(t => roleKeys.Contains(t.RoleKey)), "tiers", "دور غير معروف في أحد المستويات.")
            .Require(tiers.All(t => !string.IsNullOrWhiteSpace(t.LevelLabel)), "tiers", "اسم المستوى مطلوب.")
            .Require(tiers.All(t => (t.SolutionKinds ?? []).All(k => kinds.Contains(k)) && (!t.CanApprove || (t.SolutionKinds ?? []).Count > 0)), "tiers", "أنواع الحلول غير صحيحة.")
            .Require(tiers.All(t => t.MaxAmount is null or > 0), "tiers", "حد المبلغ أكبر من صفر.")
            .Require(tiers.All(t => t.MaxWaiverPercent is null or >= 0 and <= 1), "tiers", "نسبة التنازل بين 0 و100%.")
            // PA07: any tier other than the risk committee must carry an explicit waiver cap within the platform maximum.
            .Require(tiers.Where(t => t.CanApprove && t.RoleKey != SystemRoles.RiskCommittee).All(t => t.MaxWaiverPercent is { } w && w <= maxWaiver),
                "tiers", $"حد التنازل بلا لجنة لا يتجاوز {maxWaiver * 100:0.##}% (حد المنصة).");
        v.ThrowIfInvalid();

        await using var tx = await db.Database.BeginTransactionAsync();
        var next = (await db.ApprovalLimitPolicies.Where(p => p.OrganizationId == rc.OrganizationId).MaxAsync(p => (int?)p.VersionNo) ?? 0) + 1;
        var policy = new ApprovalLimitPolicy
        {
            OrganizationId = rc.OrganizationId!.Value, VersionNo = next, Status = req.Submit ? "pending_approval" : "draft", EffectiveFrom = req.EffectiveFrom,
            ChangeSummary = req.ChangeSummary.Trim(), ProposedByUserId = rc.UserId,
        };
        policy.Tiers.AddRange(tiers.OrderBy(t => t.Rank).Select(t => new ApprovalLimitTier
        {
            Rank = t.Rank, RoleKey = t.RoleKey, LevelLabel = t.LevelLabel.Trim(), SolutionKinds = t.SolutionKinds ?? [], SolutionKindsLabel = t.SolutionKindsLabel ?? "",
            MaxAmount = t.MaxAmount, MaxWaiverPercent = t.MaxWaiverPercent, CanApprove = t.CanApprove, EscalateToLabel = t.EscalateToLabel,
        }));
        db.ApprovalLimitPolicies.Add(policy);
        if (req.Submit) await NotifyApproversAsync(db, rc, notifier, policy);
        await audit.RecordAsync(new AuditEntry("limits.proposed", "تعديل حد موافقة (مقترح)", Detail: $"v{next} {(req.Submit ? "بانتظار الموافقة" : "مسودة")} · {policy.ChangeSummary}"));
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Results.Ok(new { version = next, status = policy.Status });
    }

    private static async Task NotifyApproversAsync(RahoonDbContext db, RequestContext rc, Notifier notifier, ApprovalLimitPolicy p)
    {
        var admins = await db.Memberships.Where(m => m.OrganizationId == rc.OrganizationId && m.Status == MembershipStatus.Active && m.UserId != p.ProposedByUserId
                                                     && m.Roles.Any(r => r.Role!.Permissions.Any(x => x.PermissionKey == P.LimitsManage)))
            .Select(m => m.UserId).ToListAsync();
        foreach (var u in admins)
            notifier.Notify(u, rc.OrganizationId, "admin", $"تعديل حدود الموافقة v{p.VersionNo} بانتظار موافقتك", p.ChangeSummary, "/settings/approval-limits");
    }

    private static async Task<ApprovalLimitPolicy> ProposalAsync(RahoonDbContext db, RequestContext rc, int version) =>
        await db.ApprovalLimitPolicies.Include(p => p.Tiers).FirstOrDefaultAsync(p => p.OrganizationId == rc.OrganizationId && p.VersionNo == version) ?? throw new NotFoundException();

    private static async Task<IResult> Submit(int version, RahoonDbContext db, RequestContext rc, AuditLog audit, Notifier notifier)
    {
        await using var tx = await db.Database.BeginTransactionAsync();
        var p = await ProposalAsync(db, rc, version);
        if (p.Status != "draft") throw new ConflictException("not_draft", "المقترح ليس مسودة.");
        if (p.ProposedByUserId != rc.UserId) throw new ForbiddenException("يرسل المقترحَ للموافقة من أعدّه.");
        p.Status = "pending_approval";
        await NotifyApproversAsync(db, rc, notifier, p);
        await audit.RecordAsync(new AuditEntry("limits.submitted", $"إرسال تعديل الحدود v{version} للموافقة"));
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Results.Ok(new { version, status = p.Status });
    }

    private static async Task<IResult> Approve(int version, LimitDecisionRequest req, RahoonDbContext db, RequestContext rc, IClock clock, PlatformMinima minima, AuditLog audit, Notifier notifier)
    {
        EndpointAccess.EnsureStepUp(rc, clock);
        await using var tx = await db.Database.BeginTransactionAsync();
        var p = await ProposalAsync(db, rc, version);
        if (p.Status != "pending_approval") throw new ConflictException("not_pending", "المقترح ليس بانتظار الموافقة.");
        if (p.ProposedByUserId == rc.UserId)
            throw new DomainException("maker_checker", "لا يعتمد مقترح التعديل من اقترحه؛ يلزم مسؤول ثانٍ.", StatusCodes.Status403Forbidden);
        var newer = await db.ApprovalLimitPolicies.AnyAsync(x => x.OrganizationId == rc.OrganizationId && x.Status == "effective" && x.VersionNo > p.VersionNo);
        if (newer) throw new ConflictException("superseded", "يوجد إصدار نافذ أحدث من هذا المقترح.");
        // Re-check platform minima at decision time (they may have been tightened since the proposal).
        var maxWaiver = await minima.MaxWaiverWithoutCommitteeAsync();
        if (p.Tiers.Where(t => t.CanApprove && t.RoleKey != SystemRoles.RiskCommittee).Any(t => t.MaxWaiverPercent is not { } w || w > maxWaiver))
            throw new DomainException("platform_minimum", $"المقترح يتجاوز حد المنصة للتنازل بلا لجنة ({maxWaiver * 100:0.##}%).");

        foreach (var old in await db.ApprovalLimitPolicies.Where(x => x.OrganizationId == rc.OrganizationId && x.Id != p.Id
                                                                      && (x.Status == "effective" || (x.Status != "rejected" && x.Status != "superseded" && x.VersionNo < p.VersionNo))).ToListAsync())
            old.Status = "superseded";
        p.Status = "effective";
        p.ApprovedByUserId = rc.UserId;
        p.ApprovedAt = clock.UtcNow;
        if (p.EffectiveFrom < clock.TodayRiyadh) p.EffectiveFrom = clock.TodayRiyadh;
        notifier.Notify(p.ProposedByUserId, rc.OrganizationId, "admin", $"اعتُمد تعديل الحدود v{p.VersionNo}", p.ChangeSummary, "/settings/approval-limits", tone: "ok");
        await audit.RecordAsync(new AuditEntry("limits.approved", $"اعتماد حدود الموافقة v{p.VersionNo}", Reason: req.Reason, Detail: p.ChangeSummary, Evidence: ["موافقة مسؤول ثانٍ", "MFA"]));
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Results.Ok(new { version, status = p.Status });
    }

    private static async Task<IResult> Reject(int version, LimitDecisionRequest req, RahoonDbContext db, RequestContext rc, IClock clock, AuditLog audit, Notifier notifier)
    {
        new Validator().Require(!string.IsNullOrWhiteSpace(req.Reason), "reason", "اكتب سبب الرفض أو السحب.").ThrowIfInvalid();
        await using var tx = await db.Database.BeginTransactionAsync();
        var p = await ProposalAsync(db, rc, version);
        if (p.Status is not ("draft" or "pending_approval")) throw new ConflictException("not_pending", "المقترح ليس مفتوحاً.");
        p.Status = "rejected";
        if (p.ProposedByUserId != rc.UserId)
            notifier.Notify(p.ProposedByUserId, rc.OrganizationId, "admin", $"رُفض تعديل الحدود v{p.VersionNo}", req.Reason, "/settings/approval-limits", tone: "warn");
        await audit.RecordAsync(new AuditEntry("limits.rejected", p.ProposedByUserId == rc.UserId ? $"سحب مقترح الحدود v{version}" : $"رفض مقترح الحدود v{version}", Reason: req.Reason!.Trim()));
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Results.Ok(new { version, status = p.Status });
    }
}
