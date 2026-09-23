using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
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
    }
}

internal sealed class MembershipConfig : IEntityTypeConfiguration<Membership>
{
    public void Configure(EntityTypeBuilder<Membership> b)
    {
        b.ToTable("memberships", "identity");
        b.HasIndex(x => new { x.UserId, x.OrganizationId }).IsUnique();
        b.HasOne(x => x.Organization).WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Team).WithMany().HasForeignKey(x => x.TeamId).OnDelete(DeleteBehavior.SetNull);
        b.HasMany(x => x.Roles).WithOne().HasForeignKey(x => x.MembershipId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class TeamConfig : IEntityTypeConfiguration<Team>
{
    public void Configure(EntityTypeBuilder<Team> b) => b.ToTable("teams", "identity");
}

internal sealed class RoleConfig : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> b)
    {
        b.ToTable("roles", "identity");
        b.HasIndex(x => new { x.OrganizationId, x.Key }).IsUnique();
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

internal sealed class InvitationConfig : IEntityTypeConfiguration<Invitation>
{
    public void Configure(EntityTypeBuilder<Invitation> b)
    {
        b.ToTable("invitations", "identity");
        b.HasIndex(x => x.TokenHash).IsUnique();
    }
}

internal sealed class RoleChangeConfig : IEntityTypeConfiguration<RoleChangeRequest>
{
    public void Configure(EntityTypeBuilder<RoleChangeRequest> b) => b.ToTable("role_change_requests", "identity");
}

// ── Cases ──

internal sealed class CaseConfig : IEntityTypeConfiguration<Case>
{
    public void Configure(EntityTypeBuilder<Case> b)
    {
        b.ToTable("cases", "cases", t =>
        {
            t.HasCheckConstraint("ck_cases_outstanding_nonneg", "outstanding_amount IS NULL OR outstanding_amount >= 0");
            t.HasCheckConstraint("ck_cases_draft_step", "draft_step BETWEEN 1 AND 6");
        });
        b.Property(x => x.Reference).HasMaxLength(20);
        b.HasIndex(x => x.Reference).IsUnique();
        b.HasIndex(x => new { x.OrganizationId, x.Status });
        b.HasIndex(x => new { x.OrganizationId, x.AssignedManagerId });
        b.HasIndex(x => new { x.OrganizationId, x.StageDueOn });
        b.HasMany(x => x.Parties).WithOne().HasForeignKey(p => p.CaseId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Property).WithOne().HasForeignKey<Property>(p => p.CaseId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Mortgage).WithOne().HasForeignKey<Mortgage>(p => p.CaseId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Financing).WithOne().HasForeignKey<FinancingContract>(p => p.CaseId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class PartyConfig : IEntityTypeConfiguration<CaseParty>
{
    public void Configure(EntityTypeBuilder<CaseParty> b)
    {
        b.ToTable("parties", "cases");
        b.HasIndex(x => new { x.OrganizationId, x.NationalIdHash });
    }
}

internal sealed class OwnerAccessConfig : IEntityTypeConfiguration<OwnerAccess>
{
    public void Configure(EntityTypeBuilder<OwnerAccess> b)
    {
        b.ToTable("owner_accesses", "cases");
        b.HasIndex(x => x.InvitationTokenHash).IsUnique();
        b.HasIndex(x => x.UserId);
        b.HasIndex(x => new { x.CaseId, x.PartyId }).IsUnique();
    }
}

internal sealed class FinancingConfig : IEntityTypeConfiguration<FinancingContract>
{
    public void Configure(EntityTypeBuilder<FinancingContract> b)
    {
        b.ToTable("financing_contracts", "cases");
        b.Property(x => x.ContractNumber).HasMaxLength(40);
        b.HasIndex(x => new { x.OrganizationId, x.ContractNumber });
    }
}

internal sealed class DebtSnapshotConfig : IEntityTypeConfiguration<DebtSnapshot>
{
    public void Configure(EntityTypeBuilder<DebtSnapshot> b)
    {
        b.ToTable("debt_snapshots", "cases", t =>
            t.HasCheckConstraint("ck_debt_total", "total = principal + profit + late_fees + other_fees"));
    }
}

internal sealed class InstallmentHistoryConfig : IEntityTypeConfiguration<InstallmentHistoryEntry>
{
    public void Configure(EntityTypeBuilder<InstallmentHistoryEntry> b)
    {
        b.ToTable("installment_history", "cases");
        b.HasIndex(x => new { x.CaseId, x.Month }).IsUnique();
    }
}

internal sealed class PropertyConfig : IEntityTypeConfiguration<Property>
{
    public void Configure(EntityTypeBuilder<Property> b) => b.ToTable("properties", "cases");
}

internal sealed class MortgageConfig : IEntityTypeConfiguration<Mortgage>
{
    public void Configure(EntityTypeBuilder<Mortgage> b) => b.ToTable("mortgages", "cases");
}

internal sealed class PiiRevealConfig : IEntityTypeConfiguration<PiiRevealLog>
{
    public void Configure(EntityTypeBuilder<PiiRevealLog> b) => b.ToTable("pii_reveal_logs", "cases");
}

internal sealed class SavedViewConfig : IEntityTypeConfiguration<SavedView>
{
    public void Configure(EntityTypeBuilder<SavedView> b) => b.ToTable("saved_views", "cases");
}

internal sealed class ReferenceCounterConfig : IEntityTypeConfiguration<ReferenceCounter>
{
    public void Configure(EntityTypeBuilder<ReferenceCounter> b)
    {
        b.ToTable("reference_counters", "cases");
        b.HasKey(x => x.Key);
        b.Property(x => x.Key).HasMaxLength(40);
    }
}

// ── Documents ──

internal sealed class DocumentTypeConfig : IEntityTypeConfiguration<DocumentType>
{
    public void Configure(EntityTypeBuilder<DocumentType> b)
    {
        b.ToTable("document_types", "documents");
        b.HasIndex(x => x.Key).IsUnique();
    }
}

internal sealed class DocumentRuleConfig : IEntityTypeConfiguration<DocumentRule>
{
    public void Configure(EntityTypeBuilder<DocumentRule> b)
    {
        b.ToTable("document_rules", "documents");
        b.HasIndex(x => new { x.OrganizationId, x.DocumentTypeKey }).IsUnique();
    }
}

internal sealed class CaseDocumentConfig : IEntityTypeConfiguration<CaseDocument>
{
    public void Configure(EntityTypeBuilder<CaseDocument> b)
    {
        b.ToTable("case_documents", "documents");
        b.HasMany(x => x.Versions).WithOne().HasForeignKey(v => v.DocumentId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class DocumentVersionConfig : IEntityTypeConfiguration<DocumentVersion>
{
    public void Configure(EntityTypeBuilder<DocumentVersion> b)
    {
        b.ToTable("document_versions", "documents", t => t.HasCheckConstraint("ck_docver_size", "size_bytes > 0 AND size_bytes <= 20971520"));
        b.HasIndex(x => new { x.DocumentId, x.VersionNo }).IsUnique();
    }
}

internal sealed class DocumentRequestConfig : IEntityTypeConfiguration<DocumentRequest>
{
    public void Configure(EntityTypeBuilder<DocumentRequest> b) => b.ToTable("document_requests", "documents");
}

internal sealed class DownloadLogConfig : IEntityTypeConfiguration<DownloadLog>
{
    public void Configure(EntityTypeBuilder<DownloadLog> b) => b.ToTable("download_logs", "documents");
}

// ── Assessment ──

internal sealed class ValuationConfig : IEntityTypeConfiguration<ValuationReport>
{
    public void Configure(EntityTypeBuilder<ValuationReport> b)
    {
        b.ToTable("valuation_reports", "assessment", t => t.HasCheckConstraint("ck_valuation_positive", "market_value > 0"));
    }
}

internal sealed class AnalysisConfig : IEntityTypeConfiguration<AffordabilityAnalysis>
{
    public void Configure(EntityTypeBuilder<AffordabilityAnalysis> b)
    {
        b.ToTable("affordability_analyses", "assessment");
        b.HasIndex(x => x.CaseId).IsUnique();
        b.Property(x => x.DsrLimit).HasPrecision(6, 4);
    }
}

// ── Solutions ──

internal sealed class SolutionConfig : IEntityTypeConfiguration<SolutionVersion>
{
    public void Configure(EntityTypeBuilder<SolutionVersion> b)
    {
        b.ToTable("solution_versions", "solutions", t =>
        {
            t.HasCheckConstraint("ck_solution_term", "term_months BETWEEN 1 AND 360");
            t.HasCheckConstraint("ck_solution_waiver", "waiver_amount >= 0 AND down_payment >= 0");
        });
        b.HasIndex(x => new { x.CaseId, x.VersionNo }).IsUnique();
        b.Property(x => x.WaiverPercent).HasPrecision(9, 4);
        b.Property(x => x.Dsr).HasPrecision(9, 4);
        b.Property(x => x.DsrLimit).HasPrecision(6, 4);
        b.Property(x => x.LockedSnapshotJson).HasColumnType("jsonb");
        b.Property(x => x.Justification).HasMaxLength(4000);
    }
}

internal sealed class ApprovalRequestConfig : IEntityTypeConfiguration<ApprovalRequest>
{
    public void Configure(EntityTypeBuilder<ApprovalRequest> b)
    {
        b.ToTable("approval_requests", "solutions");
        b.HasIndex(x => new { x.OrganizationId, x.Status, x.AssignedApproverUserId });
        b.Property(x => x.WaiverPercent).HasPrecision(9, 4);
        // At most one pending approval per subject version — protects against double submission.
        b.HasIndex(x => new { x.Subject, x.SubjectId }).IsUnique().HasFilter("status = 'Pending'");
    }
}

internal sealed class ApprovalLimitPolicyConfig : IEntityTypeConfiguration<ApprovalLimitPolicy>
{
    public void Configure(EntityTypeBuilder<ApprovalLimitPolicy> b)
    {
        b.ToTable("approval_limit_policies", "solutions");
        b.HasIndex(x => new { x.OrganizationId, x.VersionNo }).IsUnique();
        b.HasMany(x => x.Tiers).WithOne().HasForeignKey(t => t.PolicyId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class ApprovalLimitTierConfig : IEntityTypeConfiguration<ApprovalLimitTier>
{
    public void Configure(EntityTypeBuilder<ApprovalLimitTier> b)
    {
        b.ToTable("approval_limit_tiers", "solutions");
        b.Property(x => x.MaxWaiverPercent).HasPrecision(9, 4);
    }
}

internal sealed class ComplianceNoticeConfig : IEntityTypeConfiguration<ComplianceNotice>
{
    public void Configure(EntityTypeBuilder<ComplianceNotice> b) => b.ToTable("compliance_notices", "solutions");
}

internal sealed class OfferConfig : IEntityTypeConfiguration<Offer>
{
    public void Configure(EntityTypeBuilder<Offer> b)
    {
        b.ToTable("offers", "solutions");
        b.HasIndex(x => x.SolutionVersionId).IsUnique();
    }
}

internal sealed class NegotiationConfig : IEntityTypeConfiguration<NegotiationEntry>
{
    public void Configure(EntityTypeBuilder<NegotiationEntry> b) => b.ToTable("negotiation_entries", "solutions");
}

internal sealed class ConsentConfig : IEntityTypeConfiguration<ConsentRecord>
{
    public void Configure(EntityTypeBuilder<ConsentRecord> b) => b.ToTable("consent_records", "solutions");
}

// ── Agreements & payments ──

internal sealed class AgreementConfig : IEntityTypeConfiguration<Agreement>
{
    public void Configure(EntityTypeBuilder<Agreement> b)
    {
        b.ToTable("agreements", "agreements");
        b.HasIndex(x => x.Number).IsUnique();
    }
}

internal sealed class InstallmentConfig : IEntityTypeConfiguration<Installment>
{
    public void Configure(EntityTypeBuilder<Installment> b)
    {
        b.ToTable("installments", "agreements", t => t.HasCheckConstraint("ck_installment_amount", "amount > 0 AND paid_amount >= 0"));
        b.HasIndex(x => new { x.AgreementId, x.No }).IsUnique();
    }
}

internal sealed class PaymentConfig : IEntityTypeConfiguration<PaymentRecord>
{
    public void Configure(EntityTypeBuilder<PaymentRecord> b)
    {
        b.ToTable("payment_records", "agreements", t =>
        {
            t.HasCheckConstraint("ck_payment_amount", "amount > 0");
            t.HasCheckConstraint("ck_payment_checker", "matched_by_user_id IS NULL OR matched_by_user_id <> recorded_by_user_id");
        });
        b.HasIndex(x => new { x.OrganizationId, x.BankReference }).IsUnique();
    }
}

internal sealed class BreachConfig : IEntityTypeConfiguration<BreachReview>
{
    public void Configure(EntityTypeBuilder<BreachReview> b) => b.ToTable("breach_reviews", "agreements");
}

// ── Communications & complaints ──

internal sealed class MessageConfig : IEntityTypeConfiguration<CaseMessage>
{
    public void Configure(EntityTypeBuilder<CaseMessage> b)
    {
        b.ToTable("case_messages", "comms");
        b.Property(x => x.Body).HasMaxLength(4000);
    }
}

internal sealed class TaskConfig : IEntityTypeConfiguration<CaseTask>
{
    public void Configure(EntityTypeBuilder<CaseTask> b)
    {
        b.ToTable("tasks", "comms");
        b.HasIndex(x => new { x.OrganizationId, x.AssigneeUserId, x.Status });
    }
}

internal sealed class AppointmentConfig : IEntityTypeConfiguration<Appointment>
{
    public void Configure(EntityTypeBuilder<Appointment> b) => b.ToTable("appointments", "comms");
}

internal sealed class NotificationConfig : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> b)
    {
        b.ToTable("notifications", "comms");
        b.HasIndex(x => new { x.UserId, x.ReadAt });
    }
}

internal sealed class TemplateConfig : IEntityTypeConfiguration<CommunicationTemplate>
{
    public void Configure(EntityTypeBuilder<CommunicationTemplate> b)
    {
        b.ToTable("templates", "comms");
        b.HasIndex(x => new { x.OrganizationId, x.Code, x.VersionNo }).IsUnique();
    }
}

internal sealed class OutboundConfig : IEntityTypeConfiguration<OutboundMessage>
{
    public void Configure(EntityTypeBuilder<OutboundMessage> b) => b.ToTable("outbound_messages", "comms");
}

internal sealed class ComplaintConfig : IEntityTypeConfiguration<Complaint>
{
    public void Configure(EntityTypeBuilder<Complaint> b)
    {
        b.ToTable("complaints", "complaints");
        b.HasIndex(x => x.Reference).IsUnique();
        b.Ignore(x => x.IsOpen);
        b.Property(x => x.Body).HasMaxLength(4000);
        b.Property(x => x.ResponseText).HasMaxLength(4000);
        b.Property(x => x.ResponseDraft).HasMaxLength(4000);
    }
}

// ── Referral & closure ──

internal sealed class ReferralConfig : IEntityTypeConfiguration<JudicialReferral>
{
    public void Configure(EntityTypeBuilder<JudicialReferral> b)
    {
        b.ToTable("judicial_referrals", "referral");
        b.HasIndex(x => x.CaseId).IsUnique().HasFilter("status NOT IN ('Rejected','Withdrawn')");
    }
}

internal sealed class ExternalStatusConfig : IEntityTypeConfiguration<ExternalStatusEntry>
{
    public void Configure(EntityTypeBuilder<ExternalStatusEntry> b) => b.ToTable("external_status_entries", "referral");
}

internal sealed class ReconciliationConfig : IEntityTypeConfiguration<Reconciliation>
{
    public void Configure(EntityTypeBuilder<Reconciliation> b)
    {
        b.ToTable("reconciliations", "closure", t =>
            t.HasCheckConstraint("ck_recon_diff", "difference = expected_amount - received_amount - waived_amount"));
        b.HasMany(x => x.Lines).WithOne().HasForeignKey(l => l.ReconciliationId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class ReconciliationLineConfig : IEntityTypeConfiguration<ReconciliationLine>
{
    public void Configure(EntityTypeBuilder<ReconciliationLine> b) => b.ToTable("reconciliation_lines", "closure");
}

internal sealed class ClosureDocumentConfig : IEntityTypeConfiguration<ClosureDocument>
{
    public void Configure(EntityTypeBuilder<ClosureDocument> b) => b.ToTable("closure_documents", "closure");
}

// ── Providers & imports ──

internal sealed class AssignmentConfig : IEntityTypeConfiguration<ProviderAssignment>
{
    public void Configure(EntityTypeBuilder<ProviderAssignment> b)
    {
        b.ToTable("assignments", "providers");
        b.HasIndex(x => x.Reference).IsUnique();
        b.HasIndex(x => new { x.ProviderOrganizationId, x.Status });
        b.HasOne<Organization>().WithMany().HasForeignKey(x => x.ProviderOrganizationId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class AssignmentMessageConfig : IEntityTypeConfiguration<AssignmentMessage>
{
    public void Configure(EntityTypeBuilder<AssignmentMessage> b) => b.ToTable("assignment_messages", "providers");
}

internal sealed class AssignmentSubmissionConfig : IEntityTypeConfiguration<AssignmentSubmission>
{
    public void Configure(EntityTypeBuilder<AssignmentSubmission> b)
    {
        b.ToTable("assignment_submissions", "providers");
        b.HasIndex(x => new { x.AssignmentId, x.VersionNo }).IsUnique();
    }
}

internal sealed class ImportBatchConfig : IEntityTypeConfiguration<ImportBatch>
{
    public void Configure(EntityTypeBuilder<ImportBatch> b)
    {
        b.ToTable("import_batches", "cases");
        b.HasMany(x => x.Rows).WithOne().HasForeignKey(r => r.BatchId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class ImportRowConfig : IEntityTypeConfiguration<ImportRow>
{
    public void Configure(EntityTypeBuilder<ImportRow> b)
    {
        b.ToTable("import_rows", "cases");
        b.Property(x => x.RawJson).HasColumnType("jsonb");
    }
}

// ── Administration & audit ──

internal sealed class SlaRuleConfig : IEntityTypeConfiguration<SlaRule>
{
    public void Configure(EntityTypeBuilder<SlaRule> b)
    {
        b.ToTable("sla_rules", "admin");
        b.HasIndex(x => new { x.OrganizationId, x.Status }).IsUnique();
    }
}

internal sealed class IntegrationSettingConfig : IEntityTypeConfiguration<IntegrationSetting>
{
    public void Configure(EntityTypeBuilder<IntegrationSetting> b)
    {
        b.ToTable("integration_settings", "admin");
        b.HasIndex(x => x.Key).IsUnique();
    }
}

internal sealed class TempAccessConfig : IEntityTypeConfiguration<TempAccessRequest>
{
    public void Configure(EntityTypeBuilder<TempAccessRequest> b) => b.ToTable("temp_access_requests", "admin");
}

internal sealed class ApplicationConfig : IEntityTypeConfiguration<InstitutionApplication>
{
    public void Configure(EntityTypeBuilder<InstitutionApplication> b)
    {
        b.ToTable("institution_applications", "admin");
        b.HasIndex(x => x.Reference).IsUnique();
    }
}

internal sealed class RetentionConfig : IEntityTypeConfiguration<RetentionPolicy>
{
    public void Configure(EntityTypeBuilder<RetentionPolicy> b) => b.ToTable("retention_policies", "admin");
}

internal sealed class IdempotencyConfig : IEntityTypeConfiguration<IdempotencyRecord>
{
    public void Configure(EntityTypeBuilder<IdempotencyRecord> b)
    {
        b.ToTable("idempotency_records", "admin");
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
        b.HasIndex(x => new { x.CaseId, x.Seq });
        b.Property(x => x.DataJson).HasColumnType("jsonb");
        b.Property(x => x.Hash).HasMaxLength(80);
        b.Property(x => x.PrevHash).HasMaxLength(80);
    }
}
