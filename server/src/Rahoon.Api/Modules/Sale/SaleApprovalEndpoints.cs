using Microsoft.EntityFrameworkCore;
using Rahoon.Api.Infrastructure.Http;
using Rahoon.Api.Infrastructure.Persistence;
using Rahoon.Api.Infrastructure.Tenancy;
using Rahoon.Api.Infrastructure.Time;
using Rahoon.Api.Modules.Audit;
using Rahoon.Api.Modules.Cases;
using Rahoon.Api.Modules.Communications;
using Rahoon.Api.Modules.Identity;
using Rahoon.Api.Modules.Solutions;
using TaskStatus = Rahoon.Api.Modules.Communications.TaskStatus;

namespace Rahoon.Api.Modules.Sale;

public sealed record SaleDecisionRequest(string Decision, string Reason);

/// <summary>
/// Checker side of the voluntary sale (L27 approval, L32/L33 offer approval). ApprovalRequest.Subject = Sale;
/// the subject is either the sale track (opening decision) or a buyer offer. The approver holds sale.approve,
/// is neither preparer nor submitter, and confirms with a fresh OTP step-up.
/// </summary>
public static class SaleApprovalEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/sale-approvals").RequireOrg(OrganizationKind.Lender).RequirePermission(P.SaleApprove);
        g.MapGet("", Inbox);
        g.MapGet("/{id:guid}", Detail);
        g.MapPost("/{id:guid}/decision", Decide).Idempotent();
    }

    private static IQueryable<ApprovalRequest> VisibleTo(RahoonDbContext db, RequestContext rc) =>
        db.ApprovalRequests.Where(a => a.OrganizationId == rc.OrganizationId && a.Subject == ApprovalSubject.Sale
                                       && a.PreparedByUserId != rc.UserId && a.SubmittedByUserId != rc.UserId);

    private static async Task<IResult> Inbox(string? status, RahoonDbContext db, RequestContext rc, IClock clock)
    {
        var q = VisibleTo(db, rc);
        q = status == "decided" ? q.Where(a => a.Status != ApprovalStatus.Pending && a.DecidedByUserId == rc.UserId) : q.Where(a => a.Status == ApprovalStatus.Pending);
        var today = clock.TodayRiyadh;
        var rows = await q.OrderBy(a => a.DueOn).ThenBy(a => a.SubmittedAt).Take(100).Select(a => new
        {
            a.Id, a.Title, a.DueOn, a.SubmittedAt, a.Status, a.Amount, a.AssignedApproverUserId, a.DecidedAt, a.DecisionReason,
            CaseRef = db.Cases.Where(c => c.Id == a.CaseId).Select(c => c.Reference).First(),
            Submitter = db.Users.Where(u => u.Id == a.SubmittedByUserId).Select(u => u.FullName).First(),
            IsOffer = db.Set<BuyerOffer>().Any(o => o.Id == a.SubjectId),
        }).ToListAsync();
        return Results.Ok(new
        {
            items = rows.Select(r =>
            {
                var d = r.DueOn.DayNumber - today.DayNumber;
                return new
                {
                    r.Id, caseRef = r.CaseRef, title = r.Title, kind = r.IsOffer ? "offer" : "decision", amount = r.Amount, status = r.Status.ToString(),
                    meta = $"أرسله {r.Submitter} · {r.SubmittedAt.ToOffset(TimeSpan.FromHours(3)):yyyy-MM-dd HH:mm}", dueOn = r.DueOn,
                    slaTone = d < 0 ? "err" : d <= 1 ? "warn" : "ok", assignedToMe = r.AssignedApproverUserId == rc.UserId,
                    decidedAt = r.DecidedAt, decisionReason = r.DecisionReason,
                };
            }),
            total = rows.Count,
        });
    }

    private static async Task<IResult> Detail(Guid id, RahoonDbContext db, RequestContext rc, IClock clock, SaleService sales)
    {
        var a = await VisibleTo(db, rc).AsNoTracking().FirstOrDefaultAsync(x => x.Id == id) ?? throw new NotFoundException();
        var c = await db.Cases.AsNoTracking().FirstAsync(x => x.Id == a.CaseId);
        var offer = await db.Set<BuyerOffer>().AsNoTracking().FirstOrDefaultAsync(o => o.Id == a.SubjectId);
        var s = await db.Set<VoluntarySale>().AsNoTracking().FirstAsync(x => x.Id == (offer != null ? offer.SaleId : a.SubjectId));
        var debt = await sales.DebtAsync(c, s);
        var consent = await sales.ConsentAsync(s.Id);
        object figures;
        string[] effects;
        if (offer is null)
        {
            var est = s.ValuationAmount is { } v ? SaleCalculator.Estimate(v, debt, s.BrokerRate, s.OtherFeesEstimate) : null;
            figures = new { kind = "decision", valuation = s.ValuationAmount, outstandingDebt = debt, expectedSurplus = est?.ExpectedSurplus, expectedShortfall = est?.ExpectedShortfall, proposedMinPrice = s.ProposedMinPrice, request = s.RequestText };
            effects =
            [
                "تنتقل الحالة إلى «بيع طوعي»؛ تُعلَّق العروض الودية القائمة.",
                "يُطلب من المالكة توقيع موافقة صريحة بنطاق البيع وحدّها الأدنى.",
                "لا يُكلّف وسيط ولا يُعرض العقار قبل الموافقة.",
            ];
        }
        else
        {
            var f = SaleCalculator.Offer(offer.Price, debt, s.BrokerRate, consent?.MinPrice);
            figures = new
            {
                kind = "offer", code = offer.Code, price = offer.Price, payment = SaleService.PaymentLabel(offer.PaymentMethod), conditions = offer.Conditions,
                debtPayoff = f.LenderRecovery, commission = f.BrokerCommission, ownerSurplus = f.OwnerSurplus, shortfall = f.Shortfall, meetsMinimum = f.MeetsMinimum,
                ownerAcceptedAt = offer.OwnerDecisionAt, recommendation = offer.Recommendation,
            };
            effects =
            [
                "يُعتمد العرض وتُغلق بقية العروض، ويُسحب وصول الوسيط إلى ملف البيع.",
                "تبدأ متابعة التنفيذ: خطاب المديونية، اتفاقية البيع، استلام الثمن وفك الرهن (خارج المنصة).",
                "بعد استلام الثمن وفك الرهن تنتقل الحالة إلى «بانتظار التسوية المالية».",
            ];
        }
        return Results.Ok(new
        {
            a.Id, caseRef = c.Reference, title = a.Title, status = a.Status.ToString(), note = a.SubmitterNote, evidence = a.Evidence, figures, effects,
            canDecide = a.Status == ApprovalStatus.Pending && a.AssignedApproverUserId == rc.UserId,
            blockedReason = a.Status != ApprovalStatus.Pending ? "تم القرار على هذا الطلب." : a.AssignedApproverUserId != rc.UserId ? "الطلب مسند لمعتمد آخر." : null,
            stepUpActive = rc.StepUpUntil > clock.UtcNow,
        });
    }

    private static async Task<IResult> Decide(Guid id, SaleDecisionRequest req, RahoonDbContext db, RequestContext rc, IClock clock,
        CaseWorkflow workflow, SaleService sales, AuditLog audit, Notifier notifier)
    {
        var decision = req.Decision?.ToLowerInvariant();
        new Validator().Require(decision is "approve" or "return" or "reject", "decision", "اختر القرار.")
            .Require(!string.IsNullOrWhiteSpace(req.Reason) && req.Reason.Trim().Length >= 10, "reason", "سبب القرار إلزامي (10 أحرف على الأقل).").ThrowIfInvalid();
        EndpointAccess.EnsureStepUp(rc, clock);

        await using var tx = await db.Database.BeginTransactionAsync();
        var a = await VisibleTo(db, rc).FirstOrDefaultAsync(x => x.Id == id) ?? throw new NotFoundException();
        if (a.Status != ApprovalStatus.Pending) throw new ConflictException("already_decided", "تم القرار على هذا الطلب مسبقاً.");
        if (a.AssignedApproverUserId != rc.UserId) throw new ForbiddenException("الطلب مسند لمعتمد آخر.");
        var c = await db.Cases.FirstAsync(x => x.Id == a.CaseId);
        var reason = req.Reason.Trim();
        var offer = await db.Set<BuyerOffer>().FirstOrDefaultAsync(o => o.Id == a.SubjectId);
        string result;
        if (offer is null) result = await DecideOpeningAsync(a, c, decision!, reason, db, rc, clock, workflow, sales, audit, notifier);
        else result = await DecideOfferAsync(a, c, offer, decision!, reason, db, rc, clock, sales, audit, notifier);

        a.Status = decision switch { "approve" => ApprovalStatus.Approved, "return" => ApprovalStatus.Returned, _ => ApprovalStatus.Rejected };
        a.DecidedByUserId = rc.UserId;
        a.DecidedAt = clock.UtcNow;
        a.DecisionReason = reason;
        a.StepUpVerified = true;
        foreach (var t in await db.Tasks.Where(t => t.CaseId == c.Id && t.Kind == "sale_approval" && t.Status == TaskStatus.Open).ToListAsync())
        { t.Status = TaskStatus.Done; t.CompletedAt = clock.UtcNow; }
        notifier.Notify(a.SubmittedByUserId, c.OrganizationId, "approval", $"{result}: {a.Title}", reason, $"/cases/{c.Reference}/sale", c.Id, decision == "approve" ? "ok" : "warn");
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Results.Ok(new { status = a.Status.ToString(), caseStatus = CaseStatusInfo.Key(c.Status), result });
    }

    private static async Task<string> DecideOpeningAsync(ApprovalRequest a, Case c, string decision, string reason, RahoonDbContext db, RequestContext rc, IClock clock,
        CaseWorkflow workflow, SaleService sales, AuditLog audit, Notifier notifier)
    {
        var s = await db.Set<VoluntarySale>().FirstAsync(x => x.Id == a.SubjectId);
        if (decision == "approve")
        {
            // The owner's documented request is re-checked here: a withdrawn/closed track blocks even if an old consent record exists.
            var extra = new List<string>();
            if (s.Status != SaleStatus.PendingDecision || s.RequestConsentRecordId is null) extra.Add("لا يوجد طلب قائم من المالك لفتح مسار البيع.");
            // Actor = this approval decision: sale.approve, separation of duties and step-up were enforced above,
            // so the engine runs the transition as system-initiated (its sale.manage actor check is the maker's).
            await workflow.TransitionAsync(c, "start_voluntary_sale", reason, null, systemInitiated: true,
                evidence: [$"sale:{s.BuyerReference}", $"approval:{a.Id}"], extraGuardFailures: extra);
            var now = clock.UtcNow;
            s.Status = SaleStatus.AwaitingConsent;
            s.OpenedAt = now;
            s.DecisionApprovedByUserId = rc.UserId;
            // Rule 11: opening the sale suspends existing amicable offers and pending solution approvals.
            foreach (var o in await db.Offers.Where(o => o.CaseId == c.Id && (o.Status == OfferStatus.Sent || o.Status == OfferStatus.Countered)).ToListAsync())
            {
                o.Status = OfferStatus.Withdrawn;
                o.RespondedAt = now;
                var v = await db.Solutions.FirstOrDefaultAsync(x => x.Id == o.SolutionVersionId);
                if (v is not null) v.Status = SolutionStatus.Superseded;
            }
            foreach (var p in await db.ApprovalRequests.Where(x => x.CaseId == c.Id && x.Subject == ApprovalSubject.Solution && x.Status == ApprovalStatus.Pending).ToListAsync())
            { p.Status = ApprovalStatus.Superseded; p.DecidedAt = now; p.DecisionReason = "فُتح مسار البيع الطوعي"; }
            await sales.NotifyOwnerAsync(c, "نحتاج موافقتك على نطاق البيع", "حدد أقل سعر تقبله وأوقات الزيارة، ثم أكّد برمز التحقق.", "/owner/sale/consent");
            await audit.RecordAsync(new AuditEntry("sale.decision_approved", "اعتماد فتح مسار البيع الطوعي", c.Id, c.Reference, Reason: reason,
                Detail: "تأكيد برمز التحقق · عُلّقت العروض الودية القائمة · طُلبت موافقة المالك بنطاق البيع", Evidence: a.Evidence, OrganizationId: c.OrganizationId));
            return "اعتُمد";
        }
        if (decision == "return")
        {
            s.Status = SaleStatus.Requested;
            s.DecisionApprovalRequestId = null;
            await audit.RecordAsync(new AuditEntry("sale.decision_returned", "إعادة قرار البيع الطوعي للمراجعة", c.Id, c.Reference, Reason: reason, OrganizationId: c.OrganizationId));
            return "أُعيد";
        }
        s.Status = SaleStatus.DecisionRejected;
        s.ClosedAt = clock.UtcNow;
        s.CloseReason = "decision_rejected";
        await sales.NotifyOwnerAsync(c, "لم يُعتمد فتح مسار البيع الطوعي", "سيتواصل معك مسؤول حالتك لشرح السبب ومناقشة الخيارات الأخرى.", "/owner/sale");
        await audit.RecordAsync(new AuditEntry("sale.decision_rejected", "رفض فتح مسار البيع الطوعي", c.Id, c.Reference, Reason: reason, Detail: "لا تُحال الحالة تلقائياً", OrganizationId: c.OrganizationId));
        return "رُفض";
    }

    private static async Task<string> DecideOfferAsync(ApprovalRequest a, Case c, BuyerOffer offer, string decision, string reason, RahoonDbContext db, RequestContext rc, IClock clock,
        SaleService sales, AuditLog audit, Notifier notifier)
    {
        var s = await db.Set<VoluntarySale>().FirstAsync(x => x.Id == offer.SaleId);
        var consent = await sales.ConsentAsync(s.Id, track: true);
        var now = clock.UtcNow;
        if (decision == "approve")
        {
            var failures = new List<string>();
            if (c.Status != CaseStatus.VoluntarySale || s.Status != SaleStatus.Active) failures.Add("مسار البيع ليس في مرحلة العروض.");
            if (offer.Status != BuyerOfferStatus.PendingApproval) failures.Add("العرض ليس بانتظار الاعتماد.");
            if (offer.ValidUntil < clock.TodayRiyadh) failures.Add("انتهت صلاحية العرض.");
            if (consent is null || offer.Price < consent.MinPrice) failures.Add("العرض أقل من الحد الأدنى للمالكة.");
            var debt = await sales.DebtAsync(c, s);
            var f = SaleCalculator.Offer(offer.Price, debt, s.BrokerRate, consent?.MinPrice);
            if (f.Shortfall > 0 && !rc.RoleKeys.Overlaps([SystemRoles.SeniorApprover, SystemRoles.RiskCommittee]))
                failures.Add("العرض يترك عجزاً على المديونية ويتطلب معتمداً أول.");
            if (failures.Count > 0)
            {
                await audit.RecordBlockedAsync(new AuditEntry("sale.offer_approval_blocked", $"محاولة اعتماد عرض الشراء {offer.Code} محجوبة", c.Id, c.Reference,
                    Detail: "المانع: " + string.Join(" · ", failures), OrganizationId: c.OrganizationId));
                throw new DomainException("guard_failed", "لا يمكن اعتماد العرض الآن.", StatusCodes.Status422UnprocessableEntity, failures);
            }
            offer.Status = BuyerOfferStatus.Approved;
            offer.DecidedAt = now;
            s.Status = SaleStatus.OfferApproved;
            s.AcceptedOfferId = offer.Id;
            s.ListingStatus = ListingStatus.Closed;
            if (consent is not null) { consent.Status = SaleConsentStatus.Fulfilled; consent.FulfilledAt ??= now; }
            foreach (var other in await db.Set<BuyerOffer>().Where(o => o.SaleId == s.Id && o.Id != offer.Id
                         && o.Status != BuyerOfferStatus.Rejected && o.Status != BuyerOfferStatus.OwnerDeclined && o.Status != BuyerOfferStatus.Withdrawn).ToListAsync())
                other.Status = BuyerOfferStatus.Superseded;
            await sales.RevokeBrokerAsync(s, "قبول عرض شراء");
            var approver = await db.Users.Where(u => u.Id == rc.UserId).Select(u => u.FullName).FirstAsync();
            sales.CreateTrackingSteps(s, offer, DateOnly.FromDateTime((offer.OwnerDecisionAt ?? now).ToOffset(TimeSpan.FromHours(3)).DateTime), clock.TodayRiyadh,
                $"{approver} + القانونية", f.OwnerSurplus);
            await sales.NotifyOwnerAsync(c, $"اعتمد المصرف العرض {offer.Code}", "نقل الملكية والسداد يتمّان خارج المنصة، ونطلعك على كل خطوة.", "/owner/sale");
            await audit.RecordAsync(new AuditEntry("sale.offer_approved", $"اعتماد عرض الشراء {offer.Code}", c.Id, c.Reference, Reason: reason,
                Detail: $"{offer.Price:N2} · سداد المديونية {f.LenderRecovery:N2} · للمالك {f.OwnerSurplus:N2} (تقديري) · تأكيد برمز التحقق · سُحب وصول الوسيط",
                Evidence: a.Evidence, OrganizationId: c.OrganizationId));
            return "اعتُمد";
        }
        // Return keeps the owner's acceptance so the maker can resubmit; reject voids it and reopens the owner's withdrawal right.
        offer.Status = decision == "return" ? BuyerOfferStatus.OwnerAccepted : BuyerOfferStatus.Rejected;
        offer.DecidedAt = decision == "return" ? null : now;
        offer.ApprovalRequestId = decision == "return" ? null : offer.ApprovalRequestId;
        if (decision == "reject" && consent is { Status: SaleConsentStatus.Fulfilled } && consent.MandateEnd >= clock.TodayRiyadh)
        { consent.Status = SaleConsentStatus.Signed; consent.FulfilledAt = null; }
        if (decision == "reject")
            await sales.NotifyOwnerAsync(c, $"لم يعتمد المصرف العرض {offer.Code}", "يستمر عرض العقار، ويمكنك الانسحاب حتى قبول عرض آخر.", "/owner/sale");
        await audit.RecordAsync(new AuditEntry(decision == "return" ? "sale.offer_approval_returned" : "sale.offer_rejected",
            decision == "return" ? $"إعادة طلب اعتماد العرض {offer.Code}" : $"رفض عرض الشراء {offer.Code}", c.Id, c.Reference, Reason: reason, OrganizationId: c.OrganizationId));
        return decision == "return" ? "أُعيد" : "رُفض";
    }
}
