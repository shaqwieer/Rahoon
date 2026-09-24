using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Rahoon.Api.Modules.Sale;

// Voluntary sale (B8) keeps its tables in the `sale` schema.

internal sealed class VoluntarySaleConfig : IEntityTypeConfiguration<VoluntarySale>
{
    public void Configure(EntityTypeBuilder<VoluntarySale> b)
    {
        b.ToTable("sales", "sale", t =>
        {
            t.HasCheckConstraint("ck_sale_broker_rate", "broker_rate >= 0 AND broker_rate < 0.2");
            t.HasCheckConstraint("ck_sale_fees", "other_fees_estimate >= 0");
        });
        b.HasIndex(x => x.BuyerReference).IsUnique();
        // One open sale track per case (withdrawn / completed / rejected tracks stay as history).
        b.HasIndex(x => x.CaseId).IsUnique()
            .HasFilter("status IN ('Requested','PendingDecision','AwaitingConsent','Active','OfferApproved')")
            .HasDatabaseName("ux_sales_open_per_case");
        b.Property(x => x.BrokerRate).HasPrecision(9, 4);
        b.Property(x => x.RequestText).HasMaxLength(2000);
        b.Property(x => x.DecisionReason).HasMaxLength(2000);
    }
}

internal sealed class SaleConsentConfig : IEntityTypeConfiguration<SaleConsent>
{
    public void Configure(EntityTypeBuilder<SaleConsent> b)
    {
        b.ToTable("consents", "sale", t =>
        {
            t.HasCheckConstraint("ck_sale_consent_min", "min_price > 0");
            t.HasCheckConstraint("ck_sale_consent_mandate", "mandate_end > mandate_start");
        });
        b.HasOne<VoluntarySale>().WithMany().HasForeignKey(x => x.SaleId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.SaleId, x.VersionNo }).IsUnique();
    }
}

internal sealed class SalePrepItemConfig : IEntityTypeConfiguration<SalePrepItem>
{
    public void Configure(EntityTypeBuilder<SalePrepItem> b)
    {
        b.ToTable("prep_items", "sale");
        b.HasOne<VoluntarySale>().WithMany().HasForeignKey(x => x.SaleId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.SaleId, x.Key }).IsUnique();
    }
}

internal sealed class BuyerOfferConfig : IEntityTypeConfiguration<BuyerOffer>
{
    public void Configure(EntityTypeBuilder<BuyerOffer> b)
    {
        b.ToTable("buyer_offers", "sale", t =>
        {
            t.HasCheckConstraint("ck_buyer_offer_price", "price > 0");
            t.HasCheckConstraint("ck_buyer_offer_days", "proposed_transfer_days BETWEEN 1 AND 365");
        });
        b.HasOne<VoluntarySale>().WithMany().HasForeignKey(x => x.SaleId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.SaleId, x.Code }).IsUnique();
    }
}

internal sealed class SaleTrackingStepConfig : IEntityTypeConfiguration<SaleTrackingStep>
{
    public void Configure(EntityTypeBuilder<SaleTrackingStep> b)
    {
        b.ToTable("tracking_steps", "sale");
        b.HasOne<VoluntarySale>().WithMany().HasForeignKey(x => x.SaleId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.SaleId, x.Key }).IsUnique();
    }
}
