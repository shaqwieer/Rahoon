using Microsoft.EntityFrameworkCore;
using Rahoon.Api.Modules.Agreements;
using Rahoon.Api.Modules.Solutions;

namespace Rahoon.Api.Seed;

/// <summary>
/// Phase 1A-2 step 5 (lender-on-platform mode): agreements behind the seeded post-acceptance cases, which had a status but
/// no agreement or schedule. RH-2026-003870 is «تسوية معتمدة / نشطة» with installments 1–2 matched (recorded by ريم,
/// matched by عبدالعزيز) and 3–4 missed, so a breach review is open (L21 — never an automatic referral).
/// RH-2026-003702 is «بانتظار التسوية المالية»: a cash settlement AGR-2026-003702-01, paid and matched, for L26.
/// All data is fictional.
/// </summary>
public sealed partial class DevSeeder
{
    private async Task SeedAgreementsAsync()
    {
        // RH-2026-003870 — active reschedule, two missed installments → breach review with a cure period.
        var c1 = _cases["RH-2026-003870"];
        var a1 = await AgreementChainAsync(c1, "AGR-2026-003870-01", SolutionKind.Reschedule, rescheduled: 580_000.00m, waiver: 0m,
            count: 60, amount: 9_666.67m, start: D("2026-06-01"), activatedAt: At("2026-05-15T11:00:00"));
        var installments = Schedule(a1, 60);
        foreach (var i in installments.Where(i => i.No <= 2))
            Pay(a1, i, i.DueDate.AddDays(2), $"TRX-3870-{i.No:D3}", matched: true);
        foreach (var i in installments.Where(i => i.No is 3 or 4)) i.Status = InstallmentStatus.Overdue;
        a1.Status = AgreementStatus.BreachReview;
        db.BreachReviews.Add(new BreachReview
        {
            OrganizationId = c1.OrganizationId, CaseId = c1.Id, AgreementId = a1.Id, TriggeredAt = At("2026-09-08T06:00:00"),
            Trigger = "2_consecutive_missed", MissedInstallmentNos = [3, 4], CureDeadline = D("2026-09-23").AddDays(15),
        });
        Audit(c1.OrganizationId, c1, "breach.opened", "فتح مراجعة إخلال آلياً · إشعار المالك بمهلة 15 يوماً وخيار المساعدة", At("2026-09-08T06:00:00"), null,
            detail: "الأقساط 3 و4");

        // RH-2026-003702 — cash settlement paid in one installment and matched; awaiting reconciliation (L26).
        var c2 = _cases["RH-2026-003702"];
        var a2 = await AgreementChainAsync(c2, "AGR-2026-003702-01", SolutionKind.ReducedPayoff, rescheduled: 455_210.75m, waiver: 0m,
            count: 1, amount: 455_210.75m, start: D("2026-09-10"), activatedAt: At("2026-08-28T10:00:00"));
        var single = Schedule(a2, 1)[0];
        Pay(a2, single, D("2026-09-12"), "TRX-3702-001", matched: true);
        a2.Status = AgreementStatus.Completed;
        await db.SaveChangesAsync();
    }

    /// <summary>The accepted solution, its offer, the owner's consent record and the activated agreement (legal review, schedule, activation done).</summary>
    private async Task<Agreement> AgreementChainAsync(Modules.Cases.Case c, string number, SolutionKind kind, decimal rescheduled, decimal waiver,
        int count, decimal amount, DateOnly start, DateTimeOffset activatedAt)
    {
        var org = c.OrganizationId;
        var party = await db.Parties.Where(p => p.CaseId == c.Id && p.IsPrimary).Select(p => p.Id).FirstAsync();
        var sent = activatedAt.AddDays(-12);
        var v = new SolutionVersion
        {
            OrganizationId = org, CaseId = c.Id, VersionNo = 1, Kind = kind, Status = SolutionStatus.Accepted, OutstandingAtPreparation = rescheduled + waiver,
            TermMonths = count, FirstDueDate = start, LastDueDate = start.AddMonths(count - 1), WaiverAmount = waiver, RescheduledAmount = rescheduled,
            InstallmentAmount = amount, FinalInstallmentAmount = rescheduled - amount * (count - 1), PreparedByUserId = UserId("fahad"), PreparedAt = sent.AddDays(-5),
            ReviewedByUserId = UserId("sara"), LockedAt = sent.AddDays(-3), Justification = "حل معتمد (بيانات تجريبية).",
        };
        var offer = new Offer
        {
            OrganizationId = org, CaseId = c.Id, SolutionVersionId = v.Id, VersionNo = 1, SentAt = sent, ValidUntil = DateOnly.FromDateTime(sent.AddDays(10).UtcDateTime),
            Status = OfferStatus.Accepted, SentByUserId = UserId("noura"), RespondedAt = sent.AddDays(3),
        };
        var consent = new ConsentRecord
        {
            OrganizationId = org, CaseId = c.Id, OfferId = offer.Id, PartyId = party, Kind = "offer_acceptance", AcceptedAt = sent.AddDays(3), Channel = "owner_portal",
            OtpDestinationMasked = "•••• ••" + c.Reference[^2..], OtpVerifiedAt = sent.AddDays(3), Acknowledgements = ["terms_read", "voluntary"], TextHash = new string('0', 64),
        };
        var a = new Agreement
        {
            OrganizationId = org, Number = number, CaseId = c.Id, SolutionVersionId = v.Id, OfferId = offer.Id, VersionLabel = "v1", RescheduledAmount = rescheduled,
            WaiverAmount = waiver, InstallmentCount = count, InstallmentAmount = amount, DueDay = start.Day, StartDate = start, EndDate = start.AddMonths(count - 1),
            Status = AgreementStatus.Active, ConsentRecordId = consent.Id, LegalReviewDone = true, LegalReviewedByUserId = UserId("majed"), ScheduleCreated = true,
            CoreSystemUpdated = true, ActivatedByUserId = UserId("majed"), ActivatedAt = activatedAt,
        };
        consent.AgreementId = a.Id;
        db.Solutions.Add(v);
        db.Offers.Add(offer);
        db.ConsentRecords.Add(consent);
        db.Agreements.Add(a);
        Audit(org, c, "agreement.activated", $"تفعيل الاتفاق {number}", activatedAt, "majed", detail: "مراجعة القانونية · جدول السداد · سجل موافقة المالك");
        return a;
    }

    /// <summary>Monthly installments; the last one absorbs the rounding so the sum equals the rescheduled amount.</summary>
    private List<Installment> Schedule(Agreement a, int count)
    {
        var list = Enumerable.Range(1, count).Select(n => new Installment
        {
            OrganizationId = a.OrganizationId, AgreementId = a.Id, CaseId = a.CaseId, No = n, DueDate = a.StartDate.AddMonths(n - 1),
            Amount = n < count ? a.InstallmentAmount : a.RescheduledAmount - a.InstallmentAmount * (count - 1), Status = InstallmentStatus.Upcoming,
        }).ToList();
        db.Installments.AddRange(list);
        return list;
    }

    /// <summary>A manual payment (A-04) recorded by ريم and, when matched, matched by عبدالعزيز (maker ≠ checker).</summary>
    private void Pay(Agreement a, Installment i, DateOnly receivedOn, string bankReference, bool matched)
    {
        var at = new DateTimeOffset(receivedOn.ToDateTime(new TimeOnly(10, 0)), TimeSpan.FromHours(3)).ToUniversalTime();
        db.Payments.Add(new PaymentRecord
        {
            OrganizationId = a.OrganizationId, CaseId = a.CaseId, InstallmentId = i.Id, Amount = i.Amount, ReceivedOn = receivedOn, BankReference = bankReference,
            RecordedByUserId = UserId("reem"), RecordedAt = at, Status = matched ? PaymentStatus.Matched : PaymentStatus.PendingMatch,
            MatchedByUserId = matched ? UserId("aziz") : null, MatchedAt = matched ? at.AddHours(3) : null,
        });
        i.PaidAmount = matched ? i.Amount : 0m;
        i.Status = matched ? InstallmentStatus.Matched : InstallmentStatus.RecordedPendingMatch;
    }
}
