using Microsoft.EntityFrameworkCore;
using Rahoon.Api.Infrastructure.Http;
using Rahoon.Api.Infrastructure.Integrations;
using Rahoon.Api.Infrastructure.Persistence;
using Rahoon.Api.Infrastructure.Security;
using Rahoon.Api.Infrastructure.Tenancy;
using Rahoon.Api.Infrastructure.Time;
using Rahoon.Api.Modules.Audit;
using Rahoon.Api.Modules.Cases;
using Rahoon.Api.Modules.Communications;
using Rahoon.Api.Modules.Identity;
using TaskStatus = Rahoon.Api.Modules.Communications.TaskStatus;

namespace Rahoon.Api.Modules.Solutions;

public sealed record DecisionRequest(string Decision, string Reason, uint? OpenedVersion);

/// <summary>Approval inbox and decisions (L16). Hidden from whoever prepared or reviewed the request.</summary>
public static class ApprovalEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/approvals").RequireOrg(OrganizationKind.Lender);
        g.MapGet("", Inbox).RequirePermission(P.SolutionApprove);
        g.MapGet("/{id:guid}", Detail).RequirePermission(P.SolutionApprove);
        g.MapPost("/{id:guid}/decision", Decide).RequirePermission(P.SolutionApprove).Idempotent();
        g.MapPost("/{id:guid}/remind", Remind).RequirePermission(P.CaseView).Idempotent();
    }

    private static IQueryable<ApprovalRequest> VisibleTo(RahoonDbContext db, RequestContext rc) =>
        db.ApprovalRequests.Where(a => a.OrganizationId == rc.OrganizationId && a.Subject == ApprovalSubject.Solution
                                       && a.PreparedByUserId != rc.UserId && a.SubmittedByUserId != rc.UserId);

    private static async Task<IResult> Inbox(string? status, RahoonDbContext db, RequestContext rc, IClock clock)
    {
        var today = clock.TodayRiyadh;
        var q = VisibleTo(db, rc);
        q = status == "decided" ? q.Where(a => a.Status != ApprovalStatus.Pending && a.DecidedByUserId == rc.UserId) : q.Where(a => a.Status == ApprovalStatus.Pending);
        var rows = await q.OrderBy(a => a.DueOn).ThenBy(a => a.SubmittedAt).Take(100)
            .Select(a => new
            {
                a.Id, a.Title, a.DueOn, a.SubmittedAt, a.Status, a.AssignedApproverUserId, a.Amount, a.WaiverPercent, a.SubjectId, a.DecidedAt, a.DecisionReason,
                CaseRef = db.Cases.Where(c => c.Id == a.CaseId).Select(c => c.Reference).First(),
                Owner = db.Parties.Where(p => p.CaseId == a.CaseId && p.IsPrimary).Select(p => p.DisplayName).FirstOrDefault(),
                Kind = db.Solutions.Where(s => s.Id == a.SubjectId).Select(s => s.Kind).First(),
                Term = db.Solutions.Where(s => s.Id == a.SubjectId).Select(s => s.TermMonths).First(),
                Submitter = db.Users.Where(u => u.Id == a.SubmittedByUserId).Select(u => u.FullName).First(),
                Assignee = db.Users.Where(u => u.Id == a.AssignedApproverUserId).Select(u => u.FullName).FirstOrDefault(),
            }).ToListAsync();

        var items = new List<object>();
        foreach (var r in rows)
        {
            var v = await db.Solutions.AsNoTracking().FirstAsync(s => s.Id == r.SubjectId);
            var withinMyLimit = await ApprovalRouting.UserCanApproveAsync(db, rc.OrganizationId!.Value, rc.UserId, v);
            var d = r.DueOn.DayNumber - today.DayNumber;
            items.Add(new
            {
                r.Id, caseRef = r.CaseRef, title = $"{KindLabel(r.Kind)} {r.Term} شهراً — {r.Owner}", requestTitle = r.Title,
                meta = $"أرسلته {r.Submitter} · {r.SubmittedAt.ToOffset(TimeSpan.FromHours(3)):yyyy-MM-dd HH:mm}",
                slaTone = d < 0 ? "err" : d <= 1 ? "warn" : "ok",
                slaText = d < 0 ? CaseDisplay.LateDays(-d) : d == 0 ? "اليوم" : $"متبقٍ {CaseDisplay.Days(d)}",
                dueOn = r.DueOn, amount = r.Amount, status = r.Status.ToString(),
                assignedToMe = r.AssignedApproverUserId == rc.UserId, assignee = r.Assignee,
                escalated = !withinMyLimit, canDecide = r.Status == ApprovalStatus.Pending && r.AssignedApproverUserId == rc.UserId && withinMyLimit,
                decidedAt = r.DecidedAt, decisionReason = r.DecisionReason,
            });
        }
        return Results.Ok(new { items, total = items.Count });
    }

    public static string KindLabel(SolutionKind k) => k switch
    {
        SolutionKind.Reschedule => "إعادة جدولة",
        SolutionKind.ReducedPayoff => "سداد مخفض",
        SolutionKind.GracePeriod => "فترة سماح",
        SolutionKind.VoluntarySale => "بيع طوعي",
        _ => k.ToString(),
    };

    private static async Task<IResult> Detail(Guid id, RahoonDbContext db, RequestContext rc, IClock clock)
    {
        var a = await VisibleTo(db, rc).AsNoTracking().FirstOrDefaultAsync(x => x.Id == id) ?? throw new NotFoundException();
        var v = await db.Solutions.AsNoTracking().FirstAsync(s => s.Id == a.SubjectId);
        var previous = await db.Solutions.AsNoTracking().Where(s => s.CaseId == v.CaseId && s.VersionNo < v.VersionNo).OrderByDescending(s => s.VersionNo).FirstOrDefaultAsync();
        var c = await db.Cases.AsNoTracking().FirstAsync(x => x.Id == a.CaseId);
        var owner = await db.Parties.AsNoTracking().Where(p => p.CaseId == c.Id && p.IsPrimary).Select(p => p.DisplayName).FirstAsync();
        var submitter = await db.Users.Where(u => u.Id == a.SubmittedByUserId).Select(u => u.FullName).FirstAsync();
        var policy = await db.ApprovalLimitPolicies.Include(p => p.Tiers).AsNoTracking()
            .Where(p => p.OrganizationId == c.OrganizationId && p.Status == "effective").OrderByDescending(p => p.VersionNo).FirstAsync();
        var myRoles = rc.RoleKeys;
        var myTier = policy.Tiers.Where(t => t.CanApprove && myRoles.Contains(t.RoleKey)).OrderByDescending(t => t.Rank).FirstOrDefault();
        var within = await ApprovalRouting.UserCanApproveAsync(db, c.OrganizationId, rc.UserId, v);
        var validUntil = clock.TodayRiyadh.AddDays(v.OfferValidityDays);
        var amountOk = myTier?.MaxAmount is null || v.OutstandingAtPreparation <= myTier.MaxAmount;
        var waiverOk = myTier?.MaxWaiverPercent is null || v.WaiverPercent <= myTier.MaxWaiverPercent;

        return Results.Ok(new
        {
            a.Id, caseRef = c.Reference, status = a.Status.ToString(), eyebrow = $"طلب اعتماد · أرسلته {submitter} {a.SubmittedAt.ToOffset(TimeSpan.FromHours(3)):yyyy-MM-dd HH:mm}",
            title = $"{KindLabel(v.Kind)} {v.TermMonths} شهراً — {owner}", version = v.VersionNo, openedVersion = v.Version,
            figures = new object[]
            {
                new { label = "القسط", value = v.InstallmentAmount.ToString("N2"), sub = v.Dsr is null ? "—" : $"الاستقطاع {v.Dsr * 100:0.#}% {(v.Dsr <= v.DsrLimit ? "✓" : "✕")}", tone = v.Dsr <= v.DsrLimit ? "ok" : "err" },
                new { label = "المدة", value = $"{v.TermMonths} شهراً", sub = previous is null ? "الإصدار الأول" : $"كان {previous.TermMonths} في v{previous.VersionNo}", tone = "muted" },
                new { label = "التنازل", value = v.WaiverAmount.ToString("N2"), sub = myTier?.MaxWaiverPercent is null ? $"هنا {v.WaiverPercent * 100:0.##}%" : $"حدك {myTier.MaxWaiverPercent * 100:0.#}% · هنا {v.WaiverPercent * 100:0.##}%", tone = waiverOk ? "ok" : "err" },
                new { label = "المبلغ", value = v.RescheduledAmount.ToString("N2"), sub = myTier?.MaxAmount is null ? "بلا حد" : $"حدك {myTier.MaxAmount:N0}", tone = amountOk ? "ok" : "err" },
            },
            reviewerNote = a.SubmitterNote,
            evidence = a.Evidence,
            links = new { solution = $"/cases/{c.Reference}/solutions/{v.VersionNo}", compare = $"/cases/{c.Reference}/solutions/compare", preview = $"/cases/{c.Reference}/solutions/{v.VersionNo}/preview" },
            effects = new
            {
                approve = new[]
                {
                    "تنتقل الحالة إلى «بانتظار العميل»، ويُرسل العرض للمالك بقالب «عرض إعادة جدولة».",
                    $"صلاحية العرض {v.OfferValidityDays} أيام: حتى {validUntil:yyyy-MM-dd}.",
                }.Concat(v.WaiverPercent > 0.01m ? ["يُنشأ إشعار امتثال لأن التنازل > 1% (افتراض)."] : []).ToArray(),
                @return = new[] { $"يُعاد الإصدار v{v.VersionNo} للمُعِدّ مع سببك، وتعود الحالة إلى «حل مقترح».", "أي تعديل ينشئ إصداراً جديداً يمر بالسلسلة نفسها.", "لا يرى المالك شيئاً." },
                reject = new[] { $"يُرفض الإصدار v{v.VersionNo} نهائياً وتعود الحالة إلى «حل مقترح».", "لا يُحال الملف تلقائياً ولا يُرسل شيء للمالك." },
            },
            canDecide = a.Status == ApprovalStatus.Pending && a.AssignedApproverUserId == rc.UserId && within,
            escalated = !within,
            blockedReason = a.Status != ApprovalStatus.Pending ? "تم القرار على هذا الطلب." : a.AssignedApproverUserId != rc.UserId ? "الطلب مسند لمعتمد آخر." : !within ? "الطلب فوق حدك؛ يُصعّد لمعتمد أعلى." : null,
            stepUpActive = rc.StepUpUntil > clock.UtcNow,
        });
    }

    private static async Task<IResult> Decide(Guid id, DecisionRequest req, RahoonDbContext db, RequestContext rc, IClock clock,
        CaseWorkflow workflow, AuditLog audit, Notifier notifier, OfferService offers)
    {
        var decision = req.Decision?.ToLowerInvariant();
        new Validator().Require(decision is "approve" or "return" or "reject", "decision", "اختر القرار.")
            .Require(!string.IsNullOrWhiteSpace(req.Reason) && req.Reason.Trim().Length >= 10, "reason", "سبب القرار إلزامي (10 أحرف على الأقل).").ThrowIfInvalid();
        EndpointAccess.EnsureStepUp(rc, clock);

        await using var tx = await db.Database.BeginTransactionAsync();
        var a = await VisibleTo(db, rc).FirstOrDefaultAsync(x => x.Id == id) ?? throw new NotFoundException();
        var c = await db.Cases.FirstAsync(x => x.Id == a.CaseId);
        var v = await db.Solutions.FirstAsync(s => s.Id == a.SubjectId);
        if (a.Status != ApprovalStatus.Pending) throw new ConflictException("already_decided", "تم القرار على هذا الطلب مسبقاً.");
        if (req.OpenedVersion is { } ov && ov != v.Version) throw new ConflictException("request_changed", "تغيّر الطلب بعد فتحه. أعد تحميل الصفحة وراجع أحدث إصدار.");
        if (a.AssignedApproverUserId != rc.UserId) throw new ForbiddenException("الطلب مسند لمعتمد آخر.");
        if (decision == "approve" && !await ApprovalRouting.UserCanApproveAsync(db, c.OrganizationId, rc.UserId, v))
        {
            await audit.RecordBlockedAsync(new AuditEntry("approval.blocked", $"محاولة اعتماد فوق الحد: الحل v{v.VersionNo}", c.Id, c.Reference, Detail: "المبلغ أو نسبة التنازل فوق حد المعتمد", OrganizationId: c.OrganizationId));
            throw new DomainException("over_limit", "الطلب فوق حدك؛ يجب تصعيده لمعتمد أعلى.", 403);
        }

        var reason = req.Reason.Trim();
        var expected = CaseStatus.InternalApproval;
        switch (decision)
        {
            case "approve":
                await workflow.TransitionAsync(c, "approve_and_offer", reason, expected, evidence: [$"solution:v{v.VersionNo}", $"approval:{a.Id}"]);
                v.Status = SolutionStatus.Approved;
                await offers.SendAsync(c, v, rc.UserId);
                break;
            case "return":
                await workflow.TransitionAsync(c, "return_to_solution", reason, expected);
                v.Status = SolutionStatus.Returned;
                v.ReturnReason = reason;
                notifier.Notify(v.PreparedByUserId, c.OrganizationId, "approval", $"أُعيد الحل v{v.VersionNo} للتعديل", reason, $"/cases/{c.Reference}", c.Id, "warn");
                break;
            default:
                await workflow.TransitionAsync(c, "return_to_solution", reason, expected);
                v.Status = SolutionStatus.Rejected;
                v.ReturnReason = reason;
                notifier.Notify(v.PreparedByUserId, c.OrganizationId, "approval", $"رُفض الحل v{v.VersionNo}", reason, $"/cases/{c.Reference}", c.Id, "err");
                break;
        }
        a.Status = decision switch { "approve" => ApprovalStatus.Approved, "return" => ApprovalStatus.Returned, _ => ApprovalStatus.Rejected };
        a.DecidedByUserId = rc.UserId;
        a.DecidedAt = clock.UtcNow;
        a.DecisionReason = reason;
        a.StepUpVerified = true;
        notifier.Notify(a.SubmittedByUserId, c.OrganizationId, "approval",
            decision == "approve" ? $"اعتُمد الحل v{v.VersionNo} وأُرسل للمالك" : decision == "return" ? $"أُعيد الحل v{v.VersionNo}" : $"رُفض الحل v{v.VersionNo}",
            reason, $"/cases/{c.Reference}", c.Id, decision == "approve" ? "ok" : "warn");
        foreach (var t in await db.Tasks.Where(t => t.CaseId == c.Id && t.Kind == "approval" && t.Status == TaskStatus.Open).ToListAsync())
        { t.Status = TaskStatus.Done; t.CompletedAt = clock.UtcNow; }

        await audit.RecordAsync(new AuditEntry("approval.decision",
            decision == "approve" ? $"اعتماد الحل v{v.VersionNo}" : decision == "return" ? $"إعادة الحل v{v.VersionNo} للتعديل" : $"رفض الحل v{v.VersionNo}",
            c.Id, c.Reference, Reason: reason, Detail: "تأكيد برمز التحقق", Evidence: a.Evidence, OrganizationId: c.OrganizationId));
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Results.Ok(new { status = a.Status.ToString(), caseStatus = CaseStatusInfo.Key(c.Status) });
    }

    private static async Task<IResult> Remind(Guid id, RahoonDbContext db, RequestContext rc, Notifier notifier, AuditLog audit, IClock clock)
    {
        var a = await db.ApprovalRequests.FirstOrDefaultAsync(x => x.Id == id && x.OrganizationId == rc.OrganizationId && x.Status == ApprovalStatus.Pending) ?? throw new NotFoundException();
        var c = await db.Cases.FirstAsync(x => x.Id == a.CaseId);
        var recent = await db.AuditEvents.AnyAsync(e => e.CaseId == c.Id && e.Type == "approval.reminder" && e.OccurredAt > clock.UtcNow.AddHours(-12));
        if (recent) throw new DomainException("reminder_recent", "أُرسل تذكير خلال آخر 12 ساعة.", 429);
        if (a.AssignedApproverUserId is { } approver)
            notifier.Notify(approver, c.OrganizationId, "approval", $"تذكير: {a.Title} · {c.Reference}", $"المهلة {a.DueOn:yyyy-MM-dd}", $"/approvals?request={a.Id}", c.Id, "warn");
        await audit.RecordAsync(new AuditEntry("approval.reminder", $"إرسال تذكير للمعتمد — {a.Title}", c.Id, c.Reference, OrganizationId: c.OrganizationId));
        await db.SaveChangesAsync();
        return Results.Ok(new { sent = true });
    }
}

/// <summary>Creates the owner-facing offer from an approved, locked solution version and notifies the owner.</summary>
public sealed class OfferService(RahoonDbContext db, IClock clock, Notifier notifier, ISmsGateway sms, PiiProtector pii)
{
    public async Task<Offer> SendAsync(Case c, SolutionVersion v, Guid sentBy)
    {
        var offer = new Offer
        {
            OrganizationId = c.OrganizationId, CaseId = c.Id, SolutionVersionId = v.Id, VersionNo = v.VersionNo, SentAt = clock.UtcNow,
            ValidUntil = clock.TodayRiyadh.AddDays(v.OfferValidityDays), SentByUserId = sentBy,
        };
        db.Offers.Add(offer);
        v.Status = SolutionStatus.Offered;
        c.StageDueOn = offer.ValidUntil;
        var org = await db.Organizations.FirstAsync(o => o.Id == c.OrganizationId);
        db.NegotiationEntries.Add(new NegotiationEntry
        {
            OrganizationId = c.OrganizationId, CaseId = c.Id, OfferId = offer.Id, Kind = NegotiationKind.Offer, AuthorType = "lender", AuthorUserId = sentBy,
            AuthorLabel = $"{org.NameAr} — العرض v{v.VersionNo}", At = clock.UtcNow,
            Body = v.Kind == SolutionKind.ReducedPayoff
                ? $"سداد مخفض بمبلغ {v.RescheduledAmount:N2} ريال دفعة واحدة."
                : $"قسط {v.InstallmentAmount:N2} ريال لمدة {v.TermMonths} شهراً، يبدأ {v.FirstDueDate:yyyy-MM-dd}" + (v.WaiverAmount > 0 ? "، مع إلغاء غرامات التأخير." : "."),
        });

        var template = await db.Templates.AsNoTracking().Where(t => t.Code == "TPL-OFFER-01" && t.Status == TemplateStatus.Published && (t.OrganizationId == null || t.OrganizationId == c.OrganizationId)).OrderByDescending(t => t.OrganizationId != null).ThenByDescending(t => t.VersionNo).FirstOrDefaultAsync();
        var access = await db.OwnerAccesses.FirstOrDefaultAsync(o => o.CaseId == c.Id);
        var party = await db.Parties.FirstOrDefaultAsync(p => p.CaseId == c.Id && p.IsPrimary);
        if (access?.UserId is { } ownerUser)
            notifier.Notify(ownerUser, c.OrganizationId, "offer", "عرض جديد بانتظار ردك",
                template?.BodyAr.Replace("{القسط}", v.InstallmentAmount.ToString("N2")).Replace("{المدة}", $"{v.TermMonths} شهراً").Replace("{تاريخ_الصلاحية}", offer.ValidUntil.ToString("yyyy-MM-dd")),
                "/owner/options", c.Id);
        if (party?.PhoneEnc is not null && template?.BodySms is not null)
            await sms.SendAsync(pii.Unprotect(party.PhoneEnc), template.BodySms.Replace("{تاريخ_الصلاحية}", offer.ValidUntil.ToString("yyyy-MM-dd")), c.OrganizationId, c.Id, template.Code);
        return offer;
    }
}
