using Microsoft.EntityFrameworkCore;
using Rahoon.Api.Infrastructure.Persistence;

namespace Rahoon.Api.Modules.Identity;

/// <summary>
/// Brings the role rows of every organization up to its system templates (names and permissions). Role rows are copied
/// per organization, so permissions added later (e.g. market.* on 2026-10-01) would otherwise never reach existing tenants.
/// Adds missing permissions only; never removes a grant an organization changed. Runs with every migration (all
/// environments, `dotnet run -- migrate` and migrate-on-startup) and with the demo seed. Caller supplies the system scope.
/// </summary>
public static class SystemRoleSync
{
    public static async Task SyncAsync(RahoonDbContext db)
    {
        var roles = await db.Roles.IgnoreQueryFilters().Include(r => r.Permissions).ToListAsync();
        var orgKinds = await db.Organizations.IgnoreQueryFilters().ToDictionaryAsync(o => o.Id, o => o.Kind);
        foreach (var role in roles)
        {
            var t = SystemRoles.Find(role.Key);
            if (t is null || !orgKinds.TryGetValue(role.OrganizationId, out var kind) || kind != t.Kind) continue;
            role.NameAr = t.NameAr;
            role.NameEn = t.NameEn;
            var have = role.Permissions.Select(p => p.PermissionKey).ToHashSet();
            foreach (var key in t.Permissions.Distinct().Where(k => !have.Contains(k)))
                role.Permissions.Add(new RolePermission { RoleId = role.Id, PermissionKey = key, Grant = PermissionGrant.Allow });
        }
        await db.SaveChangesAsync();
    }
}
