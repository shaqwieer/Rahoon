using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Rahoon.Api.Modules.Market;

// Schema «market» (2026-10-01). No table of the archived mortgage-help schemas is touched.

internal static class Jsonb
{
    private static readonly ValueConverter<Dictionary<string, string>, string> Converter = new(
        v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
        v => JsonSerializer.Deserialize<Dictionary<string, string>>(v, (JsonSerializerOptions?)null) ?? new Dictionary<string, string>());

    private static readonly ValueComparer<Dictionary<string, string>> Comparer = new(
        (a, b) => a!.Count == b!.Count && !a.Except(b).Any(),
        v => v.Aggregate(0, (h, kv) => HashCode.Combine(h, kv.Key.GetHashCode(), kv.Value.GetHashCode())),
        v => new Dictionary<string, string>(v));

    public static PropertyBuilder<Dictionary<string, string>> AsJsonb(this PropertyBuilder<Dictionary<string, string>> p) =>
        p.HasConversion(Converter, Comparer).HasColumnType("jsonb");
}

internal sealed class ObligationPartyConfig : IEntityTypeConfiguration<ObligationParty>
{
    public void Configure(EntityTypeBuilder<ObligationParty> b)
    {
        b.ToTable("obligation_parties", "market");
        b.Property(x => x.Kind).HasMaxLength(20);
        b.Property(x => x.NameAr).HasMaxLength(200);
        b.HasIndex(x => new { x.Kind, x.NameAr }).IsUnique();
    }
}

internal sealed class SaleRequestConfig : IEntityTypeConfiguration<SaleRequest>
{
    public void Configure(EntityTypeBuilder<SaleRequest> b)
    {
        b.ToTable("sale_requests", "market");
        b.HasIndex(x => x.Reference).IsUnique();
        b.HasIndex(x => new { x.ApplicantUserId, x.ClientDraftId }).IsUnique().HasFilter("client_draft_id IS NOT NULL");
        b.HasIndex(x => new { x.OrganizationId, x.Status });
        b.Property(x => x.Reference).HasMaxLength(20);
        b.Property(x => x.PropertyType).HasMaxLength(30);
        b.Property(x => x.City).HasMaxLength(60);
        b.Property(x => x.District).HasMaxLength(100);
        b.Property(x => x.Project).HasMaxLength(150);
        b.Property(x => x.ObligationMode).HasMaxLength(20);
        b.Property(x => x.Answers).AsJsonb();
        b.Property(x => x.LocationDisplayWish).HasMaxLength(20);
        b.Property(x => x.LocationLabel).HasMaxLength(200);
        b.Property(x => x.ContactName).HasMaxLength(150);
        b.Property(x => x.ContactEmail).HasMaxLength(200);
        b.Property(x => x.RelationshipDeclared).HasMaxLength(20);
        b.Property(x => x.DeclarationsVersion).HasMaxLength(60);
        b.Property(x => x.AssignedToLabel).HasMaxLength(150);
        b.Property(x => x.DecisionReason).HasMaxLength(1000);
        b.Property(x => x.WithdrawReason).HasMaxLength(500);
        b.HasOne<Identity.User>().WithMany().HasForeignKey(x => x.ApplicantUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.Obligations).WithOne().HasForeignKey(o => o.SaleRequestId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class SaleObligationConfig : IEntityTypeConfiguration<SaleObligation>
{
    public void Configure(EntityTypeBuilder<SaleObligation> b)
    {
        b.ToTable("sale_obligations", "market");
        b.HasIndex(x => x.SaleRequestId);
        b.Property(x => x.Kind).HasMaxLength(20);
        b.Property(x => x.PartyOtherName).HasMaxLength(200);
        b.Property(x => x.RelationNote).HasMaxLength(500);
        b.Property(x => x.Answers).AsJsonb();
        b.HasOne(x => x.Party).WithMany().HasForeignKey(x => x.PartyId).OnDelete(DeleteBehavior.Restrict);
        b.Ignore(x => x.PartyDisplayName);
    }
}

internal sealed class PrivateDocumentConfig : IEntityTypeConfiguration<PrivateDocument>
{
    public void Configure(EntityTypeBuilder<PrivateDocument> b)
    {
        b.ToTable("private_documents", "market");
        b.HasIndex(x => x.SaleRequestId);
        b.HasOne<SaleRequest>().WithMany().HasForeignKey(x => x.SaleRequestId).OnDelete(DeleteBehavior.Restrict);
        b.Property(x => x.Kind).HasMaxLength(40);
        b.Property(x => x.FileName).HasMaxLength(260);
        b.Property(x => x.ContentType).HasMaxLength(100);
        b.Property(x => x.Sha256).HasMaxLength(64);
        b.Property(x => x.StorageKey).HasMaxLength(300);
        b.Property(x => x.Source).HasMaxLength(20);
        b.Property(x => x.ReviewNote).HasMaxLength(500);
    }
}

internal sealed class ListingPhotoConfig : IEntityTypeConfiguration<ListingPhoto>
{
    public void Configure(EntityTypeBuilder<ListingPhoto> b)
    {
        b.ToTable("listing_photos", "market");
        b.HasIndex(x => x.SaleRequestId);
        b.HasOne<SaleRequest>().WithMany().HasForeignKey(x => x.SaleRequestId).OnDelete(DeleteBehavior.Restrict);
        b.Property(x => x.FileName).HasMaxLength(260);
        b.Property(x => x.ContentType).HasMaxLength(100);
        b.Property(x => x.Sha256).HasMaxLength(64);
        b.Property(x => x.StorageKey).HasMaxLength(300);
        b.Property(x => x.ReviewNote).HasMaxLength(500);
    }
}

internal sealed class FigureVerificationConfig : IEntityTypeConfiguration<FigureVerification>
{
    public void Configure(EntityTypeBuilder<FigureVerification> b)
    {
        b.ToTable("figure_verifications", "market");
        b.HasIndex(x => new { x.SaleRequestId, x.FieldKey });
        b.HasOne<SaleRequest>().WithMany().HasForeignKey(x => x.SaleRequestId).OnDelete(DeleteBehavior.Restrict);
        b.Property(x => x.FieldKey).HasMaxLength(80);
        b.Property(x => x.Value).HasMaxLength(100);
        b.Property(x => x.Source).HasMaxLength(40);
        b.Property(x => x.Note).HasMaxLength(500);
        b.Property(x => x.VerifiedByLabel).HasMaxLength(150);
    }
}

internal sealed class ExternalApprovalConfig : IEntityTypeConfiguration<ExternalApproval>
{
    public void Configure(EntityTypeBuilder<ExternalApproval> b)
    {
        b.ToTable("external_approvals", "market");
        b.HasIndex(x => new { x.ObligationId, x.RecordedAt });
        b.HasOne<SaleRequest>().WithMany().HasForeignKey(x => x.SaleRequestId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<SaleObligation>().WithMany().HasForeignKey(x => x.ObligationId).OnDelete(DeleteBehavior.Restrict);
        b.Property(x => x.Conditions).HasMaxLength(1000);
        b.Property(x => x.Note).HasMaxLength(500);
        b.Property(x => x.RecordedByLabel).HasMaxLength(150);
    }
}

internal sealed class CompletionRequestConfig : IEntityTypeConfiguration<CompletionRequest>
{
    public void Configure(EntityTypeBuilder<CompletionRequest> b)
    {
        b.ToTable("completion_requests", "market");
        b.HasIndex(x => new { x.SubjectId, x.RequestedAt });
        b.Property(x => x.SubjectType).HasMaxLength(20);
        b.Property(x => x.Note).HasMaxLength(1000);
        b.Property(x => x.RequestedByLabel).HasMaxLength(150);
    }
}

internal sealed class BuyerRequestConfig : IEntityTypeConfiguration<BuyerRequest>
{
    public void Configure(EntityTypeBuilder<BuyerRequest> b)
    {
        b.ToTable("buyer_requests", "market");
        b.HasIndex(x => x.Reference).IsUnique();
        b.HasIndex(x => new { x.ApplicantUserId, x.ClientDraftId }).IsUnique().HasFilter("client_draft_id IS NOT NULL");
        // One live buyer profile per person (withdrawn or rejected ones are history).
        b.HasIndex(x => x.ApplicantUserId).IsUnique().HasFilter("status NOT IN ('Withdrawn', 'Rejected')").HasDatabaseName("ux_buyer_requests_one_live");
        b.HasIndex(x => new { x.OrganizationId, x.Status });
        b.Property(x => x.Reference).HasMaxLength(20);
        b.Property(x => x.InstallmentFrequency).HasMaxLength(20);
        b.Property(x => x.PurchaseMode).HasMaxLength(20);
        b.Property(x => x.AreasText).HasMaxLength(500);
        b.Property(x => x.Readiness).HasMaxLength(20);
        b.Property(x => x.DeliveryBy).HasMaxLength(7);
        b.Property(x => x.ContactName).HasMaxLength(150);
        b.Property(x => x.DeclarationsVersion).HasMaxLength(60);
        b.Property(x => x.CapacityReviewNote).HasMaxLength(500);
        b.Property(x => x.CapacityReviewedByLabel).HasMaxLength(150);
        b.Property(x => x.FinanceApprovalStatus).HasMaxLength(20);
        b.Property(x => x.FinanceApprovalSource).HasMaxLength(200);
        b.Property(x => x.AssignedToLabel).HasMaxLength(150);
        b.Property(x => x.DecisionReason).HasMaxLength(1000);
        b.Property(x => x.WithdrawReason).HasMaxLength(500);
        b.HasOne<Identity.User>().WithMany().HasForeignKey(x => x.ApplicantUserId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class OpportunityConfig : IEntityTypeConfiguration<Opportunity>
{
    public void Configure(EntityTypeBuilder<Opportunity> b)
    {
        b.ToTable("opportunities", "market");
        b.HasIndex(x => x.Reference).IsUnique();
        b.HasIndex(x => x.SaleRequestId);
        b.HasIndex(x => new { x.Status, x.City, x.PropertyType });
        b.HasOne<SaleRequest>().WithMany().HasForeignKey(x => x.SaleRequestId).OnDelete(DeleteBehavior.Restrict);
        b.Property(x => x.Reference).HasMaxLength(20);
        b.Property(x => x.Title).HasMaxLength(160);
        b.Property(x => x.Description).HasMaxLength(3000);
        b.Property(x => x.PropertyType).HasMaxLength(30);
        b.Property(x => x.City).HasMaxLength(60);
        b.Property(x => x.District).HasMaxLength(100);
        b.Property(x => x.Project).HasMaxLength(150);
        b.Property(x => x.Track).HasMaxLength(20);
        b.Property(x => x.Readiness).HasMaxLength(20);
        b.Property(x => x.DeliveryMonth).HasMaxLength(7);
        b.Property(x => x.Specs).AsJsonb();
        b.Property(x => x.LocationPrecision).HasMaxLength(20);
        b.Property(x => x.PauseReason).HasMaxLength(500);
        b.Property(x => x.WithdrawReason).HasMaxLength(500);
        b.Property(x => x.PreparedByLabel).HasMaxLength(150);
        b.Property(x => x.AssignedToLabel).HasMaxLength(150);
    }
}

internal sealed class OpportunityTermsConfig : IEntityTypeConfiguration<OpportunityTerms>
{
    public void Configure(EntityTypeBuilder<OpportunityTerms> b)
    {
        b.ToTable("opportunity_terms", "market");
        b.HasIndex(x => new { x.OpportunityId, x.VersionNo }).IsUnique();
        b.HasOne<Opportunity>().WithMany().HasForeignKey(x => x.OpportunityId).OnDelete(DeleteBehavior.Restrict);
        b.Property(x => x.Track).HasMaxLength(20);
        b.Property(x => x.InputJson).HasColumnType("jsonb");
        b.Property(x => x.ResultJson).HasColumnType("jsonb");
        b.Property(x => x.InstallmentFrequency).HasMaxLength(20);
        b.Property(x => x.TransferConditions).HasMaxLength(2000);
        b.Property(x => x.VerificationScope).HasMaxLength(1000);
        b.Property(x => x.PreparedByLabel).HasMaxLength(150);
        b.Property(x => x.OwnerNote).HasMaxLength(1000);
        b.Property(x => x.OwnerConfirmationText).HasMaxLength(1000);
    }
}

internal sealed class InterestConfig : IEntityTypeConfiguration<Interest>
{
    public void Configure(EntityTypeBuilder<Interest> b)
    {
        b.ToTable("interests", "market");
        b.HasIndex(x => x.Reference).IsUnique();
        // One open interest per buyer and opportunity: a second click returns the same interest.
        b.HasIndex(x => new { x.ApplicantUserId, x.OpportunityId }).IsUnique().HasFilter("status <> 'Withdrawn'").HasDatabaseName("ux_interests_one_open");
        b.HasIndex(x => new { x.OpportunityId, x.Status });
        b.HasOne<Opportunity>().WithMany().HasForeignKey(x => x.OpportunityId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<OpportunityTerms>().WithMany().HasForeignKey(x => x.TermsId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<BuyerRequest>().WithMany().HasForeignKey(x => x.BuyerRequestId).OnDelete(DeleteBehavior.Restrict);
        b.Property(x => x.Reference).HasMaxLength(20);
        b.Property(x => x.Message).HasMaxLength(1000);
        b.Property(x => x.ContactPreference).HasMaxLength(20);
        b.Property(x => x.ContactName).HasMaxLength(150);
        b.Property(x => x.AssignedToLabel).HasMaxLength(150);
        b.Property(x => x.CloseReason).HasMaxLength(500);
    }
}

internal sealed class SavedOpportunityConfig : IEntityTypeConfiguration<SavedOpportunity>
{
    public void Configure(EntityTypeBuilder<SavedOpportunity> b)
    {
        b.ToTable("saved_opportunities", "market");
        b.HasIndex(x => new { x.ApplicantUserId, x.OpportunityId }).IsUnique();
        b.HasOne<Opportunity>().WithMany().HasForeignKey(x => x.OpportunityId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class MarketEventConfig : IEntityTypeConfiguration<MarketEvent>
{
    public void Configure(EntityTypeBuilder<MarketEvent> b)
    {
        b.ToTable("events", "market");
        b.HasIndex(x => new { x.SubjectId, x.At });
        b.HasIndex(x => new { x.ApplicantUserId, x.At });
        b.Property(x => x.SubjectType).HasMaxLength(20);
        b.Property(x => x.Kind).HasMaxLength(40);
        b.Property(x => x.Title).HasMaxLength(300);
        b.Property(x => x.Body).HasMaxLength(2000);
        b.Property(x => x.FromStatus).HasMaxLength(40);
        b.Property(x => x.ToStatus).HasMaxLength(40);
        b.Property(x => x.Reason).HasMaxLength(1000);
        b.Property(x => x.ActorKind).HasMaxLength(20);
        b.Property(x => x.ActorLabel).HasMaxLength(150);
        b.Property(x => x.DataJson).HasColumnType("jsonb");
    }
}

internal sealed class MarketNotificationConfig : IEntityTypeConfiguration<MarketNotification>
{
    public void Configure(EntityTypeBuilder<MarketNotification> b)
    {
        b.ToTable("notification_log", "market");
        b.HasIndex(x => new { x.UserId, x.At });
        b.Property(x => x.Channel).HasMaxLength(20);
        b.Property(x => x.DestinationMasked).HasMaxLength(40);
        b.Property(x => x.Body).HasMaxLength(500);
        b.Property(x => x.Result).HasMaxLength(20);
        b.Property(x => x.Error).HasMaxLength(500);
    }
}

internal sealed class ContactMessageConfig : IEntityTypeConfiguration<ContactMessage>
{
    public void Configure(EntityTypeBuilder<ContactMessage> b)
    {
        b.ToTable("contact_messages", "market");
        b.HasIndex(x => x.Reference).IsUnique();
        b.HasIndex(x => new { x.Status, x.CreatedAt });
        b.Property(x => x.Reference).HasMaxLength(20);
        b.Property(x => x.Name).HasMaxLength(150);
        b.Property(x => x.PhoneMasked).HasMaxLength(40);
        b.Property(x => x.Topic).HasMaxLength(20);
        b.Property(x => x.HandledNote).HasMaxLength(500);
        b.Property(x => x.HandledByLabel).HasMaxLength(150);
    }
}
