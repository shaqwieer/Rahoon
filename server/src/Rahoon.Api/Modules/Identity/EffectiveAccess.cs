namespace Rahoon.Api.Modules.Identity;

/// <summary>
/// Combines a member's roles into effective grants, and decides grant authority. Scope is combined per permission only:
/// an "all" grant of one permission never widens another permission's scope. Unknown and reserved keys grant nothing.
/// </summary>
public static class EffectiveAccess
{
    public static IReadOnlyDictionary<string, GrantScope> Grants(IEnumerable<Role> roles)
    {
        var result = new Dictionary<string, GrantScope>();
        foreach (var g in roles.Where(r => r.ArchivedAt is null).SelectMany(r => r.Permissions))
        {
            if (!P.GrantableKeys.Contains(g.PermissionKey)) continue;
            var scope = P.IsScopable(g.PermissionKey) ? g.Scope : GrantScope.All;
            result[g.PermissionKey] = result.TryGetValue(g.PermissionKey, out var have) && have > scope ? have : scope;
        }
        return result;
    }

    public sealed record Explained(string Key, GrantScope Scope, IReadOnlyList<(Guid RoleId, string RoleName, GrantScope Scope)> From);

    /// <summary>Each effective permission with the roles that grant it (for the effective-access view).</summary>
    public static IReadOnlyList<Explained> Explain(IEnumerable<Role> roles)
    {
        var list = roles.Where(r => r.ArchivedAt is null).ToList();
        var grants = Grants(list);
        return P.Catalog.Where(p => grants.ContainsKey(p.Key)).Select(p => new Explained(p.Key, grants[p.Key],
            list.SelectMany(r => r.Permissions.Where(x => x.PermissionKey == p.Key)
                .Select(x => (r.Id, r.NameAr, P.IsScopable(p.Key) ? x.Scope : GrantScope.All))).ToList())).ToList();
    }

    /// <summary>
    /// True when <paramref name="held"/> covers every grant in <paramref name="wanted"/> at an equal or wider scope.
    /// Used both for "may I grant this" and for "may I manage this member" (no acting on someone with more access).
    /// </summary>
    public static bool Covers(IReadOnlyDictionary<string, GrantScope> held, IReadOnlyDictionary<string, GrantScope> wanted) =>
        wanted.All(w => held.TryGetValue(w.Key, out var h) && h >= w.Value);

    /// <summary>The grants in <paramref name="wanted"/> that <paramref name="held"/> does not cover.</summary>
    public static IReadOnlyList<string> Missing(IReadOnlyDictionary<string, GrantScope> held, IReadOnlyDictionary<string, GrantScope> wanted) =>
        wanted.Where(w => !(held.TryGetValue(w.Key, out var h) && h >= w.Value)).Select(w => w.Key).ToList();
}
