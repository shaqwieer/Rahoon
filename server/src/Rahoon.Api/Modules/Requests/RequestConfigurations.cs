using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Rahoon.Api.Modules.Requests;

// Schema «requests» (ADR 0001 §4.4).

internal sealed class FinancingInstitutionConfig : IEntityTypeConfiguration<FinancingInstitution>
{
    public void Configure(EntityTypeBuilder<FinancingInstitution> b)
    {
        b.ToTable("financing_institutions", "requests");
        b.Property(x => x.NameAr).HasMaxLength(200);
        b.Property(x => x.NameEn).HasMaxLength(200);
        b.Property(x => x.Kind).HasMaxLength(30);
        b.HasIndex(x => x.NameAr).IsUnique();
    }
}

internal sealed class RequestConfig : IEntityTypeConfiguration<Request>
{
    public void Configure(EntityTypeBuilder<Request> b)
    {
        b.ToTable("requests", "requests");
        b.HasIndex(x => x.Reference).IsUnique();
        b.HasIndex(x => new { x.ApplicantUserId, x.CreatedAt });
        b.HasIndex(x => new { x.OrganizationId, x.Status });
        b.Property(x => x.Reference).HasMaxLength(20);
        b.Property(x => x.InstitutionOtherName).HasMaxLength(200);
        b.Property(x => x.ApplicantFullName).HasMaxLength(200);
        b.Property(x => x.ContractNumber).HasMaxLength(60);
        b.Property(x => x.ArrearsDuration).HasMaxLength(20);
        b.Property(x => x.PropertyCity).HasMaxLength(100);
        b.Property(x => x.OutcomeCode).HasMaxLength(40);
        b.Property(x => x.NextStepText).HasMaxLength(600);
        b.HasOne(x => x.Institution).WithMany().HasForeignKey(x => x.InstitutionId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Identity.User>().WithMany().HasForeignKey(x => x.ApplicantUserId).OnDelete(DeleteBehavior.Restrict);
        b.Ignore(x => x.InstitutionDisplayName);
    }
}

internal sealed class RequestConsentConfig : IEntityTypeConfiguration<RequestConsent>
{
    public void Configure(EntityTypeBuilder<RequestConsent> b)
    {
        b.ToTable("request_consents", "requests");
        b.HasOne<Request>().WithMany().HasForeignKey(x => x.RequestId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => x.RequestId);
        b.Property(x => x.TextVersion).HasMaxLength(60);
        b.Property(x => x.RecipientName).HasMaxLength(200);
        b.Property(x => x.WithdrawnReason).HasMaxLength(40);
    }
}

internal sealed class RequestDocumentConfig : IEntityTypeConfiguration<RequestDocument>
{
    public void Configure(EntityTypeBuilder<RequestDocument> b)
    {
        b.ToTable("request_documents", "requests");
        b.HasOne<Request>().WithMany().HasForeignKey(x => x.RequestId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => x.RequestId);
        b.Property(x => x.Kind).HasMaxLength(40);
        b.Property(x => x.Name).HasMaxLength(200);
        b.Property(x => x.Source).HasMaxLength(20);
        b.HasMany(x => x.Versions).WithOne().HasForeignKey(v => v.DocumentId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class RequestDocumentVersionConfig : IEntityTypeConfiguration<RequestDocumentVersion>
{
    public void Configure(EntityTypeBuilder<RequestDocumentVersion> b)
    {
        b.ToTable("request_document_versions", "requests");
        b.HasIndex(x => new { x.DocumentId, x.VersionNo }).IsUnique();
        b.HasIndex(x => x.RequestId);
        b.Property(x => x.FileName).HasMaxLength(260);
        b.Property(x => x.ContentType).HasMaxLength(100);
        b.Property(x => x.Sha256).HasMaxLength(64);
        b.Property(x => x.StorageKey).HasMaxLength(300);
        b.Property(x => x.UploadedByLabel).HasMaxLength(200);
    }
}

internal sealed class RequestUpdateConfig : IEntityTypeConfiguration<RequestUpdate>
{
    public void Configure(EntityTypeBuilder<RequestUpdate> b)
    {
        b.ToTable("request_updates", "requests");
        b.HasOne<Request>().WithMany().HasForeignKey(x => x.RequestId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.RequestId, x.At });
        b.Property(x => x.Kind).HasMaxLength(40);
        b.Property(x => x.Title).HasMaxLength(300);
        b.Property(x => x.AuthorKind).HasMaxLength(20);
        b.Property(x => x.AuthorLabel).HasMaxLength(200);
    }
}

internal sealed class CoordinationEntryConfig : IEntityTypeConfiguration<CoordinationEntry>
{
    public void Configure(EntityTypeBuilder<CoordinationEntry> b)
    {
        b.ToTable("coordination_entries", "requests");
        b.HasOne<Request>().WithMany().HasForeignKey(x => x.RequestId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.RequestId, x.OccurredAt });
        b.Property(x => x.Kind).HasMaxLength(30);
        b.Property(x => x.Channel).HasMaxLength(20);
        b.Property(x => x.Counterpart).HasMaxLength(200);
        b.Property(x => x.RecordedByLabel).HasMaxLength(200);
    }
}

internal sealed class RequestMessageConfig : IEntityTypeConfiguration<RequestMessage>
{
    public void Configure(EntityTypeBuilder<RequestMessage> b)
    {
        b.ToTable("request_messages", "requests");
        b.HasOne<Request>().WithMany().HasForeignKey(x => x.RequestId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.RequestId, x.At });
        b.Property(x => x.AuthorKind).HasMaxLength(20);
        b.Property(x => x.AuthorLabel).HasMaxLength(200);
    }
}

internal sealed class RequestOfferConfig : IEntityTypeConfiguration<RequestOffer>
{
    public void Configure(EntityTypeBuilder<RequestOffer> b)
    {
        b.ToTable("request_offers", "requests", t =>
            t.HasCheckConstraint("ck_request_offers_verifier_not_recorder", "verified_by_user_id IS NULL OR verified_by_user_id <> recorded_by_user_id"));
        b.HasOne<Request>().WithMany().HasForeignKey(x => x.RequestId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<RequestDocument>().WithMany().HasForeignKey(x => x.LetterDocumentId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.RequestId, x.VersionNo }).IsUnique();
        b.Property(x => x.Path).HasMaxLength(4);
        b.Property(x => x.LenderReference).HasMaxLength(100);
        b.Property(x => x.LenderValidityText).HasMaxLength(300);
        b.Property(x => x.RecordedByLabel).HasMaxLength(200);
        b.Property(x => x.VerifiedByLabel).HasMaxLength(200);
        b.Property(x => x.StartText).HasMaxLength(300);
    }
}

internal sealed class RequestResponseConfig : IEntityTypeConfiguration<RequestResponse>
{
    public void Configure(EntityTypeBuilder<RequestResponse> b)
    {
        b.ToTable("request_responses", "requests");
        b.HasOne<Request>().WithMany().HasForeignKey(x => x.RequestId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<RequestOffer>().WithMany().HasForeignKey(x => x.OfferId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => x.Reference).IsUnique();
        b.HasIndex(x => x.RequestId);
        b.Property(x => x.Reference).HasMaxLength(30);
        b.Property(x => x.Kind).HasMaxLength(20);
    }
}
