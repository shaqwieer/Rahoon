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
