using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Rahoon.Api.Modules.Administration;

// B7 additions to the `admin` schema. Picked up by ApplyConfigurationsFromAssembly.

internal sealed class TempAccessViewLogConfig : IEntityTypeConfiguration<TempAccessViewLog>
{
    public void Configure(EntityTypeBuilder<TempAccessViewLog> b)
    {
        b.ToTable("temp_access_view_logs", "admin");
        b.HasIndex(x => x.RequestId);
        b.HasOne<TempAccessRequest>().WithMany().HasForeignKey(x => x.RequestId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class PlatformDefaultRuleConfig : IEntityTypeConfiguration<PlatformDefaultRule>
{
    public void Configure(EntityTypeBuilder<PlatformDefaultRule> b)
    {
        b.ToTable("platform_default_rules", "admin");
        b.HasIndex(x => x.Key).IsUnique();
        b.Property(x => x.Key).HasMaxLength(64);
        b.Property(x => x.Value).HasPrecision(9, 4);
    }
}

internal sealed class JobHeartbeatConfig : IEntityTypeConfiguration<JobHeartbeat>
{
    public void Configure(EntityTypeBuilder<JobHeartbeat> b)
    {
        b.ToTable("job_heartbeats", "admin");
        b.HasKey(x => x.Key);
        b.Property(x => x.Key).HasMaxLength(64);
    }
}
