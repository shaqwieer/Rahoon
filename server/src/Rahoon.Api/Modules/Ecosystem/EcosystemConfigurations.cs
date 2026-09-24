using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Rahoon.Api.Modules.Identity;
using Rahoon.Api.Modules.Providers;

namespace Rahoon.Api.Modules.Ecosystem;

// Service ecosystem, configuration and billing (B9) keep their tables in the `ecosystem` schema.

internal sealed class ProviderProfileConfig : IEntityTypeConfiguration<ProviderProfile>
{
    public void Configure(EntityTypeBuilder<ProviderProfile> b)
    {
        b.ToTable("provider_profiles", "ecosystem", t => t.HasCheckConstraint("ck_provider_step", "current_step BETWEEN 1 AND 6"));
        b.HasOne<Organization>().WithMany().HasForeignKey(x => x.ProviderOrganizationId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => x.ProviderOrganizationId).IsUnique();
        b.HasIndex(x => x.ApplicationRef).IsUnique();
        b.Property(x => x.StepDataJson).HasColumnType("jsonb");
        b.Property(x => x.LegalName).HasMaxLength(200);
    }
}

internal sealed class ProviderLicenseConfig : IEntityTypeConfiguration<ProviderLicense>
{
    public void Configure(EntityTypeBuilder<ProviderLicense> b)
    {
        b.ToTable("provider_licenses", "ecosystem");
        b.HasOne<ProviderProfile>().WithMany().HasForeignKey(x => x.ProviderProfileId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.ProviderProfileId, x.Kind }).IsUnique().HasFilter("is_current");
        b.Property(x => x.Kind).HasMaxLength(48);
    }
}

internal sealed class ProviderReviewDecisionConfig : IEntityTypeConfiguration<ProviderReviewDecision>
{
    public void Configure(EntityTypeBuilder<ProviderReviewDecision> b)
    {
        b.ToTable("provider_review_decisions", "ecosystem");
        b.HasOne<ProviderProfile>().WithMany().HasForeignKey(x => x.ProviderProfileId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class InstitutionProviderConfig : IEntityTypeConfiguration<InstitutionProvider>
{
    public void Configure(EntityTypeBuilder<InstitutionProvider> b)
    {
        b.ToTable("institution_providers", "ecosystem", t =>
            t.HasCheckConstraint("ck_institution_provider_rate", "commission_rate IS NULL OR (commission_rate >= 0 AND commission_rate < 0.2)"));
        b.HasOne<Organization>().WithMany().HasForeignKey(x => x.ProviderOrganizationId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.OrganizationId, x.ProviderOrganizationId }).IsUnique();
        b.Property(x => x.CommissionRate).HasPrecision(9, 4);
    }
}

internal sealed class ProviderInvoiceConfig : IEntityTypeConfiguration<ProviderInvoice>
{
    public void Configure(EntityTypeBuilder<ProviderInvoice> b)
    {
        b.ToTable("provider_invoices", "ecosystem", t =>
        {
            t.HasCheckConstraint("ck_provider_invoice_amount", "amount > 0");
            // Maker-checker: the reviewer is never the submitter.
            t.HasCheckConstraint("ck_provider_invoice_checker", "decided_by_user_id IS NULL OR submitted_by_user_id IS NULL OR decided_by_user_id <> submitted_by_user_id");
        });
        b.HasIndex(x => x.Number).IsUnique();
        // One live invoice per assignment (a rejected one may be re-issued).
        b.HasIndex(x => x.AssignmentId).IsUnique().HasFilter("status <> 'Rejected'");
        b.HasOne<ProviderAssignment>().WithMany().HasForeignKey(x => x.AssignmentId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Organization>().WithMany().HasForeignKey(x => x.LenderOrganizationId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Organization>().WithMany().HasForeignKey(x => x.ProviderOrganizationId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.ProviderOrganizationId, x.Status });
        b.HasIndex(x => new { x.LenderOrganizationId, x.Status });
    }
}

internal sealed class WorkflowVersionConfig : IEntityTypeConfiguration<WorkflowVersion>
{
    public void Configure(EntityTypeBuilder<WorkflowVersion> b)
    {
        b.ToTable("workflow_versions", "ecosystem", t =>
            // Publishing needs a second authorised user.
            t.HasCheckConstraint("ck_workflow_second_approver", "approved_by_user_id IS NULL OR submitted_by_user_id IS NULL OR approved_by_user_id <> submitted_by_user_id"));
        b.HasOne<Organization>().WithMany().HasForeignKey(x => x.InstitutionOrganizationId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.InstitutionOrganizationId, x.VersionNo }).IsUnique();
        b.HasIndex(x => x.InstitutionOrganizationId).IsUnique().HasFilter("status = 'Active'").HasDatabaseName("ux_workflow_one_active");
        b.HasIndex(x => x.InstitutionOrganizationId).IsUnique().HasFilter("status IN ('Draft','PendingApproval')").HasDatabaseName("ux_workflow_one_open_draft");
        b.Property(x => x.StagesJson).HasColumnType("jsonb");
    }
}

internal sealed class ReportScheduleConfig : IEntityTypeConfiguration<ReportSchedule>
{
    public void Configure(EntityTypeBuilder<ReportSchedule> b) => b.ToTable("report_schedules", "ecosystem");
}

internal sealed class BillingPlanConfig : IEntityTypeConfiguration<BillingPlan>
{
    public void Configure(EntityTypeBuilder<BillingPlan> b)
    {
        b.ToTable("billing_plans", "ecosystem", t => t.HasCheckConstraint("ck_plan_price", "monthly_price IS NULL OR monthly_price >= 0"));
        b.HasIndex(x => x.Key).IsUnique();
    }
}

internal sealed class InstitutionSubscriptionConfig : IEntityTypeConfiguration<InstitutionSubscription>
{
    public void Configure(EntityTypeBuilder<InstitutionSubscription> b)
    {
        b.ToTable("institution_subscriptions", "ecosystem");
        b.HasOne<Organization>().WithMany().HasForeignKey(x => x.InstitutionOrganizationId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => x.InstitutionOrganizationId).IsUnique();
    }
}

internal sealed class PlatformInvoiceConfig : IEntityTypeConfiguration<PlatformInvoice>
{
    public void Configure(EntityTypeBuilder<PlatformInvoice> b)
    {
        b.ToTable("platform_invoices", "ecosystem", t => t.HasCheckConstraint("ck_platform_invoice_amount", "amount >= 0"));
        b.HasOne<Organization>().WithMany().HasForeignKey(x => x.InstitutionOrganizationId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => x.Number).IsUnique();
    }
}

internal sealed class InstitutionIntegrationConfig : IEntityTypeConfiguration<InstitutionIntegration>
{
    public void Configure(EntityTypeBuilder<InstitutionIntegration> b)
    {
        b.ToTable("institution_integrations", "ecosystem", t =>
            t.HasCheckConstraint("ck_institution_integration_mode", "mode IN ('enabled','simulated','disabled')"));
        b.HasOne<Organization>().WithMany().HasForeignKey(x => x.InstitutionOrganizationId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.InstitutionOrganizationId, x.Capability }).IsUnique();
    }
}

internal sealed class IntegrationAttemptConfig : IEntityTypeConfiguration<IntegrationAttempt>
{
    public void Configure(EntityTypeBuilder<IntegrationAttempt> b)
    {
        b.ToTable("integration_attempts", "ecosystem");
        b.HasOne<Organization>().WithMany().HasForeignKey(x => x.InstitutionOrganizationId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.InstitutionOrganizationId, x.Capability, x.At });
    }
}
