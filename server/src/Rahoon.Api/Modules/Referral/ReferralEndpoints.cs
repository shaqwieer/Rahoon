using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Rahoon.Api.Infrastructure.Http;
using Rahoon.Api.Infrastructure.Integrations;
using Rahoon.Api.Infrastructure.Persistence;
using Rahoon.Api.Infrastructure.Security;
using Rahoon.Api.Infrastructure.Tenancy;
using Rahoon.Api.Infrastructure.Time;
using Rahoon.Api.Modules.Audit;
using Rahoon.Api.Modules.Cases;
using Rahoon.Api.Modules.Closure;
using Rahoon.Api.Modules.Communications;
using Rahoon.Api.Modules.Documents;
using Rahoon.Api.Modules.Identity;
using Rahoon.Api.Modules.Providers;

namespace Rahoon.Api.Modules.Referral;

public sealed record ChecklistEvidenceRequest(string Note, Guid? DocumentVersionId);
public sealed record ReferralRequestBody(string Reason);
public sealed record ReferralDecisionBody(string Decision, string Reason, uint? OpenedVersion);
public sealed record ExternalReferenceBody(string Authority, string RequestNumber, string Source);
public sealed record ExternalStatusBody(string StatusText, string Source, DateTimeOffset? ObservedAt, string? Note);
public sealed record ReferralTransitionBody(string Reason, string? ExpectedStatus);
public sealed record SaleResultConfirmBody(string ConfirmationSource, DateOnly OfficialConfirmationDate, decimal? OfficialSalePrice, decimal? DeclaredCosts, string? Note);
public sealed record SaleResultReturnBody(string Reason);
public sealed record AgentAssignmentBody(Guid AgentOrganizationId, DateOnly DueOn, Guid? AssigneeUserId, string? Note);
public sealed record EndAssignmentBody(string Reason);
public sealed record ReferralExceptionBody(string Type, string Title, string Description, string? ExternalStateText, Guid? OwnerUserId, DateOnly? DueOn);
public sealed record ResolveExceptionBody(string Action, string Note);

/// <summary>
/// Judicial referral (L25 manual → J01–J04 integrated-ready). Legal prepares; a different approver decides with
/// MFA step-up; the external reference and official status are entered manually and stored verbatim, never mapped
/// to the platform status. Rahoon neither refers automatically nor runs courts or auctions.
/// </summary>
public static class ReferralEndpoints
{
    public const string NoticeTemplate = "TPL-PREREF-01";
    private static readonly string[] ExceptionTypes = ["status_mismatch", "missing_document", "objection_filed", "package_rejected", "no_response", "channel_unavailable", "other"];

    public static void Map(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/cases/{reference}/referral").RequireOrg(OrganizationKind.Lender).RequirePermission(P.CaseView);
        g.MapGet("", Overview);
        g.MapGet("/readiness", Readiness);
        g.MapPost("/readiness/{key}/evidence", RecordEvidence).RequirePermission(P.ReferralInitiate).Idempotent();
        g.MapPost("/notice", SendNotice).RequirePermission(P.ReferralInitiate).Idempotent();
        g.MapPost("/request", RequestDecision).RequirePermission(P.ReferralInitiate).Idempotent();
        g.MapPost("/decision", Decide).RequirePermission(P.ReferralApprove).Idempotent();
        g.MapPost("/pack", BuildPack).RequirePermission(P.ReferralInitiate).Idempotent();
        g.MapGet("/pack", GetPack);
        g.MapGet("/pack/export", ExportPack).RequirePermission(P.ReferralInitiate);
        g.MapPut("/external-reference", SetExternalReference).RequirePermission(P.ReferralExternalUpdate).Idempotent();
        g.MapGet("/external-status", ExternalHistory);
        g.MapPost("/external-status", AddExternalStatus).RequirePermission(P.ReferralExternalUpdate).Idempotent();
        g.MapPost("/external-sale-started", ExternalSaleStarted).RequirePermission(P.ReferralExternalUpdate).Idempotent();
        g.MapPost("/external-result", ExternalResultRecorded).RequirePermission(P.ReferralExternalUpdate).Idempotent();
        g.MapPost("/sale-result/confirm", ConfirmSaleResult).RequirePermission(P.ReferralExternalUpdate).Idempotent();
        g.MapPost("/sale-result/return", ReturnSaleResult).RequirePermission(P.ReferralExternalUpdate).Idempotent();
        g.MapPost("/agent-assignments", AssignAgent).RequirePermission(P.ProviderAssign).Idempotent();
        g.MapPost("/agent-assignments/{assignmentId:guid}/end", EndAssignment).RequirePermission(P.ProviderAssign).Idempotent();
        g.MapPost("/exceptions", AddException).RequireAnyPermission(P.ReferralInitiate, P.ReferralExternalUpdate).Idempotent();

        var x = app.MapGroup("/api/referrals").RequireOrg(OrganizationKind.Lender);
        x.MapGet("/exceptions", ExceptionQueue).RequireAnyPermission(P.ReferralInitiate, P.ReferralExternalUpdate, P.ReferralApprove);
        x.MapPost("/exceptions/{id:guid}/resolve", ResolveException).RequireAnyPermission(P.ReferralInitiate, P.ReferralExternalUpdate).Idempotent();
        x.MapGet("/agents", AgentOrganizations).RequirePermission(P.ProviderAssign);
    }

    private static readonly ReferralStatus[] Decided = [ReferralStatus.Approved, ReferralStatus.HandedOff];

    // ───────── Overview (L25 / J01 / J03) ─────────

    private static async Task<IResult> Overview(string reference, CaseAccess access, ReferralService svc, RahoonDbContext db, RequestContext rc,
        IClock clock, IIntegrationRegistry integrations)
    {
        var c = await access.GetAsync(reference, track: false);
        var r = await svc.CurrentAsync(c.Id, track: false);
        var items = await svc.ReadinessAsync(c, r);
        var blockers = await svc.BlockersAsync(c, r);
        var names = await UserNamesAsync(db, r?.InitiatedByUserId, r?.ApprovedByUserId, r?.DecidedByUserId, r?.NoticeSentByUserId);
        var pack = r is null ? null : await db.Set<EvidencePack>().AsNoTracking().Include(p => p.Items).Where(p => p.ReferralId == r.Id).OrderByDescending(p => p.SeqNo).FirstOrDefaultAsync();
        List<ExternalStatusEntry> history = r is null ? [] : await db.ExternalStatusEntries.AsNoTracking().Where(e => e.ReferralId == r.Id).OrderByDescending(e => e.ObservedAt).Take(50).ToListAsync();
        var assignment = await db.Assignments.AsNoTracking().Where(a => a.CaseId == c.Id && a.Type == AssignmentType.JudicialSale).OrderByDescending(a => a.CreatedAt).FirstOrDefaultAsync();
        var agentOrg = assignment is null ? null : await db.Organizations.AsNoTracking().Where(o => o.Id == assignment.ProviderOrganizationId).Select(o => o.NameAr).FirstOrDefaultAsync();
        var result = assignment is null ? null : await db.Set<SaleResult>().AsNoTracking().FirstOrDefaultAsync(s => s.AssignmentId == assignment.Id);
        var exceptionsOpen = await db.Set<ReferralException>().CountAsync(e => e.CaseId == c.Id && e.Status != ReferralExceptionStatus.Resolved);
        var channelState = await integrations.StateAsync(IntegrationKeys.JudicialChannel);
        var channel = await db.IntegrationSettings.AsNoTracking().FirstOrDefaultAsync(i => i.Key == IntegrationKeys.JudicialChannel);
        var debt = await db.DebtSnapshots.AsNoTracking().Where(d => d.CaseId == c.Id && d.IsCurrent).OrderByDescending(d => d.AsOf).FirstOrDefaultAsync();
        var readinessMet = items.Count(i => i.Met);

        object? preview = null;
        if (result is { OfficialSalePrice: { } price } && debt is not null)
        {
            var costs = result.DeclaredCosts ?? 0m;
            var w = Waterfall.Compute(price, costs, price - costs, debt.Total);
            preview = new
            {
                label = result.Status == SaleResultStatus.Confirmed ? "تقدير من نتيجة مؤكدة — ليس توزيعاً معتمداً" : "تقدير من أرقام أبلغ بها الوكيل (غير مؤكدة رسمياً) — ليس توزيعاً",
                basis = result.Status == SaleResultStatus.Confirmed ? "confirmed" : "agent_reported",
                salePrice = w.SalePrice, procedureCosts = w.ProcedureCosts, net = w.ExpectedNet, lenderShare = w.LenderShare, ownerSurplus = w.OwnerSurplus, shortfall = w.Shortfall,
                debtBasis = $"كشف المديونية {debt.AsOf.ToOffset(TimeSpan.FromHours(3)):yyyy-MM-dd}",
            };
        }

        var actions = new List<object>();
        void Act(string key, string label, bool permitted, List<string> reasons, bool stepUp = false)
        {
            if (permitted) actions.Add(new { key, label, enabled = reasons.Count == 0, reasons, requiresStepUp = stepUp });
        }
        var solutionState = CaseWorkflow.Def("refer_judicial").From.Contains(c.Status);
        Act("send_notice", "إرسال الإشعار المسبق…", rc.Has(P.ReferralInitiate),
            r?.NoticeSentAt is not null ? ["أُرسل الإشعار مسبقاً."] : solutionState ? [] : [$"الحالة «{CaseStatusInfo.Of(c.Status).LabelAr}» ليست في مرحلة الحلول."]);
        Act("request_approval", "طلب اعتماد الإحالة…", rc.Has(P.ReferralInitiate),
            r is null ? ["أرسل الإشعار المسبق أولاً."] : r.Status is ReferralStatus.PendingApproval ? ["الطلب بانتظار قرار المعتمد."] : Decided.Contains(r.Status) ? ["القرار معتمد مسبقاً."] : blockers);
        Act("decide", "اعتماد قرار الإحالة", rc.Has(P.ReferralApprove),
            r is not { Status: ReferralStatus.PendingApproval } ? ["لا يوجد طلب بانتظار الاعتماد."]
            : r.InitiatedByUserId == rc.UserId ? ["أنت مقدم الطلب؛ الاعتماد لمعتمد آخر (فصل المهام)."] : blockers, stepUp: true);
        Act("export_pack", "تصدير الحزمة", rc.Has(P.ReferralInitiate),
            r is null || !Decided.Contains(r.Status) ? ["التصدير بعد اعتماد القرار."] : pack is null ? ["ابنِ الحزمة أولاً."] : []);
        Act("external_reference", "تسجيل مرجع الجهة", rc.Has(P.ReferralExternalUpdate),
            r is null || !Decided.Contains(r.Status) ? ["غير متاح قبل اعتماد القرار."] : []);

        return Results.Ok(new
        {
            caseRef = c.Reference, caseStatus = CaseStatusInfo.Key(c.Status), caseStatusLabel = CaseStatusInfo.Of(c.Status).LabelAr,
            note = "رهون لا تُحيل ولا تدير البيع القضائي. هذه الحزمة تجهّز ملفاً موثقاً يُقدَّم يدوياً للجهة المختصة، ثم يُسجَّل مرجعها هنا.",
            referral = r is null ? null : new
            {
                r.Id, status = r.Status.ToString(), statusLabel = ReferralService.StatusLabel(r.Status), r.Version,
                notice = r.NoticeSentAt is null ? null : new { sentAt = r.NoticeSentAt, objectionEndsOn = r.ObjectionEndsOn, periodDays = r.ObjectionPeriodDays, template = r.NoticeTemplateCode, sentBy = names.GetValueOrDefault(r.NoticeSentByUserId ?? Guid.Empty) },
                request = r.RequestedAt is null ? null : new { requestedAt = r.RequestedAt, reason = r.Reason, by = names.GetValueOrDefault(r.InitiatedByUserId) },
                decision = r.DecidedAt is null ? null : new { decidedAt = r.DecidedAt, reason = r.DecisionReason, by = names.GetValueOrDefault(r.DecidedByUserId ?? Guid.Empty), approved = r.ApprovedAt is not null },
            },
            readiness = new { items, metCount = readinessMet, total = items.Count, summary = $"{readinessMet} من {items.Count} مكتملة" },
            blockers,
            pack = pack is null ? null : new { pack.Reference, status = pack.Status.ToString(), pack.ManifestSha256, itemCount = pack.Items.Count, pack.BuiltAt, pack.ExportedAt },
            externalReference = r?.ExternalRequestNumber is null ? null : new
            {
                authority = r.ExternalAuthority, requestNumber = r.ExternalRequestNumber, requestNumberMasked = ReferralService.MaskExternal(r.ExternalRequestNumber),
                source = r.ExternalReferenceSource, enteredAt = r.ExternalReferenceEnteredAt, agent = agentOrg,
            },
            officialStatus = r?.OfficialStatusText is null ? null : new { text = r.OfficialStatusText, source = r.OfficialStatusSource, syncedAt = r.OfficialStatusSyncedAt, verbatim = true },
            statusSeparation = "الحالة الرسمية الخارجية تُعرض منفصلة دائماً عن حالة المنصة، مع مصدرها ووقت إدخالها، ولا تغيّرها.",
            history = history.Select(HistoryRow),
            integration = new { key = IntegrationKeys.JudicialChannel, state = channelState, note = channel?.Note, entryMode = channelState == IntegrationState.Enabled ? "integrated" : "manual" },
            agentAssignment = assignment is null ? null : new
            {
                assignment.Id, assignment.Reference, agent = agentOrg, status = assignment.Status.ToString(), assignment.DueOn, assignment.CreatedAt, assignment.AccessExpiresAt,
            },
            saleResult = result is null || result.Status == SaleResultStatus.Draft ? null : new
            {
                status = result.Status.ToString(), result.OfficialSalePrice, result.SaleMinutesDate, result.DeclaredCosts, result.SubmittedAt,
                sourceLabel = result.Status == SaleResultStatus.Confirmed ? $"مؤكد · {result.ConfirmationSource}" : "أبلغ بها الوكيل (غير مؤكد رسمياً بعد)",
                evidenceCount = result.EvidenceVersionIds.Count, result.ConfirmedAt,
            },
            waterfallPreview = preview,
            openExceptions = exceptionsOpen,
            actions,
        });
    }

    internal static object HistoryRow(ExternalStatusEntry e) => new
    {
        e.Id, text = e.StatusText, e.Source, e.ObservedAt, sourceKind = e.SourceKind, e.Kind, e.Note, officiallyConfirmed = e.OfficiallyConfirmed,
        tag = e.SourceKind switch
        {
            ExternalSourceKind.Channel => "القناة المعتمدة",
            ExternalSourceKind.AgentReported => "أبلغ بها الوكيل",
            ExternalSourceKind.Internal => "داخلي",
            _ => "إدخال يدوي",
        },
        suffix = e.SourceKind == ExternalSourceKind.AgentReported && !e.OfficiallyConfirmed ? "(غير مؤكد رسمياً بعد)" : null,
    };

    private static async Task<Dictionary<Guid, string>> UserNamesAsync(RahoonDbContext db, params Guid?[] ids)
    {
        var set = ids.Where(i => i is { } v && v != Guid.Empty).Select(i => i!.Value).Distinct().ToList();
        return await db.Users.Where(u => set.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName);
    }

    private static async Task<IResult> Readiness(string reference, CaseAccess access, ReferralService svc)
    {
        var c = await access.GetAsync(reference, track: false);
        var r = await svc.CurrentAsync(c.Id, track: false);
        var items = await svc.ReadinessAsync(c, r);
        return Results.Ok(new { items, metCount = items.Count(i => i.Met), total = items.Count, computedAt = DateTimeOffset.UtcNow });
    }

    private static async Task<IResult> RecordEvidence(string reference, string key, ChecklistEvidenceRequest req, CaseAccess access, RahoonDbContext db,
        RequestContext rc, IClock clock, AuditLog audit)
    {
        new Validator().Require(ReferralService.AttestableKeys.Contains(key), "key", "هذا البند يُحسب من بيانات المنصة ولا يُسجَّل يدوياً.")
            .Require(!string.IsNullOrWhiteSpace(req.Note) && req.Note.Trim().Length >= 10, "note", "صف الدليل (10 أحرف على الأقل).").ThrowIfInvalid();
        await using var tx = await db.Database.BeginTransactionAsync();
        var c = await access.GetAsync(reference, track: false);
        if (req.DocumentVersionId is { } vid && !await db.DocumentVersions.AnyAsync(v => v.Id == vid && v.CaseId == c.Id))
            Validate.Throw("documentVersionId", "المستند غير موجود على هذه الحالة.");
        db.Set<ReferralChecklistEvidence>().Add(new ReferralChecklistEvidence
        {
            OrganizationId = c.OrganizationId, CaseId = c.Id, Key = key, Note = req.Note.Trim(), DocumentVersionId = req.DocumentVersionId,
            RecordedByUserId = rc.UserId, RecordedAt = clock.UtcNow,
        });
        await audit.RecordAsync(new AuditEntry("referral.readiness_evidence", $"تسجيل دليل لبند الجاهزية «{key}»", c.Id, c.Reference, Detail: req.Note.Trim(), OrganizationId: c.OrganizationId));
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Results.Ok(new { recorded = true });
    }

    // ───────── Pre-referral notice (L25 «إرسال…», f8 step 2) ─────────

    private static async Task<IResult> SendNotice(string reference, CaseAccess access, ReferralService svc, RahoonDbContext db, RequestContext rc,
        IClock clock, AuditLog audit, Notifier notifier, ISmsGateway sms, PiiProtector pii)
    {
        await using var tx = await db.Database.BeginTransactionAsync();
        var c = await access.GetAsync(reference, track: false);
        if (!CaseWorkflow.Def("refer_judicial").From.Contains(c.Status))
            throw new DomainException("not_eligible", $"الإشعار المسبق يُرسل فقط في مرحلة الحلول؛ الحالة الآن «{CaseStatusInfo.Of(c.Status).LabelAr}».", StatusCodes.Status409Conflict);
        var r = await svc.CurrentAsync(c.Id);
        if (r?.NoticeSentAt is not null) throw new ConflictException("notice_already_sent", "أُرسل الإشعار المسبق لهذه الإحالة مسبقاً.");

        var days = await Analytics.OperationalSettings.DaysAsync(db, c.OrganizationId, Analytics.OperationalSettings.ObjectionDays, 15);
        if (r is null)
        {
            r = new JudicialReferral { OrganizationId = c.OrganizationId, CaseId = c.Id, InitiatedByUserId = rc.UserId };
            db.Referrals.Add(r);
        }
        var now = clock.UtcNow;
        r.NoticeSentAt = now;
        r.NoticeSentByUserId = rc.UserId;
        r.NoticeTemplateCode = NoticeTemplate;
        r.ObjectionPeriodDays = days;
        r.ObjectionEndsOn = clock.TodayRiyadh.AddDays(days);
        r.Status = ReferralStatus.NoticeSent;

        var org = await db.Organizations.AsNoTracking().FirstAsync(o => o.Id == c.OrganizationId);
        var template = await db.Templates.AsNoTracking().Where(t => t.Code == NoticeTemplate && t.Status == TemplateStatus.Published).OrderByDescending(t => t.VersionNo).FirstOrDefaultAsync();
        var body = Fill(template?.BodyAr ?? DefaultNoticeBody, org.NameAr, days, r.ObjectionEndsOn.Value);
        var smsBody = Fill(template?.BodySms ?? DefaultNoticeSms, org.NameAr, days, r.ObjectionEndsOn.Value);

        db.Messages.Add(new CaseMessage
        {
            OrganizationId = c.OrganizationId, CaseId = c.Id, Channel = MessageChannel.Portal, AuthorType = "system", AuthorLabel = org.NameAr,
            Body = body, TemplateKey = NoticeTemplate, At = now,
        });
        var access0 = await db.OwnerAccesses.AsNoTracking().FirstOrDefaultAsync(o => o.CaseId == c.Id);
        if (access0?.UserId is { } ownerUser)
            notifier.Notify(ownerUser, c.OrganizationId, "referral_notice", "إشعار مهم بشأن حالتك", body, "/owner/referral", c.Id, "warn");
        var party = await db.Parties.AsNoTracking().FirstOrDefaultAsync(p => p.CaseId == c.Id && p.IsPrimary);
        if (party?.PhoneEnc is not null) await sms.SendAsync(pii.Unprotect(party.PhoneEnc), smsBody, c.OrganizationId, c.Id, NoticeTemplate);

        await audit.RecordAsync(new AuditEntry("referral.notice_sent", "إرسال الإشعار المسبق قبل الإحالة للمالك", c.Id, c.Reference,
            Detail: $"قالب {NoticeTemplate} · مهلة الاعتراض {CaseDisplay.Days(days)} حتى {r.ObjectionEndsOn:yyyy-MM-dd} · رسالة نصية (تجريبية) + البوابة", OrganizationId: c.OrganizationId));
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Results.Ok(new { referralId = r.Id, sentAt = r.NoticeSentAt, objectionEndsOn = r.ObjectionEndsOn, periodDays = days, channels = new[] { "portal", "sms_sandbox" } });
    }

    public const string DefaultNoticeBody =
        "نود إعلامك بأن {المصرف} يدرس إحالة حالتك إلى الجهة المختصة بعد تعذّر الوصول إلى حل ودي حتى الآن. هذا ليس قراراً نهائياً، ولن يُتخذ أي إجراء قبل {تاريخ_انتهاء_المهلة}. " +
        "يحق لك خلال {المهلة} تقديم اعتراض من بوابتك أو طلب حل آخر، ويحق لك الاطلاع على ملخص المبالغ، والتواصل مع مسؤول حالتك في أي وقت. أي اعتراض تقدمه تراجعه جهة مستقلة عن فريق حالتك.";

    public const string DefaultNoticeSms = "رهون: إشعار مهم بشأن حالتك مع {المصرف}. يحق لك الاعتراض حتى {تاريخ_انتهاء_المهلة}. ادخل من رابط الدعوة للتفاصيل.";

    private static string Fill(string template, string lender, int days, DateOnly ends) =>
        template.Replace("{المصرف}", lender).Replace("{المهلة}", CaseDisplay.Days(days)).Replace("{تاريخ_انتهاء_المهلة}", ends.ToString("yyyy-MM-dd"));

    // ───────── Decision request and approval (maker-checker + step-up) ─────────

    private static async Task<IResult> RequestDecision(string reference, ReferralRequestBody req, CaseAccess access, ReferralService svc, RahoonDbContext db,
        RequestContext rc, IClock clock, AuditLog audit, Notifier notifier)
    {
        new Validator().Require(!string.IsNullOrWhiteSpace(req.Reason) && req.Reason.Trim().Length >= 10, "reason", "سبب طلب الإحالة إلزامي (10 أحرف على الأقل).").ThrowIfInvalid();
        var c = await access.GetAsync(reference);
        var r = await svc.CurrentAsync(c.Id) ?? throw new ConflictException("no_referral", "أرسل الإشعار المسبق للمالك أولاً.");
        if (r.Status == ReferralStatus.PendingApproval) throw new ConflictException("already_requested", "الطلب بانتظار قرار المعتمد.");
        if (Decided.Contains(r.Status)) throw new ConflictException("already_decided", "قرار الإحالة معتمد مسبقاً.");
        if (!CaseWorkflow.Def("refer_judicial").From.Contains(c.Status))
            throw new DomainException("not_eligible", $"لا يمكن طلب الإحالة والحالة «{CaseStatusInfo.Of(c.Status).LabelAr}».", StatusCodes.Status409Conflict);

        var blockers = await svc.BlockersAsync(c, r);
        if (blockers.Count > 0)
        {
            // Refusals are audited in their own transaction before any chain append in this request.
            await audit.RecordBlockedAsync(svc.Blocked(c, "referral.request_blocked", "طلب اعتماد إحالة محجوب", blockers, req.Reason.Trim()));
            throw new DomainException("guard_failed", "لا يمكن طلب اعتماد الإحالة الآن.", StatusCodes.Status422UnprocessableEntity, blockers);
        }

        await using var tx = await db.Database.BeginTransactionAsync();
        r.Status = ReferralStatus.PendingApproval;
        r.InitiatedByUserId = rc.UserId;
        r.Reason = req.Reason.Trim();
        r.RequestedAt = clock.UtcNow;
        var approvers = await db.Memberships.Where(m => m.OrganizationId == c.OrganizationId && m.Status == MembershipStatus.Active && m.UserId != rc.UserId
                && m.Roles.Any(x => x.Role!.Permissions.Any(p => p.PermissionKey == P.ReferralApprove))).Select(m => m.UserId).ToListAsync();
        foreach (var u in approvers)
            notifier.Notify(u, c.OrganizationId, "approval", $"طلب اعتماد إحالة قضائية · {c.Reference}", "قرار عالي الأثر · موافقتان · رمز تحقق.", $"/cases/{c.Reference}/referral", c.Id, "warn");
        await audit.RecordAsync(new AuditEntry("referral.requested", "طلب اعتماد قرار الإحالة القضائية", c.Id, c.Reference, Reason: r.Reason,
            Detail: "الجاهزية 8 من 8 · الأثر: ← إحالة قضائية، تُقفل حزمة الأدلة", OrganizationId: c.OrganizationId));
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Results.Ok(new { status = r.Status.ToString(), r.Version });
    }

    private static async Task<IResult> Decide(string reference, ReferralDecisionBody req, CaseAccess access, ReferralService svc, RahoonDbContext db,
        RequestContext rc, IClock clock, CaseWorkflow workflow, AuditLog audit, Notifier notifier)
    {
        var decision = req.Decision?.ToLowerInvariant();
        new Validator().Require(decision is "approve" or "reject", "decision", "اختر القرار.")
            .Require(!string.IsNullOrWhiteSpace(req.Reason) && req.Reason.Trim().Length >= 10, "reason", "سبب القرار إلزامي (10 أحرف على الأقل).").ThrowIfInvalid();
        EndpointAccess.EnsureStepUp(rc, clock);

        await using var tx = await db.Database.BeginTransactionAsync();
        var c = await access.GetAsync(reference);
        var r = await svc.CurrentAsync(c.Id);
        if (r is not { Status: ReferralStatus.PendingApproval }) throw new ConflictException("not_pending", "لا يوجد طلب إحالة بانتظار الاعتماد.");
        if (req.OpenedVersion is { } ov && ov != r.Version) throw new ConflictException("request_changed", "تغيّر الطلب بعد فتحه. أعد تحميل الصفحة.");
        var reason = req.Reason.Trim();
        if (r.InitiatedByUserId == rc.UserId)
        {
            await audit.RecordBlockedAsync(svc.Blocked(c, "referral.decision_blocked", "محاولة اعتماد إحالة من مقدم الطلب", ["مقدم الطلب لا يعتمد قراره (فصل المهام)."], reason));
            throw new DomainException("separation_of_duties", "أنت مقدم طلب الإحالة؛ الاعتماد لمعتمد آخر.", StatusCodes.Status403Forbidden);
        }

        var pack = await db.Set<EvidencePack>().Where(p => p.ReferralId == r.Id).OrderByDescending(p => p.SeqNo).FirstOrDefaultAsync();
        if (decision == "approve")
        {
            // Readiness is re-evaluated at decision time; complaint and live-offer blockers come from the workflow guards.
            var extra = (await svc.ReadinessAsync(c, r)).Where(i => !i.Met && i.Key != "no_open_complaints").Select(i => $"{i.Label}: {i.Detail}").ToList();
            await workflow.TransitionAsync(c, "refer_judicial", reason, extraGuardFailures: extra,
                evidence: pack is null ? [$"referral:{r.Id}"] : [$"referral:{r.Id}", $"pack:{pack.Reference}"]);
            r.Status = ReferralStatus.Approved;
            r.ApprovedByUserId = rc.UserId;
            r.ApprovedAt = clock.UtcNow;
            if (pack is { Status: EvidencePackStatus.Draft }) { pack.Status = EvidencePackStatus.Locked; pack.LockedAt = clock.UtcNow; }
        }
        else r.Status = ReferralStatus.Rejected;
        r.DecidedByUserId = rc.UserId;
        r.DecidedAt = clock.UtcNow;
        r.DecisionReason = reason;

        notifier.Notify(r.InitiatedByUserId, c.OrganizationId, "approval", decision == "approve" ? $"اعتُمد قرار الإحالة · {c.Reference}" : $"رُفض طلب الإحالة · {c.Reference}",
            reason, $"/cases/{c.Reference}/referral", c.Id, decision == "approve" ? "ok" : "warn");
        await audit.RecordAsync(new AuditEntry("referral.decision", decision == "approve" ? "اعتماد قرار الإحالة القضائية (موافقتان)" : "رفض طلب الإحالة القضائية",
            c.Id, c.Reference, Reason: reason, Detail: "تأكيد برمز التحقق · مقدم الطلب ≠ المعتمد", OrganizationId: c.OrganizationId));
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Results.Ok(new { status = r.Status.ToString(), caseStatus = CaseStatusInfo.Key(c.Status) });
    }

    // ───────── Evidence pack (J02) ─────────

    private static async Task<IResult> BuildPack(string reference, CaseAccess access, ReferralService svc, RahoonDbContext db, RequestContext rc, IClock clock, AuditLog audit)
    {
        await using var tx = await db.Database.BeginTransactionAsync();
        var c = await access.GetAsync(reference, track: false);
        var r = await svc.CurrentAsync(c.Id) ?? throw new ConflictException("no_referral", "أرسل الإشعار المسبق للمالك أولاً.");
        if (Decided.Contains(r.Status)) throw new ConflictException("pack_locked", "حزمة الأدلة مقفلة بعد اعتماد القرار.");
        var set = db.Set<EvidencePack>();
        var seq = await set.Where(p => p.CaseId == c.Id).Select(p => (int?)p.SeqNo).MaxAsync() ?? 0;
        var pack = new EvidencePack
        {
            OrganizationId = c.OrganizationId, CaseId = c.Id, ReferralId = r.Id, SeqNo = seq + 1,
            Reference = $"PKG-{c.Reference.Split('-')[^1].TrimStart('0')}-{seq + 1:D2}", ManifestSha256 = "", MappingsJson = "[]",
            BuiltByUserId = rc.UserId, BuiltAt = clock.UtcNow,
        };
        var (items, mappings, hash) = await svc.BuildPackAsync(c, r, pack.Id);
        pack.ManifestSha256 = hash;
        pack.MappingsJson = JsonSerializer.Serialize(mappings, JsonOptions.Web);
        pack.Items = items;
        set.Add(pack);
        await audit.RecordAsync(new AuditEntry("referral.pack_built", $"بناء حزمة الأدلة {pack.Reference}", c.Id, c.Reference,
            Detail: $"{items.Count} عناصر · {hash}", Evidence: [pack.Reference], OrganizationId: c.OrganizationId));
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Results.Ok(PackView(pack, mappings));
    }

    private static object PackView(EvidencePack pack, List<FieldMapping> mappings)
    {
        var blocking = mappings.Count(m => m.Blocking);
        var unverified = pack.Items.Count(i => !i.Verified);
        var reasons = new List<string>();
        if (blocking > 0) reasons.Add(blocking == 1 ? "حقل واحد غير مطابق يجب حلّه." : blocking == 2 ? "حقلان غير متطابقين يجب حلّهما." : $"{blocking} حقول غير متطابقة يجب حلّها.");
        if (unverified > 0) reasons.Add($"عناصر غير متحقق منها في الحزمة: {unverified}.");
        return new
        {
            pack.Id, pack.Reference, status = pack.Status.ToString(), pack.ManifestSha256, pack.BuiltAt, pack.LockedAt, pack.ExportedAt,
            items = pack.Items.OrderBy(i => i.Seq).Select(i => new { n = i.Seq.ToString("D2"), i.Title, i.SourceType, i.SourceRef, version = i.VersionLabel, i.Sha256, status = i.Verified ? "متحقق" : "غير متحقق", i.Verified }),
            mappings, blockingCount = blocking, exportBlockedReasons = reasons,
        };
    }

    private static async Task<IResult> GetPack(string reference, CaseAccess access, RahoonDbContext db)
    {
        var c = await access.GetAsync(reference, track: false);
        var pack = await db.Set<EvidencePack>().AsNoTracking().Include(p => p.Items).Where(p => p.CaseId == c.Id).OrderByDescending(p => p.SeqNo).FirstOrDefaultAsync()
                   ?? throw new NotFoundException();
        return Results.Ok(PackView(pack, JsonSerializer.Deserialize<List<FieldMapping>>(pack.MappingsJson, JsonOptions.Web) ?? []));
    }

    /// <summary>Numbered manifest export (CSV or JSON) — only after approval, with every item verified and no blocking mismatch. Audited.</summary>
    private static async Task<IResult> ExportPack(string reference, string? format, CaseAccess access, ReferralService svc, RahoonDbContext db, RequestContext rc, IClock clock, AuditLog audit)
    {
        var fmt = (format ?? "json").ToLowerInvariant();
        if (fmt is not ("json" or "csv")) Validate.Throw("format", "الصيغة: json أو csv.");
        var c = await access.GetAsync(reference, track: false);
        var r = await svc.CurrentAsync(c.Id) ?? throw new NotFoundException();
        var pack = await db.Set<EvidencePack>().Include(p => p.Items).Where(p => p.ReferralId == r.Id).OrderByDescending(p => p.SeqNo).FirstOrDefaultAsync()
                   ?? throw new ConflictException("no_pack", "ابنِ الحزمة أولاً.");
        var mappings = JsonSerializer.Deserialize<List<FieldMapping>>(pack.MappingsJson, JsonOptions.Web) ?? [];
        var reasons = new List<string>();
        if (!Decided.Contains(r.Status)) reasons.Add("التصدير بعد اعتماد قرار الإحالة.");
        if (mappings.Any(m => m.Blocking)) reasons.Add("توجد حقول غير متطابقة.");
        if (pack.Items.Any(i => !i.Verified)) reasons.Add("توجد عناصر غير متحقق منها.");
        if (reasons.Count > 0)
        {
            await audit.RecordBlockedAsync(svc.Blocked(c, "referral.pack_export_blocked", $"محاولة تصدير الحزمة {pack.Reference}", reasons));
            throw new DomainException("export_blocked", "لا يمكن تصدير الحزمة الآن.", StatusCodes.Status409Conflict, reasons);
        }

        var now = clock.UtcNow;
        pack.Status = EvidencePackStatus.Exported;
        pack.ExportedAt = now;
        pack.ExportedByUserId = rc.UserId;
        r.EvidencePackExportedAt = now;
        r.EvidencePackHash = pack.ManifestSha256;
        await audit.RecordAsync(new AuditEntry("referral.pack_exported", $"تصدير حزمة الأدلة {pack.Reference} ({fmt.ToUpperInvariant()})", c.Id, c.Reference,
            Detail: pack.ManifestSha256, Evidence: [pack.Reference], OrganizationId: c.OrganizationId));
        await db.SaveChangesAsync();

        var items = pack.Items.OrderBy(i => i.Seq).ToList();
        if (fmt == "csv")
        {
            var sb = new StringBuilder("seq,title,source_type,source_ref,version,sha256,verified\n");
            foreach (var i in items)
                sb.Append($"{i.Seq:D2},{Csv(i.Title)},{i.SourceType},{Csv(i.SourceRef)},{Csv(i.VersionLabel)},{i.Sha256},{(i.Verified ? "yes" : "no")}\n");
            sb.Append($"# manifest_sha256,{pack.ManifestSha256}\n");
            return Results.File(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray(), "text/csv; charset=utf-8", $"{pack.Reference}-manifest.csv");
        }
        var manifest = new
        {
            package = pack.Reference, caseRef = c.Reference, exportedAt = now, manifestSha256 = pack.ManifestSha256,
            note = "حزمة تُقدَّم يدوياً للجهة المختصة. رهون لا تُحيل ولا تدير البيع.",
            items = items.Select(i => new { n = i.Seq.ToString("D2"), i.Title, i.SourceType, i.SourceRef, version = i.VersionLabel, i.Sha256, i.Verified }),
            mappings,
        };
        return Results.File(JsonSerializer.SerializeToUtf8Bytes(manifest, JsonOptions.Web), "application/json", $"{pack.Reference}-manifest.json");
    }

    private static string Csv(string v) => v.Contains(',') || v.Contains('"') ? $"\"{v.Replace("\"", "\"\"")}\"" : v;

    // ───────── External reference and official status (J03) ─────────

    private static async Task<(Case, JudicialReferral)> DecidedReferralAsync(string reference, CaseAccess access, ReferralService svc, bool trackCase = false)
    {
        var c = await access.GetAsync(reference, track: trackCase);
        var r = await svc.CurrentAsync(c.Id);
        if (r is null || !Decided.Contains(r.Status)) throw new ConflictException("referral_not_approved", "غير متاح قبل اعتماد قرار الإحالة.");
        return (c, r);
    }

    private static async Task<IResult> SetExternalReference(string reference, ExternalReferenceBody req, CaseAccess access, ReferralService svc, RahoonDbContext db,
        RequestContext rc, IClock clock, AuditLog audit)
    {
        new Validator().Require(!string.IsNullOrWhiteSpace(req.RequestNumber) && req.RequestNumber.Length <= 100, "requestNumber", "رقم الطلب لدى الجهة المختصة مطلوب.")
            .Require(!string.IsNullOrWhiteSpace(req.Authority) && req.Authority.Length <= 200, "authority", "اسم الجهة المختصة مطلوب.")
            .Require(!string.IsNullOrWhiteSpace(req.Source) && req.Source.Length <= 200, "source", "مصدر المرجع مطلوب (مثل: إشعار الجهة).").ThrowIfInvalid();
        await using var tx = await db.Database.BeginTransactionAsync();
        var (c, r) = await DecidedReferralAsync(reference, access, svc);
        var previous = r.ExternalRequestNumber;
        // Stored verbatim — no normalisation or mapping.
        r.ExternalAuthority = req.Authority;
        r.ExternalRequestNumber = req.RequestNumber;
        r.ExternalReferenceSource = req.Source;
        r.ExternalReferenceEnteredAt = clock.UtcNow;
        r.ExternalReferenceEnteredByUserId = rc.UserId;
        r.IntegrationState = IntegrationState.Unavailable;
        await audit.RecordAsync(new AuditEntry("referral.external_reference", previous is null ? "تسجيل مرجع الجهة الخارجي" : "تعديل مرجع الجهة الخارجي", c.Id, c.Reference,
            Detail: $"{req.Authority} · {ReferralService.MaskExternal(req.RequestNumber)} · المصدر: {req.Source} · إدخال يدوي", OrganizationId: c.OrganizationId));
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Results.Ok(new { authority = r.ExternalAuthority, requestNumber = r.ExternalRequestNumber, source = r.ExternalReferenceSource, enteredAt = r.ExternalReferenceEnteredAt, entryMode = "manual" });
    }

    private static async Task<IResult> ExternalHistory(string reference, CaseAccess access, ReferralService svc, RahoonDbContext db)
    {
        var c = await access.GetAsync(reference, track: false);
        var r = await svc.CurrentAsync(c.Id, track: false);
        if (r is null) return Results.Ok(new { items = Array.Empty<object>() });
        var rows = await db.ExternalStatusEntries.AsNoTracking().Where(e => e.ReferralId == r.Id).OrderByDescending(e => e.ObservedAt).ToListAsync();
        return Results.Ok(new { items = rows.Select(HistoryRow), note = "نص الحالة كما ورد حرفياً · لا يُعدَّل · لا يغيّر حالة المنصة" });
    }

    private static async Task<IResult> AddExternalStatus(string reference, ExternalStatusBody req, CaseAccess access, ReferralService svc, RahoonDbContext db,
        RequestContext rc, IClock clock, AuditLog audit)
    {
        var observed = req.ObservedAt ?? clock.UtcNow;
        new Validator().Require(!string.IsNullOrWhiteSpace(req.StatusText) && req.StatusText.Length <= 2000, "statusText", "نص الحالة الرسمية كما ورد مطلوب.")
            .Require(!string.IsNullOrWhiteSpace(req.Source) && req.Source.Length <= 200, "source", "مصدر الحالة مطلوب (مثل: إشعار الجهة، اتصال رسمي).")
            .Require(observed <= clock.UtcNow.AddMinutes(5), "observedAt", "وقت الرصد لا يكون في المستقبل.").ThrowIfInvalid();
        await using var tx = await db.Database.BeginTransactionAsync();
        var (c, r) = await DecidedReferralAsync(reference, access, svc);
        if (r.ExternalRequestNumber is null) throw new ConflictException("no_external_reference", "سجّل مرجع الجهة الخارجي أولاً.");
        var entry = new ExternalStatusEntry
        {
            OrganizationId = c.OrganizationId, ReferralId = r.Id, CaseId = c.Id,
            StatusText = req.StatusText, // verbatim: never trimmed, translated or mapped
            Source = req.Source, ObservedAt = observed, EnteredByUserId = rc.UserId, Note = req.Note,
            SourceKind = ExternalSourceKind.Manual, Kind = "status", OfficiallyConfirmed = true,
        };
        db.ExternalStatusEntries.Add(entry);
        if (r.OfficialStatusSyncedAt is null || observed >= r.OfficialStatusSyncedAt)
        {
            r.OfficialStatusText = req.StatusText;
            r.OfficialStatusSource = req.Source;
            r.OfficialStatusSyncedAt = observed;
        }
        await audit.RecordAsync(new AuditEntry("referral.external_status", "تسجيل الحالة الرسمية الخارجية (إدخال يدوي)", c.Id, c.Reference,
            Detail: $"«{req.StatusText}» · المصدر: {req.Source} · لا يغيّر حالة المنصة", OrganizationId: c.OrganizationId));
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Results.Ok(new { entry = HistoryRow(entry), caseStatus = CaseStatusInfo.Key(c.Status) });
    }

    private static async Task<IResult> ExternalSaleStarted(string reference, ReferralTransitionBody req, CaseAccess access, ReferralService svc, RahoonDbContext db, CaseWorkflow workflow)
    {
        await using var tx = await db.Database.BeginTransactionAsync();
        var (c, r) = await DecidedReferralAsync(reference, access, svc, trackCase: true);
        var extra = r.ExternalRequestNumber is null ? new List<string> { "لم يُسجل مرجع الجهة الخارجي بعد." } : [];
        await workflow.TransitionAsync(c, "external_sale_started", req.Reason?.Trim(), Expected(req.ExpectedStatus), evidence: [$"referral:{r.Id}"], extraGuardFailures: extra);
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Results.Ok(new { status = CaseStatusInfo.Key(c.Status), label = CaseStatusInfo.Of(c.Status).LabelAr });
    }

    private static async Task<IResult> ExternalResultRecorded(string reference, ReferralTransitionBody req, CaseAccess access, ReferralService svc, RahoonDbContext db, CaseWorkflow workflow)
    {
        await using var tx = await db.Database.BeginTransactionAsync();
        var (c, r) = await DecidedReferralAsync(reference, access, svc, trackCase: true);
        var confirmed = await db.Set<SaleResult>().AnyAsync(s => s.CaseId == c.Id && s.Status == SaleResultStatus.Confirmed);
        var extra = confirmed ? new List<string>() : ["لم تُؤكَّد نتيجة البيع رسمياً بعد (نتيجة الوكيل وحدها لا تكفي)."];
        await workflow.TransitionAsync(c, "external_result_recorded", req.Reason?.Trim(), Expected(req.ExpectedStatus), evidence: [$"referral:{r.Id}"], extraGuardFailures: extra);
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Results.Ok(new { status = CaseStatusInfo.Key(c.Status), label = CaseStatusInfo.Of(c.Status).LabelAr });
    }

    private static CaseStatus? Expected(string? key) => key is null ? null : CaseStatusInfo.Parse(key) ?? throw new ValidationFailedException(new Dictionary<string, string[]> { ["expectedStatus"] = ["حالة غير معروفة."] });

    // ───────── Agent result confirmation (J07 → F01 input) ─────────

    private static async Task<IResult> ConfirmSaleResult(string reference, SaleResultConfirmBody req, CaseAccess access, ReferralService svc, RahoonDbContext db,
        RequestContext rc, IClock clock, AuditLog audit, Notifier notifier)
    {
        new Validator().Require(!string.IsNullOrWhiteSpace(req.ConfirmationSource), "confirmationSource", "اذكر المصدر الرسمي للتأكيد (مثل: محضر البيع الرسمي، إشعار الجهة).")
            .Require(req.OfficialConfirmationDate <= clock.TodayRiyadh, "officialConfirmationDate", "تاريخ التأكيد لا يكون في المستقبل.")
            .Require(req.OfficialSalePrice is null or > 0, "officialSalePrice", "ثمن البيع يجب أن يكون أكبر من صفر.")
            .Require(req.DeclaredCosts is null or >= 0, "declaredCosts", "التكاليف لا تكون سالبة.").ThrowIfInvalid();
        await using var tx = await db.Database.BeginTransactionAsync();
        var (c, r) = await DecidedReferralAsync(reference, access, svc);
        var result = await db.Set<SaleResult>().FirstOrDefaultAsync(s => s.CaseId == c.Id && s.Status == SaleResultStatus.Submitted)
                     ?? throw new ConflictException("no_submitted_result", "لا توجد نتيجة مرسلة من الوكيل بانتظار التأكيد.");
        var price = req.OfficialSalePrice ?? result.OfficialSalePrice!.Value;
        var costs = req.DeclaredCosts ?? result.DeclaredCosts ?? 0m;
        var corrected = price != result.OfficialSalePrice || costs != (result.DeclaredCosts ?? 0m);
        if (corrected && string.IsNullOrWhiteSpace(req.Note)) Validate.Throw("note", "الأرقام الرسمية تختلف عما أبلغ به الوكيل؛ اذكر السبب.");
        if (costs > price) Validate.Throw("declaredCosts", "التكاليف لا تتجاوز ثمن البيع.");

        result.OfficialSalePrice = price;
        result.DeclaredCosts = costs;
        result.Status = SaleResultStatus.Confirmed;
        result.ConfirmedByUserId = rc.UserId;
        result.ConfirmedAt = clock.UtcNow;
        result.ConfirmationSource = req.ConfirmationSource.Trim();
        result.OfficialConfirmationDate = req.OfficialConfirmationDate;
        result.ConfirmationNote = req.Note?.Trim();
        foreach (var e in await db.ExternalStatusEntries.Where(e => e.ReferralId == r.Id && e.SourceKind == ExternalSourceKind.AgentReported && !e.OfficiallyConfirmed).ToListAsync())
            e.OfficiallyConfirmed = true;

        // Delivery ends the agent's working access; read-only for 7 days, then the lender org drops out of the agent's data scope.
        var assignment = await db.Assignments.FirstAsync(a => a.Id == result.AssignmentId);
        assignment.Status = AssignmentStatus.Accepted;
        assignment.DeliveredAt = clock.UtcNow;
        assignment.AccessExpiresAt = clock.UtcNow.AddDays(7);
        if (assignment.AssigneeUserId is { } agentUser)
            notifier.Notify(agentUser, assignment.ProviderOrganizationId, "assignment", $"أُكّدت نتيجة البيع · {assignment.Reference}", "وصولك للقراءة فقط لمدة 7 أيام.", $"/agent/cases/{assignment.Reference}", null, "ok");
        await audit.RecordAsync(new AuditEntry("referral.sale_result_confirmed", "تأكيد نتيجة البيع رسمياً", c.Id, c.Reference,
            Detail: $"ثمن البيع {price:N2} · التكاليف {costs:N2} · المصدر: {result.ConfirmationSource}" + (corrected ? " · صُححت أرقام الوكيل" : ""),
            Reason: req.Note?.Trim(), OrganizationId: c.OrganizationId));
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Results.Ok(new { status = result.Status.ToString(), officialSalePrice = price, declaredCosts = costs, caseStatus = CaseStatusInfo.Key(c.Status) });
    }

    private static async Task<IResult> ReturnSaleResult(string reference, SaleResultReturnBody req, CaseAccess access, ReferralService svc, RahoonDbContext db,
        RequestContext rc, IClock clock, AuditLog audit, Notifier notifier)
    {
        new Validator().Require(!string.IsNullOrWhiteSpace(req.Reason) && req.Reason.Trim().Length >= 5, "reason", "اذكر سبب الإعادة.").ThrowIfInvalid();
        await using var tx = await db.Database.BeginTransactionAsync();
        var (c, _) = await DecidedReferralAsync(reference, access, svc);
        var result = await db.Set<SaleResult>().FirstOrDefaultAsync(s => s.CaseId == c.Id && s.Status == SaleResultStatus.Submitted)
                     ?? throw new ConflictException("no_submitted_result", "لا توجد نتيجة مرسلة من الوكيل.");
        result.Status = SaleResultStatus.Returned;
        result.ConfirmationNote = req.Reason.Trim();
        var assignment = await db.Assignments.FirstAsync(a => a.Id == result.AssignmentId);
        assignment.Status = AssignmentStatus.Returned;
        if (assignment.AssigneeUserId is { } agentUser)
            notifier.Notify(agentUser, assignment.ProviderOrganizationId, "assignment", $"أُعيدت نتيجة البيع للاستكمال · {assignment.Reference}", req.Reason.Trim(), $"/agent/cases/{assignment.Reference}", null, "warn");
        await audit.RecordAsync(new AuditEntry("referral.sale_result_returned", "إعادة نتيجة البيع للوكيل", c.Id, c.Reference, Reason: req.Reason.Trim(), OrganizationId: c.OrganizationId));
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Results.Ok(new { status = result.Status.ToString() });
    }

    // ───────── Hand-off to a judicial sale agent (J05 source) ─────────

    private static async Task<IResult> AgentOrganizations(RahoonDbContext db) =>
        Results.Ok(await db.Organizations.AsNoTracking().Where(o => o.Kind == OrganizationKind.JudicialAgent && o.Status == OrganizationStatus.Active)
            .OrderBy(o => o.NameAr).Select(o => new { o.Id, o.NameAr, o.City }).ToListAsync());

    private static async Task<IResult> AssignAgent(string reference, AgentAssignmentBody req, CaseAccess access, ReferralService svc, RahoonDbContext db,
        RequestContext rc, IClock clock, AuditLog audit, Notifier notifier, Providers.ProviderAssignmentService assignments)
    {
        new Validator().Require(req.DueOn > clock.TodayRiyadh, "dueOn", "تاريخ التسليم في المستقبل.").ThrowIfInvalid();
        await using var tx = await db.Database.BeginTransactionAsync();
        var (c, r) = await DecidedReferralAsync(reference, access, svc);
        if (c.Status is not (CaseStatus.JudicialReferral or CaseStatus.ExternalJudicialSale))
            throw new DomainException("not_eligible", "التكليف متاح للحالات المحالة فقط.", StatusCodes.Status409Conflict);
        var agent = await db.Organizations.AsNoTracking().FirstOrDefaultAsync(o => o.Id == req.AgentOrganizationId && o.Kind == OrganizationKind.JudicialAgent && o.Status == OrganizationStatus.Active)
                    ?? throw new ValidationFailedException(new Dictionary<string, string[]> { ["agentOrganizationId"] = ["اختر مكتب وكيل بيع معتمداً."] });
        if (await db.Assignments.AnyAsync(a => a.CaseId == c.Id && a.Type == AssignmentType.JudicialSale && a.Status != AssignmentStatus.Cancelled && a.Status != AssignmentStatus.Closed))
            throw new ConflictException("agent_already_assigned", "يوجد تكليف وكيل بيع نشط لهذه الحالة.");
        if (req.AssigneeUserId is { } uid && !await db.Memberships.IgnoreQueryFilters().AnyAsync(m => m.UserId == uid && m.OrganizationId == agent.Id && m.Status == MembershipStatus.Active))
            Validate.Throw("assigneeUserId", "المستخدم ليس عضواً في مكتب الوكيل.");

        var property = await db.Properties.AsNoTracking().FirstOrDefaultAsync(p => p.CaseId == c.Id);
        string[] shared = ["title_deed", "financing_contract", "valuation_report"];
        var docIds = await db.Documents.Where(d => d.CaseId == c.Id && shared.Contains(d.DocumentTypeKey) && d.Status == DocumentStatus.Verified).Select(d => d.Id).ToListAsync();
        var year = clock.TodayRiyadh.Year;
        // Shared counter `assignment:YYYY` (merge fix, Phase 1A-2 step 6): a count-based number collided with seeded references.
        var assignmentRef = await assignments.NextReferenceAsync(year);
        var a = new ProviderAssignment
        {
            OrganizationId = c.OrganizationId, Reference = assignmentRef, CaseId = c.Id, ProviderOrganizationId = agent.Id, AssigneeUserId = req.AssigneeUserId,
            Type = AssignmentType.JudicialSale, Title = "بيع قضائي بتكليف من الجهة المختصة", PropertyLabel = property?.ShortLabel ?? "العقار",
            DueOn = req.DueOn, Scope = ["المعاينة وتوثيق حالة العقار", "رفع خطة البيع للجهة المختصة", "تسليم محضر البيع والأدلة"],
            SharedDocumentIds = docIds, CreatedByUserId = rc.UserId, CreatedAt = clock.UtcNow,
        };
        db.Assignments.Add(a);
        r.Status = ReferralStatus.HandedOff;
        if (req.AssigneeUserId is { } assignee)
            notifier.Notify(assignee, agent.Id, "assignment", $"تكليف بيع قضائي جديد · {a.Reference}", a.PropertyLabel, $"/agent/cases/{a.Reference}", null);
        await audit.RecordAsync(new AuditEntry("referral.agent_assigned", $"تكليف وكيل البيع — {agent.NameAr}", c.Id, c.Reference,
            Detail: $"{a.Reference} · مستندات مشتركة: {docIds.Count} · التسليم {req.DueOn:yyyy-MM-dd}", Reason: req.Note, OrganizationId: c.OrganizationId));
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Results.Ok(new { a.Id, a.Reference, sharedDocuments = docIds.Count });
    }

    private static async Task<IResult> EndAssignment(string reference, Guid assignmentId, EndAssignmentBody req, CaseAccess access, RahoonDbContext db, IClock clock, AuditLog audit)
    {
        new Validator().Require(!string.IsNullOrWhiteSpace(req.Reason) && req.Reason.Trim().Length >= 5, "reason", "اذكر سبب إنهاء التكليف.").ThrowIfInvalid();
        await using var tx = await db.Database.BeginTransactionAsync();
        var c = await access.GetAsync(reference, track: false);
        var a = await db.Assignments.FirstOrDefaultAsync(x => x.Id == assignmentId && x.CaseId == c.Id && x.Type == AssignmentType.JudicialSale) ?? throw new NotFoundException();
        a.Status = AssignmentStatus.Closed;
        a.AccessExpiresAt = clock.UtcNow;
        await audit.RecordAsync(new AuditEntry("referral.agent_access_ended", $"إنهاء تكليف ووصول الوكيل {a.Reference}", c.Id, c.Reference, Reason: req.Reason.Trim(), OrganizationId: c.OrganizationId));
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Results.Ok(new { status = a.Status.ToString(), a.AccessExpiresAt });
    }

    // ───────── Exceptions (J04) ─────────

    private static async Task<IResult> AddException(string reference, ReferralExceptionBody req, CaseAccess access, ReferralService svc, RahoonDbContext db,
        RequestContext rc, IClock clock, AuditLog audit, Notifier notifier)
    {
        new Validator().Require(ExceptionTypes.Contains(req.Type), "type", "نوع الاستثناء غير معروف.")
            .Require(!string.IsNullOrWhiteSpace(req.Title) && req.Title.Length <= 200, "title", "العنوان مطلوب.")
            .Require(!string.IsNullOrWhiteSpace(req.Description) && req.Description.Trim().Length >= 10, "description", "صف الاستثناء (10 أحرف على الأقل).")
            .Require(req.DueOn is null || req.DueOn >= clock.TodayRiyadh, "dueOn", "تاريخ الاستحقاق لا يكون في الماضي.").ThrowIfInvalid();
        await using var tx = await db.Database.BeginTransactionAsync();
        var c = await access.GetAsync(reference, track: false);
        var r = await svc.CurrentAsync(c.Id, track: false);
        if (req.OwnerUserId is { } owner && !await db.Memberships.AnyAsync(m => m.UserId == owner && m.OrganizationId == c.OrganizationId && m.Status == MembershipStatus.Active))
            Validate.Throw("ownerUserId", "المسؤول ليس عضواً في المنشأة.");
        var year = clock.TodayRiyadh.Year;
        var n = await db.Set<ReferralException>().IgnoreQueryFilters().CountAsync(e => e.Reference.StartsWith($"EXC-{year}-"));
        var e = new ReferralException
        {
            OrganizationId = c.OrganizationId, CaseId = c.Id, ReferralId = r?.Id, Reference = $"EXC-{year}-{n + 1:D4}", Type = req.Type, Title = req.Title.Trim(),
            Description = req.Description.Trim(), PlatformState = CaseStatusInfo.Of(c.Status).LabelAr, ExternalStateText = req.ExternalStateText,
            OwnerUserId = req.OwnerUserId ?? rc.UserId, DueOn = req.DueOn, CreatedByUserId = rc.UserId,
            Status = req.Type == "channel_unavailable" ? ReferralExceptionStatus.Info : ReferralExceptionStatus.Open,
        };
        db.Set<ReferralException>().Add(e);
        if (e.OwnerUserId is { } o && o != rc.UserId)
            notifier.Notify(o, c.OrganizationId, "referral", $"استثناء مسند إليك · {c.Reference}", e.Title, $"/referrals/exceptions?selected={e.Id}", c.Id, "warn");
        await audit.RecordAsync(new AuditEntry("referral.exception_opened", $"تسجيل استثناء {e.Reference}: {e.Title}", c.Id, c.Reference,
            Detail: e.Description + " · لا انتقال تلقائي عند التعارض", OrganizationId: c.OrganizationId));
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Results.Ok(new { e.Id, e.Reference, status = e.Status.ToString() });
    }

    private static async Task<IResult> ExceptionQueue(string? status, CaseAccess access, RahoonDbContext db)
    {
        var cases = await access.VisibleAsync();
        var q = db.Set<ReferralException>().AsNoTracking().Where(e => cases.Any(c => c.Id == e.CaseId));
        q = status switch
        {
            "resolved" => q.Where(e => e.Status == ReferralExceptionStatus.Resolved),
            "all" => q,
            _ => q.Where(e => e.Status != ReferralExceptionStatus.Resolved),
        };
        var rows = await q.OrderBy(e => e.Status).ThenBy(e => e.DueOn).ThenByDescending(e => e.CreatedAt).Take(200)
            .Select(e => new
            {
                e.Id, e.Reference, e.Type, e.Title, e.Description, status = e.Status.ToString(), e.PlatformState, e.ExternalStateText, e.DueOn, e.CreatedAt,
                caseRef = db.Cases.Where(c => c.Id == e.CaseId).Select(c => c.Reference).First(),
                owner = db.Users.Where(u => u.Id == e.OwnerUserId).Select(u => u.FullName).FirstOrDefault(),
                e.ResolutionAction, e.ResolutionNote, e.ResolvedAt,
            }).ToListAsync();
        // Count semantics (B10 conflict 1): «مفتوحة» counts every non-resolved exception (open + pending + info).
        return Results.Ok(new { items = rows, unresolved = rows.Count(r => r.status != nameof(ReferralExceptionStatus.Resolved)), countLabel = "غير المحلولة (مفتوح + قيد الانتظار + معلومة)" });
    }

    private static async Task<IResult> ResolveException(Guid id, ResolveExceptionBody req, CaseAccess access, RahoonDbContext db, RequestContext rc, IClock clock, AuditLog audit)
    {
        new Validator().Require(req.Action is "record_and_wait" or "escalate" or "corrected" or "other", "action", "اختر طريقة الحل.")
            .Require(!string.IsNullOrWhiteSpace(req.Note) && req.Note.Trim().Length >= 5, "note", "حل الاستثناء يتطلب ملاحظة.").ThrowIfInvalid();
        await using var tx = await db.Database.BeginTransactionAsync();
        var cases = await access.VisibleAsync();
        var e = await db.Set<ReferralException>().FirstOrDefaultAsync(x => x.Id == id && cases.Any(c => c.Id == x.CaseId)) ?? throw new NotFoundException();
        if (e.Status == ReferralExceptionStatus.Resolved) throw new ConflictException("already_resolved", "حُل هذا الاستثناء مسبقاً.");
        var c = await db.Cases.AsNoTracking().FirstAsync(x => x.Id == e.CaseId);
        e.ResolutionAction = req.Action;
        e.ResolutionNote = req.Note.Trim();
        // Escalation waits for the authority's answer; nothing here transitions the case.
        e.Status = req.Action == "escalate" ? ReferralExceptionStatus.Pending : ReferralExceptionStatus.Resolved;
        if (e.Status == ReferralExceptionStatus.Resolved) { e.ResolvedByUserId = rc.UserId; e.ResolvedAt = clock.UtcNow; }
        await audit.RecordAsync(new AuditEntry("referral.exception_resolved", req.Action == "escalate" ? $"تصعيد الاستثناء {e.Reference}" : $"حل الاستثناء {e.Reference}",
            c.Id, c.Reference, Reason: e.ResolutionNote, Detail: $"الإجراء: {req.Action} · حالة المنصة دون تغيير ({CaseStatusInfo.Of(c.Status).LabelAr})", OrganizationId: c.OrganizationId));
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Results.Ok(new { status = e.Status.ToString(), caseStatus = CaseStatusInfo.Key(c.Status) });
    }
}
