using Microsoft.EntityFrameworkCore;
using Rahoon.Api.Infrastructure.Persistence;

namespace Rahoon.Api.Modules.Identity;

/// <summary>
/// Brings the Rahoon team's role rows up to the system templates: creates missing roles, refreshes names, adds missing
/// permissions and removes keys that are no longer in the catalog. Never removes a catalog grant the team changed.
/// Runs with every migration (`dotnet run -- migrate` and migrate-on-startup) and with the demo seed. Caller supplies
/// the system scope.
/// </summary>
public static class SystemRoleSync
{
    public static async Task SyncAsync(RahoonDbContext db)
    {
        var orgIds = await db.Organizations.IgnoreQueryFilters().Where(o => o.Kind == OrganizationKind.Operator).Select(o => o.Id).ToListAsync();
        var roles = await db.Roles.IgnoreQueryFilters().Include(r => r.Permissions).Where(r => orgIds.Contains(r.OrganizationId)).ToListAsync();
        foreach (var orgId in orgIds)
        {
            foreach (var t in SystemRoles.Templates)
            {
                var role = roles.FirstOrDefault(r => r.OrganizationId == orgId && r.Key == t.Key);
                if (role is null)
                {
                    role = new Role { OrganizationId = orgId, Key = t.Key, NameAr = t.NameAr, NameEn = t.NameEn };
                    db.Roles.Add(role);
                }
                role.NameAr = t.NameAr;
                role.NameEn = t.NameEn;
                var have = role.Permissions.Select(p => p.PermissionKey).ToHashSet();
                foreach (var key in t.Permissions.Distinct().Where(k => !have.Contains(k)))
                    role.Permissions.Add(new RolePermission { RoleId = role.Id, PermissionKey = key });
            }
            foreach (var role in roles.Where(r => r.OrganizationId == orgId))
                role.Permissions.RemoveAll(p => !P.AllKeys.Contains(p.PermissionKey));
        }
        await db.SaveChangesAsync();
    }
}
