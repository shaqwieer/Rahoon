using Microsoft.EntityFrameworkCore;
using Rahoon.Api.Infrastructure.Persistence;

namespace Rahoon.Api.Modules.Identity;

/// <summary>
/// Brings the Rahoon team's roles to the default matrix (<see cref="SystemRoles"/>), idempotently:
/// 1. renames the pre-1.5 role keys in place (team_lead → platform_owner, …) so existing members keep their access;
/// 2. creates missing system roles and sets each one's grants and scopes exactly to its template;
/// 3. on custom roles, only removes keys no longer in the catalog and normalizes the scope of non-scopable grants.
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
            var mine = roles.Where(r => r.OrganizationId == orgId).ToList();
            foreach (var (oldKey, newKey) in SystemRoles.Renamed)
                if (mine.FirstOrDefault(r => r.Key == oldKey) is { } old && mine.All(r => r.Key != newKey))
                    old.Key = newKey;

            foreach (var t in SystemRoles.Templates)
            {
                var role = mine.FirstOrDefault(r => r.Key == t.Key);
                if (role is null)
                {
                    role = new Role { OrganizationId = orgId, Key = t.Key, NameAr = t.NameAr, NameEn = t.NameEn };
                    db.Roles.Add(role);
                    mine.Add(role);
                }
                role.NameAr = t.NameAr;
                role.NameEn = t.NameEn;
                role.DescriptionAr = t.DescriptionAr;
                role.IsSystem = true;
                role.ArchivedAt = null;
                var wanted = t.Grants.ToDictionary(g => g.Key, g => g.Scope);
                role.Permissions.RemoveAll(p => !wanted.ContainsKey(p.PermissionKey));
                foreach (var p in role.Permissions) p.Scope = wanted[p.PermissionKey];
                foreach (var (key, scope) in wanted.Where(w => role.Permissions.All(p => p.PermissionKey != w.Key)))
                    role.Permissions.Add(new RolePermission { RoleId = role.Id, PermissionKey = key, Scope = scope });
            }

            foreach (var role in mine.Where(r => !SystemRoles.IsSystem(r.Key)))
            {
                role.IsSystem = false;
                role.Permissions.RemoveAll(p => !P.AllKeys.Contains(p.PermissionKey));
                foreach (var p in role.Permissions.Where(p => !P.IsScopable(p.PermissionKey))) p.Scope = GrantScope.All;
            }
        }
        await db.SaveChangesAsync();
    }
}
