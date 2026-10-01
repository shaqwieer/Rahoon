using Microsoft.EntityFrameworkCore;
using Rahoon.Api.Infrastructure.Persistence;

namespace Rahoon.Api.Modules.Market;

public sealed record Workload(int SaleRequests, int BuyerRequests, int Opportunities, int Interests)
{
    public int Total => SaleRequests + BuyerRequests + Opportunities + Interests;
}

/// <summary>
/// A team member's open assigned work, and moving it to another member or back to the unassigned queue (when a member is
/// suspended or removed, or on a manager's request). Every moved record keeps an internal event naming the former and the
/// new assignee; closed work keeps its historical assignee.
/// </summary>
public static class TeamWorkload
{
    private static readonly SaleRequestStatus[] OpenSale =
        [SaleRequestStatus.Submitted, SaleRequestStatus.UnderReview, SaleRequestStatus.NeedsCompletion, SaleRequestStatus.ApprovedForListing];
    private static readonly BuyerRequestStatus[] OpenBuyer =
        [BuyerRequestStatus.Submitted, BuyerRequestStatus.UnderReview, BuyerRequestStatus.NeedsCompletion, BuyerRequestStatus.ApprovedForMatching];
    private static readonly OpportunityStatus[] ClosedOpportunity = [OpportunityStatus.Withdrawn, OpportunityStatus.Completed];
    private static readonly InterestStatus[] OpenInterest = [InterestStatus.Received, InterestStatus.InFollowUp];

    public static async Task<Workload> CountAsync(RahoonDbContext db, Guid userId) => new(
        await db.SaleRequests.CountAsync(r => r.AssignedToUserId == userId && OpenSale.Contains(r.Status)),
        await db.BuyerRequests.CountAsync(r => r.AssignedToUserId == userId && OpenBuyer.Contains(r.Status)),
        await db.Opportunities.CountAsync(o => o.AssignedToUserId == userId && !ClosedOpportunity.Contains(o.Status)),
        await db.Interests.CountAsync(i => i.AssignedToUserId == userId && OpenInterest.Contains(i.Status)));

    public static async Task<Dictionary<Guid, int>> TotalsAsync(RahoonDbContext db, IReadOnlyCollection<Guid> userIds)
    {
        var counts = new Dictionary<Guid, int>();
        void Add(IEnumerable<(Guid Id, int N)> rows) { foreach (var (id, n) in rows) counts[id] = counts.GetValueOrDefault(id) + n; }
        Add((await db.SaleRequests.Where(r => r.AssignedToUserId != null && userIds.Contains(r.AssignedToUserId.Value) && OpenSale.Contains(r.Status))
            .GroupBy(r => r.AssignedToUserId!.Value).Select(g => new { g.Key, n = g.Count() }).ToListAsync()).Select(x => (x.Key, x.n)));
        Add((await db.BuyerRequests.Where(r => r.AssignedToUserId != null && userIds.Contains(r.AssignedToUserId.Value) && OpenBuyer.Contains(r.Status))
            .GroupBy(r => r.AssignedToUserId!.Value).Select(g => new { g.Key, n = g.Count() }).ToListAsync()).Select(x => (x.Key, x.n)));
        Add((await db.Opportunities.Where(o => o.AssignedToUserId != null && userIds.Contains(o.AssignedToUserId.Value) && !ClosedOpportunity.Contains(o.Status))
            .GroupBy(o => o.AssignedToUserId!.Value).Select(g => new { g.Key, n = g.Count() }).ToListAsync()).Select(x => (x.Key, x.n)));
        Add((await db.Interests.Where(i => i.AssignedToUserId != null && userIds.Contains(i.AssignedToUserId.Value) && OpenInterest.Contains(i.Status))
            .GroupBy(i => i.AssignedToUserId!.Value).Select(g => new { g.Key, n = g.Count() }).ToListAsync()).Select(x => (x.Key, x.n)));
        return counts;
    }

    /// <summary>Moves all open work of <paramref name="fromUserId"/> to <paramref name="to"/> (null = the unassigned queue). Caller saves.</summary>
    public static async Task<Workload> ReassignAsync(RahoonDbContext db, MarketService market, Guid fromUserId, string fromLabel, (Guid Id, string Label)? to, string reason, DateTimeOffset now)
    {
        var title = to is { } t ? $"أُعيد إسناده من {fromLabel} إلى {t.Label}" : $"أُعيد إلى قائمة غير المسندة (كان لدى {fromLabel})";
        var data = new { from = fromUserId, fromLabel, to = to?.Id, toLabel = to?.Label };

        var sale = await db.SaleRequests.Where(r => r.AssignedToUserId == fromUserId && OpenSale.Contains(r.Status)).ToListAsync();
        foreach (var r in sale)
        {
            r.AssignedToUserId = to?.Id; r.AssignedToLabel = to?.Label; r.AssignedAt = to is null ? null : now;
            market.Event(r.OrganizationId, "sale_request", r.Id, r.ApplicantUserId, "reassigned", title, visible: false, reason: reason, data: data);
        }
        var buyer = await db.BuyerRequests.Where(r => r.AssignedToUserId == fromUserId && OpenBuyer.Contains(r.Status)).ToListAsync();
        foreach (var r in buyer)
        {
            r.AssignedToUserId = to?.Id; r.AssignedToLabel = to?.Label;
            market.Event(r.OrganizationId, "buyer_request", r.Id, r.ApplicantUserId, "reassigned", title, visible: false, reason: reason, data: data);
        }
        var opps = await db.Opportunities.Where(o => o.AssignedToUserId == fromUserId && !ClosedOpportunity.Contains(o.Status)).ToListAsync();
        foreach (var o in opps)
        {
            o.AssignedToUserId = to?.Id; o.AssignedToLabel = to?.Label;
            market.Event(o.OrganizationId, "opportunity", o.Id, o.ApplicantUserId, "reassigned", title, visible: false, reason: reason, data: data);
        }
        var interests = await db.Interests.Where(i => i.AssignedToUserId == fromUserId && OpenInterest.Contains(i.Status)).ToListAsync();
        foreach (var i in interests)
        {
            i.AssignedToUserId = to?.Id; i.AssignedToLabel = to?.Label;
            market.Event(i.OrganizationId, "interest", i.Id, i.ApplicantUserId, "reassigned", title, visible: false, reason: reason, data: data);
        }
        return new Workload(sale.Count, buyer.Count, opps.Count, interests.Count);
    }
}
