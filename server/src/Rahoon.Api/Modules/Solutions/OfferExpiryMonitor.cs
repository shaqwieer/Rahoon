using Microsoft.EntityFrameworkCore;
using Rahoon.Api.Infrastructure.Persistence;
using Rahoon.Api.Infrastructure.Time;
using Rahoon.Api.Modules.Audit;
using Rahoon.Api.Modules.Communications;

namespace Rahoon.Api.Modules.Solutions;

/// <summary>
/// Marks offers past their validity as expired (D07 «منتهي الصلاحية: طلب عرض جديد») and tells the case
/// manager. Expiry never changes the case status on its own — the team decides the next step.
/// </summary>
public sealed class OfferExpiryMonitor(RahoonDbContext db, IClock clock, Notifier notifier, AuditLog audit)
{
    public async Task<int> RunOnceAsync(CancellationToken ct = default)
    {
        using var _ = db.Request.BeginSystemScope();
        var today = clock.TodayRiyadh;
        var expired = await db.Offers.Where(o => o.Status == OfferStatus.Sent && o.ValidUntil < today).ToListAsync(ct);
        foreach (var offer in expired)
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            offer.Status = OfferStatus.Expired;
            var v = await db.Solutions.FirstAsync(s => s.Id == offer.SolutionVersionId, ct);
            v.Status = SolutionStatus.Expired;
            var c = await db.Cases.FirstAsync(x => x.Id == offer.CaseId, ct);
            if (c.AssignedManagerId is { } mid && await db.Memberships.Where(m => m.Id == mid).Select(m => (Guid?)m.UserId).FirstOrDefaultAsync(ct) is { } mgr)
            {
                notifier.Notify(mgr, c.OrganizationId, "offer", $"انتهت صلاحية العرض v{offer.VersionNo} · {c.Reference}", "لم يصل رد من المالك. يمكن التواصل معه أو إعداد إصدار جديد.", $"/cases/{c.Reference}", c.Id, "warn");
                notifier.Task(c.OrganizationId, c.Id, "متابعة المالك بعد انتهاء صلاحية العرض", mgr, BusinessDays.Add(today, 2), "follow_up", $"/cases/{c.Reference}", mgr);
            }
            await audit.RecordAsync(new AuditEntry("offer.expired", $"انتهاء صلاحية العرض v{offer.VersionNo} دون رد", c.Id, c.Reference, OrganizationId: c.OrganizationId));
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        return expired.Count;
    }
}
