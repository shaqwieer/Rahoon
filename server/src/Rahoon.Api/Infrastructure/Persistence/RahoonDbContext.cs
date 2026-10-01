using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Rahoon.Api.Infrastructure.Storage;
using Rahoon.Api.Infrastructure.Tenancy;
using Rahoon.Api.Modules.Audit;
using Rahoon.Api.Modules.Identity;
using Rahoon.Api.Modules.Market;

namespace Rahoon.Api.Infrastructure.Persistence;

public sealed class RahoonDbContext(DbContextOptions<RahoonDbContext> options, RequestContext requestContext) : DbContext(options)
{
    private readonly RequestContext _rc = requestContext;

    public RequestContext Request => _rc;

    // Identity: the Rahoon team (staff) and owners/buyers (individuals)
    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<User> Users => Set<User>();
    public DbSet<IndividualProfile> IndividualProfiles => Set<IndividualProfile>();
    public DbSet<TermsAcceptance> TermsAcceptances => Set<TermsAcceptance>();
    public DbSet<Membership> Memberships => Set<Membership>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<MembershipRole> MembershipRoles => Set<MembershipRole>();
    public DbSet<Session> Sessions => Set<Session>();
    public DbSet<OtpChallenge> OtpChallenges => Set<OtpChallenge>();

    // Exit/buy marketplace: operator tenant + the person (seller or buyer)
    public DbSet<SaleRequest> SaleRequests => Set<SaleRequest>();
    public DbSet<SaleObligation> SaleObligations => Set<SaleObligation>();
    public DbSet<PrivateDocument> PrivateDocuments => Set<PrivateDocument>();
    public DbSet<ListingPhoto> ListingPhotos => Set<ListingPhoto>();
    public DbSet<FigureVerification> FigureVerifications => Set<FigureVerification>();
    public DbSet<ExternalApproval> ExternalApprovals => Set<ExternalApproval>();
    public DbSet<CompletionRequest> CompletionRequests => Set<CompletionRequest>();
    public DbSet<BuyerRequest> BuyerRequests => Set<BuyerRequest>();
    public DbSet<Opportunity> Opportunities => Set<Opportunity>();
    public DbSet<OpportunityTerms> OpportunityTerms => Set<OpportunityTerms>();
    public DbSet<Interest> Interests => Set<Interest>();
    public DbSet<SavedOpportunity> SavedOpportunities => Set<SavedOpportunity>();
    public DbSet<MarketEvent> MarketEvents => Set<MarketEvent>();
    public DbSet<MarketNotification> MarketNotifications => Set<MarketNotification>();
    public DbSet<ContactMessage> ContactMessages => Set<ContactMessage>();

    // Saudi organization directory (developers, banks, finance companies)
    public DbSet<Modules.OrgDirectory.DirectoryOrganization> DirectoryOrganizations => Set<Modules.OrgDirectory.DirectoryOrganization>();

    // Files (metadata separate from payload)
    public DbSet<StoredFile> StoredFiles => Set<StoredFile>();
    public DbSet<FileBlob> FileBlobs => Set<FileBlob>();

    // Platform plumbing and audit
    public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();
    public DbSet<ReferenceCounter> ReferenceCounters => Set<ReferenceCounter>();
    public DbSet<OutboundSms> OutboundSms => Set<OutboundSms>();
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();

    protected override void ConfigureConventions(ModelConfigurationBuilder b)
    {
        b.Properties<decimal>().HavePrecision(18, 2);
        b.Properties<string>().HaveMaxLength(2000);
        foreach (var enumType in typeof(RahoonDbContext).Assembly.GetTypes().Where(t => t.IsEnum && t.Namespace?.StartsWith("Rahoon.Api.Modules") == true))
        {
            b.Properties(enumType).HaveConversion<string>().HaveMaxLength(48);
        }
    }

    protected override void OnModelCreating(ModelBuilder mb)
    {
        mb.ApplyConfigurationsFromAssembly(typeof(RahoonDbContext).Assembly);

        foreach (var et in mb.Model.GetEntityTypes().ToList())
        {
            var clr = et.ClrType;

            if (typeof(IConcurrencyVersioned).IsAssignableFrom(clr))
                mb.Entity(clr).Property(nameof(IConcurrencyVersioned.Version)).IsRowVersion();

            if (typeof(IApplicantOwned).IsAssignableFrom(clr))
                ApplyApplicantFilterMethod.MakeGenericMethod(clr).Invoke(this, [mb]);
            else if (typeof(IOrgOwned).IsAssignableFrom(clr))
                ApplyTenantFilterMethod.MakeGenericMethod(clr).Invoke(this, [mb]);

            // Referential integrity for the tenant key.
            if (clr != typeof(Organization) && et.FindProperty("OrganizationId") is { } orgProp && !HasForeignKeyOn(et, "OrganizationId"))
            {
                mb.Entity(clr).HasOne(typeof(Organization)).WithMany().HasForeignKey("OrganizationId").OnDelete(DeleteBehavior.Restrict)
                    .IsRequired(!orgProp.IsNullable);
            }
        }
    }

    private static bool HasForeignKeyOn(Microsoft.EntityFrameworkCore.Metadata.IMutableEntityType et, string property) =>
        et.GetForeignKeys().Any(fk => fk.Properties.Any(p => p.Name == property));

    private static readonly MethodInfo ApplyTenantFilterMethod =
        typeof(RahoonDbContext).GetMethod(nameof(ApplyTenantFilter), BindingFlags.NonPublic | BindingFlags.Instance)!;

    private void ApplyTenantFilter<T>(ModelBuilder mb) where T : class, IOrgOwned =>
        mb.Entity<T>().HasQueryFilter(e => _rc.SystemBypass || _rc.DataOrganizationIds.Contains(e.OrganizationId));

    private static readonly MethodInfo ApplyApplicantFilterMethod =
        typeof(RahoonDbContext).GetMethod(nameof(ApplyApplicantFilter), BindingFlags.NonPublic | BindingFlags.Instance)!;

    /// <summary>One combined filter (a second HasQueryFilter call would replace the first): tenant match, or the individual's own row.</summary>
    private void ApplyApplicantFilter<T>(ModelBuilder mb) where T : class, IApplicantOwned =>
        mb.Entity<T>().HasQueryFilter(e => _rc.SystemBypass || _rc.DataOrganizationIds.Contains(e.OrganizationId)
                                           || (_rc.Scope == SessionScope.Individual && e.ApplicantUserId == _rc.UserId));
}
