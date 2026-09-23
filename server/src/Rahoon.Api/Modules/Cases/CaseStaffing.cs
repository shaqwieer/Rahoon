using Microsoft.EntityFrameworkCore;
using Rahoon.Api.Infrastructure.Http;
using Rahoon.Api.Infrastructure.Persistence;
using Rahoon.Api.Modules.Identity;
using TaskStatus = Rahoon.Api.Modules.Communications.TaskStatus;

namespace Rahoon.Api.Modules.Cases;

public sealed record StaffPick(Guid UserId, Guid MembershipId, string Name, string RoleKey);

/// <summary>
/// Picks the institution member who receives routed work (correction requests, revaluation tasks,
/// cancellation approvals): an active member holding the permission, never one of the excluded users,
/// preferring the listed roles, then the lightest open-task load, then name.
/// </summary>
public static class CaseStaffing
{
    public static async Task<StaffPick?> PickAsync(RahoonDbContext db, Guid orgId, string permission, IReadOnlyCollection<Guid> exclude, params string[] preferRoles)
    {
        var rows = await (
            from m in db.Memberships
            where m.OrganizationId == orgId && m.Status == MembershipStatus.Active && !exclude.Contains(m.UserId)
            join mr in db.MembershipRoles on m.Id equals mr.MembershipId
            join r in db.Roles on mr.RoleId equals r.Id
            where db.RolePermissions.Any(p => p.RoleId == r.Id && p.PermissionKey == permission)
            select new { m.UserId, MembershipId = m.Id, RoleKey = r.Key, Name = m.User!.FullName }).ToListAsync();
        if (rows.Count == 0) return null;

        var ids = rows.Select(r => r.UserId).Distinct().ToList();
        var load = await db.Tasks.Where(t => t.OrganizationId == orgId && t.Status == TaskStatus.Open && t.AssigneeUserId != null && ids.Contains(t.AssigneeUserId.Value))
            .GroupBy(t => t.AssigneeUserId!.Value).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count);

        int Rank(string role) => Array.IndexOf(preferRoles, role) is var i && i >= 0 ? i : preferRoles.Length;
        var best = rows
            .OrderBy(r => Rank(r.RoleKey))
            .ThenBy(r => load.GetValueOrDefault(r.UserId))
            .ThenBy(r => r.Name, StringComparer.Ordinal)
            .First();
        return new StaffPick(best.UserId, best.MembershipId, best.Name, best.RoleKey);
    }

    /// <summary>The given user as a pick, if they are an active member of the org holding the permission.</summary>
    public static async Task<StaffPick?> MemberWithAsync(RahoonDbContext db, Guid orgId, Guid userId, string permission) =>
        await (
            from m in db.Memberships
            where m.OrganizationId == orgId && m.UserId == userId && m.Status == MembershipStatus.Active
            join mr in db.MembershipRoles on m.Id equals mr.MembershipId
            join r in db.Roles on mr.RoleId equals r.Id
            where db.RolePermissions.Any(p => p.RoleId == r.Id && p.PermissionKey == permission)
            select new StaffPick(m.UserId, m.Id, m.User!.FullName, r.Key)).FirstOrDefaultAsync();

    public static void EnsureNotTerminal(Case c, string message = "الحالة مغلقة أو ملغاة؛ لا يمكن تعديلها.")
    {
        if (CaseStatusInfo.IsTerminal(c.Status)) throw new ConflictException("case_closed", message);
    }

    /// <summary>Arabic count of installments with number agreement: قسط واحد / قسطان / 3 أقساط / 12 قسطاً.</summary>
    public static string Installments(int n) => n switch
    {
        1 => "قسط واحد",
        2 => "قسطان",
        >= 3 and <= 10 => $"{n} أقساط",
        _ => $"{n} قسطاً",
    };
}
