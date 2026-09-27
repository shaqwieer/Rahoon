using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Rahoon.Api.Modules.Closure;

// B10 closure entities (F02–F04). Reconciliation / ReconciliationLine / ClosureDocument keep their
// configuration in Infrastructure/Persistence/Configurations.cs.

internal sealed class DistributionConfig : IEntityTypeConfiguration<Distribution>
{
    public void Configure(EntityTypeBuilder<Distribution> b)
    {
        b.ToTable("distributions", "closure", t =>
        {
            t.HasCheckConstraint("ck_distribution_waterfall", "net_proceeds = lender_share + other_fees + owner_surplus");
            t.HasCheckConstraint("ck_distribution_net", "net_proceeds = sale_price - procedure_costs");
            t.HasCheckConstraint("ck_distribution_duties",
                "(checked_by_user_id IS NULL OR checked_by_user_id <> prepared_by_user_id) AND " +
                "(approved_by_user_id IS NULL OR (approved_by_user_id <> prepared_by_user_id AND approved_by_user_id <> checked_by_user_id))");
        });
        b.HasMany(x => x.Lines).WithOne().HasForeignKey(l => l.DistributionId).OnDelete(DeleteBehavior.Cascade);
        // One live distribution per case.
        b.HasIndex(x => x.CaseId).IsUnique().HasFilter("status <> 'Returned'");
    }
}

internal sealed class DistributionLineConfig : IEntityTypeConfiguration<DistributionLine>
{
    public void Configure(EntityTypeBuilder<DistributionLine> b) => b.ToTable("distribution_lines", "closure");
}

internal sealed class ClosureRequestConfig : IEntityTypeConfiguration<ClosureRequest>
{
    public void Configure(EntityTypeBuilder<ClosureRequest> b)
    {
        b.ToTable("closure_requests", "closure");
        b.Property(x => x.TraceJson).HasColumnType("jsonb");
        b.Property(x => x.Note).HasColumnType("text");
        b.HasIndex(x => x.CaseId).IsUnique().HasFilter("status = 'Pending'");
    }
}
