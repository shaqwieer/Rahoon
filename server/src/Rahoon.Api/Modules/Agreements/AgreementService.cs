using Microsoft.EntityFrameworkCore;
using Rahoon.Api.Infrastructure.Persistence;
using Rahoon.Api.Infrastructure.Time;
using Rahoon.Api.Modules.Cases;
using Rahoon.Api.Modules.Solutions;

namespace Rahoon.Api.Modules.Agreements;

public sealed class AgreementService(RahoonDbContext db, IClock clock)
{
    /// <summary>AGR-YYYY-NNNNNN-NN from the case reference plus a per-case sequence.</summary>
    public async Task<string> NextNumberAsync(Case c)
    {
        var n = await db.Agreements.CountAsync(a => a.CaseId == c.Id) + 1;
        return $"AGR-{c.Reference[3..]}-{n:D2}";
    }

    /// <summary>
    /// Owner acceptance creates the agreement in «بانتظار التفعيل». Consent alone does not
    /// activate it: legal review, schedule creation and the activation decision still follow.
    /// </summary>
    public async Task<Agreement> CreatePendingAsync(Case c, Offer offer, SolutionVersion v, ConsentRecord consent)
    {
        var agreement = new Agreement
        {
            OrganizationId = c.OrganizationId, Number = await NextNumberAsync(c), CaseId = c.Id, SolutionVersionId = v.Id, OfferId = offer.Id,
            VersionLabel = $"v{v.VersionNo}", RescheduledAmount = v.RescheduledAmount, WaiverAmount = v.WaiverAmount,
            InstallmentCount = v.Kind == SolutionKind.ReducedPayoff ? 1 : v.TermMonths, InstallmentAmount = v.InstallmentAmount,
            DueDay = v.FirstDueDate.AddMonths(v.GraceMonths).Day, StartDate = v.FirstDueDate.AddMonths(v.GraceMonths), EndDate = v.LastDueDate,
            BreachMissedConsecutive = v.BreachMissedConsecutive, BreachCureDays = v.BreachCureDays, ConsentRecordId = consent.Id,
            SignatureMethod = SignatureMethod.ConsentRecordOnly,
        };
        db.Agreements.Add(agreement);
        consent.AgreementId = agreement.Id;
        return agreement;
    }

    /// <summary>Generates the installment schedule; the last installment absorbs rounding so Σ = rescheduled amount.</summary>
    public async Task GenerateScheduleAsync(Agreement a)
    {
        if (await db.Installments.AnyAsync(i => i.AgreementId == a.Id)) return;
        var v = await db.Solutions.FirstAsync(s => s.Id == a.SolutionVersionId);
        for (var i = 1; i <= a.InstallmentCount; i++)
        {
            db.Installments.Add(new Installment
            {
                OrganizationId = a.OrganizationId, AgreementId = a.Id, CaseId = a.CaseId, No = i,
                DueDate = a.StartDate.AddMonths(i - 1),
                Amount = i == a.InstallmentCount ? v.FinalInstallmentAmount : a.InstallmentAmount,
                Status = InstallmentStatus.Upcoming,
            });
        }
        a.ScheduleCreated = true;
        RefreshStatuses(await db.Installments.Where(x => x.AgreementId == a.Id).ToListAsync(), clock.TodayRiyadh);
    }

    /// <summary>Upcoming → Due (within 30 days) → Overdue (past due, unpaid). Matched/pending are left as is.</summary>
    public static void RefreshStatuses(IEnumerable<Installment> installments, DateOnly today)
    {
        foreach (var i in installments)
        {
            if (i.Status is InstallmentStatus.Matched or InstallmentStatus.RecordedPendingMatch or InstallmentStatus.Waived) continue;
            if (i.Status == InstallmentStatus.Partial && i.DueDate >= today) continue;
            i.Status = i.DueDate < today ? InstallmentStatus.Overdue : i.DueDate <= today.AddDays(30) ? InstallmentStatus.Due : InstallmentStatus.Upcoming;
        }
    }
}
