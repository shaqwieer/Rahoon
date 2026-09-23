using Microsoft.EntityFrameworkCore;
using Rahoon.Api.Infrastructure.Persistence;
using Rahoon.Api.Modules.Identity;

namespace Rahoon.Api.Modules.Solutions;

public sealed record ResolvedApprover(Guid UserId, string Name, string TierLabel, string RoleKey, decimal? MaxAmount, decimal? MaxWaiverPercent, int TierRank);

/// <summary>
/// Picks the approver for a solution from the institution's effective approval-limit policy (A04):
/// the lowest tier whose limits cover the amount, waiver % and solution kind, then an active member
/// of that tier who is neither preparer nor reviewer (separation of duties), least loaded first.
/// Exceeding a tier escalates to the next tier rather than blocking entry (solution builder spec).
/// Amount basis: outstanding at preparation — an assumption pending product confirmation.
/// </summary>
public static class ApprovalRouting
{
    public static async Task<ResolvedApprover?> ResolveApproverAsync(RahoonDbContext db, Guid orgId, SolutionVersion v, IEnumerable<Guid> excludedUserIds)
    {
        var policy = await db.ApprovalLimitPolicies.Include(p => p.Tiers)
            .Where(p => p.OrganizationId == orgId && p.Status == "effective").OrderByDescending(p => p.VersionNo).FirstOrDefaultAsync();
        if (policy is null) return null;

        var excluded = excludedUserIds.ToHashSet();
        var amount = v.OutstandingAtPreparation;
        var kind = v.Kind.ToString();
        foreach (var tier in policy.Tiers.Where(t => t.CanApprove).OrderBy(t => t.Rank))
        {
            if (!tier.SolutionKinds.Contains(kind)) continue;
            if (tier.MaxAmount is { } max && amount > max) continue;
            if (tier.MaxWaiverPercent is { } maxW && v.WaiverPercent > maxW) continue;

            var candidates = await db.Memberships
                .Where(m => m.OrganizationId == orgId && m.Status == MembershipStatus.Active && m.Roles.Any(r => r.Role!.Key == tier.RoleKey))
                .Select(m => new { m.UserId, m.User!.FullName, m.CreatedAt })
                .ToListAsync();
            candidates = candidates.Where(c => !excluded.Contains(c.UserId)).ToList();
            if (candidates.Count == 0) continue;

            var ids = candidates.Select(c => c.UserId).ToList();
            var load = await db.ApprovalRequests.Where(a => a.Status == ApprovalStatus.Pending && a.AssignedApproverUserId != null && ids.Contains(a.AssignedApproverUserId.Value))
                .GroupBy(a => a.AssignedApproverUserId).Select(g => new { g.Key, N = g.Count() }).ToListAsync();
            var pick = candidates.OrderBy(c => load.FirstOrDefault(l => l.Key == c.UserId)?.N ?? 0).ThenBy(c => c.CreatedAt).First();
            return new ResolvedApprover(pick.UserId, pick.FullName, tier.LevelLabel, tier.RoleKey, tier.MaxAmount, tier.MaxWaiverPercent, tier.Rank);
        }
        return null;
    }

    /// <summary>Checks at decision time that the deciding user's tier still covers the request.</summary>
    public static async Task<bool> UserCanApproveAsync(RahoonDbContext db, Guid orgId, Guid userId, SolutionVersion v)
    {
        var policy = await db.ApprovalLimitPolicies.Include(p => p.Tiers)
            .Where(p => p.OrganizationId == orgId && p.Status == "effective").OrderByDescending(p => p.VersionNo).FirstOrDefaultAsync();
        if (policy is null) return false;
        var roleKeys = await db.MembershipRoles.Where(r => db.Memberships.Any(m => m.Id == r.MembershipId && m.UserId == userId && m.OrganizationId == orgId && m.Status == MembershipStatus.Active))
            .Select(r => r.Role!.Key).ToListAsync();
        return policy.Tiers.Any(t => t.CanApprove && roleKeys.Contains(t.RoleKey) && t.SolutionKinds.Contains(v.Kind.ToString())
                                     && (t.MaxAmount is null || v.OutstandingAtPreparation <= t.MaxAmount)
                                     && (t.MaxWaiverPercent is null || v.WaiverPercent <= t.MaxWaiverPercent));
    }
}
