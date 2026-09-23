using Microsoft.EntityFrameworkCore;
using Rahoon.Api.Infrastructure.Http;
using Rahoon.Api.Infrastructure.Persistence;
using Rahoon.Api.Infrastructure.Tenancy;
using Rahoon.Api.Infrastructure.Time;
using Rahoon.Api.Modules.Audit;
using Rahoon.Api.Modules.Identity;

namespace Rahoon.Api.Modules.Communications;

public sealed record TemplateDraftRequest(string BodyAr, string? BodyEn, string? BodySms);
public sealed record ToneCheckRequest(string? BodyAr, string? BodyEn);
public sealed record TemplateReturnRequest(string? Reason);

/// <summary>
/// Communication templates, institution (A05) and platform base (PA09). Templates are versioned and not tenant-filtered
/// by EF (OrganizationId null = platform base), so every query here scopes explicitly. An institution edit creates an
/// org-owned version; the effective template for a tenant is its latest published copy, else the latest base.
/// Flow: draft → pending_compliance → published by a template.publish holder who is not the editor; the previous
/// published version of the same scope becomes superseded.
/// </summary>
public sealed class TemplateService(RahoonDbContext db, RequestContext rc, IClock clock, AuditLog audit)
{
    private static readonly TemplateStatus[] Open = [TemplateStatus.Draft, TemplateStatus.PendingCompliance];

    private IQueryable<CommunicationTemplate> Visible(Guid? org) => db.Templates.Where(t => t.OrganizationId == null || t.OrganizationId == org);

    public async Task<IReadOnlyList<object>> ListAsync(Guid? org)
    {
        var all = await Visible(org).AsNoTracking().ToListAsync();
        var since = clock.UtcNow.AddDays(-90);
        var usage = await db.OutboundMessages.AsNoTracking().Where(m => m.TemplateCode != null && m.CreatedAt >= since && (org == null || m.OrganizationId == org))
            .GroupBy(m => m.TemplateCode!).Select(g => new { g.Key, N = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.N);
        return all.GroupBy(t => t.Code).OrderBy(g => g.Key).Select(g =>
        {
            var effective = Effective(g, org);
            var draft = OpenVersion(g, org);
            return (object)new
            {
                code = g.Key, title = (effective ?? draft)?.Title, audience = (effective ?? draft)?.Audience,
                effectiveVersion = effective?.VersionNo, source = effective?.OrganizationId is null ? "platform" : "institution",
                draft = draft is null ? null : new { version = draft.VersionNo, status = draft.Status },
                usage90d = usage.GetValueOrDefault(g.Key), updatedAt = (draft ?? effective)?.UpdatedAt,
            };
        }).ToList();
    }

    private static CommunicationTemplate? Effective(IEnumerable<CommunicationTemplate> versions, Guid? org) =>
        versions.Where(t => t.Status == TemplateStatus.Published && t.OrganizationId == org).MaxBy(t => t.VersionNo)
        ?? versions.Where(t => t.Status == TemplateStatus.Published && t.OrganizationId == null).MaxBy(t => t.VersionNo);

    private static CommunicationTemplate? OpenVersion(IEnumerable<CommunicationTemplate> versions, Guid? org) =>
        versions.Where(t => t.OrganizationId == org && Open.Contains(t.Status)).MaxBy(t => t.VersionNo);

    private async Task<List<CommunicationTemplate>> VersionsAsync(Guid? org, string code, bool track = false)
    {
        var q = Visible(org).Where(t => t.Code == code);
        if (!track) q = q.AsNoTracking();
        var list = await q.ToListAsync();
        return list.Count == 0 ? throw new NotFoundException() : list;
    }

    public async Task<object> GetAsync(Guid? org, string code)
    {
        var versions = await VersionsAsync(org, code);
        var effective = Effective(versions, org);
        var draft = OpenVersion(versions, org);
        var current = draft ?? effective ?? versions.MaxBy(v => v.VersionNo)!;
        var editorId = current.LastEditedByUserId ?? current.PublishedByUserId;
        var editor = editorId is { } e ? await db.Users.Where(u => u.Id == e).Select(u => u.FullName).FirstOrDefaultAsync() : null;
        var since = clock.UtcNow.AddDays(-90);
        var usage = await db.OutboundMessages.CountAsync(m => m.TemplateCode == code && m.CreatedAt >= since && (org == null || m.OrganizationId == org));
        return new
        {
            code, current.Title, current.Audience, header = $"«{current.Title}» · {(current.Audience == "owner" ? "للمالك" : current.Audience)}",
            codeLabel = $"{code} · v{current.VersionNo}", version = current.VersionNo, status = current.Status,
            bodyAr = current.BodyAr, bodyEn = current.BodyEn, bodySms = current.BodySms, variables = current.Variables,
            effective = effective is null ? null : new { version = effective.VersionNo, source = effective.OrganizationId is null ? "platform" : "institution", effective.BodyAr },
            draft = draft is null ? null : new { version = draft.VersionNo, status = draft.Status, editedBySelf = draft.LastEditedByUserId == rc.UserId },
            tone = TemplateRules.Tone(current.BodyAr, current.BodyEn, effective?.BodyAr, effective?.BodyEn),
            usage = $"أُرسل {usage} مرة خلال 90 يوماً", lastEdit = editor is null ? null : $"آخر تعديل: {editor} · {current.UpdatedAt.ToOffset(TimeSpan.FromHours(3)):yyyy-MM-dd}",
            canPublish = draft is not null && draft.LastEditedByUserId != rc.UserId,
        };
    }

    public async Task<IReadOnlyList<ToneCheck>> ToneAsync(Guid? org, string code, ToneCheckRequest req)
    {
        var versions = await VersionsAsync(org, code);
        var effective = Effective(versions, org);
        var current = OpenVersion(versions, org) ?? effective ?? versions.MaxBy(v => v.VersionNo)!;
        return TemplateRules.Tone(req.BodyAr ?? current.BodyAr, req.BodyAr is null ? current.BodyEn : req.BodyEn, effective?.BodyAr, effective?.BodyEn);
    }

    public async Task<CommunicationTemplate> SaveDraftAsync(Guid? org, string code, TemplateDraftRequest req)
    {
        var versions = await VersionsAsync(org, code, track: true);
        var effective = Effective(versions, org);
        var draft = OpenVersion(versions, org);
        var basis = draft ?? effective ?? versions.MaxBy(v => v.VersionNo)!;
        var bodyAr = req.BodyAr?.Trim() ?? "";
        var v = new Validator()
            .Require(bodyAr.Length is > 0 and <= 1000, "bodyAr", "اكتب نص القالب بالعربية (حتى 1000 حرف).")
            .Require((req.BodySms?.Length ?? 0) <= 320, "bodySms", "الرسالة النصية حتى 320 حرفاً.")
            .Require((req.BodyEn?.Length ?? 0) <= 1500, "bodyEn", "النص الإنجليزي حتى 1500 حرف.");
        foreach (var (field, msgs) in TemplateRules.UnknownVariables(basis.Variables, bodyAr, req.BodyEn, req.BodySms))
            v.Require(false, field, msgs[0]);
        v.ThrowIfInvalid();

        if (draft is null)
        {
            // Institution versions continue after the highest base or own version; base versions after base ones.
            var next = versions.Max(t => t.VersionNo) + 1;
            draft = new CommunicationTemplate
            {
                OrganizationId = org, Code = code, Title = basis.Title, Audience = basis.Audience, VersionNo = next, Variables = [.. basis.Variables],
                BodyAr = bodyAr, Status = TemplateStatus.Draft,
            };
            db.Templates.Add(draft);
        }
        draft.BodyAr = bodyAr;
        draft.BodyEn = string.IsNullOrWhiteSpace(req.BodyEn) ? null : req.BodyEn.Trim();
        draft.BodySms = string.IsNullOrWhiteSpace(req.BodySms) ? null : req.BodySms.Trim();
        draft.Status = TemplateStatus.Draft; // editing a version under compliance review returns it to draft
        draft.LastEditedByUserId = rc.UserId;
        draft.UpdatedAt = clock.UtcNow;
        await audit.RecordAsync(new AuditEntry("template.draft_saved", $"حفظ مسودة قالب {code} v{draft.VersionNo}", OrganizationId: org ?? rc.OrganizationId));
        return draft;
    }

    public async Task<CommunicationTemplate> SubmitAsync(Guid? org, string code)
    {
        var versions = await VersionsAsync(org, code, track: true);
        var draft = versions.Where(t => t.OrganizationId == org && t.Status == TemplateStatus.Draft).MaxBy(t => t.VersionNo)
                    ?? throw new ConflictException("no_draft", "لا توجد مسودة لإرسالها.");
        var effective = Effective(versions, org);
        var blocking = TemplateRules.Tone(draft.BodyAr, draft.BodyEn, effective?.BodyAr, effective?.BodyEn).Where(t => t.Level == "block" && !t.Ok).ToList();
        if (blocking.Count > 0) throw new DomainException("tone_check_failed", "لا يُرسل القالب للاعتماد قبل معالجة فحص النبرة.", reasons: blocking.Select(b => b.Label).ToList());
        draft.Status = TemplateStatus.PendingCompliance;
        draft.UpdatedAt = clock.UtcNow;
        await audit.RecordAsync(new AuditEntry("template.submitted", $"إرسال قالب {code} v{draft.VersionNo} لاعتماد الامتثال", OrganizationId: org ?? rc.OrganizationId));
        return draft;
    }

    public async Task<CommunicationTemplate> PublishAsync(Guid? org, string code, bool allowFromDraft)
    {
        var versions = await VersionsAsync(org, code, track: true);
        var candidate = versions.Where(t => t.OrganizationId == org && (t.Status == TemplateStatus.PendingCompliance || (allowFromDraft && t.Status == TemplateStatus.Draft)))
                            .MaxBy(t => t.VersionNo) ?? throw new ConflictException("nothing_to_publish", "لا يوجد إصدار بانتظار الاعتماد.");
        if (candidate.LastEditedByUserId == rc.UserId)
            throw new DomainException("maker_checker", "لا ينشر القالب من حرّره؛ النشر لجهة الامتثال المستقلة.", StatusCodes.Status403Forbidden);
        var effective = Effective(versions, org);
        if (TemplateRules.Tone(candidate.BodyAr, candidate.BodyEn, effective?.BodyAr, effective?.BodyEn).Any(t => t.Level == "block" && !t.Ok))
            throw new DomainException("tone_check_failed", "القالب لا يجتاز فحص النبرة.");
        foreach (var old in versions.Where(t => t.OrganizationId == org && t.Status == TemplateStatus.Published)) old.Status = TemplateStatus.Superseded;
        candidate.Status = TemplateStatus.Published;
        candidate.PublishedByUserId = rc.UserId;
        candidate.UpdatedAt = clock.UtcNow;
        await audit.RecordAsync(new AuditEntry("template.published", "نشر قالب", Detail: $"{code} v{candidate.VersionNo}", OrganizationId: org ?? rc.OrganizationId));
        return candidate;
    }

    public async Task<CommunicationTemplate> ReturnAsync(Guid? org, string code, string reason)
    {
        var versions = await VersionsAsync(org, code, track: true);
        var pending = versions.Where(t => t.OrganizationId == org && t.Status == TemplateStatus.PendingCompliance).MaxBy(t => t.VersionNo)
                      ?? throw new ConflictException("nothing_pending", "لا يوجد إصدار بانتظار الاعتماد.");
        pending.Status = TemplateStatus.Draft;
        await audit.RecordAsync(new AuditEntry("template.returned", $"إعادة قالب {code} v{pending.VersionNo} للتعديل", Reason: reason, OrganizationId: org ?? rc.OrganizationId));
        return pending;
    }
}

public static class TemplateEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        // A05 — institution templates.
        var g = app.MapGroup("/api/settings/templates").RequireOrg(OrganizationKind.Lender).RequireAnyPermission(P.TemplateEdit, P.TemplatePublish);
        g.MapGet("", async (TemplateService s, RequestContext rc) => Results.Ok(await s.ListAsync(rc.OrganizationId)));
        g.MapGet("/{code}", async (string code, TemplateService s, RequestContext rc) => Results.Ok(await s.GetAsync(rc.OrganizationId, code)));
        g.MapPost("/{code}/tone-check", async (string code, ToneCheckRequest req, TemplateService s, RequestContext rc) => Results.Ok(await s.ToneAsync(rc.OrganizationId, code, req)));
        g.MapPut("/{code}", (string code, TemplateDraftRequest req, TemplateService s, RequestContext rc, RahoonDbContext db) =>
            Commit(db, async () => await s.SaveDraftAsync(rc.OrganizationId, code, req))).RequirePermission(P.TemplateEdit).Idempotent();
        g.MapPost("/{code}/submit", (string code, TemplateService s, RequestContext rc, RahoonDbContext db) =>
            Commit(db, async () => await s.SubmitAsync(rc.OrganizationId, code))).RequirePermission(P.TemplateEdit).Idempotent();
        g.MapPost("/{code}/publish", (string code, TemplateService s, RequestContext rc, RahoonDbContext db) =>
            Commit(db, async () => await s.PublishAsync(rc.OrganizationId, code, allowFromDraft: false))).RequirePermission(P.TemplatePublish).Idempotent();
        g.MapPost("/{code}/return", (string code, TemplateReturnRequest req, TemplateService s, RequestContext rc, RahoonDbContext db) =>
        {
            new Validator().Require(!string.IsNullOrWhiteSpace(req.Reason), "reason", "اكتب سبب الإعادة.").ThrowIfInvalid();
            return Commit(db, async () => await s.ReturnAsync(rc.OrganizationId, code, req.Reason!.Trim()));
        }).RequirePermission(P.TemplatePublish).Idempotent();

        // PA09 — platform base templates (copied/overridden by institutions).
        var p = app.MapGroup("/api/platform/defaults/templates").RequireOrg(OrganizationKind.Platform).RequirePermission(P.PlatformDefaults);
        p.MapGet("", async (TemplateService s) => Results.Ok(await s.ListAsync(null)));
        p.MapGet("/{code}", async (string code, TemplateService s) => Results.Ok(await s.GetAsync(null, code)));
        p.MapPut("/{code}", (string code, TemplateDraftRequest req, TemplateService s, RahoonDbContext db) =>
            Commit(db, async () => await s.SaveDraftAsync(null, code, req))).Idempotent();
        p.MapPost("/{code}/publish", (string code, TemplateService s, RahoonDbContext db) =>
            Commit(db, async () => await s.PublishAsync(null, code, allowFromDraft: true))).Idempotent();
    }

    private static async Task<IResult> Commit(RahoonDbContext db, Func<Task<CommunicationTemplate>> action)
    {
        await using var tx = await db.Database.BeginTransactionAsync();
        var t = await action();
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Results.Ok(new { t.Code, version = t.VersionNo, status = t.Status });
    }
}
