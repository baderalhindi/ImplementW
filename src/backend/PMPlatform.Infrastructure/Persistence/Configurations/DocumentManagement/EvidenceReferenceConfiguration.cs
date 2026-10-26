using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PMPlatform.Domain.DocumentManagement;
using PMPlatform.Domain.IdentityAccess;
using PMPlatform.Domain.MasterDataConfig;

namespace PMPlatform.Infrastructure.Persistence.Configurations.DocumentManagement;

internal sealed class EvidenceReferenceConfiguration : IEntityTypeConfiguration<EvidenceReference>
{
    /// <summary>Named: the generated name exceeds PostgreSQL's 63 characters. ERD: one reference per link, version and type.</summary>
    public const string PinKey = "ix_evidence_reference_link_version_type";

    public void Configure(EntityTypeBuilder<EvidenceReference> builder)
    {
        builder.ToTable("evidence_reference", "document_management");

        builder.HasIndex(e => new { e.BusinessLinkId, e.DocumentVersionId, e.EvidenceTypeItemId }).IsUnique().HasDatabaseName(PinKey);
        builder.HasIndex(e => e.DocumentVersionId);

        builder.HasOne<BusinessLink>().WithMany().HasForeignKey(e => e.BusinessLinkId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<DocumentVersion>().WithMany().HasForeignKey(e => e.DocumentVersionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<MasterDataItem>().WithMany().HasForeignKey(e => e.EvidenceTypeItemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.DesignatedByUserId).OnDelete(DeleteBehavior.Restrict);

        builder.HasRowVersion();
    }
}
