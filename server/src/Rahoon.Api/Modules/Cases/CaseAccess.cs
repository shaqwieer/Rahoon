using Microsoft.EntityFrameworkCore;
using Rahoon.Api.Infrastructure.Http;
using Rahoon.Api.Infrastructure.Persistence;
using Rahoon.Api.Infrastructure.Tenancy;
using Rahoon.Api.Infrastructure.Time;
using Rahoon.Api.Modules.Identity;
using Rahoon.Api.Modules.Solutions;

namespace Rahoon.Api.Modules.Cases;

public sealed class CaseForbiddenException()
    : DomainException("case_forbidden", "لا تملك صلاحية عرض هذه الحالة. الحالة تتبع منشأة أخرى أو فريقاً آخر، ولم نعرض أي بيانات عنها.", StatusCodes.Status403Forbidden);

/// <summary>
/// Case visibility for lender staff: tenant isolation is enforced by the EF filter;
/// within the tenant, users without case.view_all see only their own and their team's
/// cases plus cases where they hold a task or a pending approval.
/// </summary>
public sealed class CaseAccess(RahoonDbContext db, RequestContext rc)
{
    private List<Guid>? _teamManagers;

    public async Task<IQueryable<Case>> VisibleAsync()
    {
        if (!rc.IsLenderStaff || !rc.Has(P.CaseView)) throw new ForbiddenException();
        var q = db.Cases.Where(c => c.OrganizationId == rc.OrganizationId);
        if (rc.Has(P.CaseViewAll)) return q;

        _teamManagers ??= await TeamMembershipIdsAsync();
        var me = rc.MembershipId;
        var uid = rc.UserId;
        var managers = _teamManagers;
        return q.Where(c => c.AssignedManagerId == me
                            || (c.AssignedManagerId != null && managers.Contains(c.AssignedManagerId.Value))
                            || db.Tasks.Any(t => t.CaseId == c.Id && t.AssigneeUserId == uid)
                            || db.ApprovalRequests.Any(a => a.CaseId == c.Id && a.AssignedApproverUserId == uid)
                            || db.Solutions.Any(s => s.CaseId == c.Id && s.PreparedByUserId == uid));
    }

    private async Task<List<Guid>> TeamMembershipIdsAsync()
    {
        var team = await db.Memberships.Where(m => m.Id == rc.MembershipId).Select(m => m.TeamId).FirstOrDefaultAsync();
        if (team is null) return [];
        return await db.Memberships.Where(m => m.TeamId == team).Select(m => m.Id).ToListAsync();
    }

    /// <summary>Loads a visible case by reference or throws the same refusal whether it exists or not.</summary>
    public async Task<Case> GetAsync(string reference, bool track = true)
    {
        var q = await VisibleAsync();
        if (!track) q = q.AsNoTracking();
        return await q.FirstOrDefaultAsync(c => c.Reference == reference) ?? throw new CaseForbiddenException();
    }
}

public sealed record SlaInfo(string Tone, string Text, string? DueOn, int? DaysLeft);

public static class CaseDisplay
{
    /// <summary>Arabic day count with correct number agreement: يوم / يومان / 3 أيام / 12 يوماً.</summary>
    public static string Days(int n) => n switch
    {
        1 => "يوم واحد",
        2 => "يومان",
        >= 3 and <= 10 => $"{n} أيام",
        _ => $"{n} يوماً",
    };

    public static string LateDays(int n) => n switch
    {
        1 => "متأخر يوم",
        2 => "متأخر يومين",
        >= 3 and <= 10 => $"متأخر {n} أيام",
        _ => $"متأخر {n} يوماً",
    };

    public static SlaInfo Sla(Case c, DateOnly today)
    {
        if (c.Status == CaseStatus.Paused || c.SlaPausedAt != null)
            return new SlaInfo("paused", "المهلة موقوفة", null, null);
        if (CaseStatusInfo.IsTerminal(c.Status)) return new SlaInfo("none", "—", null, null);
        if (c.StageDueOn is not { } due)
            return new SlaInfo("ok", "ضمن المهلة", null, null);
        var left = due.DayNumber - today.DayNumber;
        var dueText = due.ToString("yyyy-MM-dd");
        if (left < 0) return new SlaInfo("err", LateDays(-left), dueText, left);
        if (left == 0) return new SlaInfo("warn", "اليوم", dueText, 0);
        if (c.Status == CaseStatus.Valuation) return new SlaInfo("info", Days(left), dueText, left);
        return new SlaInfo(left <= 2 ? "warn" : "ok", Days(left), dueText, left);
    }

    public static string Title(string ownerDisplay, string? propertyLabel) =>
        propertyLabel is null ? ownerDisplay : $"{ownerDisplay} — {propertyLabel}";

    public static string ShortName(string fullName)
    {
        var parts = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2 ? $"{parts[0]} {parts[^1][0]}." : fullName;
    }
}

public sealed record NextActionCheck(bool Ok, string Text);
public sealed record NextActionButton(string Label, string? Href, bool Enabled, string? DisabledReason, string? Action = null);
public sealed record NextAction(
    string Eyebrow, bool ForYou, string Title, string? Body, string? DueTone, string? DueText,
    IReadOnlyList<NextActionCheck> Checks, NextActionButton? Primary, NextActionButton? Secondary,
    string? AssigneeName, string? AssigneeRole);

/// <summary>Builds the single dominant next action (C04) for the caller, honoring hidden-vs-disabled rules.</summary>
public sealed class NextActionBuilder(RahoonDbContext db, RequestContext rc, IClock clock, CaseWorkflow workflow)
{
    public async Task<NextAction> BuildAsync(Case c)
    {
        var today = clock.TodayRiyadh;
        var sla = CaseDisplay.Sla(c, today);
        string? dueText = sla.DaysLeft is { } d ? (d < 0 ? CaseDisplay.LateDays(-d) : d == 0 ? "اليوم" : "متبقٍ " + CaseDisplay.Days(d)) : null;
        var dueTone = sla.Tone is "err" or "warn" ? sla.Tone : sla.Tone == "ok" ? "ok" : "info";

        switch (c.Status)
        {
            case CaseStatus.ProposedSolution:
            case CaseStatus.Negotiation:
            {
                var latest = await db.Solutions.Where(s => s.CaseId == c.Id).OrderByDescending(s => s.VersionNo).FirstOrDefaultAsync();
                if (latest is { Status: SolutionStatus.InReview })
                {
                    var preparer = await UserName(latest.PreparedByUserId);
                    var isPreparer = latest.PreparedByUserId == rc.UserId;
                    var checks = await SubmissionChecksAsync(c, latest);
                    var approver = await ApprovalRouting.ResolveApproverAsync(db, c.OrganizationId, latest, [latest.PreparedByUserId, rc.UserId]);
                    if (rc.Has(P.SolutionReview) && !isPreparer)
                    {
                        var blocked = checks.Where(x => !x.Ok).Select(x => x.Text).ToList();
                        return new NextAction("الإجراء التالي · لك", true, $"مراجعة الحل v{latest.VersionNo} وإرساله للموافقة الداخلية",
                            $"أعدّه {preparer} {latest.PreparedAt.ToOffset(TimeSpan.FromHours(3)):yyyy-MM-dd HH:mm}. بعد الإرسال يُقفل الإصدار ويُسند إلى {approver?.Name ?? "المعتمد المختص"}. لا يصل للمالك قبل الاعتماد.",
                            dueTone, dueText, checks,
                            new NextActionButton("مراجعة وإرسال…", $"/cases/{c.Reference}/solutions/{latest.VersionNo}/submit", blocked.Count == 0, blocked.Count == 0 ? null : string.Join(" · ", blocked)),
                            new NextActionButton("معاينة كما يراه المالك", $"/cases/{c.Reference}/solutions/{latest.VersionNo}/preview", true, null),
                            null, null);
                    }
                    return new NextAction("الإجراء التالي · ليس لك", false, $"مراجعة الحل v{latest.VersionNo} وإرساله للموافقة",
                        isPreparer ? "أنت مُعِدّ هذا الإصدار؛ المراجعة والاعتماد لغيرك (فصل المهام)." : null, dueTone, dueText, checks, null, null,
                        await ManagerName(c), "مديرة الحالة");
                }
                if (rc.Has(P.SolutionPrepare))
                {
                    var draft = latest is { Status: SolutionStatus.Draft };
                    var n = draft ? latest!.VersionNo : (latest?.VersionNo ?? 0) + 1;
                    return new NextAction("الإجراء التالي · لك", true, draft ? $"استكمال الحل v{n}" : $"إعداد الحل v{n}",
                        c.Status == CaseStatus.Negotiation ? "المالك قدّم عرضاً مقابلاً؛ أي إصدار جديد يمر بسلسلة الموافقة نفسها." : "حدد نوع الحل ومعاملاته، وراجع أثره على القدرة ومسار الموافقة قبل التسليم.",
                        dueTone, dueText, [], new NextActionButton(draft ? "متابعة البناء" : "بدء منشئ الحل", $"/cases/{c.Reference}/solutions/new", true, null), null, null, null);
                }
                return Generic(c, "إعداد الحل", "المحلل الائتماني", dueTone, dueText);
            }
            case CaseStatus.InternalApproval:
            {
                var req = await db.ApprovalRequests.Where(a => a.CaseId == c.Id && a.Status == Solutions.ApprovalStatus.Pending).OrderByDescending(a => a.SubmittedAt).FirstOrDefaultAsync();
                if (req is null) return Generic(c, "بانتظار قرار المعتمد", null, dueTone, dueText);
                var approverName = req.AssignedApproverUserId is { } a ? await UserName(a) : "المعتمد المختص";
                var mine = req.AssignedApproverUserId == rc.UserId && rc.Has(P.SolutionApprove);
                if (mine)
                    return new NextAction("الإجراء التالي · لك", true, req.Title, "راجع الأدلة والفروق ثم قرر بسبب مكتوب. القرار يتطلب رمز التحقق.", dueTone, dueText, [],
                        new NextActionButton("فتح طلب الموافقة", $"/approvals?request={req.Id}", true, null), null, null, null);
                var why = req.PreparedByUserId == rc.UserId || req.SubmittedByUserId == rc.UserId ? " الاعتماد مخفي عنك لأنك مُعِدّ أو مراجِع الطلب (فصل المهام)." : "";
                return new NextAction("الإجراء التالي · ليس لك", false, req.Title,
                    $"أُرسل {req.SubmittedAt.ToOffset(TimeSpan.FromHours(3)):yyyy-MM-dd HH:mm} · المهلة {req.DueOn:yyyy-MM-dd}.{why}", "info", "بانتظار المعتمد", [],
                    null, rc.Has(P.CaseView) ? new NextActionButton("إرسال تذكير", null, true, null, "remind_approver") : null, approverName, "معتمد");
            }
            default:
            {
                var actions = await workflow.AvailableActionsAsync(c);
                var main = actions.FirstOrDefault(a => a.Key is not ("pause" or "resume" or "restructure_after_breach"));
                var (title, body) = c.Status switch
                {
                    CaseStatus.Draft => ($"استكمال بيانات الحالة (الخطوة {c.DraftStep} من 6)", "الحالة مسودة ولن يصل أي تواصل للمالك قبل إنشائها."),
                    CaseStatus.AwaitingData => ("استكمال البيانات الإلزامية وبدء التحقق", "يُتاح الانتقال للتحقق عند اكتمال بيانات المالك والعقد والعقار والمديونية."),
                    CaseStatus.Verification => ("مراجعة المستندات الأساسية والتحقق", "الصك والهوية وعقد التمويل يجب أن تكون متحققاً منها قبل التقييم."),
                    CaseStatus.Valuation => ("بانتظار تقرير التقييم ومراجعته", "بعد تقرير صالح (≤ 90 يوماً) ومراجعة القانونية للرهن يُتاح إعداد الحل."),
                    CaseStatus.AwaitingCustomer => ("متابعة رد المالك على العرض", "القبول يُنشئ سجل موافقة واتفاقاً بانتظار التفعيل؛ الرفض يعيد لإعداد الحل ولا يفتح الإحالة."),
                    CaseStatus.ActiveSettlement => ("متابعة الأقساط ومطابقتها", "تُسجَّل الدفعات يدوياً وتُطابق من موظف مالية مختلف."),
                    CaseStatus.AwaitingReconciliation => ("مطابقة المتحصلات وإعداد التسوية المالية", "الإغلاق يتطلب فرقاً صفرياً أو مفسَّراً واعتماداً ثانياً."),
                    CaseStatus.Paused => ($"الحالة موقوفة: {c.PauseReason}", "المهل مجمّدة. الاستئناف يعيد الحالة إلى مرحلتها السابقة."),
                    CaseStatus.VoluntarySale => ("متابعة مسار البيع الطوعي", null),
                    CaseStatus.JudicialReferral => ("متابعة المرجع الخارجي للإحالة", "المنصة لا تدير مزاداً؛ تُسجل الحالة الرسمية كما وردت."),
                    CaseStatus.ExternalJudicialSale => ("متابعة الحالة الرسمية للبيع القضائي الخارجي", null),
                    CaseStatus.Closed => ("الحالة مغلقة", "مستندات الإغلاق متاحة للمالك."),
                    _ => ("متابعة الحالة", null),
                };
                if (main is null)
                    return new NextAction("الإجراء التالي", false, title, body, dueTone, dueText, [], null, null, await ManagerName(c), "المسؤول عن الحالة");
                var checks = main.Reasons.Select(r => new NextActionCheck(false, r)).ToList();
                return new NextAction("الإجراء التالي · لك", true, title, body, dueTone, dueText, checks,
                    new NextActionButton(main.LabelAr + (main.RequiresReason ? "…" : ""), null, main.Enabled, main.Enabled ? null : string.Join(" · ", main.Reasons), main.Key), null, null, null);
            }
        }
    }

    public async Task<List<NextActionCheck>> SubmissionChecksAsync(Case c, SolutionVersion v)
    {
        var def = CaseWorkflow.Def("submit_for_approval");
        var failures = await workflow.EvaluateGuardsAsync(c, def);
        var valuation = await db.ValuationReports.Where(r => r.CaseId == c.Id && r.Status == Assessment.ValuationStatus.Accepted).OrderByDescending(r => r.ReportDate).FirstOrDefaultAsync();
        var checks = new List<NextActionCheck>
        {
            new(!failures.Any(f => f.Contains("تقييم")), valuation is null ? "تقييم صالح" : $"تقييم صالح حتى {valuation.ValidUntil:yyyy-MM-dd}"),
            new(!failures.Any(f => f.Contains("تحليل")), "تحليل القدرة"),
            new(v.WaiverAmount == 0 || !string.IsNullOrWhiteSpace(v.Justification), "مبرر التنازل"),
            new(!failures.Any(f => f.Contains("شكوى")), "لا شكوى مفتوحة"),
        };
        return checks;
    }

    private NextAction Generic(Case c, string title, string? role, string? tone, string? due) =>
        new("الإجراء التالي · ليس لك", false, title, null, tone, due, [], null, null, null, role);

    private async Task<string> UserName(Guid id) => await db.Users.Where(u => u.Id == id).Select(u => u.FullName).FirstOrDefaultAsync() ?? "—";

    private async Task<string?> ManagerName(Case c) => c.AssignedManagerId is null ? null
        : await db.Memberships.Where(m => m.Id == c.AssignedManagerId).Select(m => m.User!.FullName).FirstOrDefaultAsync();
}
