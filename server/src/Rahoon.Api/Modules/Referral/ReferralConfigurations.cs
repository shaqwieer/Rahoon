using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Rahoon.Api.Modules.Referral;

// B10 referral entities (J01–J07). Existing JudicialReferral / ExternalStatusEntry keep their
// configuration in Infrastructure/Persistence/Configurations.cs.

internal sealed class ReferralChecklistEvidenceConfig : IEntityTypeConfiguration<ReferralChecklistEvidence>
{
    public void Configure(EntityTypeBuilder<ReferralChecklistEvidence> b) => b.ToTable("checklist_evidence", "referral");
}

internal sealed class EvidencePackConfig : IEntityTypeConfiguration<EvidencePack>
{
    public void Configure(EntityTypeBuilder<EvidencePack> b)
    {
        b.ToTable("evidence_packs", "referral");
        b.HasIndex(x => new { x.CaseId, x.SeqNo }).IsUnique();
        b.HasIndex(x => x.Reference);
        b.Property(x => x.MappingsJson).HasColumnType("jsonb");
        b.HasMany(x => x.Items).WithOne().HasForeignKey(i => i.PackId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class EvidencePackItemConfig : IEntityTypeConfiguration<EvidencePackItem>
{
    public void Configure(EntityTypeBuilder<EvidencePackItem> b)
    {
        b.ToTable("evidence_pack_items", "referral");
        b.HasIndex(x => new { x.PackId, x.Seq }).IsUnique();
    }
}

internal sealed class ReferralExceptionConfig : IEntityTypeConfiguration<ReferralException>
{
    public void Configure(EntityTypeBuilder<ReferralException> b)
    {
        b.ToTable("exceptions", "referral");
        b.HasIndex(x => x.Reference).IsUnique();
        b.Property(x => x.Description).HasColumnType("text");
        b.Property(x => x.ResolutionNote).HasColumnType("text");
    }
}

internal sealed class SaleResultConfig : IEntityTypeConfiguration<SaleResult>
{
    public void Configure(EntityTypeBuilder<SaleResult> b)
    {
        b.ToTable("sale_results", "referral", t =>
            t.HasCheckConstraint("ck_sale_result_amounts", "(official_sale_price IS NULL OR official_sale_price > 0) AND (declared_costs IS NULL OR declared_costs >= 0)"));
        b.HasIndex(x => x.AssignmentId).IsUnique();
    }
}

internal sealed class AgentUpdateConfig : IEntityTypeConfiguration<AgentUpdate>
{
    public void Configure(EntityTypeBuilder<AgentUpdate> b)
    {
        b.ToTable("agent_updates", "referral");
        b.Property(x => x.Text).HasColumnType("text");
    }
}

internal sealed class SalePlanMilestoneConfig : IEntityTypeConfiguration<SalePlanMilestone>
{
    public void Configure(EntityTypeBuilder<SalePlanMilestone> b) => b.ToTable("sale_plan_milestones", "referral");
}
