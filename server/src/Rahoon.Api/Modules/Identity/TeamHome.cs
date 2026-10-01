using Rahoon.Api.Infrastructure.Tenancy;

namespace Rahoon.Api.Modules.Identity;

/// <summary>Where a signed-in team member lands: the first console area their grants open, never a page that refuses them.</summary>
public static class TeamHome
{
    /// <summary>Each area with every permission its page needs (the overview reads request queues too).</summary>
    private static readonly (string[] Permissions, string Path)[] Order =
    [
        ([P.DashboardView, P.MarketView], "/team"),
        ([P.MarketView], "/team/sale"),
        ([P.TeamRead], "/team/members"),
        ([P.RolesRead], "/team/roles"),
        ([P.AuditRead], "/team/audit"),
        ([P.DirectoryRead], "/team/organizations"),
        ([P.DirectoryManage], "/team/organizations"),
    ];

    public static string For(RequestContext rc) => Order.FirstOrDefault(o => o.Permissions.All(rc.Has)).Path ?? "/access-denied";
}
