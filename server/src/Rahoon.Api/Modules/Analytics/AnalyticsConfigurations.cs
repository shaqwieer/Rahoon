using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Rahoon.Api.Modules.Analytics;

internal sealed class OperationalSettingConfig : IEntityTypeConfiguration<OperationalSetting>
{
    public void Configure(EntityTypeBuilder<OperationalSetting> b)
    {
        // Seeded platform defaults are decided by the system (decided_by_user_id NULL); every later change needs a second person.
        b.ToTable("operational_settings", "analytics", t =>
            t.HasCheckConstraint("ck_setting_second_person", "decided_by_user_id IS NULL OR decided_by_user_id <> proposed_by_user_id"));
        b.Property(x => x.ValueJson).HasColumnType("jsonb");
        b.HasIndex(x => new { x.OrganizationId, x.Key, x.VersionNo }).IsUnique();
        b.HasIndex(x => new { x.OrganizationId, x.Key }, "ix_operational_settings_one_effective").IsUnique().HasFilter("status = 'Effective'");
        b.HasIndex(x => new { x.OrganizationId, x.Key }, "ix_operational_settings_one_pending").IsUnique().HasFilter("status = 'PendingApproval'");
    }
}

internal sealed class InsightFeedbackConfig : IEntityTypeConfiguration<InsightFeedback>
{
    public void Configure(EntityTypeBuilder<InsightFeedback> b) => b.ToTable("insight_feedback", "analytics");
}

internal sealed class DocumentDraftConfig : IEntityTypeConfiguration<DocumentDraft>
{
    public void Configure(EntityTypeBuilder<DocumentDraft> b)
    {
        b.ToTable("document_drafts", "analytics");
        b.Property(x => x.GeneratedBody).HasColumnType("text");
        b.Property(x => x.CurrentBody).HasColumnType("text");
        b.Property(x => x.Limitations).HasColumnType("text");
        b.Property(x => x.FilledValuesJson).HasColumnType("jsonb");
        b.Property(x => x.FlagsJson).HasColumnType("jsonb");
        b.Property(x => x.InputsJson).HasColumnType("jsonb");
    }
}

internal sealed class DocumentDraftVersionConfig : IEntityTypeConfiguration<DocumentDraftVersion>
{
    public void Configure(EntityTypeBuilder<DocumentDraftVersion> b)
    {
        b.ToTable("document_draft_versions", "analytics");
        b.Property(x => x.Body).HasColumnType("text");
        b.HasIndex(x => new { x.DraftId, x.VersionNo }).IsUnique();
    }
}

internal sealed class PredictionConfig : IEntityTypeConfiguration<Prediction>
{
    public void Configure(EntityTypeBuilder<Prediction> b)
    {
        b.ToTable("predictions", "analytics");
        b.Property(x => x.FactorsJson).HasColumnType("jsonb");
        b.Property(x => x.InputsJson).HasColumnType("jsonb");
        b.Property(x => x.Limitations).HasColumnType("text");
        b.Property(x => x.ScoreLow).HasPrecision(5, 2);
        b.Property(x => x.ScorePoint).HasPrecision(5, 2);
        b.Property(x => x.ScoreHigh).HasPrecision(5, 2);
    }
}

internal sealed class PredictionOpinionConfig : IEntityTypeConfiguration<PredictionOpinion>
{
    public void Configure(EntityTypeBuilder<PredictionOpinion> b)
    {
        b.ToTable("prediction_opinions", "analytics", t =>
            t.HasCheckConstraint("ck_opinion_override_reason", "value <> 'override' OR (reason IS NOT NULL AND length(btrim(reason)) > 0)"));
        b.Property(x => x.Reason).HasColumnType("text");
    }
}
