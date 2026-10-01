using Rahoon.Api.Infrastructure.Http;
using Rahoon.Api.Infrastructure.Persistence;
using Rahoon.Api.Infrastructure.Tenancy;
using Rahoon.Api.Modules.Identity;

namespace Rahoon.Api.Modules.Market;

/// <summary>
/// Resource scope of the team's grants, applied in the database query (lists, counts) and on every loaded record
/// (details, mutations, downloads). "Assigned" work is:
/// - a sale or buyer request assigned to the member;
/// - an opportunity assigned to the member, or prepared from a sale request assigned to them;
/// - an interest assigned to the member, or made on an opportunity assigned to them;
/// - a private document or photo of a sale request assigned to them.
/// Each permission is checked with its own scope; holding one permission for all work never widens another.
/// </summary>
public static class TeamScope
{
    public static IQueryable<SaleRequest> Scoped(this IQueryable<SaleRequest> q, RequestContext rc, string permission = P.MarketView)
    {
        var me = rc.UserId;
        return rc.ScopeOf(permission) switch
        {
            GrantScope.All => q,
            GrantScope.Assigned => q.Where(r => r.AssignedToUserId == me),
            _ => q.Where(_ => false),
        };
    }

    public static IQueryable<BuyerRequest> Scoped(this IQueryable<BuyerRequest> q, RequestContext rc, string permission = P.MarketView)
    {
        var me = rc.UserId;
        return rc.ScopeOf(permission) switch
        {
            GrantScope.All => q,
            GrantScope.Assigned => q.Where(r => r.AssignedToUserId == me),
            _ => q.Where(_ => false),
        };
    }

    public static IQueryable<Opportunity> Scoped(this IQueryable<Opportunity> q, RahoonDbContext db, RequestContext rc, string permission = P.MarketView)
    {
        var me = rc.UserId;
        return rc.ScopeOf(permission) switch
        {
            GrantScope.All => q,
            GrantScope.Assigned => q.Where(o => o.AssignedToUserId == me || db.SaleRequests.Any(s => s.Id == o.SaleRequestId && s.AssignedToUserId == me)),
            _ => q.Where(_ => false),
        };
    }

    public static IQueryable<Interest> Scoped(this IQueryable<Interest> q, RahoonDbContext db, RequestContext rc, string permission = P.MarketView)
    {
        var me = rc.UserId;
        return rc.ScopeOf(permission) switch
        {
            GrantScope.All => q,
            GrantScope.Assigned => q.Where(i => i.AssignedToUserId == me || db.Opportunities.Any(o => o.Id == i.OpportunityId && o.AssignedToUserId == me)),
            _ => q.Where(_ => false),
        };
    }

    /// <summary>
    /// Permission held for this record (its assignees given). Out of view scope → 404 (the record's existence is not
    /// revealed); visible but without this permission's scope → 403.
    /// </summary>
    public static void Need(RequestContext rc, string permission, params Guid?[] assignees)
    {
        if (!rc.CanOn(P.MarketView, assignees)) throw new NotFoundException();
        if (!rc.CanOn(permission, assignees)) throw new ForbiddenException();
    }

    public static Guid?[] Assignees(SaleRequest r) => [r.AssignedToUserId];
    public static Guid?[] Assignees(BuyerRequest r) => [r.AssignedToUserId];
    public static Guid?[] Assignees(Opportunity o, SaleRequest? sale) => [o.AssignedToUserId, sale?.AssignedToUserId];
    public static Guid?[] Assignees(Interest i, Opportunity? o) => [i.AssignedToUserId, o?.AssignedToUserId];
}
