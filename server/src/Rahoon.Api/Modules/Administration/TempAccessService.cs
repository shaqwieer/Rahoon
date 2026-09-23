using System.Text.RegularExpressions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Rahoon.Api.Infrastructure.Http;
using Rahoon.Api.Infrastructure.Persistence;
using Rahoon.Api.Infrastructure.Security;
using Rahoon.Api.Infrastructure.Tenancy;
using Rahoon.Api.Infrastructure.Time;
using Rahoon.Api.Modules.Audit;
using Rahoon.Api.Modules.Cases;
using Rahoon.Api.Modules.Communications;
using Rahoon.Api.Modules.Identity;

namespace Rahoon.Api.Modules.Administration;

public sealed record TempAccessCreateRequest(string Handle, string Reason, string SupportTicketRef, int DurationMinutes);
public sealed record TempAccessDecisionRequest(string? Reason);

/// <summary>
/// Audited temporary support access (PA06): a platform support user asks for read-only access to one case
/// (reason + SUP ticket, ≤ 4 h). It becomes active only after BOTH an org_admin of the case's institution and a
/// platform auditor approve — neither may be the requester. It expires automatically; every screen opened is
/// logged, and the institution is notified after expiry. While active the middleware adds the tenant to the
/// requester's readable organizations, so views here read only <see cref="TempAccessRequest.CaseId"/>, read-only.
/// </summary>
public sealed partial class TempAccessService(RahoonDbContext db, RequestContext rc, IClock clock, AuditLog audit, Notifier notifier,
    IDataProtectionProvider protection, IConfiguration config)
{
    public const int MaxDurationMinutes = 240;
    public const string ScopeLabel = "قراءة فقط · تبويب المستندات والقوالب · بلا تنزيل";
    public static readonly int[] Durations = [30, 60, 120, 240];

    private IDataProtector Handles => protection.CreateProtector("Rahoon.PlatformMonitor.CaseHandle.v1");

    [GeneratedRegex(@"^SUP-\d{4}-\d{4}$")]
    private static partial Regex TicketRegex();

    public string HandleFor(Guid caseId) => Handles.Protect(caseId.ToString("N"));

    public string MaskedId(Guid caseId) => Mask.OpaqueCaseId(caseId, PlatformMasking.Salt(config));

    private Guid? ResolveHandle(string? handle)
    {
        try { return Guid.TryParseExact(Handles.Unprotect(handle ?? ""), "N", out var id) ? id : null; }
        catch (System.Security.Cryptography.CryptographicException) { return null; }
    }

    public static string DurationLabel(int minutes) => minutes switch
    {
        30 => "30 دقيقة", 60 => "ساعة", 120 => "ساعتان", 180 => "3 ساعات", 240 => "4 ساعات", _ => $"{minutes} دقيقة",
    };

    // ───────── Request ─────────

    public async Task<TempAccessRequest> RequestAsync(TempAccessCreateRequest req)
    {
        var ticket = req.SupportTicketRef?.Trim().ToUpperInvariant() ?? "";
        new Validator()
            .Require(!string.IsNullOrWhiteSpace(req.Reason) && req.Reason.Trim().Length is >= 10 and <= 1000, "reason", "اكتب السبب بوضوح (10 أحرف على الأقل).")
            .Require(TicketRegex().IsMatch(ticket), "supportTicketRef", "اربط الطلب بطلب دعم بصيغة SUP-YYYY-NNNN.")
            .Require(req.DurationMinutes is >= 15 and <= MaxDurationMinutes, "durationMinutes", $"المدة بين 15 دقيقة و{MaxDurationMinutes / 60} ساعات.")
            .ThrowIfInvalid();
        var caseId = ResolveHandle(req.Handle) ?? throw new NotFoundException();
        Guid orgId;
        using (rc.BeginSystemScope())
            orgId = await db.Cases.Where(c => c.Id == caseId).Select(c => (Guid?)c.OrganizationId).FirstOrDefaultAsync() ?? throw new NotFoundException();

        var now = clock.UtcNow;
        if (await db.TempAccessRequests.AnyAsync(t => t.RequesterUserId == rc.UserId && t.CaseId == caseId
                                                     && (t.Status == TempAccessStatus.Pending || (t.Status == TempAccessStatus.Active && t.ExpiresAt > now))))
            throw new ConflictException("request_open", "لديك طلب وصول مفتوح لهذه الحالة.");

        var request = new TempAccessRequest
        {
            OrganizationId = orgId, CaseId = caseId, MaskedCaseId = MaskedId(caseId), RequesterUserId = rc.UserId,
            Reason = req.Reason.Trim(), SupportTicketRef = ticket, DurationMinutes = req.DurationMinutes, CreatedAt = now,
        };
        db.TempAccessRequests.Add(request);
        var orgName = await db.Organizations.Where(o => o.Id == orgId).Select(o => o.NameAr).FirstAsync();
        var detail = $"{request.MaskedCaseId} · {orgName} · {DurationLabel(request.DurationMinutes)} · قراءة · {ticket}";
        await audit.RecordAsync(new AuditEntry("temp_access.requested", "طلب وصول دعم مؤقت", Reason: request.Reason, Detail: detail));
        await audit.RecordAsync(new AuditEntry("temp_access.requested", "طلب وصول دعم مؤقت من فريق رهون", Reason: request.Reason,
            Detail: $"{request.MaskedCaseId} · {DurationLabel(request.DurationMinutes)} · {ScopeLabel} · بانتظار موافقتكم", OrganizationId: orgId));
        foreach (var u in await OrgAdminsAsync(orgId))
            notifier.Notify(u, orgId, "security", "طلب وصول دعم مؤقت بانتظار موافقتك", $"{request.MaskedCaseId} · {ticket} · {DurationLabel(request.DurationMinutes)}", "/settings/temp-access", tone: "warn");
        foreach (var u in await PlatformAuditorsAsync())
            notifier.Notify(u, rc.OrganizationId, "security", "طلب وصول مؤقت بانتظار اعتماد المدقق", detail, "/platform/cases/temp-access", tone: "warn");
        return request;
    }

    private async Task<List<Guid>> OrgAdminsAsync(Guid orgId)
    {
        using var _ = rc.BeginSystemScope();
        return await db.Memberships.Where(m => m.OrganizationId == orgId && m.Status == MembershipStatus.Active && m.Roles.Any(r => r.Role!.Key == SystemRoles.OrgAdmin))
            .Select(m => m.UserId).ToListAsync();
    }

    private async Task<List<Guid>> PlatformAuditorsAsync()
    {
        using var _ = rc.BeginSystemScope();
        return await db.Memberships.Where(m => m.Organization!.Kind == OrganizationKind.Platform && m.Status == MembershipStatus.Active
                                               && m.Roles.Any(r => r.Role!.Permissions.Any(p => p.PermissionKey == P.PlatformTempAccessApprove)))
            .Select(m => m.UserId).ToListAsync();
    }

    // ───────── Decisions ─────────

    public async Task<TempAccessRequest> ApproveAsync(Guid id, bool asInstitution)
    {
        EndpointAccess.EnsureStepUp(rc, clock);
        await ExpireDueAsync();
        var t = await db.TempAccessRequests.FirstOrDefaultAsync(x => x.Id == id && (!asInstitution || x.OrganizationId == rc.OrganizationId)) ?? throw new NotFoundException();
        if (t.Status != TempAccessStatus.Pending) throw new ConflictException("not_pending", "الطلب ليس بانتظار الموافقة.");
        if (t.RequesterUserId == rc.UserId)
            throw new DomainException("maker_checker", "لا يوافق مقدم الطلب على طلبه؛ يلزم مسؤول المنشأة ومدقق المنصة.", StatusCodes.Status403Forbidden);
        var now = clock.UtcNow;
        if (asInstitution)
        {
            if (!rc.RoleKeys.Contains(SystemRoles.OrgAdmin)) throw new ForbiddenException("يوافق على الوصول المؤقت مسؤول المنشأة.");
            if (t.InstitutionApproverUserId is not null) throw new ConflictException("already_approved", "وافقت المنشأة مسبقاً.");
            t.InstitutionApproverUserId = rc.UserId;
            t.InstitutionApprovedAt = now;
        }
        else
        {
            if (t.AuditorApproverUserId is not null) throw new ConflictException("already_approved", "وافق مدقق المنصة مسبقاً.");
            t.AuditorApproverUserId = rc.UserId;
            t.AuditorApprovedAt = now;
        }
        await audit.RecordAsync(new AuditEntry("temp_access.approval", asInstitution ? "موافقة المنشأة على وصول دعم مؤقت" : "موافقة مدقق المنصة على وصول دعم مؤقت",
            Detail: t.MaskedCaseId, OrganizationId: t.OrganizationId));

        if (t is { InstitutionApproverUserId: not null, AuditorApproverUserId: not null })
        {
            t.Status = TempAccessStatus.Active;
            t.StartsAt = now;
            t.ExpiresAt = now.AddMinutes(t.DurationMinutes);
            var orgName = await db.Organizations.Where(o => o.Id == t.OrganizationId).Select(o => o.NameAr).FirstAsync();
            var detail = $"{t.MaskedCaseId} · {DurationLabel(t.DurationMinutes)} · قراءة · ينتهي {t.ExpiresAt.Value.ToOffset(TimeSpan.FromHours(3)):HH:mm}";
            var platformOrg = await PlatformOrgIdAsync();
            await audit.RecordAsync(new AuditEntry("temp_access.granted", "منح وصول مؤقت", Reason: t.Reason, Detail: $"{orgName} · {detail}", OrganizationId: platformOrg));
            await audit.RecordAsync(new AuditEntry("temp_access.granted", "منح وصول دعم مؤقت لفريق رهون", Reason: t.Reason, Detail: detail, OrganizationId: t.OrganizationId));
            notifier.Notify(t.RequesterUserId, platformOrg, "security", "فُعّل الوصول المؤقت", detail, $"/platform/cases/temp-access/{t.Id}", tone: "ok");
        }
        return t;
    }

    public async Task<TempAccessRequest> RejectAsync(Guid id, bool asInstitution, string? reason)
    {
        new Validator().Require(!string.IsNullOrWhiteSpace(reason), "reason", "اكتب سبب الرفض.").ThrowIfInvalid();
        var t = await db.TempAccessRequests.FirstOrDefaultAsync(x => x.Id == id && (!asInstitution || x.OrganizationId == rc.OrganizationId)) ?? throw new NotFoundException();
        if (t.Status != TempAccessStatus.Pending) throw new ConflictException("not_pending", "الطلب ليس بانتظار الموافقة.");
        if (asInstitution && !rc.RoleKeys.Contains(SystemRoles.OrgAdmin)) throw new ForbiddenException("يقرر الوصول المؤقت مسؤول المنشأة.");
        if (t.RequesterUserId == rc.UserId) throw new DomainException("maker_checker", "لا يقرر مقدم الطلب طلبه.", StatusCodes.Status403Forbidden);
        t.Status = TempAccessStatus.Rejected;
        t.RejectedByUserId = rc.UserId;
        t.DecisionNote = reason!.Trim();
        t.ClosedAt = clock.UtcNow;
        notifier.Notify(t.RequesterUserId, await PlatformOrgIdAsync(), "security", "رُفض طلب الوصول المؤقت", t.DecisionNote, "/platform/cases", tone: "warn");
        await audit.RecordAsync(new AuditEntry("temp_access.rejected", "رفض طلب وصول مؤقت", Reason: t.DecisionNote, Detail: t.MaskedCaseId, OrganizationId: t.OrganizationId));
        return t;
    }

    public async Task<TempAccessRequest> RevokeAsync(Guid id, string? reason)
    {
        var t = await db.TempAccessRequests.FirstOrDefaultAsync(x => x.Id == id) ?? throw new NotFoundException();
        if (t.RequesterUserId != rc.UserId && !rc.Has(P.PlatformTempAccessApprove)) throw new NotFoundException();
        if (t.Status is not (TempAccessStatus.Pending or TempAccessStatus.Active)) throw new ConflictException("closed", "الطلب مغلق.");
        var wasActive = t.Status == TempAccessStatus.Active;
        t.Status = TempAccessStatus.Revoked;
        t.ClosedAt = clock.UtcNow;
        t.DecisionNote = reason?.Trim();
        if (wasActive) t.ExpiresAt = clock.UtcNow;
        await audit.RecordAsync(new AuditEntry("temp_access.revoked", wasActive ? "إنهاء وصول مؤقت قبل موعده" : "سحب طلب وصول مؤقت", Reason: t.DecisionNote,
            Detail: t.MaskedCaseId, OrganizationId: t.OrganizationId));
        if (wasActive) await NotifyInstitutionClosedAsync(t, "أُنهي وصول الدعم المؤقت");
        return t;
    }

    private async Task<Guid?> PlatformOrgIdAsync() =>
        await db.Organizations.Where(o => o.Kind == OrganizationKind.Platform).Select(o => (Guid?)o.Id).FirstOrDefaultAsync();

    // ───────── Expiry ─────────

    /// <summary>Marks overdue active grants expired and notifies the institution (idempotent; called lazily and by the job).</summary>
    public async Task<int> ExpireDueAsync()
    {
        var now = clock.UtcNow;
        List<TempAccessRequest> due;
        using (rc.BeginSystemScope())
            due = await db.TempAccessRequests.Where(t => t.Status == TempAccessStatus.Active && t.ExpiresAt <= now).ToListAsync();
        if (due.Count == 0) return 0;
        // The audit chain is serialised per organization only inside a transaction.
        await using var own = db.Database.CurrentTransaction is null ? await db.Database.BeginTransactionAsync() : null;
        foreach (var t in due)
        {
            t.Status = TempAccessStatus.Expired;
            t.ClosedAt = now;
            await audit.RecordAsync(new AuditEntry("temp_access.expired", "انتهاء وصول مؤقت آلياً", Detail: t.MaskedCaseId, OrganizationId: t.OrganizationId));
            await NotifyInstitutionClosedAsync(t, "انتهى وصول الدعم المؤقت");
        }
        using (rc.BeginSystemScope()) await db.SaveChangesAsync();
        if (own is not null) await own.CommitAsync();
        return due.Count;
    }

    private async Task NotifyInstitutionClosedAsync(TempAccessRequest t, string title)
    {
        var views = await db.Set<TempAccessViewLog>().CountAsync(v => v.RequestId == t.Id);
        foreach (var u in await OrgAdminsAsync(t.OrganizationId))
            notifier.Notify(u, t.OrganizationId, "security", title, $"{t.MaskedCaseId} · فُتحت {views} شاشة للقراءة فقط · {t.SupportTicketRef}", "/settings/temp-access");
        t.InstitutionNotifiedAt = clock.UtcNow;
    }

    // ───────── Read-only view under an active grant ─────────

    public async Task<object> ViewAsync(Guid id, string? screen)
    {
        await ExpireDueAsync();
        var now = clock.UtcNow;
        var t = await db.TempAccessRequests.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && x.RequesterUserId == rc.UserId) ?? throw new NotFoundException();
        if (t.Status != TempAccessStatus.Active || t.ExpiresAt <= now)
            throw new DomainException("temp_access_inactive", "لا يوجد وصول مؤقت نشط لهذه الحالة.", StatusCodes.Status403Forbidden);
        var s = screen is "documents" or "templates" ? screen : "overview";

        // Explicitly scoped to the granted case; projections only (read-only, no files).
        var c = await db.Cases.AsNoTracking().Where(x => x.Id == t.CaseId && x.OrganizationId == t.OrganizationId)
            .Select(x => new { x.Reference, x.Status, x.StatusChangedAt }).FirstOrDefaultAsync() ?? throw new NotFoundException();
        object? content = s switch
        {
            "documents" => await db.Documents.AsNoTracking().Where(d => d.CaseId == t.CaseId && !d.Internal).OrderBy(d => d.CreatedAt)
                .Select(d => new { d.Name, d.DocumentTypeKey, status = d.Status, versions = d.VersionCount, d.ValidUntil, download = false }).ToListAsync(),
            "templates" => await db.Templates.AsNoTracking().Where(x => x.Status == TemplateStatus.Published && (x.OrganizationId == null || x.OrganizationId == t.OrganizationId))
                .OrderBy(x => x.Code).Select(x => new { x.Code, x.VersionNo, x.Title, x.BodyAr, x.BodySms, source = x.OrganizationId == null ? "platform" : "institution" }).ToListAsync(),
            _ => null,
        };
        db.Set<TempAccessViewLog>().Add(new TempAccessViewLog { RequestId = t.Id, OrganizationId = t.OrganizationId, UserId = rc.UserId, Screen = s, At = now });
        await audit.RecordAsync(new AuditEntry("temp_access.viewed", $"فتح شاشة أثناء وصول مؤقت: {s}", Detail: $"{t.MaskedCaseId} · {t.SupportTicketRef}", OrganizationId: t.OrganizationId));
        await db.SaveChangesAsync();
        return new
        {
            requestId = t.Id, maskedId = t.MaskedCaseId, reference = c.Reference, status = CaseStatusInfo.Of(c.Status).LabelAr, statusKey = CaseStatusInfo.Key(c.Status),
            screen = s, scope = ScopeLabel, readOnly = true, expiresAt = t.ExpiresAt, content,
        };
    }
}

/// <summary>Expires temporary grants every minute (disabled in tests: they expire lazily through the endpoints).</summary>
public sealed class TempAccessExpiryService(IServiceScopeFactory scopes, IConfiguration config, ILogger<TempAccessExpiryService> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!config.GetValue("Jobs:TempAccessExpiry", true)) return;
        await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopes.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<RahoonDbContext>();
                using (db.Request.BeginSystemScope())
                {
                    await using var tx = await db.Database.BeginTransactionAsync(stoppingToken);
                    var n = await scope.ServiceProvider.GetRequiredService<TempAccessService>().ExpireDueAsync();
                    await JobHeartbeats.TouchAsync(db, "temp_access_expiry", scope.ServiceProvider.GetRequiredService<IClock>().UtcNow, $"expired {n}");
                    await tx.CommitAsync(stoppingToken);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                log.LogError(ex, "Temp access expiry run failed");
            }
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }
}

public static class JobHeartbeats
{
    public static async Task TouchAsync(RahoonDbContext db, string key, DateTimeOffset at, string? result)
    {
        var hb = await db.Set<JobHeartbeat>().FirstOrDefaultAsync(h => h.Key == key);
        if (hb is null) db.Set<JobHeartbeat>().Add(hb = new JobHeartbeat { Key = key });
        hb.LastRunAt = at;
        hb.LastResult = result;
        await db.SaveChangesAsync();
    }
}
