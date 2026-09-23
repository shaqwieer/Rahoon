using Rahoon.Api.Infrastructure.Persistence;

namespace Rahoon.Api.Modules.Identity;

public enum OrganizationStatus { Onboarding, Active, Suspended }

/// <summary>A tenant: lender institution, service provider, judicial agent office, or the platform itself.</summary>
public sealed class Organization : Entity, IHasTimestamps
{
    public required string NameAr { get; set; }
    public string? NameEn { get; set; }
    public required string ShortCode { get; set; }
    public required string Initials { get; set; }
    public OrganizationKind Kind { get; set; }
    public OrganizationStatus Status { get; set; } = OrganizationStatus.Active;
    public string? LicenseNumber { get; set; }
    public string? City { get; set; }
    public string DefaultOwnerLanguage { get; set; } = "ar";
    public int IdleTimeoutMinutes { get; set; } = 30;
    public bool MfaRequired { get; set; } = true;
    public List<string> AllowedEmailDomains { get; set; } = [];
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public enum UserStatus { Active, Locked, Disabled }
public enum MfaMethod { Sms, Authenticator }

public sealed class User : Entity, IHasTimestamps
{
    public required string Email { get; set; }
    public required string FullName { get; set; }
    public string? FullNameEn { get; set; }
    public string? Phone { get; set; }
    public string? PasswordHash { get; set; }
    public UserStatus Status { get; set; } = UserStatus.Active;
    public bool MfaEnrolled { get; set; }
    public MfaMethod MfaMethod { get; set; } = MfaMethod.Sms;
    public string PreferredLocale { get; set; } = "ar";
    public string NumeralStyle { get; set; } = "latn";
    public int FailedLoginCount { get; set; }
    public DateTimeOffset? LockedUntil { get; set; }
    public DateTimeOffset? LastLoginAt { get; set; }
    public DateTimeOffset? PasswordChangedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public enum MembershipStatus { Invited, Active, Suspended, Revoked }

public sealed class Membership : Entity, IOrgOwned, IHasTimestamps
{
    public Guid OrganizationId { get; set; }
    public Organization? Organization { get; set; }
    public Guid UserId { get; set; }
    public User? User { get; set; }
    public MembershipStatus Status { get; set; } = MembershipStatus.Active;
    public string? Title { get; set; }
    public Guid? TeamId { get; set; }
    public Team? Team { get; set; }
    public List<MembershipRole> Roles { get; set; } = [];
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class Team : Entity, IOrgOwned
{
    public Guid OrganizationId { get; set; }
    public required string NameAr { get; set; }
}

public sealed class Role : Entity, IOrgOwned
{
    public Guid OrganizationId { get; set; }
    public required string Key { get; set; }
    public required string NameAr { get; set; }
    public required string NameEn { get; set; }
    public List<RolePermission> Permissions { get; set; } = [];
}

public enum PermissionGrant { Allow, Conditional }

public sealed class RolePermission
{
    public Guid RoleId { get; set; }
    public required string PermissionKey { get; set; }
    public PermissionGrant Grant { get; set; } = PermissionGrant.Allow;
}

public sealed class MembershipRole
{
    public Guid MembershipId { get; set; }
    public Guid RoleId { get; set; }
    public Role? Role { get; set; }
}

public enum SessionScope { None, Organization, Owner }
public enum SessionStage { MfaPending, Active }

/// <summary>
/// Server-side session. The browser holds only an opaque random token in an
/// HttpOnly cookie; we store its SHA-256. Revocation is immediate.
/// </summary>
public sealed class Session : Entity
{
    public Guid UserId { get; set; }
    public User? User { get; set; }
    public required byte[] TokenHash { get; set; }
    public required byte[] CsrfHash { get; set; }
    public SessionStage Stage { get; set; }
    public SessionScope Scope { get; set; }
    public Guid? OrganizationId { get; set; }
    public Guid? MembershipId { get; set; }
    public Guid? OwnerAccessId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset LastSeenAt { get; set; }
    public DateTimeOffset IdleExpiresAt { get; set; }
    public DateTimeOffset AbsoluteExpiresAt { get; set; }
    public DateTimeOffset? MfaVerifiedAt { get; set; }
    public DateTimeOffset? StepUpUntil { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public string? RevokedReason { get; set; }
    public string? Ip { get; set; }
    public string? UserAgent { get; set; }
    public string? City { get; set; }
}

public enum OtpPurpose { Login, StepUp, OwnerInvite, OwnerLogin, Consent }

/// <summary>One-time code sent through the (sandboxed) SMS channel. Stored hashed.</summary>
public sealed class OtpChallenge : Entity
{
    public Guid? UserId { get; set; }
    public Guid? SessionId { get; set; }
    public OtpPurpose Purpose { get; set; }
    public required byte[] CodeHash { get; set; }
    public string? Destination { get; set; }
    public string? Context { get; set; }
    public int Attempts { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? ConsumedAt { get; set; }
}

public enum InvitationStatus { Pending, Accepted, Expired, Cancelled }

public sealed class Invitation : Entity, IOrgOwned
{
    public Guid OrganizationId { get; set; }
    public required string Email { get; set; }
    public required string FullName { get; set; }
    public string? Phone { get; set; }
    public Guid RoleId { get; set; }
    public Guid? TeamId { get; set; }
    public required byte[] TokenHash { get; set; }
    public InvitationStatus Status { get; set; } = InvitationStatus.Pending;
    public Guid InvitedByUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? AcceptedAt { get; set; }
}

public enum RoleChangeStatus { Pending, Approved, Rejected }

/// <summary>Maker-checker for role changes (A02).</summary>
public sealed class RoleChangeRequest : Entity, IOrgOwned
{
    public Guid OrganizationId { get; set; }
    public Guid MembershipId { get; set; }
    public Guid ToRoleId { get; set; }
    public Guid? FromRoleId { get; set; }
    public string? Reason { get; set; }
    public RoleChangeStatus Status { get; set; }
    public Guid RequestedByUserId { get; set; }
    public Guid? DecidedByUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? DecidedAt { get; set; }
}
