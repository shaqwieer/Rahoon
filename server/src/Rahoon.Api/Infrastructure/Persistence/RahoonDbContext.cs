using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Rahoon.Api.Infrastructure.Tenancy;
using Rahoon.Api.Modules.Administration;
using Rahoon.Api.Modules.Agreements;
using Rahoon.Api.Modules.Assessment;
using Rahoon.Api.Modules.Audit;
using Rahoon.Api.Modules.Cases;
using Rahoon.Api.Modules.Closure;
using Rahoon.Api.Modules.Communications;
using Rahoon.Api.Modules.Complaints;
using Rahoon.Api.Modules.Documents;
using Rahoon.Api.Modules.Identity;
using Rahoon.Api.Modules.Imports;
using Rahoon.Api.Modules.Providers;
using Rahoon.Api.Modules.Referral;
using Rahoon.Api.Modules.Solutions;

namespace Rahoon.Api.Infrastructure.Persistence;

public sealed class RahoonDbContext(DbContextOptions<RahoonDbContext> options, RequestContext requestContext) : DbContext(options)
{
    private readonly RequestContext _rc = requestContext;

    public RequestContext Request => _rc;

    // Identity & tenants
    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Membership> Memberships => Set<Membership>();
    public DbSet<Team> Teams => Set<Team>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<MembershipRole> MembershipRoles => Set<MembershipRole>();
    public DbSet<Session> Sessions => Set<Session>();
    public DbSet<OtpChallenge> OtpChallenges => Set<OtpChallenge>();
    public DbSet<Invitation> Invitations => Set<Invitation>();
    public DbSet<RoleChangeRequest> RoleChangeRequests => Set<RoleChangeRequest>();

    // Cases
    public DbSet<Case> Cases => Set<Case>();
    public DbSet<CaseParty> Parties => Set<CaseParty>();
    public DbSet<OwnerAccess> OwnerAccesses => Set<OwnerAccess>();
    public DbSet<FinancingContract> FinancingContracts => Set<FinancingContract>();
    public DbSet<DebtSnapshot> DebtSnapshots => Set<DebtSnapshot>();
    public DbSet<InstallmentHistoryEntry> InstallmentHistory => Set<InstallmentHistoryEntry>();
    public DbSet<Property> Properties => Set<Property>();
    public DbSet<Mortgage> Mortgages => Set<Mortgage>();
    public DbSet<PiiRevealLog> PiiRevealLogs => Set<PiiRevealLog>();
    public DbSet<SavedView> SavedViews => Set<SavedView>();
    public DbSet<ReferenceCounter> ReferenceCounters => Set<ReferenceCounter>();

    // Documents
    public DbSet<DocumentType> DocumentTypes => Set<DocumentType>();
    public DbSet<DocumentRule> DocumentRules => Set<DocumentRule>();
    public DbSet<CaseDocument> Documents => Set<CaseDocument>();
    public DbSet<DocumentVersion> DocumentVersions => Set<DocumentVersion>();
    public DbSet<DocumentRequest> DocumentRequests => Set<DocumentRequest>();
    public DbSet<DownloadLog> DownloadLogs => Set<DownloadLog>();

    // Assessment
    public DbSet<ValuationReport> ValuationReports => Set<ValuationReport>();
    public DbSet<AffordabilityAnalysis> Analyses => Set<AffordabilityAnalysis>();

    // Solutions & approvals
    public DbSet<SolutionVersion> Solutions => Set<SolutionVersion>();
    public DbSet<ApprovalRequest> ApprovalRequests => Set<ApprovalRequest>();
    public DbSet<ApprovalLimitPolicy> ApprovalLimitPolicies => Set<ApprovalLimitPolicy>();
    public DbSet<ApprovalLimitTier> ApprovalLimitTiers => Set<ApprovalLimitTier>();
    public DbSet<ComplianceNotice> ComplianceNotices => Set<ComplianceNotice>();
    public DbSet<Offer> Offers => Set<Offer>();
    public DbSet<NegotiationEntry> NegotiationEntries => Set<NegotiationEntry>();
    public DbSet<ConsentRecord> ConsentRecords => Set<ConsentRecord>();

    // Agreements & payments
    public DbSet<Agreement> Agreements => Set<Agreement>();
    public DbSet<Installment> Installments => Set<Installment>();
    public DbSet<PaymentRecord> Payments => Set<PaymentRecord>();
    public DbSet<BreachReview> BreachReviews => Set<BreachReview>();

    // Communications & complaints
    public DbSet<CaseMessage> Messages => Set<CaseMessage>();
    public DbSet<CaseTask> Tasks => Set<CaseTask>();
    public DbSet<Appointment> Appointments => Set<Appointment>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<CommunicationTemplate> Templates => Set<CommunicationTemplate>();
    public DbSet<OutboundMessage> OutboundMessages => Set<OutboundMessage>();
    public DbSet<Complaint> Complaints => Set<Complaint>();

    // Referral & closure
    public DbSet<JudicialReferral> Referrals => Set<JudicialReferral>();
    public DbSet<ExternalStatusEntry> ExternalStatusEntries => Set<ExternalStatusEntry>();
    public DbSet<Reconciliation> Reconciliations => Set<Reconciliation>();
    public DbSet<ReconciliationLine> ReconciliationLines => Set<ReconciliationLine>();
    public DbSet<ClosureDocument> ClosureDocuments => Set<ClosureDocument>();

    // Providers, imports, administration, audit
    public DbSet<ProviderAssignment> Assignments => Set<ProviderAssignment>();
    public DbSet<AssignmentMessage> AssignmentMessages => Set<AssignmentMessage>();
    public DbSet<AssignmentSubmission> AssignmentSubmissions => Set<AssignmentSubmission>();
    public DbSet<ImportBatch> ImportBatches => Set<ImportBatch>();
    public DbSet<ImportRow> ImportRows => Set<ImportRow>();
    public DbSet<SlaRule> SlaRules => Set<SlaRule>();
    public DbSet<IntegrationSetting> IntegrationSettings => Set<IntegrationSetting>();
    public DbSet<TempAccessRequest> TempAccessRequests => Set<TempAccessRequest>();
    public DbSet<InstitutionApplication> InstitutionApplications => Set<InstitutionApplication>();
    public DbSet<RetentionPolicy> RetentionPolicies => Set<RetentionPolicy>();
    public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();
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

            if (typeof(IOrgOwned).IsAssignableFrom(clr))
                ApplyTenantFilterMethod.MakeGenericMethod(clr).Invoke(this, [mb]);

            // Referential integrity for the two ubiquitous foreign keys.
            if (clr != typeof(Case) && et.FindProperty("CaseId") is { } caseProp && !HasForeignKeyOn(et, "CaseId"))
            {
                mb.Entity(clr).HasOne(typeof(Case)).WithMany().HasForeignKey("CaseId").OnDelete(DeleteBehavior.Restrict)
                    .IsRequired(!caseProp.IsNullable);
                mb.Entity(clr).HasIndex("CaseId");
            }
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
}
