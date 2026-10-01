using Rahoon.Api.Modules.Identity;

namespace Rahoon.Api.Infrastructure.Tenancy;

/// <summary>
/// Who is calling and what data they may touch, resolved on the server from the
/// authenticated session and membership — never from client-supplied tenant ids.
/// </summary>
public sealed class RequestContext
{
    public bool IsAuthenticated { get; private set; }
    public Guid UserId { get; private set; }
    public Guid SessionId { get; private set; }
    public string UserName { get; private set; } = "";
    public SessionScope Scope { get; private set; }
    public SessionStage Stage { get; private set; }

    public Guid? OrganizationId { get; private set; }
    public OrganizationKind? OrganizationKind { get; private set; }
    public string? OrganizationName { get; private set; }
    public Guid? MembershipId { get; private set; }
    public IReadOnlySet<string> RoleKeys { get; private set; } = new HashSet<string>();
    public IReadOnlySet<string> Permissions { get; private set; } = new HashSet<string>();
    /// <summary>Effective scope of each held permission: the widest scope among the roles granting that permission only.</summary>
    public IReadOnlyDictionary<string, GrantScope> Grants { get; private set; } = new Dictionary<string, GrantScope>();
    public string? PrimaryRoleNameAr { get; private set; }

    /// <summary>Organizations whose rows EF may read in this request (global query filter).</summary>
    public IReadOnlyList<Guid> DataOrganizationIds { get; private set; } = [];
    /// <summary>Only for migrations, seeding and trusted background jobs.</summary>
    public bool SystemBypass { get; private set; }

    public string? IpMasked { get; set; }

    public bool Has(string permission) => Permissions.Contains(permission);
    public GrantScope? ScopeOf(string permission) => Grants.TryGetValue(permission, out var s) ? s : null;
    /// <summary>Holds the permission over all of the team's work (not just assigned work).</summary>
    public bool HasAll(string permission) => ScopeOf(permission) == GrantScope.All;
    /// <summary>Holds the permission for a record whose assignees (the record's own, and the work it belongs to) are given.</summary>
    public bool CanOn(string permission, params Guid?[] assignees) => ScopeOf(permission) switch
    {
        GrantScope.All => true,
        GrantScope.Assigned => assignees.Any(a => a == UserId),
        _ => false,
    };
    /// <summary>Rahoon team member (operator tenant).</summary>
    public bool IsOperator => Scope == SessionScope.Organization && OrganizationKind == Modules.Identity.OrganizationKind.Operator;
    /// <summary>Owner or buyer (mobile sign-in): no organization data at all; access to their own rows only.</summary>
    public bool IsIndividual => Scope == SessionScope.Individual;

    public string ActorType => IsIndividual ? "individual" : IsOperator ? "rahoon_team" : "user";

    public void SetAnonymous() => IsAuthenticated = false;

    public void SetSession(Guid userId, Guid sessionId, string userName, SessionScope scope, SessionStage stage)
    {
        IsAuthenticated = true;
        UserId = userId;
        SessionId = sessionId;
        UserName = userName;
        Scope = scope;
        Stage = stage;
    }

    public void SetOrganization(Guid orgId, OrganizationKind kind, string orgName, Guid membershipId,
        IReadOnlySet<string> roles, IReadOnlyDictionary<string, GrantScope> grants, string? primaryRoleNameAr, IReadOnlyList<Guid> dataOrgIds)
    {
        var permissions = grants.Keys.ToHashSet();
        Grants = grants;
        OrganizationId = orgId;
        OrganizationKind = kind;
        OrganizationName = orgName;
        MembershipId = membershipId;
        RoleKeys = roles;
        Permissions = permissions;
        PrimaryRoleNameAr = primaryRoleNameAr;
        DataOrganizationIds = dataOrgIds;
    }

    /// <summary>Individual session: no organization, no tenant data (DataOrganizationIds stays empty).</summary>
    public void SetIndividual()
    {
        OrganizationId = null;
        OrganizationName = null;
        DataOrganizationIds = [];
    }

    /// <summary>Elevate for trusted internal work. Callers must not expose results unfiltered.</summary>
    public IDisposable BeginSystemScope()
    {
        var previous = SystemBypass;
        SystemBypass = true;
        return new Restore(() => SystemBypass = previous);
    }

    public static RequestContext System()
    {
        var ctx = new RequestContext { SystemBypass = true };
        return ctx;
    }

    private sealed class Restore(Action a) : IDisposable { public void Dispose() => a(); }
}
