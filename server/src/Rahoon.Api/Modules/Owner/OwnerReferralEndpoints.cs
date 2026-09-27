using Microsoft.EntityFrameworkCore;
using Rahoon.Api.Infrastructure.Http;
using Rahoon.Api.Infrastructure.Persistence;
using Rahoon.Api.Infrastructure.Tenancy;
using Rahoon.Api.Infrastructure.Time;
using Rahoon.Api.Modules.Cases;
using Rahoon.Api.Modules.Referral;

namespace Rahoon.Api.Modules.Owner;

/// <summary>
/// Owner view of a referral (B10 owner mobile screen): the pre-referral notice with the right to object, and the
/// official status exactly as the authority reported it, in calm language. Shows nothing before the notice, and never
/// the internal decision trail, agent-reported results, sync failures, exceptions or any decision-support output.
/// </summary>
public static class OwnerReferralEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/owner").RequireOwner().AddEndpointFilter(ClosedReadOnlyFilter);
        g.MapGet("/referral", Referral);
    }

    /// <summary>After closure the owner portal is read-only (the access window itself is enforced at session resolution).</summary>
    public static async ValueTask<object?> ClosedReadOnlyFilter(EndpointFilterInvocationContext ctx, EndpointFilterDelegate next)
    {
        var http = ctx.HttpContext;
        if (!HttpMethods.IsGet(http.Request.Method) && !HttpMethods.IsHead(http.Request.Method))
        {
            var rc = http.RequestServices.GetRequiredService<RequestContext>();
            var db = http.RequestServices.GetRequiredService<RahoonDbContext>();
            if (rc.OwnerCaseId is { } caseId && await db.Cases.AnyAsync(c => c.Id == caseId && c.Status == CaseStatus.Closed))
                throw new DomainException("case_closed_read_only", "حالتك مغلقة؛ يمكنك الاطلاع على مستنداتها وتنزيلها فقط.", StatusCodes.Status403Forbidden);
        }
        return await next(ctx);
    }

    private static async Task<IResult> Referral(RahoonDbContext db, RequestContext rc, IClock clock)
    {
        var c = await db.Cases.AsNoTracking().FirstOrDefaultAsync(x => x.Id == rc.OwnerCaseId) ?? throw new ForbiddenException();
        var r = await db.Referrals.AsNoTracking()
            .Where(x => x.CaseId == c.Id && x.NoticeSentAt != null && x.Status != ReferralStatus.Rejected && x.Status != ReferralStatus.Withdrawn)
            .OrderByDescending(x => x.CreatedAt).FirstOrDefaultAsync();
        if (r is null) return Results.Ok(new { visible = false });

        var today = clock.TodayRiyadh;
        var org = await db.Organizations.AsNoTracking().Where(o => o.Id == c.OrganizationId).Select(o => o.NameAr).FirstAsync();
        var objectionOpen = r.ObjectionEndsOn is { } ends && today <= ends;
        var withAuthority = c.Status is CaseStatus.JudicialReferral or CaseStatus.ExternalJudicialSale || r.OfficialStatusText is not null;
        return Results.Ok(new
        {
            visible = true,
            lenderName = org,
            title = withAuthority ? "حالة الإجراء لدى الجهة المختصة" : "إشعار مهم بشأن حالتك",
            // Verbatim text from the authority (manual entry by legal); never the agent's report or an internal status.
            officialStatus = r.OfficialStatusText is null ? null : new
            {
                text = r.OfficialStatusText,
                reportedOn = r.OfficialStatusSyncedAt?.ToOffset(TimeSpan.FromHours(3)).ToString("yyyy-MM-dd"),
                sourceLabel = "كما أبلغتنا الجهة",
            },
            message = withAuthority
                ? "يتابع الإجراءَ جهةٌ رسمية مختصة، ونعرض لك هنا ما تبلغنا به. سيُسدَّد التمويل من ثمن البيع، ويعود لك أي فائض."
                : $"يدرس {org} إحالة حالتك للجهة المختصة بعد تعذّر الوصول إلى حل ودي. هذا ليس قراراً نهائياً، ولن يُتخذ أي إجراء قبل انتهاء مهلة الاعتراض.",
            notice = new
            {
                sentOn = r.NoticeSentAt?.ToOffset(TimeSpan.FromHours(3)).ToString("yyyy-MM-dd"),
                objectionEndsOn = r.ObjectionEndsOn,
                periodDays = r.ObjectionPeriodDays,
                objectionOpen,
                daysLeft = objectionOpen ? r.ObjectionEndsOn!.Value.DayNumber - today.DayNumber : (int?)null,
            },
            rights = withAuthority
                ? new[] { "الاطلاع على ملخص المبالغ والتسوية", "تقديم اعتراض للجهة المختصة مباشرة", "التواصل مع مسؤول حالتك في المصرف" }
                : new[] { $"تقديم اعتراض خلال {CaseDisplay.Days(r.ObjectionPeriodDays)} من بوابتك، وتراجعه جهة مستقلة عن فريق حالتك", "طلب حل آخر", "الاطلاع على ملخص المبالغ", "التواصل مع مسؤول حالتك في أي وقت" },
            actions = new object[]
            {
                new { key = "message", label = "مراسلة مسؤول الحالة", route = "/owner/messages", enabled = true },
                new { key = "object", label = "تقديم اعتراض", route = "/owner/complaints?type=objection", enabled = objectionOpen && c.Status != CaseStatus.Closed },
                new { key = "debt", label = "ملخص المبالغ", route = "/owner/debt", enabled = true },
            },
        });
    }
}
