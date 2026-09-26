using Microsoft.EntityFrameworkCore;
using Rahoon.Api.Infrastructure.Http;
using Rahoon.Api.Infrastructure.Persistence;
using Rahoon.Api.Infrastructure.Tenancy;
using Rahoon.Api.Infrastructure.Time;
using Rahoon.Api.Modules.Audit;
using Rahoon.Api.Modules.Identity;

namespace Rahoon.Api.Modules.Requests;

/// <summary>
/// Loads a request for the caller. Individuals reach only their own rows through the query filter (never a system
/// scope); another individual's reference answers 404 like a missing one, and a write attempt on it is audited.
/// Rahoon team members see requests assigned to them, or all with <c>request.view_all</c>.
/// </summary>
public sealed class RequestAccess(RahoonDbContext db, RequestContext rc, AuditLog audit, IServiceScopeFactory scopes)
{
    public async Task<Request> ForApplicantAsync(string reference, bool write = false, bool track = true)
    {
        if (!rc.IsIndividual) throw new ForbiddenException();
        var q = db.Requests.Include(r => r.Institution).AsQueryable();
        if (!track) q = q.AsNoTracking();
        var r = await q.FirstOrDefaultAsync(x => x.Reference == reference);
        if (r is not null && r.ApplicantUserId == rc.UserId) return r;
        if (write) await AuditForeignWriteAsync(reference);
        throw new NotFoundException();
    }

    /// <summary>
    /// Team access: operator membership (tenant filter) + assignment, unless the member may view all. Drafts are never
    /// visible to the team — nothing is shared before the individual submits.
    /// </summary>
    public async Task<Request> ForTeamAsync(string reference, bool track = true)
    {
        if (!rc.IsOperator) throw new ForbiddenException();
        var q = db.Requests.Include(r => r.Institution).AsQueryable();
        if (!track) q = q.AsNoTracking();
        var r = await q.FirstOrDefaultAsync(x => x.Reference == reference && x.Status != RequestStatus.Draft) ?? throw new NotFoundException();
        if (!await CanSeeAsync(r))
        {
            await audit.RecordBlockedAsync(RequestWorkflow.Entry(r, "request.access_blocked", "محاولة فتح طلب غير مسند", detail: "الطلب غير مسند للعضو.", blocked: true));
            throw new ForbiddenException("هذا الطلب غير مسند إليك.");
        }
        return r;
    }

    /// <summary>Lead: all. Coordinator: assigned to them, or not yet assigned. Verifier: offers awaiting their verification (step 7).</summary>
    public async Task<bool> CanSeeAsync(Request r)
    {
        if (rc.Has(P.RequestViewAll) || r.AssignedCoordinatorId == rc.UserId) return true;
        if (r.AssignedCoordinatorId is null && rc.Has(P.RequestReview)) return true;
        return rc.Has(P.RequestOfferVerify) && await VerifierMaySeeAsync(r);
    }

    private Task<bool> VerifierMaySeeAsync(Request r) => Task.FromResult(false);

    /// <summary>An individual tried to change someone else's request: record it (own transaction), answer 404.</summary>
    private async Task AuditForeignWriteAsync(string reference)
    {
        using var scope = scopes.CreateScope();
        var other = scope.ServiceProvider.GetRequiredService<RahoonDbContext>();
        Guid? orgId;
        using (other.Request.BeginSystemScope())
            orgId = await other.Requests.Where(x => x.Reference == reference).Select(x => (Guid?)x.OrganizationId).FirstOrDefaultAsync();
        if (orgId is null) return;
        await audit.RecordBlockedAsync(new AuditEntry("request.access_blocked", "محاولة تعديل طلب لا يخص المستخدم",
            Detail: "رُفض الوصول وأُجيب بأن الطلب غير موجود.", Blocked: true, OrganizationId: orgId,
            SubjectType: "request", SubjectReference: reference));
    }
}

/// <summary>Shared request helpers: references, the operator tenant, timeline entries.</summary>
public sealed class RequestService(RahoonDbContext db, IClock clock)
{
    public async Task<string> NextReferenceAsync()
    {
        var year = clock.TodayRiyadh.Year;
        var key = $"request:{year}";
        var value = await db.Database.SqlQuery<long>($"""
            INSERT INTO cases.reference_counters (key, value) VALUES ({key}, 301)
            ON CONFLICT (key) DO UPDATE SET value = cases.reference_counters.value + 1
            RETURNING value AS "Value"
            """).ToListAsync();
        return $"REQ-{year}-{value[0]:D5}";
    }

    /// <summary>The «فريق رهون» tenant. The MVP has exactly one (ADR 0001 §4.1); provisioning it is a deployment step.</summary>
    public async Task<Guid> OperatorOrganizationIdAsync()
    {
        var id = await db.Organizations.AsNoTracking()
            .Where(o => o.Kind == OrganizationKind.Operator && o.Status == OrganizationStatus.Active)
            .OrderBy(o => o.CreatedAt).Select(o => (Guid?)o.Id).FirstOrDefaultAsync();
        return id ?? throw new DomainException("operator_missing", "خدمة الطلبات غير مهيأة بعد. حاول لاحقاً أو تواصل معنا.", StatusCodes.Status503ServiceUnavailable);
    }

    public RequestUpdate AddUpdate(Request r, string kind, string title, string? body = null, string authorKind = "system", string? authorLabel = null,
        Guid? authorUserId = null, bool visible = true)
    {
        var u = new RequestUpdate
        {
            OrganizationId = r.OrganizationId, RequestId = r.Id, ApplicantUserId = r.ApplicantUserId, Kind = kind, Title = title, Body = body,
            AuthorKind = authorKind, AuthorLabel = authorLabel, AuthorUserId = authorUserId, VisibleToApplicant = visible, At = clock.UtcNow,
        };
        db.RequestUpdates.Add(u);
        return u;
    }
}
