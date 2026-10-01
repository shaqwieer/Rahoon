using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Rahoon.Api.Infrastructure.Persistence;

namespace Rahoon.Api.Modules.OrgDirectory;

/// <summary>Kinds of organization in the directory. One organization may have several (e.g. a bank that also develops).</summary>
public static class OrgTypes
{
    public const string Developer = "developer";
    public const string Bank = "bank";
    public const string FinanceCompany = "finance_company";

    public static readonly IReadOnlyList<string> All = [Developer, Bank, FinanceCompany];

    public static readonly IReadOnlyDictionary<string, string> Labels = new Dictionary<string, string>
    {
        [Developer] = "مطور عقاري",
        [Bank] = "بنك",
        [FinanceCompany] = "شركة تمويل",
    };

    /// <summary>Directory types an obligation of the given kind may name (developer → developers; financier → banks and finance companies).</summary>
    public static IReadOnlyList<string> ForObligationKind(string kind) => kind == "developer" ? [Developer] : [Bank, FinanceCompany];
}

public static class DirectoryOrigins
{
    public const string Import = "import";
    public const string Manual = "manual";
}

/// <summary>
/// A real Saudi organization an owner or buyer can name: a real-estate developer, a bank or a finance company. Inclusion in
/// the directory says only that the organization exists and was found in the cited source — never that Rahoon is partnered
/// with it, that it supports contract transfers, or (unless <see cref="LicenseNumber"/> comes from the source) that it is licensed.
/// Records are deactivated, never deleted. Not tenant data.
/// </summary>
public sealed class DirectoryOrganization : Entity, IHasTimestamps, IConcurrencyVersioned
{
    public required string NameAr { get; set; }
    public string? NameEn { get; set; }
    /// <summary>Normalized Arabic name used to match and deduplicate (see <see cref="DirectoryNames.Normalize"/>).</summary>
    public required string NormalizedNameAr { get; set; }
    public string? NormalizedNameEn { get; set; }
    /// <summary>developer · bank · finance_company (one or more).</summary>
    public List<string> Types { get; set; } = [];
    public string? Website { get; set; }
    /// <summary>License or registration number, only when the cited authoritative source publishes it.</summary>
    public string? LicenseNumber { get; set; }
    public string? RegistrationNumber { get; set; }
    /// <summary>Human name of the source (e.g. «البنك المركزي السعودي — البنوك المرخصة»).</summary>
    public string? SourceName { get; set; }
    public string? SourceUrl { get; set; }
    /// <summary>Date the record was last confirmed against its source (or by an administrator).</summary>
    public DateOnly? VerifiedOn { get; set; }
    public bool Active { get; set; } = true;
    /// <summary>import · manual — how the record was first created.</summary>
    public required string Origin { get; set; }
    /// <summary>Stable keys of the import sources that found this record (e.g. «sama-banks:alrajhi»), used to match on rerun.</summary>
    public List<string> ImportKeys { get; set; } = [];
    public DateTimeOffset? LastImportedAt { get; set; }
    /// <summary>Set by any administrator edit: imports never change such a record again (only add new import keys).</summary>
    public DateTimeOffset? AdminEditedAt { get; set; }
    public Guid? AdminEditedByUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public uint Version { get; set; }
}

internal sealed class DirectoryOrganizationConfig : IEntityTypeConfiguration<DirectoryOrganization>
{
    public void Configure(EntityTypeBuilder<DirectoryOrganization> b)
    {
        b.ToTable("organizations", "directory");
        b.HasKey(x => x.Id).HasName("pk_directory_organizations");
        b.Property(x => x.NameAr).HasMaxLength(200);
        b.Property(x => x.NameEn).HasMaxLength(200);
        b.Property(x => x.NormalizedNameAr).HasMaxLength(200);
        b.Property(x => x.NormalizedNameEn).HasMaxLength(200);
        b.Property(x => x.Types).HasColumnType("text[]");
        b.Property(x => x.ImportKeys).HasColumnType("text[]");
        b.Property(x => x.Website).HasMaxLength(300);
        b.Property(x => x.LicenseNumber).HasMaxLength(60);
        b.Property(x => x.RegistrationNumber).HasMaxLength(60);
        b.Property(x => x.SourceName).HasMaxLength(200);
        b.Property(x => x.SourceUrl).HasMaxLength(500);
        b.Property(x => x.Origin).HasMaxLength(20);
        b.HasIndex(x => x.NormalizedNameAr).IsUnique();
        b.HasIndex(x => x.Types).HasMethod("gin");
        b.HasIndex(x => x.ImportKeys).HasMethod("gin");
    }
}
