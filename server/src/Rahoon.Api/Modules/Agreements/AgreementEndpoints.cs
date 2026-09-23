using Microsoft.EntityFrameworkCore;
using Rahoon.Api.Infrastructure.Http;
using Rahoon.Api.Infrastructure.Integrations;
using Rahoon.Api.Infrastructure.Persistence;
using Rahoon.Api.Infrastructure.Tenancy;
using Rahoon.Api.Infrastructure.Time;
using Rahoon.Api.Modules.Audit;
using Rahoon.Api.Modules.Cases;
using Rahoon.Api.Modules.Communications;
using Rahoon.Api.Modules.Identity;
using Rahoon.Api.Modules.Solutions;
using TaskStatus = Rahoon.Api.Modules.Communications.TaskStatus;

namespace Rahoon.Api.Modules.Agreements;

public sealed record LegalReviewRequest(string Note);
public sealed record RecordPaymentRequest(int InstallmentNo, decimal Amount, DateOnly ReceivedOn, string BankReference, string? VarianceReason, Guid? ProofDocumentVersionId);
public sealed record PaymentDecisionRequest(string? Reason);
public sealed record BreachOutcomeRequest(string Outcome, string Note);
public sealed record NegotiationNoteRequest(string Body, bool Internal);
public sealed record DeclineCounterRequest(string Reason);
public sealed record ExtensionDecisionRequest(bool Approve, string? Reason);

public static class AgreementEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/cases/{reference}").RequirePermission(P.CaseView);
        g.MapGet("/negotiation", Negotiation);
        g.MapPost("/negotiation/notes", AddNote).RequirePermission(P.NegotiationManage).Idempotent();
        g.MapPost("/negotiation/decline-counter", DeclineCounter).RequirePermission(P.NegotiationManage).Idempotent();
        g.MapPost("/offer-extensions/{id:guid}/decision", DecideExtension).RequirePermission(P.NegotiationManage).Idempotent();

        g.MapGet("/agreement", Agreement);
        g.MapPost("/agreement/legal-review", LegalReview).RequirePermission(P.AgreementActivate).Idempotent();
        g.MapPost("/agreement/schedule", CreateSchedule).RequirePermission(P.AgreementPrepare).Idempotent();
        g.MapPost("/agreement/activate", Activate).RequirePermission(P.AgreementActivate).Idempotent();

        g.MapGet("/payments", Payments);
        g.MapPost("/payments", RecordPayment).RequirePermission(P.PaymentRecord).Idempotent();
        g.MapPost("/payments/{id:guid}/match", MatchPayment).RequirePermission(P.PaymentMatch).Idempotent();
        g.MapPost("/payments/{id:guid}/reject", RejectPayment).RequirePermission(P.PaymentMatch).Idempotent();
        g.MapGet("/breach", Breach);
        g.MapPost("/breach/{id:guid}/outcome", BreachOutcome).RequirePermission(P.BreachManage).Idempotent();
        app.MapGet("/api/payments/check-reference", CheckReference).RequirePermission(P.PaymentRecord);
    }

    // ───────── Negotiation (L18) ─────────

    private static async Task<IResult> Negotiation(string reference, CaseAccess access, RahoonDbContext db, RequestContext rc, IClock clock)
    {
        var c = await access.GetAsync(reference, track: false);
        var entries = await db.NegotiationEntries.AsNoTracking().Where(e => e.CaseId == c.Id).OrderBy(e => e.At).ToListAsync();
        var offers = await db.Offers.AsNoTracking().Where(o => o.CaseId == c.Id).OrderByDescending(o => o.SentAt).ToListAsync();
        var latestOffer = offers.FirstOrDefault();
        var counter = entries.LastOrDefault(e => e.Kind == NegotiationKind.OwnerCounter);
        SolutionVersion? offered = latestOffer is null ? null : await db.Solutions.AsNoTracking().FirstAsync(s => s.Id == latestOffer.SolutionVersionId);
        var extensions = await db.ApprovalRequests.AsNoTracking().Where(a => a.CaseId == c.Id && a.Subject == ApprovalSubject.OfferExtension && a.Status == ApprovalStatus.Pending)
            .Select(a => new { a.Id, a.Title, a.SubjectVersionNo, mine = a.AssignedApproverUserId == rc.UserId }).ToListAsync();

        object? comparison = null;
        if (offered is not null && counter is not null)
        {
            var reqStart = counter.RequestedStartDate ?? offered.FirstDueDate;
            var reqEnd = reqStart.AddMonths(offered.TermMonths - 1);
            comparison = new
            {
                version = $"v{offered.VersionNo}",
                rows = new[]
                {
                    new { item = "القسط", offered = offered.InstallmentAmount.ToString("N2"), requested = (counter.RequestedInstallment ?? offered.InstallmentAmount).ToString("N2"), changed = counter.RequestedInstallment is not null && counter.RequestedInstallment != offered.InstallmentAmount },
                    new { item = "يوم الاستحقاق", offered = offered.FirstDueDate.Day.ToString(), requested = (counter.RequestedDueDay ?? offered.FirstDueDate.Day).ToString(), changed = counter.RequestedDueDay is not null && counter.RequestedDueDay != offered.FirstDueDate.Day },
                    new { item = "البدء", offered = offered.FirstDueDate.ToString("yyyy-MM-dd"), requested = reqStart.ToString("yyyy-MM-dd"), changed = reqStart != offered.FirstDueDate },
                    new { item = "الانتهاء", offered = offered.LastDueDate.ToString("yyyy-MM-dd"), requested = reqEnd.ToString("yyyy-MM-dd"), changed = reqEnd != offered.LastDueDate },
                },
                counterEntryId = counter.Id,
            };
        }
        return Results.Ok(new
        {
            status = CaseStatusInfo.Key(c.Status),
            thread = entries.Where(e => rc.IsLenderStaff).Select(e => new
            {
                e.Id, kind = e.Kind.ToString(), e.AuthorType, e.AuthorLabel, e.At, e.Body, e.InternalOnly,
                terms = e.Kind switch
                {
                    NegotiationKind.Offer => offers.FirstOrDefault(o => o.Id == e.OfferId) is { } o ? $"صالح حتى {o.ValidUntil:yyyy-MM-dd}" : null,
                    NegotiationKind.OwnerCounter => "طلب: " + string.Join(" · ", new[]
                    {
                        e.RequestedDueDay is { } d ? $"تاريخ الاستحقاق يوم {d}" : null,
                        e.RequestedStartDate is { } s ? $"البدء {s:yyyy-MM-dd}" : null,
                        e.RequestedInstallment is { } i ? $"القسط {i:N2}" : null,
                    }.Where(x => x is not null)),
                    _ when e.InternalOnly => "مرئية لفريق الحالة فقط",
                    _ => null,
                },
            }),
            comparison,
            offer = latestOffer is null ? null : new { latestOffer.Id, status = latestOffer.Status.ToString(), latestOffer.ValidUntil, version = latestOffer.VersionNo },
            extensions,
            canAct = c.Status == CaseStatus.Negotiation && rc.Has(P.NegotiationManage),
            note = "أي v3 يمر بالمراجعة والاعتماد من جديد. الاعتذار يعيد الحالة إلى «حل مقترح»، لا إلى الإحالة.",
        });
    }

    private static async Task<IResult> AddNote(string reference, NegotiationNoteRequest req, CaseAccess access, RahoonDbContext db, RequestContext rc, IClock clock, Notifier notifier)
    {
        new Validator().Require(!string.IsNullOrWhiteSpace(req.Body) && req.Body.Length <= 2000, "body", "اكتب النص (حتى 2000 حرف).").ThrowIfInvalid();
        var c = await access.GetAsync(reference, track: false);
        db.NegotiationEntries.Add(new NegotiationEntry
        {
            OrganizationId = c.OrganizationId, CaseId = c.Id, Kind = req.Internal ? NegotiationKind.InternalNote : NegotiationKind.LenderClarification, AuthorType = "lender",
            AuthorUserId = rc.UserId, AuthorLabel = $"{rc.UserName} — {(req.Internal ? "ملاحظة داخلية" : "توضيح")}", At = clock.UtcNow, Body = req.Body.Trim(), InternalOnly = req.Internal,
        });
        if (!req.Internal)
        {
            db.Messages.Add(new CaseMessage { OrganizationId = c.OrganizationId, CaseId = c.Id, Channel = MessageChannel.Portal, AuthorType = "lender", AuthorUserId = rc.UserId, AuthorLabel = rc.UserName, Body = req.Body.Trim(), At = clock.UtcNow });
            var owner = await db.OwnerAccesses.Where(o => o.CaseId == c.Id && o.UserId != null).Select(o => o.UserId).FirstOrDefaultAsync();
            if (owner is { } ou) notifier.Notify(ou, c.OrganizationId, "message", "رسالة جديدة من مسؤول حالتك", null, "/owner/messages", c.Id);
        }
        await db.SaveChangesAsync();
        return Results.Ok(new { saved = true });
    }

    /// <summary>Apology for a counteroffer: back to «حل مقترح» with a reason — never to referral.</summary>
    private static async Task<IResult> DeclineCounter(string reference, DeclineCounterRequest req, CaseAccess access, RahoonDbContext db, RequestContext rc,
        IClock clock, CaseWorkflow workflow, Notifier notifier)
    {
        await using var tx = await db.Database.BeginTransactionAsync();
        var c = await access.GetAsync(reference);
        await workflow.TransitionAsync(c, "counter_declined", req.Reason?.Trim(), CaseStatus.Negotiation);
        db.NegotiationEntries.Add(new NegotiationEntry
        {
            OrganizationId = c.OrganizationId, CaseId = c.Id, Kind = NegotiationKind.LenderDecline, AuthorType = "lender", AuthorUserId = rc.UserId,
            AuthorLabel = $"{rc.UserName} — اعتذار عن الطلب", At = clock.UtcNow, Body = req.Reason!.Trim(),
        });
        db.Messages.Add(new CaseMessage { OrganizationId = c.OrganizationId, CaseId = c.Id, Channel = MessageChannel.Portal, AuthorType = "lender", AuthorUserId = rc.UserId, AuthorLabel = rc.UserName, Body = $"نعتذر عن عدم إمكانية تلبية طلبك: {req.Reason.Trim()} سنعمل معك على خيار آخر مناسب.", At = clock.UtcNow });
        var owner = await db.OwnerAccesses.Where(o => o.CaseId == c.Id && o.UserId != null).Select(o => o.UserId).FirstOrDefaultAsync();
        if (owner is { } ou) notifier.Notify(ou, c.OrganizationId, "offer", "ردّ على اقتراحك", req.Reason.Trim(), "/owner/messages", c.Id);
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Results.Ok(new { status = CaseStatusInfo.Key(c.Status) });
    }

    /// <summary>Case-manager approval of an offer deadline extension requested by the complaint reviewer (maker-checker).</summary>
    private static async Task<IResult> DecideExtension(string reference, Guid id, ExtensionDecisionRequest req, CaseAccess access, RahoonDbContext db, RequestContext rc, IClock clock, AuditLog audit)
    {
        var c = await access.GetAsync(reference, track: false);
        var a = await db.ApprovalRequests.FirstOrDefaultAsync(x => x.Id == id && x.CaseId == c.Id && x.Subject == ApprovalSubject.OfferExtension && x.Status == ApprovalStatus.Pending) ?? throw new NotFoundException();
        if (a.SubmittedByUserId == rc.UserId) throw new ForbiddenException("لا يعتمد مقدم الطلب طلبه.");
        if (a.AssignedApproverUserId != rc.UserId) throw new ForbiddenException("الطلب مسند لمدير الحالة.");
        a.Status = req.Approve ? ApprovalStatus.Approved : ApprovalStatus.Rejected;
        a.DecidedByUserId = rc.UserId;
        a.DecidedAt = clock.UtcNow;
        a.DecisionReason = req.Reason;
        if (req.Approve)
        {
            var offer = await db.Offers.FirstAsync(o => o.Id == a.SubjectId);
            offer.ValidUntil = offer.ValidUntil.AddDays(a.SubjectVersionNo);
            var k = await db.Cases.FirstAsync(x => x.Id == c.Id);
            k.StageDueOn = offer.ValidUntil;
        }
        await audit.RecordAsync(new AuditEntry("offer.extension", req.Approve ? $"تمديد مهلة العرض {a.SubjectVersionNo} أيام" : "رفض تمديد مهلة العرض", c.Id, c.Reference, Reason: req.Reason, OrganizationId: c.OrganizationId));
        await db.SaveChangesAsync();
        return Results.Ok(new { status = a.Status.ToString() });
    }

    // ───────── Agreement (L19) ─────────

    private static async Task<IResult> Agreement(string reference, CaseAccess access, RahoonDbContext db, RequestContext rc, IIntegrationRegistry integrations)
    {
        var c = await access.GetAsync(reference, track: false);
        var a = await db.Agreements.AsNoTracking().Where(x => x.CaseId == c.Id).OrderByDescending(x => x.CreatedAt).FirstOrDefaultAsync();
        if (a is null) return Results.Ok(new { agreement = (object?)null });
        var consent = a.ConsentRecordId is null ? null : await db.ConsentRecords.AsNoTracking().FirstOrDefaultAsync(x => x.Id == a.ConsentRecordId);
        var party = await db.Parties.AsNoTracking().FirstAsync(p => p.CaseId == c.Id && p.IsPrimary);
        var org = await db.Organizations.AsNoTracking().FirstAsync(o => o.Id == c.OrganizationId);
        var fin = await db.FinancingContracts.AsNoTracking().FirstOrDefaultAsync(f => f.CaseId == c.Id);
        var legalName = a.LegalReviewedByUserId is null ? null : await db.Users.Where(u => u.Id == a.LegalReviewedByUserId).Select(u => u.FullName).FirstAsync();
        var offer = a.OfferId is null ? null : await db.Offers.AsNoTracking().FirstOrDefaultAsync(o => o.Id == a.OfferId);
        var signing = await integrations.StateAsync(IntegrationKeys.LicensedSigning);
        return Results.Ok(new
        {
            agreement = new
            {
                a.Number, a.VersionLabel, status = a.Status.ToString(),
                terms = new[]
                {
                    new { k = "الأطراف", v = $"{org.NameAr} · {party.DisplayName} (الهوية {party.NationalIdMasked})" },
                    new { k = "العقد الأصلي", v = fin is null ? "—" : $"{Infrastructure.Security.Mask.Reference(fin.ContractNumber)}" + (fin.ContractDate is { } cd ? $" بتاريخ {cd:yyyy-MM-dd}" : "") },
                    new { k = "المبلغ المعاد جدولته", v = $"{a.RescheduledAmount:N2} ر.س" + (a.WaiverAmount > 0 ? $" (بعد إلغاء غرامات {a.WaiverAmount:N2})" : "") },
                    new { k = "الأقساط", v = $"{a.InstallmentCount} قسطاً × {a.InstallmentAmount:N2} ر.س · يوم {a.DueDay} من كل شهر" },
                    new { k = "الفترة", v = $"{a.StartDate:yyyy-MM-dd} إلى {a.EndDate:yyyy-MM-dd} · {Hijri.Format(a.StartDate)}" },
                    new { k = "شرط التأخر", v = $"قسطان متتاليان ← مراجعة ومهلة تصحيح {a.BreachCureDays} يوماً" },
                    new { k = "طريقة السداد", v = "تحويل بنكي إلى حساب المصرف (خارج المنصة)" },
                },
                consent = consent is null ? null : new
                {
                    title = $"قبل {party.DisplayName} العرض {a.VersionLabel} داخل البوابة",
                    meta = $"{consent.AcceptedAt.ToOffset(TimeSpan.FromHours(3)):yyyy-MM-dd HH:mm} · رمز تحقق إلى {consent.OtpDestinationMasked} · جهاز: {consent.Device}",
                    acks = "أقرّ بقراءة الشروط، واطّلع على «ماذا لو تأخرت؟»",
                    consent.TextHash,
                },
                steps = new[]
                {
                    new { key = "legal_review", label = legalName is null ? "مراجعة القانونية" : $"مراجعة القانونية · {legalName}", done = a.LegalReviewDone },
                    new { key = "schedule", label = $"إنشاء جدول السداد ({a.InstallmentCount} قسطاً)", done = a.ScheduleCreated },
                    new { key = "core_system", label = "تحديث نظام التمويل الأساسي · مهمة للمالية", done = a.CoreSystemUpdated },
                },
                versionMatchesOffer = offer?.VersionNo.ToString() == a.VersionLabel.TrimStart('v'),
                signing = new { state = signing, note = "التوقيع الإلكتروني المرخّص: غير مفعّل في هذه المرحلة. الأثر الملزم لسجل الموافقة — افتراض يتطلب تأكيداً قانونياً." },
                permissions = new
                {
                    canLegalReview = rc.Has(P.AgreementActivate) && !a.LegalReviewDone && a.Status == AgreementStatus.PendingActivation,
                    canCreateSchedule = rc.Has(P.AgreementPrepare) && !a.ScheduleCreated && a.Status == AgreementStatus.PendingActivation,
                    canActivate = rc.Has(P.AgreementActivate) && a.Status == AgreementStatus.PendingActivation,
                },
            },
        });
    }

    private static async Task<Agreement> PendingAsync(RahoonDbContext db, Guid caseId) =>
        await db.Agreements.Where(x => x.CaseId == caseId && x.Status == AgreementStatus.PendingActivation).FirstOrDefaultAsync()
        ?? throw new ConflictException("no_pending_agreement", "لا يوجد اتفاق بانتظار التفعيل.");

    private static async Task<IResult> LegalReview(string reference, LegalReviewRequest req, CaseAccess access, RahoonDbContext db, RequestContext rc, IClock clock, AuditLog audit)
    {
        if (string.IsNullOrWhiteSpace(req.Note)) Validate.Throw("note", "ملاحظة المراجعة مطلوبة.");
        var c = await access.GetAsync(reference, track: false);
        var a = await PendingAsync(db, c.Id);
        a.LegalReviewDone = true;
        a.LegalReviewedByUserId = rc.UserId;
        await audit.RecordAsync(new AuditEntry("agreement.legal_review", $"مراجعة القانونية للاتفاق {a.Number}", c.Id, c.Reference, Reason: req.Note.Trim(), OrganizationId: c.OrganizationId));
        await db.SaveChangesAsync();
        return Results.Ok(new { done = true });
    }

    private static async Task<IResult> CreateSchedule(string reference, CaseAccess access, RahoonDbContext db, AgreementService service, AuditLog audit)
    {
        var c = await access.GetAsync(reference, track: false);
        var a = await PendingAsync(db, c.Id);
        await service.GenerateScheduleAsync(a);
        await audit.RecordAsync(new AuditEntry("agreement.schedule", $"إنشاء جدول السداد ({a.InstallmentCount} قسطاً)", c.Id, c.Reference, OrganizationId: c.OrganizationId));
        await db.SaveChangesAsync();
        return Results.Ok(new { installments = a.InstallmentCount });
    }

    /// <summary>Activation requires the owner's verified consent AND legal review AND a schedule (guards in the workflow).</summary>
    private static async Task<IResult> Activate(string reference, CaseAccess access, RahoonDbContext db, RequestContext rc, IClock clock, CaseWorkflow workflow, Notifier notifier, AuditLog audit)
    {
        await using var tx = await db.Database.BeginTransactionAsync();
        var c = await access.GetAsync(reference);
        var a = await PendingAsync(db, c.Id);
        await workflow.TransitionAsync(c, "activate_agreement", $"تفعيل {a.Number}", CaseStatus.AwaitingCustomer, evidence: [a.Number, "consent_record"]);
        a.Status = AgreementStatus.Active;
        a.ActivatedAt = clock.UtcNow;
        a.ActivatedByUserId = rc.UserId;
        c.StageDueOn = null;
        var finance = await db.Memberships.Where(m => m.OrganizationId == c.OrganizationId && m.Status == MembershipStatus.Active && m.Roles.Any(r => r.Role!.Key == SystemRoles.Finance))
            .Select(m => (Guid?)m.UserId).FirstOrDefaultAsync();
        notifier.Task(c.OrganizationId, c.Id, $"تحديث نظام التمويل الأساسي بالاتفاق {a.Number} (يدوي)", finance, BusinessDays.Add(clock.TodayRiyadh, 2), "core_system_update", $"/cases/{c.Reference}/agreement", rc.UserId);
        foreach (var t in await db.Tasks.Where(t => t.CaseId == c.Id && t.Kind == "agreement" && t.Status == TaskStatus.Open).ToListAsync()) { t.Status = TaskStatus.Done; t.CompletedAt = clock.UtcNow; }
        var owner = await db.OwnerAccesses.Where(o => o.CaseId == c.Id && o.UserId != null).Select(o => o.UserId).FirstOrDefaultAsync();
        if (owner is { } ou) notifier.Notify(ou, c.OrganizationId, "agreement", "فُعّل اتفاقك", $"أول قسط {a.StartDate:yyyy-MM-dd}. تجد جدول السداد في المدفوعات.", "/owner/payments", c.Id, "ok");
        await audit.RecordAsync(new AuditEntry("agreement.activated", $"تفعيل الاتفاق {a.Number}", c.Id, c.Reference, OrganizationId: c.OrganizationId));
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Results.Ok(new { status = CaseStatusInfo.Key(c.Status) });
    }

    // ───────── Payments (L20) ─────────

    private static async Task<IResult> Payments(string reference, CaseAccess access, RahoonDbContext db, RequestContext rc, IClock clock)
    {
        var c = await access.GetAsync(reference, track: false);
        var a = await db.Agreements.AsNoTracking().Where(x => x.CaseId == c.Id && x.Status != AgreementStatus.PendingActivation).OrderByDescending(x => x.CreatedAt).FirstOrDefaultAsync();
        if (a is null) return Results.Ok(new { agreement = (object?)null });
        var installments = await db.Installments.Where(i => i.AgreementId == a.Id).OrderBy(i => i.No).ToListAsync();
        AgreementService.RefreshStatuses(installments, clock.TodayRiyadh);
        await db.SaveChangesAsync();
        var payments = await db.Payments.AsNoTracking().Where(p => p.CaseId == c.Id).OrderBy(p => p.RecordedAt).ToListAsync();
        var users = await db.Users.Where(u => payments.Select(p => p.RecordedByUserId).Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName);
        var paid = payments.Where(p => p.Status == PaymentStatus.Matched).Sum(p => p.Amount);
        var breach = await db.BreachReviews.AsNoTracking().Where(b => b.CaseId == c.Id && b.Status == BreachStatus.Open).FirstOrDefaultAsync();
        return Results.Ok(new
        {
            agreement = new { a.Number, a.InstallmentCount, status = a.Status.ToString() },
            kpis = new
            {
                rescheduled = a.RescheduledAmount, paid, paidCount = installments.Count(i => i.Status == InstallmentStatus.Matched),
                remaining = a.RescheduledAmount - paid, pendingMatch = payments.Count(p => p.Status == PaymentStatus.PendingMatch),
            },
            installments = installments.Select(i =>
            {
                var p = payments.LastOrDefault(x => x.InstallmentId == i.Id && x.Status != PaymentStatus.Rejected);
                return new
                {
                    i.Id, i.No, i.DueDate, i.Amount, paid = p?.Amount, reference = p?.BankReference, status = i.Status.ToString(),
                    statusLabel = i.Status switch
                    {
                        InstallmentStatus.Matched => "مطابقة", InstallmentStatus.RecordedPendingMatch => "مسجلة — بانتظار المطابقة", InstallmentStatus.Due => "مستحقة",
                        InstallmentStatus.Upcoming => "قادمة", InstallmentStatus.Partial => "جزئية", InstallmentStatus.Overdue => "متأخرة", InstallmentStatus.Waived => "معفاة", _ => "—",
                    },
                    paymentId = p?.Id, recordedBy = p is null ? null : users.GetValueOrDefault(p.RecordedByUserId),
                    canMatch = p is { Status: PaymentStatus.PendingMatch } && p.RecordedByUserId != rc.UserId && rc.Has(P.PaymentMatch),
                    matchBlockedReason = p is { Status: PaymentStatus.PendingMatch } && p.RecordedByUserId == rc.UserId ? "سجّلت هذه الدفعة؛ يطابقها موظف مالية آخر." : null,
                };
            }),
            nextDue = installments.FirstOrDefault(i => i.Status is InstallmentStatus.Due or InstallmentStatus.Overdue or InstallmentStatus.Upcoming) is { } n ? new { n.No, n.DueDate, n.Amount } : null,
            breach = breach is null ? null : new { breach.Id, breach.CureDeadline, missed = breach.MissedInstallmentNos },
            canRecord = rc.Has(P.PaymentRecord),
        });
    }

    private static async Task<IResult> CheckReference(string reference, RahoonDbContext db, RequestContext rc)
    {
        var r = reference.Trim().ToUpperInvariant();
        var used = await db.Payments.AnyAsync(p => p.OrganizationId == rc.OrganizationId && p.BankReference == r && p.Status != PaymentStatus.Rejected);
        return Results.Ok(new { available = !used, message = used ? "المرجع مستخدم في دفعة سابقة." : "المرجع غير مستخدم في أي دفعة سابقة" });
    }

    private static async Task<IResult> RecordPayment(string reference, RecordPaymentRequest req, CaseAccess access, RahoonDbContext db, RequestContext rc, IClock clock, AuditLog audit, Notifier notifier)
    {
        var bankRef = req.BankReference?.Trim().ToUpperInvariant() ?? "";
        var v = new Validator();
        v.Require(req.Amount > 0, "amount", "المبلغ مطلوب ويجب أن يكون أكبر من صفر.");
        v.Require(req.ReceivedOn <= clock.TodayRiyadh, "receivedOn", "تاريخ الاستلام لا يكون في المستقبل.");
        v.Require(bankRef.Length is >= 6 and <= 40, "bankReference", "مرجع التحويل البنكي مطلوب.");
        v.ThrowIfInvalid();

        await using var tx = await db.Database.BeginTransactionAsync();
        var c = await access.GetAsync(reference, track: false);
        var a = await db.Agreements.FirstOrDefaultAsync(x => x.CaseId == c.Id && (x.Status == AgreementStatus.Active || x.Status == AgreementStatus.BreachReview))
            ?? throw new ConflictException("no_active_agreement", "لا يوجد اتفاق نشط لتسجيل دفعة عليه.");
        var inst = await db.Installments.FirstOrDefaultAsync(i => i.AgreementId == a.Id && i.No == req.InstallmentNo) ?? throw new NotFoundException();
        if (inst.Status is InstallmentStatus.Matched or InstallmentStatus.RecordedPendingMatch)
            throw new ConflictException("installment_recorded", $"القسط {inst.No} مسجل مسبقاً.");
        if (req.Amount != inst.Amount && string.IsNullOrWhiteSpace(req.VarianceReason))
            throw new ValidationFailedException(new Dictionary<string, string[]> { ["varianceReason"] = ["المبلغ يختلف عن القسط؛ اذكر السبب."] });
        if (await db.Payments.AnyAsync(p => p.OrganizationId == c.OrganizationId && p.BankReference == bankRef && p.Status != PaymentStatus.Rejected))
            throw new ValidationFailedException(new Dictionary<string, string[]> { ["bankReference"] = ["المرجع مستخدم في دفعة سابقة."] });

        var payment = new PaymentRecord
        {
            OrganizationId = c.OrganizationId, CaseId = c.Id, InstallmentId = inst.Id, Amount = req.Amount, ReceivedOn = req.ReceivedOn, BankReference = bankRef,
            ProofDocumentVersionId = req.ProofDocumentVersionId, VarianceReason = req.VarianceReason?.Trim(), RecordedByUserId = rc.UserId, RecordedAt = clock.UtcNow,
        };
        db.Payments.Add(payment);
        inst.Status = InstallmentStatus.RecordedPendingMatch;
        var checkers = await db.Memberships.Where(m => m.OrganizationId == c.OrganizationId && m.Status == MembershipStatus.Active && m.UserId != rc.UserId
                && m.Roles.Any(r => r.Role!.Permissions.Any(p => p.PermissionKey == P.PaymentMatch))).Select(m => m.UserId).ToListAsync();
        foreach (var u in checkers) notifier.Notify(u, c.OrganizationId, "payment", $"دفعة بانتظار المطابقة · {c.Reference}", $"القسط {inst.No} · {req.Amount:N2} · {bankRef}", $"/cases/{c.Reference}/payments", c.Id);
        await audit.RecordAsync(new AuditEntry("payment.recorded", $"تسجيل دفعة القسط {inst.No} — بانتظار المطابقة", c.Id, c.Reference, Detail: $"{req.Amount:N2} ر.س · {bankRef}", OrganizationId: c.OrganizationId));
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Results.Ok(new { payment.Id, status = "PendingMatch" });
    }

    /// <summary>Checker step: a different finance user confirms the bank reference. Cures breach / completes settlement when applicable.</summary>
    private static async Task<IResult> MatchPayment(string reference, Guid id, CaseAccess access, RahoonDbContext db, RequestContext rc, IClock clock,
        AuditLog audit, CaseWorkflow workflow, Notifier notifier)
    {
        await using var tx = await db.Database.BeginTransactionAsync();
        var c = await access.GetAsync(reference);
        var p = await db.Payments.FirstOrDefaultAsync(x => x.Id == id && x.CaseId == c.Id) ?? throw new NotFoundException();
        if (p.Status != PaymentStatus.PendingMatch) throw new ConflictException("already_matched", "عولجت هذه الدفعة مسبقاً.");
        if (p.RecordedByUserId == rc.UserId)
        {
            await audit.RecordBlockedAsync(new AuditEntry("payment.match_blocked", "محاولة مطابقة دفعة من مسجّلها", c.Id, c.Reference, Detail: p.BankReference, OrganizationId: c.OrganizationId));
            throw new ForbiddenException("سجّلت هذه الدفعة بنفسك؛ يطابقها موظف مالية آخر (صانع ومدقق).");
        }
        var inst = await db.Installments.FirstAsync(i => i.Id == p.InstallmentId);
        p.Status = PaymentStatus.Matched;
        p.MatchedByUserId = rc.UserId;
        p.MatchedAt = clock.UtcNow;
        inst.PaidAmount += p.Amount;
        inst.Status = inst.PaidAmount >= inst.Amount ? InstallmentStatus.Matched : InstallmentStatus.Partial;

        var agreement = await db.Agreements.FirstAsync(a => a.Id == inst.AgreementId);
        var all = await db.Installments.Where(i => i.AgreementId == agreement.Id).ToListAsync();
        AgreementService.RefreshStatuses(all, clock.TodayRiyadh);

        // Breach is cured automatically once no installment is overdue (L21 «تلقائي بعد المطابقة»).
        var breach = await db.BreachReviews.FirstOrDefaultAsync(b => b.CaseId == c.Id && b.Status == BreachStatus.Open);
        if (breach is not null && all.All(i => i.Status != InstallmentStatus.Overdue))
        {
            breach.Status = BreachStatus.Cured;
            breach.ClosedAt = clock.UtcNow;
            breach.OutcomeNote = "صُحح التأخر بعد مطابقة الدفعات.";
            agreement.Status = AgreementStatus.Active;
        }

        var owner = await db.OwnerAccesses.Where(o => o.CaseId == c.Id && o.UserId != null).Select(o => o.UserId).FirstOrDefaultAsync();
        if (owner is { } ou) notifier.Notify(ou, c.OrganizationId, "payment", $"استلمنا القسط {inst.No}", $"مرجع {p.BankReference}", "/owner/payments", c.Id, "ok");
        // Guards read the database: persist the match first. Transition before any audit append so a refusal is audited cleanly.
        await db.SaveChangesAsync();
        if (all.All(i => i.Status is InstallmentStatus.Matched or InstallmentStatus.Waived))
        {
            await workflow.TransitionAsync(c, "settlement_completed", "سُددت كل الأقساط وطوبقت", CaseStatus.ActiveSettlement, systemInitiated: true);
            agreement.Status = AgreementStatus.Completed;
            notifier.Task(c.OrganizationId, c.Id, "إعداد التسوية المالية والإغلاق", rc.UserId, BusinessDays.Add(clock.TodayRiyadh, 5), "reconciliation", $"/cases/{c.Reference}/closure", rc.UserId);
        }
        await audit.RecordAsync(new AuditEntry("payment.matched", $"مطابقة دفعة القسط {inst.No}", c.Id, c.Reference, Detail: $"{p.Amount:N2} ر.س · {p.BankReference}", OrganizationId: c.OrganizationId));
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Results.Ok(new { status = "Matched", caseStatus = CaseStatusInfo.Key(c.Status) });
    }

    private static async Task<IResult> RejectPayment(string reference, Guid id, PaymentDecisionRequest req, CaseAccess access, RahoonDbContext db, RequestContext rc, IClock clock, AuditLog audit)
    {
        if (string.IsNullOrWhiteSpace(req.Reason)) Validate.Throw("reason", "سبب الرفض مطلوب.");
        var c = await access.GetAsync(reference, track: false);
        var p = await db.Payments.FirstOrDefaultAsync(x => x.Id == id && x.CaseId == c.Id) ?? throw new NotFoundException();
        if (p.Status != PaymentStatus.PendingMatch) throw new ConflictException("already_matched", "عولجت هذه الدفعة مسبقاً.");
        if (p.RecordedByUserId == rc.UserId) throw new ForbiddenException("سجّلت هذه الدفعة بنفسك؛ يراجعها موظف مالية آخر.");
        p.Status = PaymentStatus.Rejected;
        p.RejectReason = req.Reason!.Trim();
        p.MatchedByUserId = rc.UserId;
        p.MatchedAt = clock.UtcNow;
        var inst = await db.Installments.FirstAsync(i => i.Id == p.InstallmentId);
        inst.Status = inst.DueDate < clock.TodayRiyadh ? InstallmentStatus.Overdue : InstallmentStatus.Due;
        await audit.RecordAsync(new AuditEntry("payment.rejected", $"رفض دفعة القسط {inst.No}", c.Id, c.Reference, Reason: p.RejectReason, OrganizationId: c.OrganizationId));
        await db.SaveChangesAsync();
        return Results.Ok(new { status = "Rejected" });
    }

    // ───────── Breach (L21) ─────────

    private static async Task<IResult> Breach(string reference, CaseAccess access, RahoonDbContext db, RequestContext rc)
    {
        var c = await access.GetAsync(reference, track: false);
        var reviews = await db.BreachReviews.AsNoTracking().Where(b => b.CaseId == c.Id).OrderByDescending(b => b.TriggeredAt).ToListAsync();
        var events = await db.AuditEvents.AsNoTracking().Where(e => e.CaseId == c.Id && (e.Type.StartsWith("breach.") || e.Type == "installment.missed" || e.Type == "owner.reminder"))
            .OrderBy(e => e.Seq).Select(e => new { e.Type, e.Title, e.OccurredAt }).ToListAsync();
        var access2 = await db.OwnerAccesses.AsNoTracking().FirstOrDefaultAsync(o => o.CaseId == c.Id);
        return Results.Ok(new
        {
            reviews = reviews.Select(b => new { b.Id, status = b.Status.ToString(), b.TriggeredAt, b.CureDeadline, missed = b.MissedInstallmentNos, b.OutcomeNote }),
            timeline = events.Select(e => new
            {
                icon = e.Type switch { "installment.missed" => "event_busy", "owner.reminder" => "sms", "breach.opened" => "flag", _ => "history" },
                tone = e.Type switch { "installment.missed" => "err", "breach.opened" => "warn", _ => "muted" },
                date = e.OccurredAt.ToOffset(TimeSpan.FromHours(3)).ToString("yyyy-MM-dd"), text = e.Title,
            }),
            paths = new[]
            {
                new { key = "cure", title = "تصحيح", description = "يسدد المالك المتأخر ← تعود التسوية طبيعية", owner = "تلقائي بعد المطابقة" },
                new { key = "restructuring", title = "إعادة هيكلة", description = "حل جديد vN يمر بالمراجعة والاعتماد", owner = "المحلل ← المعتمد" },
                new { key = "other_options", title = "تقييم خيارات أخرى", description = "بيع طوعي بموافقة المالك، أو تقييم إحالة منفصل", owner = "مدير الحالات + القانونية" },
            },
            contactHours = access2?.ContactHours,
            note = "لا يوجد انتقال تلقائي إلى الإحالة. أي تصعيد يتطلب قراراً منفصلاً من القانونية ومعتمداً.",
        });
    }

    private static async Task<IResult> BreachOutcome(string reference, Guid id, BreachOutcomeRequest req, CaseAccess access, RahoonDbContext db, RequestContext rc,
        IClock clock, CaseWorkflow workflow, AuditLog audit)
    {
        new Validator().Require(req.Outcome is "restructuring" or "other_options" or "closed", "outcome", "اختر المسار.")
            .Require(!string.IsNullOrWhiteSpace(req.Note), "note", "اكتب مبرر المسار.").ThrowIfInvalid();
        await using var tx = await db.Database.BeginTransactionAsync();
        var c = await access.GetAsync(reference);
        var b = await db.BreachReviews.FirstOrDefaultAsync(x => x.Id == id && x.CaseId == c.Id && x.Status == BreachStatus.Open) ?? throw new NotFoundException();
        if (req.Outcome == "restructuring")
            await workflow.TransitionAsync(c, "restructure_after_breach", req.Note.Trim(), CaseStatus.ActiveSettlement);
        b.Status = req.Outcome switch { "restructuring" => BreachStatus.Restructuring, "other_options" => BreachStatus.OtherOptions, _ => BreachStatus.Closed };
        b.OutcomeNote = req.Note.Trim();
        b.ClosedByUserId = rc.UserId;
        b.ClosedAt = clock.UtcNow;
        await audit.RecordAsync(new AuditEntry("breach.outcome", $"مسار مراجعة الإخلال: {req.Outcome switch { "restructuring" => "إعادة هيكلة", "other_options" => "تقييم خيارات أخرى", _ => "إغلاق المراجعة" }}", c.Id, c.Reference, Reason: req.Note.Trim(), OrganizationId: c.OrganizationId));
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Results.Ok(new { status = b.Status.ToString(), caseStatus = CaseStatusInfo.Key(c.Status) });
    }
}
