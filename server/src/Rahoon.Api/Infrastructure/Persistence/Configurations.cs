using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Rahoon.Api.Modules.Audit;
using Rahoon.Api.Infrastructure.Storage;
using Rahoon.Api.Modules.Identity;

namespace Rahoon.Api.Infrastructure.Persistence;

// Each module keeps its tables in its own PostgreSQL schema (modular monolith).

internal sealed class OrganizationConfig : IEntityTypeConfiguration<Organization>
{
    public void Configure(EntityTypeBuilder<Organization> b)
    {
        b.ToTable("organizations", "identity");
        b.HasIndex(x => x.ShortCode).IsUnique();
        b.Property(x => x.NameAr).HasMaxLength(200);
    }
}

internal sealed class UserConfig : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> b)
    {
        b.ToTable("users", "identity");
        b.Property(x => x.Email).HasMaxLength(254);
        b.HasIndex(x => x.Email).IsUnique();
        b.Property(x => x.PasswordHash).HasMaxLength(400);
        b.Property(x => x.AccountKind).HasMaxLength(20).HasDefaultValue(AccountKind.Staff).HasSentinel((AccountKind)(-1));
    }
}

internal sealed class IndividualProfileConfig : IEntityTypeConfiguration<IndividualProfile>
{
    public void Configure(EntityTypeBuilder<IndividualProfile> b)
    {
        b.ToTable("individual_profiles", "identity");
        b.HasIndex(x => x.UserId).IsUnique();
        b.HasIndex(x => x.PhoneHash);
        b.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        b.Property(x => x.PhoneHash).HasMaxLength(64);
        b.Property(x => x.TermsVersion).HasMaxLength(60);
    }
}

internal sealed class TermsAcceptanceConfig : IEntityTypeConfiguration<TermsAcceptance>
{
    public void Configure(EntityTypeBuilder<TermsAcceptance> b)
    {
        b.ToTable("terms_acceptances", "identity");
        b.HasIndex(x => new { x.UserId, x.AcceptedAt });
        b.Property(x => x.Version).HasMaxLength(60);
    }
}

internal sealed class MembershipConfig : IEntityTypeConfiguration<Membership>
{
    public void Configure(EntityTypeBuilder<Membership> b)
    {
        b.ToTable("memberships", "identity");
        b.Property(x => x.StatusReason).HasMaxLength(500);
        b.HasIndex(x => new { x.UserId, x.OrganizationId }).IsUnique();
        b.HasOne(x => x.Organization).WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.Roles).WithOne().HasForeignKey(x => x.MembershipId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class RoleConfig : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> b)
    {
        b.ToTable("roles", "identity");
        b.HasIndex(x => new { x.OrganizationId, x.Key }).IsUnique();
        b.Property(x => x.NameAr).HasMaxLength(120);
        b.Property(x => x.NameEn).HasMaxLength(120);
        b.Property(x => x.DescriptionAr).HasMaxLength(500);
        b.HasMany(x => x.Permissions).WithOne().HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class RolePermissionConfig : IEntityTypeConfiguration<RolePermission>
{
    public void Configure(EntityTypeBuilder<RolePermission> b)
    {
        b.ToTable("role_permissions", "identity");
        b.HasKey(x => new { x.RoleId, x.PermissionKey });
        b.Property(x => x.PermissionKey).HasMaxLength(64);
        b.Property(x => x.Scope).HasMaxLength(16).HasDefaultValue(GrantScope.All).HasSentinel((GrantScope)(-1));
    }
}

internal sealed class StaffInvitationConfig : IEntityTypeConfiguration<StaffInvitation>
{
    public void Configure(EntityTypeBuilder<StaffInvitation> b)
    {
        b.ToTable("staff_invitations", "identity");
        b.Property(x => x.Email).HasMaxLength(254);
        b.Property(x => x.FullName).HasMaxLength(200);
        b.Property(x => x.Phone).HasMaxLength(20);
        b.Property(x => x.Title).HasMaxLength(120);
        b.Property(x => x.InvitedByLabel).HasMaxLength(200);
        b.HasIndex(x => x.TokenHash).IsUnique();
        // One pending invitation per e-mail: a double submit or a second inviter gets a conflict, never a second link.
        b.HasIndex(x => new { x.OrganizationId, x.Email }).IsUnique().HasFilter("status = 'Pending'");
    }
}

internal sealed class MembershipRoleConfig : IEntityTypeConfiguration<MembershipRole>
{
    public void Configure(EntityTypeBuilder<MembershipRole> b)
    {
        b.ToTable("membership_roles", "identity");
        b.HasKey(x => new { x.MembershipId, x.RoleId });
        b.HasOne(x => x.Role).WithMany().HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class SessionConfig : IEntityTypeConfiguration<Session>
{
    public void Configure(EntityTypeBuilder<Session> b)
    {
        b.ToTable("sessions", "identity");
        b.HasIndex(x => x.TokenHash).IsUnique();
        b.HasIndex(x => x.UserId);
        b.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class OtpConfig : IEntityTypeConfiguration<OtpChallenge>
{
    public void Configure(EntityTypeBuilder<OtpChallenge> b)
    {
        b.ToTable("otp_challenges", "identity");
        b.HasIndex(x => new { x.SessionId, x.Purpose });
        b.ToTable(t => t.HasCheckConstraint("ck_otp_attempts", "attempts >= 0 AND attempts <= 10"));
    }
}

internal sealed class ReferenceCounterConfig : IEntityTypeConfiguration<ReferenceCounter>
{
    public void Configure(EntityTypeBuilder<ReferenceCounter> b)
    {
        b.ToTable("reference_counters", "app");
        b.HasKey(x => x.Key);
        b.Property(x => x.Key).HasMaxLength(40);
    }
}

internal sealed class OutboundSmsConfig : IEntityTypeConfiguration<OutboundSms>
{
    public void Configure(EntityTypeBuilder<OutboundSms> b)
    {
        b.ToTable("outbound_sms", "app");
        b.Property(x => x.Destination).HasMaxLength(40);
        b.Property(x => x.Provider).HasMaxLength(40);
        b.Property(x => x.Result).HasMaxLength(20);
        b.HasIndex(x => x.CreatedAt);
    }
}

// ── Files: metadata and payload are separate tables so lists never load bytes ──

internal sealed class StoredFileConfig : IEntityTypeConfiguration<StoredFile>
{
    public void Configure(EntityTypeBuilder<StoredFile> b)
    {
        b.ToTable("stored_files", "files");
        b.Property(x => x.FileName).HasMaxLength(255);
        b.Property(x => x.ContentType).HasMaxLength(100);
        b.Property(x => x.Sha256).HasMaxLength(64);
        b.Property(x => x.Provider).HasMaxLength(40);
        b.Property(x => x.StorageRef).HasMaxLength(500);
        b.Property(x => x.Visibility).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.SubjectType).HasMaxLength(40);
        b.HasIndex(x => new { x.SubjectType, x.SubjectId });
        b.HasIndex(x => new { x.Provider, x.StorageRef }).IsUnique();
        b.ToTable(t => t.HasCheckConstraint("ck_stored_files_size", "size_bytes > 0"));
    }
}

internal sealed class FileBlobConfig : IEntityTypeConfiguration<FileBlob>
{
    public void Configure(EntityTypeBuilder<FileBlob> b)
    {
        b.ToTable("file_blobs", "files");
        b.Property(x => x.Content).HasColumnType("bytea");
    }
}

internal sealed class IdempotencyConfig : IEntityTypeConfiguration<IdempotencyRecord>
{
    public void Configure(EntityTypeBuilder<IdempotencyRecord> b)
    {
        b.ToTable("idempotency_records", "app");
        b.HasKey(x => new { x.UserId, x.Key });
        b.Property(x => x.Key).HasMaxLength(100);
        b.Property(x => x.ResponseJson).HasColumnType("text"); // exact bytes for replay
    }
}

internal sealed class AuditEventConfig : IEntityTypeConfiguration<AuditEvent>
{
    public void Configure(EntityTypeBuilder<AuditEvent> b)
    {
        b.ToTable("audit_events", "audit");
        b.HasKey(x => x.Seq);
        b.Property(x => x.Seq).UseIdentityAlwaysColumn();
        b.HasIndex(x => x.Id).IsUnique();
        b.HasIndex(x => new { x.OrganizationId, x.OccurredAt });
        // Text, not jsonb: jsonb rewrites the JSON (spacing, key order) and the stored hash would no longer verify.
        b.Property(x => x.DataJson).HasColumnType("text");
        b.Property(x => x.Hash).HasMaxLength(80);
        b.Property(x => x.PrevHash).HasMaxLength(80);
        b.Property(x => x.SubjectType).HasMaxLength(20);
        b.Property(x => x.SubjectReference).HasMaxLength(40);
        b.HasIndex(x => new { x.SubjectReference, x.Seq });
    }
}
