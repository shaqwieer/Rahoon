using Rahoon.Api.Infrastructure.Persistence;

namespace Rahoon.Api.Modules.Identity;

public enum OrganizationStatus { Onboarding, Active, Suspended }

/// <summary>A tenant. Only the Rahoon team (operator) exists: it owns the marketplace data and employs the team.</summary>
public sealed class Organization : Entity, IHasTimestamps
{
    public required string NameAr { get; set; }
    public string? NameEn { get; set; }
    public required string ShortCode { get; set; }
    public required string Initials { get; set; }
    public OrganizationKind Kind { get; set; }
    public OrganizationStatus Status { get; set; } = OrganizationStatus.Active;
    public int IdleTimeoutMinutes { get; set; } = 30;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>Pending = an individual's registration awaiting its first SMS verification (never gets a resolved session).</summary>
public enum UserStatus { Active, Locked, Disabled, Pending }

/// <summary>Staff (Rahoon team member, e-mail + password + SMS code) or Individual (owner/buyer, mobile sign-in).</summary>
public enum AccountKind { Staff, Individual }

public sealed class User : Entity, IHasTimestamps
{
    /// <summary>Staff login e-mail. Individuals get a reserved non-routable placeholder.</summary>
    public required string Email { get; set; }
    public required string FullName { get; set; }
    public AccountKind AccountKind { get; set; } = AccountKind.Staff;
    public string? Phone { get; set; }
    public string? PasswordHash { get; set; }
    public UserStatus Status { get; set; } = UserStatus.Active;
    public bool MfaEnrolled { get; set; }
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
    public List<MembershipRole> Roles { get; set; } = [];
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class Role : Entity, IOrgOwned
{
    public Guid OrganizationId { get; set; }
    public required string Key { get; set; }
    public required string NameAr { get; set; }
    public required string NameEn { get; set; }
    public List<RolePermission> Permissions { get; set; } = [];
}

public sealed class RolePermission
{
    public Guid RoleId { get; set; }
    public required string PermissionKey { get; set; }
}

public sealed class MembershipRole
{
    public Guid MembershipId { get; set; }
    public Guid RoleId { get; set; }
    public Role? Role { get; set; }
}

public enum SessionScope { None, Organization, Individual }
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
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset LastSeenAt { get; set; }
    public DateTimeOffset IdleExpiresAt { get; set; }
    public DateTimeOffset AbsoluteExpiresAt { get; set; }
    public DateTimeOffset? MfaVerifiedAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public string? RevokedReason { get; set; }
    public string? Ip { get; set; }
    public string? UserAgent { get; set; }
    public string? City { get; set; }
}

public enum OtpPurpose { Login, IndividualAccess }

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

/// <summary>An owner or buyer who signs in with a mobile number. The mobile is encrypted, with an HMAC lookup hash.</summary>
public sealed class IndividualProfile : Entity, IHasTimestamps
{
    public Guid UserId { get; set; }
    public User? User { get; set; }
    public required string PhoneEnc { get; set; }
    public required string PhoneHash { get; set; }
    public required string PhoneMasked { get; set; }
    /// <summary>Set when the mobile number was first verified by SMS code (registration completed).</summary>
    public DateTimeOffset? PhoneVerifiedAt { get; set; }
    public string? TermsVersion { get; set; }
    public DateTimeOffset? TermsAcceptedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>Every acceptance of the terms and privacy policy, with the version shown (evidence).</summary>
public sealed class TermsAcceptance : Entity
{
    public Guid UserId { get; set; }
    public required string Version { get; set; }
    public DateTimeOffset AcceptedAt { get; set; }
    public string? IpMasked { get; set; }
}
