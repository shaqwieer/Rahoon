using Microsoft.EntityFrameworkCore;
using Rahoon.Api.Infrastructure.Http;
using Rahoon.Api.Infrastructure.Persistence;
using Rahoon.Api.Infrastructure.Tenancy;
using Rahoon.Api.Infrastructure.Time;
using Rahoon.Api.Modules.Audit;
using Rahoon.Api.Modules.Communications;
using Rahoon.Api.Modules.Identity;

namespace Rahoon.Api.Modules.Requests;

public sealed record ConcernBody(string? Kind, string? Subject, string? Text);
public sealed record ConcernAnswerBody(string? Outcome, string? Response);
public sealed record ReferralBody(string? SpecialistType, string? SpecialistName, string? Note, string? ApplicantText);
public sealed record ReasonBody(string? Reason);

/// <summary>
/// P4 «معالجة العقبات» (Phase 1A step 8, D-4 T08): the individual objects to data, amounts or a decision, or complains
/// about the service; the Rahoon team answers. A complaint is answered by a member other than the request's assigned
/// coordinator (independence, interim rule). No response time is promised (Q6/Q12). An upheld objection to
/// «غير مناسب للخدمة» can reopen the study. Specialist referral is a manual record (V9).
/// </summary>
public static class ConcernEndpoints
{
    public static readonly string[] Kinds = ["objection", "complaint"];
    public static readonly string[] Subjects = ["data", "amount", "decision", "service", "other"];
    public static readonly string[] Outcomes = ["upheld", "not_upheld", "clarified"];
    public static readonly string[] SpecialistTypes = ["legal", "financial_counselling", "social_support", "other"];

    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/my/requests/{reference}/concerns", Raise).RequireIndividual().Idempotent();

        var team = app.MapGroup("/api/team").RequireOrg(OrganizationKind.Operator);
        team.MapGet("/concerns", Queue).RequirePermission(P.RequestObjectionHandle);
        team.MapPost("/concerns/{id:guid}/answer", Answer).RequirePermission(P.RequestObjectionHandle).Idempotent();
        team.MapPost("/requests/{reference}/reopen", Reopen).RequirePermission(P.RequestObjectionHandle).Idempotent();
        team.MapPost("/requests/{reference}/referrals", Refer).RequirePermission(P.RequestReview).Idempotent();
    }

    private static string? Clean(string? s, int max) => TeamRequestEndpoints.Clean(s, max);

    private static async Task<string> NextReferenceAsync(RahoonDbContext db, IClock clock, string kind)
    {
        var year = clock.TodayRiyadh.Year;
        var prefix = kind == "complaint" ? "CMP" : "OBJ";
        var key = $"request-{kind}:{year}";
        var value = await db.Database.SqlQuery<long>($"""
            INSERT INTO cases.reference_counters (key, value) VALUES ({key}, 1)
            ON CONFLICT (key) DO UPDATE SET value = cases.reference_counters.value + 1
            RETURNING value AS "Value"
            """).ToListAsync();
        return $"{prefix}-{year}-{value[0]:D5}";
    }

    // ───────── individual ─────────

    private static async Task<IResult> Raise(string reference, ConcernBody b, RequestAccess access, RahoonDbContext db, RequestContext rc,
        RequestService svc, IClock clock, AuditLog audit)
    {
        var text = Clean(b.Text, 2000);
        new Validator()
            .Require(b.Kind is not null && Kinds.Contains(b.Kind), "kind", "اختر: اعتراض أو شكوى.")
            .Require(b.Subject is not null && Subjects.Contains(b.Subject), "subject", "اختر موضوع ما تعترض عليه أو تشكو منه.")
            .Require(text is not null, "text", "اكتب ما حدث وما تراه صحيحاً.")
            .ThrowIfInvalid();
        await using var tx = await db.Database.BeginTransactionAsync();
        var r = await access.ForApplicantAsync(reference, write: true);
        if (r.Status == RequestStatus.Draft) throw new ConflictException("draft", "الطلب لم يُرسل بعد؛ عدّل بياناته مباشرة.");
        var concern = new RequestConcern
        {
            OrganizationId = r.OrganizationId, RequestId = r.Id, ApplicantUserId = r.ApplicantUserId, Reference = await NextReferenceAsync(db, clock, b.Kind!),
            Kind = b.Kind!, Subject = b.Subject!, Text = text!,
        };
        db.RequestConcerns.Add(concern);
        var label = b.Kind == "complaint" ? "شكوى" : "اعتراض";
        svc.AddUpdate(r, "concern", $"قدّمت {label} برقم ⁨{concern.Reference}⁩", text, authorKind: "applicant");
        await audit.RecordAsync(RequestWorkflow.Entry(r, "request.concern_raised", $"{label} من العميل ({concern.Reference})", detail: b.Subject));
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        var message = b.Kind == "complaint"
            ? $"وصلت شكواك برقم {concern.Reference}. يراجعها عضو في فريق رهون غير المنسق المسؤول عن طلبك، ونبلغك بالرد في صفحة طلبك."
            : $"وصل اعتراضك برقم {concern.Reference} إلى فريق رهون، ونبلغك بالرد في صفحة طلبك.";
        return Results.Ok(new { concern.Reference, message });
    }

    // ───────── T08 team ─────────

    private static async Task<IResult> Queue(string? tab, RahoonDbContext db, RequestContext rc, IClock clock)
    {
        var answered = tab == "answered";
        var rows = await db.RequestConcerns.AsNoTracking()
            .Where(c => answered ? c.Status == RequestConcernStatus.Answered : c.Status == RequestConcernStatus.Open)
            .Join(db.Requests.Include(r => r.Institution), c => c.RequestId, r => r.Id, (c, r) => new { c, r })
            .OrderBy(x => x.c.CreatedAt).Take(200).ToListAsync();
        var now = clock.UtcNow;
        return Results.Ok(new
        {
            tab = answered ? "answered" : "open",
            items = rows.Select(x => new
            {
                x.c.Id, x.c.Reference, x.c.Kind, x.c.Subject, x.c.Text, x.c.CreatedAt, x.c.Outcome, x.c.ResponseText, x.c.RespondedByLabel, x.c.RespondedAt,
                requestReference = x.r.Reference, requestStatus = RequestStatusInfo.Key(x.r.Status), institutionName = x.r.InstitutionDisplayName,
                applicantName = x.r.ApplicantFullName is { Length: > 0 } n ? Infrastructure.Security.Mask.PersonName(n) : "—",
                // Independence (interim): the request's own coordinator does not answer a complaint about it.
                mayAnswer = !(x.c.Kind == "complaint" && x.r.AssignedCoordinatorId == rc.UserId),
                daysOpen = (int)Math.Floor((now - x.c.CreatedAt).TotalDays),
            }),
        });
    }

    private static async Task<IResult> Answer(Guid id, ConcernAnswerBody b, RahoonDbContext db, RequestContext rc, RequestService svc, IClock clock,
        AuditLog audit, Notifier notifier)
    {
        var response = Clean(b.Response, 2000);
        new Validator()
            .Require(b.Outcome is not null && Outcomes.Contains(b.Outcome), "outcome", "اختر نتيجة المراجعة.")
            .Require(response is not null, "response", "اكتب الرد كما سيراه العميل.")
            .ThrowIfInvalid();
        await using var tx = await db.Database.BeginTransactionAsync();
        var c = await db.RequestConcerns.FirstOrDefaultAsync(x => x.Id == id) ?? throw new NotFoundException();
        var r = await db.Requests.FirstAsync(x => x.Id == c.RequestId);
        if (c.Status != RequestConcernStatus.Open) throw new ConflictException("answered", "رُدّ على هذا مسبقاً.");
        if (c.Kind == "complaint" && r.AssignedCoordinatorId == rc.UserId)
        {
            await audit.RecordBlockedAsync(RequestWorkflow.Entry(r, "request.concern_answer_blocked", $"محاولة الرد على شكوى {c.Reference} من المنسق المسند",
                detail: "المانع: يرد على الشكوى عضو غير المنسق المسؤول عن الطلب. لم يُسجل الرد.", blocked: true));
            throw new ForbiddenException("يرد على هذه الشكوى عضو آخر غير المنسق المسؤول عن الطلب.");
        }
        c.Status = RequestConcernStatus.Answered;
        c.Outcome = b.Outcome;
        c.ResponseText = response;
        c.RespondedByUserId = rc.UserId;
        c.RespondedByLabel = rc.UserName;
        c.RespondedAt = clock.UtcNow;
        var label = c.Kind == "complaint" ? "شكواك" : "اعتراضك";
        svc.AddUpdate(r, "concern_answer", $"رد فريق رهون على {label} (⁨{c.Reference}⁩)", response, authorKind: "team", authorLabel: "فريق رهون", authorUserId: rc.UserId);
        notifier.Notify(r.ApplicantUserId, null, "request", $"رد على {label}", response, $"/my/requests/{r.Reference}");
        await audit.RecordAsync(RequestWorkflow.Entry(r, "request.concern_answered", $"الرد على {c.Reference}", detail: b.Outcome));
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Results.Ok(new { status = "answered" });
    }

    private static async Task<IResult> Reopen(string reference, ReasonBody b, RequestAccess access, RahoonDbContext db, RequestContext rc,
        RequestWorkflow workflow, RequestService svc, Notifier notifier)
    {
        await using var tx = await db.Database.BeginTransactionAsync();
        var r = await access.ForTeamAsync(reference);
        var upheld = await db.RequestConcerns.AnyAsync(c => c.RequestId == r.Id && c.Kind == "objection" && c.Outcome == "upheld");
        await workflow.TransitionAsync(r, "reopen", Clean(b.Reason, 1000), RequestStatus.NotEligible,
            extraGuardFailures: upheld ? null : ["لا يوجد اعتراض مقبول من العميل على هذا القرار."]);
        r.NotEligibleReason = null;
        svc.AddUpdate(r, "status", "أعاد فريق رهون فتح دراسة طلبك بعد اعتراضك", authorKind: "team", authorLabel: "فريق رهون", authorUserId: rc.UserId);
        notifier.Notify(r.ApplicantUserId, null, "request", "أُعيد فتح دراسة طلبك", null, $"/my/requests/{r.Reference}");
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Results.Ok(new { status = RequestStatusInfo.Key(r.Status) });
    }

    private static async Task<IResult> Refer(string reference, ReferralBody b, RequestAccess access, RahoonDbContext db, RequestContext rc,
        RequestService svc, IClock clock, AuditLog audit)
    {
        var name = Clean(b.SpecialistName, 200);
        var text = Clean(b.ApplicantText, 1000);
        new Validator()
            .Require(b.SpecialistType is not null && SpecialistTypes.Contains(b.SpecialistType), "specialistType", "اختر نوع المختص.")
            .Require(name is not null, "specialistName", "اكتب اسم المختص أو الجهة.")
            .Require(text is not null, "applicantText", "اكتب ما سيراه العميل عن الإحالة.")
            .ThrowIfInvalid();
        var r = await access.ForTeamAsync(reference);
        if (r.AssignedCoordinatorId != rc.UserId && !rc.Has(P.RequestViewAll)) throw new ForbiddenException("هذا الطلب مسند لعضو آخر.");
        db.SpecialistReferrals.Add(new SpecialistReferral
        {
            OrganizationId = r.OrganizationId, RequestId = r.Id, ApplicantUserId = r.ApplicantUserId, SpecialistType = b.SpecialistType!, SpecialistName = name!,
            Note = Clean(b.Note, 1000), ApplicantText = text!, RecordedByUserId = rc.UserId, RecordedByLabel = rc.UserName, At = clock.UtcNow,
        });
        svc.AddUpdate(r, "referral", "أحالك فريق رهون إلى مختص", text, authorKind: "team", authorLabel: "فريق رهون", authorUserId: rc.UserId);
        await audit.RecordAsync(RequestWorkflow.Entry(r, "request.specialist_referral", $"تسجيل إحالة إلى مختص ({b.SpecialistType})", detail: name));
        await db.SaveChangesAsync();
        return Results.Ok(new { ok = true });
    }
}
