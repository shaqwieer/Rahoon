using Microsoft.EntityFrameworkCore;
using Rahoon.Api.Infrastructure.Http;
using Rahoon.Api.Infrastructure.Persistence;
using Rahoon.Api.Infrastructure.Security;
using Rahoon.Api.Infrastructure.Storage;
using Rahoon.Api.Infrastructure.Tenancy;
using Rahoon.Api.Infrastructure.Time;
using Rahoon.Api.Modules.Audit;
using Rahoon.Api.Modules.Communications;
using Rahoon.Api.Modules.Documents;
using Rahoon.Api.Modules.Identity;

namespace Rahoon.Api.Modules.Requests;

public sealed record AssignRequest(Guid UserId);
public sealed record NextStepBody(string? NextStep);
public sealed record IdentityCheckBody(string? Method, string? Note);
public sealed record RequestInfoBody(List<string>? Items, string? Message);
public sealed record CoordinationBody(
    string? Channel, DateTimeOffset? OccurredAt, string? Counterpart, string? Summary, List<Guid>? EvidenceDocumentIds,
    bool VisibleToApplicant, string? ApplicantText, Guid? CorrectsEntryId, string? Kind = null);
public sealed record TeamUpdateBody(string? Text, string? NextStep);
public sealed record MessageBody(string? Body);
public sealed record NotEligibleBody(string? Reason);

/// <summary>
/// «فريق رهون» workspace API (ADR 0001 §4.2, design request D-4 T01–T04). Operator members only; a coordinator works on
/// requests assigned to them (or not yet assigned), the team lead sees all. Drafts never reach the team.
/// Internal timers (V5) are computed here and never exposed to the individual.
/// </summary>
public static class TeamRequestEndpoints
{
    public static readonly string[] Channels = ["phone", "email", "letter", "visit", "other"];
    public static readonly string[] TeamDocKinds = ["lender_letter", "coordination_evidence", "identity_evidence", "other"];

    /// <summary>V5 interim thresholds (days in the current status). Internal only — never a commitment to the individual.</summary>
    public const int WarnDaysTeam = 3, AlertDaysTeam = 7, WarnDaysOther = 10;

    public static void Map(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/team").RequireOrg(OrganizationKind.Operator).RequireAnyPermission(P.RequestViewAssigned, P.RequestViewAll);
        g.MapGet("/requests", Queue);
        g.MapGet("/members", Members).RequirePermission(P.RequestAssign);
        g.MapGet("/requests/{reference}", Detail);
        g.MapPost("/requests/{reference}/assign", Assign).RequirePermission(P.RequestAssign).Idempotent();
        g.MapPost("/requests/{reference}/take", Take).RequirePermission(P.RequestReview).Idempotent();
        g.MapPost("/requests/{reference}/pick-up", PickUp).RequirePermission(P.RequestReview).Idempotent();
        g.MapPost("/requests/{reference}/identity-check", IdentityCheck).RequirePermission(P.RequestReview).Idempotent();
        g.MapPost("/requests/{reference}/request-info", RequestInfo).RequirePermission(P.RequestRequestInfo).Idempotent();
        g.MapPost("/requests/{reference}/coordination", Coordinate).RequirePermission(P.RequestCoordinate).Idempotent();
        g.MapPost("/requests/{reference}/start-coordination", StartCoordination).RequirePermission(P.RequestCoordinate).Idempotent();
        g.MapPost("/requests/{reference}/updates", TeamUpdate).RequirePermission(P.RequestMessage).Idempotent();
        g.MapPost("/requests/{reference}/messages", Message).RequirePermission(P.RequestMessage).Idempotent();
        g.MapPost("/requests/{reference}/notes", Note).RequirePermission(P.RequestReview).Idempotent();
        g.MapPost("/requests/{reference}/not-eligible", NotEligible).RequirePermission(P.RequestReview).Idempotent();
        g.MapPost("/requests/{reference}/documents", Upload).RequirePermission(P.RequestCoordinate).DisableAntiforgery();
        g.MapGet("/requests/{reference}/documents/{versionId:guid}/file", Download);
    }

    // ───────── T01 queue ─────────

    private static async Task<IResult> Queue(string? tab, RahoonDbContext db, RequestContext rc, IClock clock)
    {
        var q = db.Requests.AsNoTracking().Include(r => r.Institution).Where(r => r.Status != RequestStatus.Draft);
        var all = rc.Has(P.RequestViewAll);
        tab = tab switch { "unassigned" or "all" or "closed" => tab, _ => "mine" };
        if (tab == "all" && !all) tab = "mine";
        var terminal = RequestStatusInfo.Terminal;
        q = tab switch
        {
            "unassigned" => q.Where(r => r.AssignedCoordinatorId == null && !terminal.Contains(r.Status)),
            "all" => q.Where(r => !terminal.Contains(r.Status)),
            "closed" => all ? q.Where(r => terminal.Contains(r.Status)) : q.Where(r => terminal.Contains(r.Status) && r.AssignedCoordinatorId == rc.UserId),
            _ => q.Where(r => r.AssignedCoordinatorId == rc.UserId && !terminal.Contains(r.Status)),
        };
        if (!all && !rc.Has(P.RequestReview)) q = q.Where(r => r.AssignedCoordinatorId == rc.UserId);
        var rows = await q.OrderBy(r => r.StatusChangedAt).Take(200).ToListAsync();
        var names = await CoordinatorNamesAsync(db, rows.Select(r => r.AssignedCoordinatorId));
        var now = clock.UtcNow;

        var counts = new
        {
            mine = await db.Requests.CountAsync(r => r.AssignedCoordinatorId == rc.UserId && r.Status != RequestStatus.Draft && !terminal.Contains(r.Status)),
            unassigned = rc.Has(P.RequestReview) || all ? await db.Requests.CountAsync(r => r.AssignedCoordinatorId == null && r.Status != RequestStatus.Draft && !terminal.Contains(r.Status)) : 0,
            all = all ? await db.Requests.CountAsync(r => r.Status != RequestStatus.Draft && !terminal.Contains(r.Status)) : 0,
        };
        return Results.Ok(new
        {
            tab,
            canViewAll = all,
            counts,
            items = rows.Select(r => new
            {
                r.Reference,
                applicantName = r.ApplicantFullName is { Length: > 0 } n ? Mask.PersonName(n) : "—",
                institutionName = r.InstitutionDisplayName,
                status = RequestStatusInfo.Key(r.Status),
                waitingOn = RequestStatusInfo.WaitingKey(r.WaitingOn),
                r.SubmittedAt,
                lastUpdate = r.UpdatedAt,
                assignedTo = r.AssignedCoordinatorId is { } a ? names.GetValueOrDefault(a) : null,
                timer = Timer(r, now),
            }),
        });
    }

    private static async Task<IResult> Members(RahoonDbContext db, RequestContext rc)
    {
        var members = await db.Memberships.AsNoTracking().Include(m => m.User).Include(m => m.Roles).ThenInclude(r => r.Role!).ThenInclude(r => r.Permissions)
            .Where(m => m.OrganizationId == rc.OrganizationId && m.Status == MembershipStatus.Active).ToListAsync();
        return Results.Ok(members
            .Where(m => m.Roles.Any(r => r.Role!.Permissions.Any(p => p.PermissionKey == P.RequestReview)))
            .Select(m => new { userId = m.UserId, name = m.User!.FullName, role = m.Roles.Select(r => r.Role!.NameAr).FirstOrDefault(), m.Title })
            .OrderBy(m => m.name));
    }

    internal static object Timer(Request r, DateTimeOffset now)
    {
        var inStatus = (int)Math.Floor((now - r.StatusChangedAt).TotalDays);
        var sinceSubmit = r.SubmittedAt is { } s ? (int)Math.Floor((now - s).TotalDays) : (int?)null;
        var level = RequestStatusInfo.IsTerminal(r.Status) ? "none"
            : r.WaitingOn == RequestWaitingOn.Team ? (inStatus >= AlertDaysTeam ? "alert" : inStatus >= WarnDaysTeam ? "warn" : "ok")
            : inStatus >= WarnDaysOther ? "warn" : "ok";
        return new { daysInStatus = inStatus, daysSinceSubmitted = sinceSubmit, level };
    }

    private static async Task<Dictionary<Guid, string>> CoordinatorNamesAsync(RahoonDbContext db, IEnumerable<Guid?> ids)
    {
        var set = ids.Where(i => i is not null).Select(i => i!.Value).Distinct().ToList();
        return set.Count == 0 ? [] : await db.Users.AsNoTracking().Where(u => set.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName);
    }

    // ───────── T02 review ─────────

    private static async Task<IResult> Detail(string reference, RequestAccess access, RahoonDbContext db, RequestContext rc, RequestWorkflow workflow, IClock clock) =>
        Results.Ok(await TeamDetailAsync(await access.ForTeamAsync(reference, track: false), db, rc, workflow, clock));

    internal static async Task<object> TeamDetailAsync(Request r, RahoonDbContext db, RequestContext rc, RequestWorkflow workflow, IClock clock)
    {
        var profile = await db.IndividualProfiles.AsNoTracking().FirstAsync(p => p.UserId == r.ApplicantUserId);
        var names = await CoordinatorNamesAsync(db, [r.AssignedCoordinatorId, r.IdentityCheckedByUserId]);
        var consents = await db.RequestConsents.AsNoTracking().Where(c => c.RequestId == r.Id).OrderByDescending(c => c.OtpVerifiedAt).ToListAsync();
        var active = await RequestWorkflow.ActiveConsentAsync(db, r);
        var docs = await db.RequestDocuments.AsNoTracking().Include(d => d.Versions).Where(d => d.RequestId == r.Id).OrderBy(d => d.CreatedAt).ToListAsync();
        var timeline = await db.RequestUpdates.AsNoTracking().Where(u => u.RequestId == r.Id).OrderByDescending(u => u.At)
            .Select(u => new { u.Kind, u.Title, u.Body, u.At, u.AuthorKind, u.AuthorLabel, visible = u.VisibleToApplicant }).ToListAsync();
        var coordination = await db.CoordinationEntries.AsNoTracking().Where(e => e.RequestId == r.Id).OrderByDescending(e => e.OccurredAt).ThenByDescending(e => e.CreatedAt).ToListAsync();
        var messages = await db.RequestMessages.AsNoTracking().Where(m => m.RequestId == r.Id).OrderBy(m => m.At)
            .Select(m => new { m.Id, m.AuthorKind, m.AuthorLabel, m.Body, m.IsInternal, m.At }).ToListAsync();
        var duplicateOf = r.DuplicateOfRequestId is { } dupId ? await db.Requests.AsNoTracking().Where(x => x.Id == dupId).Select(x => x.Reference).FirstOrDefaultAsync() : null;
        var docName = docs.ToDictionary(d => d.Id, d => d);
        var offers = await db.RequestOffers.AsNoTracking().Where(o => o.RequestId == r.Id).OrderByDescending(o => o.VersionNo).ToListAsync();
        var responses = await db.RequestResponses.AsNoTracking().Where(x => x.RequestId == r.Id).OrderByDescending(x => x.At)
            .Select(x => new { x.Id, x.Reference, x.Kind, x.Text, x.At, x.RelayedAt, x.ConsentTextSnapshot, x.OtpVerifiedAt }).ToListAsync();
        var concerns = await db.RequestConcerns.AsNoTracking().Where(c => c.RequestId == r.Id).OrderByDescending(c => c.CreatedAt)
            .Select(c => new { c.Id, c.Reference, c.Kind, c.Subject, c.Text, status = c.Status == RequestConcernStatus.Open ? "open" : "answered", c.Outcome, c.ResponseText, c.RespondedByLabel, c.RespondedAt, c.CreatedAt })
            .ToListAsync();
        var referrals = await db.SpecialistReferrals.AsNoTracking().Where(x => x.RequestId == r.Id).OrderByDescending(x => x.At)
            .Select(x => new { x.SpecialistType, x.SpecialistName, x.Note, x.ApplicantText, x.RecordedByLabel, x.At }).ToListAsync();

        var actions = new List<object>();
        foreach (var t in RequestWorkflow.Transitions.Where(t => t.Permission.Length > 0 && t.From.Contains(r.Status) && rc.Has(t.Permission)))
        {
            if (t.Key is "publish_offer") continue; // done from the verification screen (step 7)
            var reasons = await workflow.EvaluateGuardsAsync(r, t);
            if (t.Key == "pick_up" && r.AssignedCoordinatorId is { } a && a != rc.UserId && !rc.Has(P.RequestViewAll)) reasons.Add("الطلب مسند لعضو آخر.");
            actions.Add(new { t.Key, label = t.LabelAr, enabled = reasons.Count == 0, reasons, t.RequiresReason });
        }

        return new
        {
            r.Reference,
            status = RequestStatusInfo.Key(r.Status),
            statusLabel = RequestStatusInfo.LabelAr(r.Status),
            waitingOn = RequestStatusInfo.WaitingKey(r.WaitingOn),
            r.NextStepText,
            r.CreatedAt,
            r.SubmittedAt,
            r.StatusChangedAt,
            assigned = r.AssignedCoordinatorId is { } ac ? new { userId = ac, name = names.GetValueOrDefault(ac), at = r.AssignedAt } : null,
            assignedToMe = r.AssignedCoordinatorId == rc.UserId,
            applicant = new
            {
                name = r.ApplicantFullName,
                idMasked = profile.NationalIdMasked,
                idType = profile.IdType,
                phoneMasked = profile.PhoneMasked,
                identityAssurance = profile.IdentityAssurance,
                identityCheck = r.IdentityCheckedAt is { } ic ? new { at = ic, by = names.GetValueOrDefault(r.IdentityCheckedByUserId ?? Guid.Empty), note = r.IdentityCheckNote } : null,
            },
            institutionName = r.InstitutionDisplayName,
            institutionListed = r.InstitutionId is not null,
            fields = new
            {
                r.ContractNumber, r.MonthlyInstallment, r.ArrearsDuration, r.PropertyCity,
                pathPreference = MyRequestEndpoints.PreferenceKey(r.PathPreference), r.AffordableMonthly, r.SituationText,
            },
            consentActive = active is not null,
            consents = consents.Select(c => new { c.Id, c.TextVersion, c.TextSnapshot, c.RecipientName, recordedAt = c.OtpVerifiedAt, c.WithdrawnAt, c.WithdrawnReason, c.DataCategories, c.IpMasked }),
            documents = docs.Select(d =>
            {
                var v = d.Versions.OrderByDescending(x => x.VersionNo).FirstOrDefault();
                return new
                {
                    d.Id, d.Kind, d.Name, d.Source, visibility = d.Visibility == RequestDocumentVisibility.TeamOnly ? "team_only" : "applicant_and_team",
                    d.AddedAfterSubmit, versionId = v?.Id, fileName = v?.FileName, uploadedAt = v?.UploadedAt, uploadedBy = v?.UploadedByLabel,
                    scan = v?.ScanStatus.ToString().ToLowerInvariant(), sizeBytes = v?.SizeBytes,
                };
            }),
            timeline,
            coordination = coordination.Select(e => new
            {
                e.Id, e.Kind, e.Channel, e.OccurredAt, e.Counterpart, e.Summary, e.VisibleToApplicant, e.ApplicantText, e.CorrectsEntryId,
                recordedBy = e.RecordedByLabel, recordedAt = e.CreatedAt,
                evidence = e.EvidenceDocumentIds.Where(docName.ContainsKey).Select(id => new
                {
                    id, name = docName[id].Name, versionId = docName[id].CurrentVersionId,
                }),
            }),
            messages,
            duplicateOf,
            r.NotEligibleReason,
            outcome = r.Status == RequestStatus.Closed ? new { code = r.OutcomeCode, summary = r.OutcomeSummary } : null,
            actions,
            offers = offers.Select(o => OfferEndpoints.TeamOffer(o, rc.UserId)),
            responses,
            concerns,
            referrals,
            timer = Timer(r, clock.UtcNow),
            can = new
            {
                assign = rc.Has(P.RequestAssign),
                take = r.AssignedCoordinatorId is null && rc.Has(P.RequestReview) && !RequestStatusInfo.IsTerminal(r.Status),
                identityCheck = rc.Has(P.RequestReview) && r.IdentityCheckedAt is null && r.Status != RequestStatus.Submitted && !RequestStatusInfo.IsTerminal(r.Status),
                coordinate = rc.Has(P.RequestCoordinate) && CoordinationOpen(r) && active is not null,
                message = rc.Has(P.RequestMessage) && !RequestStatusInfo.IsTerminal(r.Status),
                note = rc.Has(P.RequestReview),
                recordOffer = rc.Has(P.RequestOfferRecord) && r.Status == RequestStatus.LenderCoordination && active is not null,
                verify = rc.Has(P.RequestOfferVerify) && offers.Any(o => o.Status == RequestOfferStatus.PendingVerification && o.RecordedByUserId != rc.UserId),
                relay = rc.Has(P.RequestResponseRelay) && r.Status == RequestStatus.ResponseRecorded && responses.Count > 0 && responses[0].RelayedAt == null,
                close = rc.Has(P.RequestClose) && r.Status is RequestStatus.ResponseRecorded or RequestStatus.LenderCoordination,
                refer = rc.Has(P.RequestReview) && (r.AssignedCoordinatorId == rc.UserId || rc.Has(P.RequestViewAll)),
                answerConcerns = rc.Has(P.RequestObjectionHandle),
            },
        };
    }

    private static bool CoordinationOpen(Request r) =>
        r.Status is RequestStatus.TeamReview or RequestStatus.LenderCoordination or RequestStatus.OfferAvailable or RequestStatus.ResponseRecorded;

    /// <summary>Coordinator actions require the request to be theirs (or unassigned, or the member views all).</summary>
    private static void EnsureWorker(Request r, RequestContext rc)
    {
        if (r.AssignedCoordinatorId == rc.UserId || rc.Has(P.RequestViewAll)) return;
        throw new ForbiddenException(r.AssignedCoordinatorId is null ? "استلم الطلب أولاً." : "هذا الطلب مسند لعضو آخر.");
    }

    // ───────── assignment ─────────

    private static async Task<IResult> Assign(string reference, AssignRequest body, RequestAccess access, RahoonDbContext db, RequestContext rc,
        RequestService svc, IClock clock, AuditLog audit)
    {
        var r = await access.ForTeamAsync(reference);
        if (RequestStatusInfo.IsTerminal(r.Status)) throw new ConflictException("closed", "الطلب مغلق.");
        var member = await db.Memberships.AsNoTracking().Include(m => m.User).Include(m => m.Roles).ThenInclude(x => x.Role!).ThenInclude(x => x.Permissions)
            .FirstOrDefaultAsync(m => m.OrganizationId == r.OrganizationId && m.UserId == body.UserId && m.Status == MembershipStatus.Active);
        if (member is null || !member.Roles.Any(x => x.Role!.Permissions.Any(p => p.PermissionKey == P.RequestReview)))
            Validate.Throw("userId", "اختر عضواً من الفريق يملك صلاحية دراسة الطلبات.");
        await AssignToAsync(r, member!.UserId, member.User!.FullName, db, rc, svc, clock, audit);
        await db.SaveChangesAsync();
        return Results.Ok(new { assignedTo = member.User.FullName });
    }

    private static async Task<IResult> Take(string reference, RequestAccess access, RahoonDbContext db, RequestContext rc, RequestService svc, IClock clock, AuditLog audit)
    {
        var r = await access.ForTeamAsync(reference);
        if (r.AssignedCoordinatorId is not null && r.AssignedCoordinatorId != rc.UserId) throw new ConflictException("assigned", "أُسند الطلب لعضو آخر.");
        if (RequestStatusInfo.IsTerminal(r.Status)) throw new ConflictException("closed", "الطلب مغلق.");
        if (r.AssignedCoordinatorId is null) await AssignToAsync(r, rc.UserId, rc.UserName, db, rc, svc, clock, audit);
        await db.SaveChangesAsync();
        return Results.Ok(new { assignedTo = rc.UserName });
    }

    private static async Task AssignToAsync(Request r, Guid userId, string name, RahoonDbContext db, RequestContext rc, RequestService svc, IClock clock, AuditLog audit)
    {
        var previous = r.AssignedCoordinatorId;
        r.AssignedCoordinatorId = userId;
        r.AssignedAt = clock.UtcNow;
        svc.AddUpdate(r, "assignment", previous is null ? $"أُسند الطلب إلى {name}" : $"أُعيد إسناد الطلب إلى {name}", authorKind: "team", authorLabel: rc.UserName, authorUserId: rc.UserId, visible: false);
        await audit.RecordAsync(RequestWorkflow.Entry(r, "request.assigned", $"إسناد الطلب إلى {name}", detail: previous is null ? null : "إعادة إسناد"));
    }

    // ───────── review actions ─────────

    private static async Task<IResult> PickUp(string reference, NextStepBody body, RequestAccess access, RahoonDbContext db, RequestContext rc,
        RequestWorkflow workflow, RequestService svc, IClock clock, AuditLog audit)
    {
        await using var tx = await db.Database.BeginTransactionAsync();
        var r = await access.ForTeamAsync(reference);
        if (r.AssignedCoordinatorId is null) await AssignToAsync(r, rc.UserId, rc.UserName, db, rc, svc, clock, audit);
        EnsureWorker(r, rc);
        await workflow.TransitionAsync(r, "pick_up", expected: RequestStatus.Submitted, nextStep: body.NextStep);
        svc.AddUpdate(r, "status", "بدأ فريق رهون دراسة طلبك", authorKind: "team", authorLabel: "فريق رهون", authorUserId: rc.UserId);
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Results.Ok(new { status = RequestStatusInfo.Key(r.Status) });
    }

    private static async Task<IResult> IdentityCheck(string reference, IdentityCheckBody body, RequestAccess access, RahoonDbContext db, RequestContext rc,
        RequestService svc, IClock clock, AuditLog audit)
    {
        var note = Clean(body.Note, 500);
        if (note is null) Validate.Throw("note", "اكتب كيف تحققت من الهوية (مثلاً: مطابقة صورة الهوية مع بيانات الجهة).");
        var r = await access.ForTeamAsync(reference);
        EnsureWorker(r, rc);
        if (r.Status is RequestStatus.Submitted || RequestStatusInfo.IsTerminal(r.Status)) throw new ConflictException("state", "ابدأ دراسة الطلب أولاً.");
        if (r.IdentityCheckedAt is not null) throw new ConflictException("already", "سُجّل التحقق من الهوية مسبقاً.");
        r.IdentityCheckedAt = clock.UtcNow;
        r.IdentityCheckedByUserId = rc.UserId;
        r.IdentityCheckNote = note;
        svc.AddUpdate(r, "identity_check", "تحقق فريق رهون من هوية العميل", note, authorKind: "team", authorLabel: rc.UserName, authorUserId: rc.UserId, visible: false);
        await audit.RecordAsync(RequestWorkflow.Entry(r, "request.identity_checked", "تسجيل التحقق من هوية العميل", detail: note));
        await db.SaveChangesAsync();
        return Results.Ok(new { r.IdentityCheckedAt });
    }

    /// <summary>T03: ask the individual for information. The request waits on them; answering returns it to where it was.</summary>
    private static async Task<IResult> RequestInfo(string reference, RequestInfoBody body, RequestAccess access, RahoonDbContext db, RequestContext rc,
        RequestWorkflow workflow, RequestService svc, Notifier notifier)
    {
        var items = (body.Items ?? []).Select(i => Clean(i, 200)).Where(i => i is not null).Cast<string>().Distinct().Take(10).ToList();
        var message = Clean(body.Message, 1000);
        new Validator().Require(message is not null, "message", "اكتب رسالة واضحة للعميل تشرح ما نحتاجه ولماذا.").ThrowIfInvalid();
        await using var tx = await db.Database.BeginTransactionAsync();
        var r = await access.ForTeamAsync(reference);
        EnsureWorker(r, rc);
        await workflow.TransitionAsync(r, "request_info", message, nextStep: message);
        r.InfoRequestIsConsent = false;
        var listed = items.Count == 0 ? message : string.Join("\n", items.Select(i => "• " + i)) + "\n\n" + message;
        svc.AddUpdate(r, "info_request", "طلب فريق رهون معلومة منك", listed, authorKind: "team", authorLabel: "فريق رهون", authorUserId: rc.UserId);
        notifier.Notify(r.ApplicantUserId, null, "request", "نحتاج معلومة منك لمتابعة طلبك", message, $"/my/requests/{r.Reference}", tone: "warn");
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Results.Ok(new { status = RequestStatusInfo.Key(r.Status) });
    }

    /// <summary>
    /// T04: append a coordination entry (manual channel). Requires active consent — without it nothing may be shared with
    /// the lender (refusals audited). Corrections are new entries referencing the corrected one.
    /// </summary>
    private static async Task<IResult> Coordinate(string reference, CoordinationBody body, RequestAccess access, RahoonDbContext db, RequestContext rc,
        RequestService svc, IClock clock, AuditLog audit, Notifier notifier)
    {
        var now = clock.UtcNow;
        var counterpart = Clean(body.Counterpart, 200);
        var summary = Clean(body.Summary, 2000);
        var applicantText = Clean(body.ApplicantText, 1000);
        var kind = body.Kind is "response_relay" or "offer_received" or "lender_response" ? body.Kind : "general";
        new Validator()
            .Require(body.Channel is not null && Channels.Contains(body.Channel), "channel", "اختر قناة التواصل.")
            .Require(body.OccurredAt is { } at && at <= now.AddMinutes(5) && at >= now.AddYears(-1), "occurredAt", "أدخل تاريخ ووقت التواصل (ليس في المستقبل).")
            .Require(counterpart is not null, "counterpart", "اكتب الطرف لدى الجهة (الاسم أو الصفة كما ذُكر).")
            .Require(summary is not null, "summary", "اكتب ملخص التواصل.")
            .Require(!body.VisibleToApplicant || applicantText is not null, "applicantText", "اكتب النص الذي سيظهر للعميل.")
            .ThrowIfInvalid();

        await using var tx = await db.Database.BeginTransactionAsync();
        var r = await access.ForTeamAsync(reference);
        EnsureWorker(r, rc);
        if (!CoordinationOpen(r)) throw new ConflictException("state", r.Status == RequestStatus.Submitted ? "ابدأ دراسة الطلب أولاً." : "لا يمكن تسجيل تنسيق والطلب في حالته الحالية.");
        if (await RequestWorkflow.ActiveConsentAsync(db, r) is null)
        {
            await audit.RecordBlockedAsync(RequestWorkflow.Entry(r, "request.coordination_blocked", "محاولة تنسيق مع الجهة دون موافقة سارية",
                detail: "المانع: لا توجد موافقة موثقة سارية من العميل. لم يُسجل القيد.", blocked: true));
            throw new DomainException("consent_required", "لا يمكن التنسيق مع الجهة الممولة دون موافقة سارية من العميل على المشاركة.", StatusCodes.Status422UnprocessableEntity);
        }
        if (body.CorrectsEntryId is { } corr && !await db.CoordinationEntries.AnyAsync(e => e.Id == corr && e.RequestId == r.Id))
            Validate.Throw("correctsEntryId", "القيد المراد تصحيحه غير موجود.");
        var evidence = (body.EvidenceDocumentIds ?? []).Distinct().ToList();
        if (evidence.Count > 0 && await db.RequestDocuments.CountAsync(d => d.RequestId == r.Id && evidence.Contains(d.Id)) != evidence.Count)
            Validate.Throw("evidenceDocumentIds", "مرفق غير موجود في هذا الطلب.");

        var entry = new CoordinationEntry
        {
            OrganizationId = r.OrganizationId, RequestId = r.Id, ApplicantUserId = r.ApplicantUserId, Kind = kind!, Channel = body.Channel!,
            OccurredAt = body.OccurredAt!.Value, Counterpart = counterpart!, Summary = summary!, EvidenceDocumentIds = evidence,
            VisibleToApplicant = body.VisibleToApplicant, ApplicantText = body.VisibleToApplicant ? applicantText : null, CorrectsEntryId = body.CorrectsEntryId,
            RecordedByUserId = rc.UserId, RecordedByLabel = rc.UserName,
        };
        db.CoordinationEntries.Add(entry);
        if (entry.VisibleToApplicant)
        {
            svc.AddUpdate(r, "coordination", "تحديث من التنسيق مع جهتك الممولة", applicantText, authorKind: "team", authorLabel: "فريق رهون", authorUserId: rc.UserId);
            notifier.Notify(r.ApplicantUserId, null, "request", "تحديث على طلبك", applicantText, $"/my/requests/{r.Reference}");
        }
        await audit.RecordAsync(RequestWorkflow.Entry(r, "request.coordination_recorded",
            body.CorrectsEntryId is null ? "قيد في سجل التنسيق مع الجهة" : "تصحيح قيد في سجل التنسيق", detail: $"{entry.Channel} · {counterpart}",
            evidence: evidence.Select(e => e.ToString()).ToList()));
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Results.Ok(new { entry.Id });
    }

    private static async Task<IResult> StartCoordination(string reference, NextStepBody body, RequestAccess access, RahoonDbContext db, RequestContext rc,
        RequestWorkflow workflow, RequestService svc)
    {
        await using var tx = await db.Database.BeginTransactionAsync();
        var r = await access.ForTeamAsync(reference);
        EnsureWorker(r, rc);
        await workflow.TransitionAsync(r, "start_coordination", expected: RequestStatus.TeamReview, nextStep: body.NextStep);
        svc.AddUpdate(r, "status", "بدأ فريق رهون التنسيق مع جهتك الممولة", "نشارك جهتك البيانات التي وافقت عليها فقط، ونبلغك بما يصلنا منها.",
            authorKind: "team", authorLabel: "فريق رهون", authorUserId: rc.UserId);
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Results.Ok(new { status = RequestStatusInfo.Key(r.Status) });
    }

    /// <summary>A plain-language update to the individual's timeline, optionally changing the displayed next step (no dates, Q6).</summary>
    private static async Task<IResult> TeamUpdate(string reference, TeamUpdateBody body, RequestAccess access, RahoonDbContext db, RequestContext rc,
        RequestService svc, AuditLog audit, Notifier notifier)
    {
        var text = Clean(body.Text, 1000);
        if (text is null) Validate.Throw("text", "اكتب التحديث الذي سيراه العميل.");
        var r = await access.ForTeamAsync(reference);
        EnsureWorker(r, rc);
        if (RequestStatusInfo.IsTerminal(r.Status)) throw new ConflictException("closed", "الطلب مغلق.");
        svc.AddUpdate(r, "team_update", "تحديث من فريق رهون", text, authorKind: "team", authorLabel: "فريق رهون", authorUserId: rc.UserId);
        if (Clean(body.NextStep, 600) is { } ns) r.NextStepText = ns;
        notifier.Notify(r.ApplicantUserId, null, "request", "تحديث على طلبك", text, $"/my/requests/{r.Reference}");
        await audit.RecordAsync(RequestWorkflow.Entry(r, "request.update_published", "نشر تحديث للعميل"));
        await db.SaveChangesAsync();
        return Results.Ok(new { ok = true });
    }

    private static async Task<IResult> Message(string reference, MessageBody body, RequestAccess access, RahoonDbContext db, RequestContext rc, IClock clock, Notifier notifier)
    {
        var text = Clean(body.Body, 2000);
        if (text is null) Validate.Throw("body", "اكتب الرسالة.");
        var r = await access.ForTeamAsync(reference);
        EnsureWorker(r, rc);
        if (RequestStatusInfo.IsTerminal(r.Status)) throw new ConflictException("closed", "الطلب مغلق.");
        db.RequestMessages.Add(new RequestMessage
        {
            OrganizationId = r.OrganizationId, RequestId = r.Id, ApplicantUserId = r.ApplicantUserId, AuthorKind = "team", AuthorUserId = rc.UserId,
            AuthorLabel = "فريق رهون", Body = text!, At = clock.UtcNow,
        });
        notifier.Notify(r.ApplicantUserId, null, "request", "رسالة من فريق رهون", text, $"/my/requests/{r.Reference}/messages");
        await db.SaveChangesAsync();
        return Results.Ok(new { ok = true });
    }

    private static async Task<IResult> Note(string reference, MessageBody body, RequestAccess access, RahoonDbContext db, RequestContext rc, IClock clock)
    {
        var text = Clean(body.Body, 2000);
        if (text is null) Validate.Throw("body", "اكتب الملاحظة.");
        var r = await access.ForTeamAsync(reference);
        db.RequestMessages.Add(new RequestMessage
        {
            OrganizationId = r.OrganizationId, RequestId = r.Id, ApplicantUserId = r.ApplicantUserId, AuthorKind = "team", AuthorUserId = rc.UserId,
            AuthorLabel = rc.UserName, Body = text!, IsInternal = true, At = clock.UtcNow,
        });
        await db.SaveChangesAsync();
        return Results.Ok(new { ok = true });
    }

    /// <summary>D-6 variant 1 (Q4 interim): free-text reason in plain language, category «خارج نطاق الخدمة». The individual may object (step 8).</summary>
    private static async Task<IResult> NotEligible(string reference, NotEligibleBody body, RequestAccess access, RahoonDbContext db, RequestContext rc,
        RequestWorkflow workflow, RequestService svc, Notifier notifier)
    {
        var reason = Clean(body.Reason, 1000);
        if (reason is null) Validate.Throw("reason", "اكتب السبب بلغة واضحة يفهمها العميل.");
        await using var tx = await db.Database.BeginTransactionAsync();
        var r = await access.ForTeamAsync(reference);
        EnsureWorker(r, rc);
        await workflow.TransitionAsync(r, "not_eligible", reason);
        svc.AddUpdate(r, "not_eligible", "رأى فريق رهون أن الطلب غير مناسب للخدمة حالياً", reason, authorKind: "team", authorLabel: "فريق رهون", authorUserId: rc.UserId);
        notifier.Notify(r.ApplicantUserId, null, "request", "تحديث على طلبك", reason, $"/my/requests/{r.Reference}");
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Results.Ok(new { status = RequestStatusInfo.Key(r.Status) });
    }

    // ───────── team documents (lender letters, evidence) ─────────

    private static async Task<IResult> Upload(string reference, HttpRequest http, RequestAccess access, RahoonDbContext db, RequestContext rc,
        IDocumentStorage storage, IFileScanner scanner, IClock clock, AuditLog audit, RequestService svc)
    {
        if (!http.HasFormContentType) throw new DomainException("form_required", "أرسل الملف كنموذج متعدد الأجزاء.", 400);
        var form = await http.ReadFormAsync();
        var file = form.Files.GetFile("file") ?? throw new ValidationFailedException(new Dictionary<string, string[]> { ["file"] = ["اختر ملفاً."] });
        var kind = form["kind"].ToString();
        if (!TeamDocKinds.Contains(kind)) Validate.Throw("kind", "نوع المستند غير معروف.");
        var visible = form["visibleToApplicant"].ToString() == "true";
        var name = Clean(form["name"], 200) ?? kind switch
        {
            "lender_letter" => "خطاب الجهة الممولة",
            "coordination_evidence" => "مرفق تنسيق",
            "identity_evidence" => "إثبات هوية",
            _ => "مستند",
        };
        var r = await access.ForTeamAsync(reference);
        EnsureWorker(r, rc);
        if (RequestStatusInfo.IsTerminal(r.Status)) throw new ConflictException("closed", "الطلب مغلق.");

        await using var stream = file.OpenReadStream();
        var stored = await storage.SaveAsync(stream, file.FileName, r.OrganizationId);
        var scan = await scanner.ScanAsync(stored.StorageKey);
        if (scan == ScanStatus.Infected) throw new DomainException("file_infected", "رُفض الملف: فشل فحص الأمان.");
        await using var tx = await db.Database.BeginTransactionAsync();
        var doc = new RequestDocument
        {
            OrganizationId = r.OrganizationId, RequestId = r.Id, ApplicantUserId = r.ApplicantUserId, Kind = kind, Name = name, Source = "team",
            Visibility = visible ? RequestDocumentVisibility.ApplicantAndTeam : RequestDocumentVisibility.TeamOnly, AddedAfterSubmit = true,
        };
        db.RequestDocuments.Add(doc);
        var v = new RequestDocumentVersion
        {
            OrganizationId = r.OrganizationId, RequestId = r.Id, ApplicantUserId = r.ApplicantUserId, DocumentId = doc.Id, VersionNo = 1,
            FileName = Path.GetFileName(file.FileName), ContentType = stored.ContentType, SizeBytes = stored.SizeBytes, Sha256 = stored.Sha256,
            StorageKey = stored.StorageKey, UploadedByUserId = rc.UserId, UploadedByLabel = rc.UserName, UploadedAt = clock.UtcNow, ScanStatus = scan,
        };
        db.RequestDocumentVersions.Add(v);
        doc.VersionCount = 1;
        doc.CurrentVersionId = v.Id;
        if (visible) svc.AddUpdate(r, "document", $"أضاف فريق رهون مستنداً: {name}", authorKind: "team", authorLabel: "فريق رهون", authorUserId: rc.UserId);
        await audit.RecordAsync(RequestWorkflow.Entry(r, "request.document_uploaded", $"رفع {name} (فريق رهون)", detail: $"{kind} · {(visible ? "يظهر للعميل" : "داخلي")} · فحص: {scan}"));
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Results.Ok(new { documentId = doc.Id, versionId = v.Id });
    }

    private static async Task<IResult> Download(string reference, Guid versionId, RequestAccess access, RahoonDbContext db, RequestContext rc,
        IDocumentStorage storage, IClock clock, AuditLog audit)
    {
        var r = await access.ForTeamAsync(reference, track: false);
        var v = await db.RequestDocumentVersions.AsNoTracking().FirstOrDefaultAsync(x => x.Id == versionId && x.RequestId == r.Id) ?? throw new NotFoundException();
        var doc = await db.RequestDocuments.AsNoTracking().FirstAsync(d => d.Id == v.DocumentId);
        var watermark = $"{rc.UserName} · {clock.UtcNow.ToOffset(TimeSpan.FromHours(3)):yyyy-MM-dd HH:mm} · {r.Reference}";
        await audit.RecordAsync(RequestWorkflow.Entry(r, "request.document_downloaded", $"تنزيل {doc.Name} v{v.VersionNo}", detail: watermark));
        await db.SaveChangesAsync();
        return Results.File(await storage.OpenReadAsync(v.StorageKey), v.ContentType, v.FileName);
    }

    internal static string? Clean(string? s, int max) => string.IsNullOrWhiteSpace(s) ? null : s.Trim()[..Math.Min(s.Trim().Length, max)];
}
