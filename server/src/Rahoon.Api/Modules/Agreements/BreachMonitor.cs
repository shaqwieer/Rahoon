using Microsoft.EntityFrameworkCore;
using Rahoon.Api.Infrastructure.Persistence;
using Rahoon.Api.Infrastructure.Time;
using Rahoon.Api.Modules.Audit;
using Rahoon.Api.Modules.Communications;

namespace Rahoon.Api.Modules.Agreements;

/// <summary>
/// Marks missed installments and opens a breach review when the agreement's clause is met
/// (N consecutive missed → review with a cure period). The case's public state is unchanged
/// and nothing is ever referred automatically (L21).
/// </summary>
public sealed class BreachMonitor(RahoonDbContext db, IClock clock, Notifier notifier, AuditLog audit)
{
    public async Task<int> RunOnceAsync(CancellationToken ct = default)
    {
        using var _ = db.Request.BeginSystemScope();
        var today = clock.TodayRiyadh;
        var opened = 0;
        var agreements = await db.Agreements.Where(a => a.Status == AgreementStatus.Active || a.Status == AgreementStatus.BreachReview).ToListAsync(ct);
        foreach (var a in agreements)
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var c = await db.Cases.FirstAsync(x => x.Id == a.CaseId, ct);
            var installments = await db.Installments.Where(i => i.AgreementId == a.Id).OrderBy(i => i.No).ToListAsync(ct);
            var newlyMissed = installments.Where(i => i.DueDate < today && i.Status is InstallmentStatus.Due or InstallmentStatus.Upcoming).ToList();
            AgreementService.RefreshStatuses(installments, today);

            foreach (var m in newlyMissed.Where(i => i.Status == InstallmentStatus.Overdue))
                await audit.RecordAsync(new AuditEntry("installment.missed", $"لم يُسدد القسط {m.No}", c.Id, c.Reference, OrganizationId: c.OrganizationId));

            var overdue = installments.Where(i => i.Status == InstallmentStatus.Overdue).Select(i => i.No).OrderBy(n => n).ToList();
            var consecutive = LongestRun(overdue);
            var hasOpen = await db.BreachReviews.AnyAsync(b => b.AgreementId == a.Id && b.Status == BreachStatus.Open, ct);
            if (consecutive.Count >= a.BreachMissedConsecutive && !hasOpen)
            {
                var review = new BreachReview
                {
                    OrganizationId = a.OrganizationId, CaseId = c.Id, AgreementId = a.Id, TriggeredAt = clock.UtcNow,
                    Trigger = $"{a.BreachMissedConsecutive}_consecutive_missed", MissedInstallmentNos = consecutive, CureDeadline = today.AddDays(a.BreachCureDays),
                };
                db.BreachReviews.Add(review);
                a.Status = AgreementStatus.BreachReview;
                var owner = await db.OwnerAccesses.Where(o => o.CaseId == c.Id && o.UserId != null).Select(o => o.UserId).FirstOrDefaultAsync(ct);
                // Owner messaging starts with help, not consequences.
                if (owner is { } ou)
                    notifier.Notify(ou, c.OrganizationId, "payment", "هل تحتاج مساعدة في الأقساط؟",
                        $"لاحظنا تأخر قسطين. لديك حتى {review.CureDeadline:yyyy-MM-dd} للتصحيح، ويمكنك طلب مكالمة لنبحث معك عن حل.", "/owner/help", c.Id, "warn");
                var mgr = c.AssignedManagerId is null ? null : await db.Memberships.Where(m => m.Id == c.AssignedManagerId).Select(m => (Guid?)m.UserId).FirstOrDefaultAsync(ct);
                notifier.Task(c.OrganizationId, c.Id, "التواصل مع المالك قبل انتهاء مهلة التصحيح", mgr, review.CureDeadline, "breach", $"/cases/{c.Reference}/payments/breach", mgr ?? Guid.Empty);
                await audit.RecordAsync(new AuditEntry("breach.opened", $"فتح مراجعة إخلال آلياً · إشعار المالك بمهلة {a.BreachCureDays} يوماً وخيار المساعدة", c.Id, c.Reference,
                    Detail: $"الأقساط {string.Join(" و", consecutive)}", OrganizationId: c.OrganizationId));
                opened++;
            }
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        await Administration.JobHeartbeats.TouchAsync(db, "breach_monitor", clock.UtcNow, $"opened {opened}");
        return opened;
    }

    private static List<int> LongestRun(List<int> sorted)
    {
        var best = new List<int>();
        var cur = new List<int>();
        foreach (var n in sorted)
        {
            if (cur.Count > 0 && n != cur[^1] + 1) cur = [];
            cur.Add(n);
            if (cur.Count > best.Count) best = [.. cur];
        }
        return best;
    }
}

/// <summary>Runs the hourly jobs — breach detection and offer expiry (disabled in tests; they call RunOnceAsync directly).</summary>
public sealed class BreachMonitorService(IServiceScopeFactory scopes, IConfiguration config, ILogger<BreachMonitorService> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!config.GetValue("Jobs:BreachMonitor", true)) return;
        await Task.Delay(TimeSpan.FromSeconds(20), stoppingToken);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopes.CreateScope();
                var opened = await scope.ServiceProvider.GetRequiredService<BreachMonitor>().RunOnceAsync(stoppingToken);
                if (opened > 0) log.LogInformation("Breach monitor opened {Count} review(s)", opened);
                var expired = await scope.ServiceProvider.GetRequiredService<Solutions.OfferExpiryMonitor>().RunOnceAsync(stoppingToken);
                if (expired > 0) log.LogInformation("Offer expiry marked {Count} offer(s)", expired);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                log.LogError(ex, "Breach monitor run failed");
            }
            await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
        }
    }
}
