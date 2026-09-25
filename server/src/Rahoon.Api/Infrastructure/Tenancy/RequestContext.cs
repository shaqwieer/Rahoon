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
    public string? PrimaryRoleNameAr { get; private set; }

    /// <summary>Owner (debtor) session: the single case the owner may see.</summary>
    public Guid? OwnerCaseId { get; private set; }
    public Guid? OwnerPartyId { get; private set; }
    public Guid? OwnerAccessId { get; private set; }

    /// <summary>Organizations whose rows EF may read in this request (global query filter).</summary>
    public IReadOnlyList<Guid> DataOrganizationIds { get; private set; } = [];
    /// <summary>Only for migrations, seeding and trusted background jobs.</summary>
    public bool SystemBypass { get; private set; }

    public DateTimeOffset? StepUpUntil { get; private set; }
    public string? IpMasked { get; set; }

    public bool Has(string permission) => Permissions.Contains(permission);
    public bool IsLenderStaff => Scope == SessionScope.Organization && OrganizationKind == Modules.Identity.OrganizationKind.Lender;
    public bool IsProvider => Scope == SessionScope.Organization && OrganizationKind == Modules.Identity.OrganizationKind.ServiceProvider;
    public bool IsJudicialAgent => Scope == SessionScope.Organization && OrganizationKind == Modules.Identity.OrganizationKind.JudicialAgent;
    public bool IsPlatform => Scope == SessionScope.Organization && OrganizationKind == Modules.Identity.OrganizationKind.Platform;
    public bool IsOwner => Scope == SessionScope.Owner;
    /// <summary>Self-registered individual (ADR 0001): no organization data at all; access to own requests only.</summary>
    public bool IsIndividual => Scope == SessionScope.Individual;

    public string ActorType => IsOwner ? "owner" : IsIndividual ? "individual" : IsProvider || IsJudicialAgent ? "provider" : IsPlatform ? "platform" : "user";

    public void SetAnonymous() => IsAuthenticated = false;

    public void SetSession(Guid userId, Guid sessionId, string userName, SessionScope scope, SessionStage stage, DateTimeOffset? stepUpUntil)
    {
        IsAuthenticated = true;
        UserId = userId;
        SessionId = sessionId;
        UserName = userName;
        Scope = scope;
        Stage = stage;
        StepUpUntil = stepUpUntil;
    }

    public void SetOrganization(Guid orgId, OrganizationKind kind, string orgName, Guid membershipId,
        IReadOnlySet<string> roles, IReadOnlySet<string> permissions, string? primaryRoleNameAr, IReadOnlyList<Guid> dataOrgIds)
    {
        OrganizationId = orgId;
        OrganizationKind = kind;
        OrganizationName = orgName;
        MembershipId = membershipId;
        RoleKeys = roles;
        Permissions = permissions;
        PrimaryRoleNameAr = primaryRoleNameAr;
        DataOrganizationIds = dataOrgIds;
    }

    public void SetOwner(Guid lenderOrgId, string orgName, Guid caseId, Guid partyId, Guid ownerAccessId)
    {
        OrganizationId = lenderOrgId;
        OrganizationName = orgName;
        OwnerCaseId = caseId;
        OwnerPartyId = partyId;
        OwnerAccessId = ownerAccessId;
        DataOrganizationIds = [lenderOrgId];
    }

    /// <summary>Individual session: no organization, no tenant data (DataOrganizationIds stays empty).</summary>
    public void SetIndividual()
    {
        OrganizationId = null;
        OrganizationName = null;
        DataOrganizationIds = [];
    }

    public void AddDataOrganizations(IEnumerable<Guid> orgIds) =>
        DataOrganizationIds = DataOrganizationIds.Concat(orgIds).Distinct().ToList();

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
