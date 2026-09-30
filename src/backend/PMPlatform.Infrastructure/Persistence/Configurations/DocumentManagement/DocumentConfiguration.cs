using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PMPlatform.Domain.DocumentManagement;
using PMPlatform.Domain.IdentityAccess;
using PMPlatform.Domain.MasterDataConfig;
using ProjectEntity = PMPlatform.Domain.Project.Project;

namespace PMPlatform.Infrastructure.Persistence.Configurations.DocumentManagement;

internal sealed class DocumentConfiguration : IEntityTypeConfiguration<Document>
{
    public void Configure(EntityTypeBuilder<Document> builder)
    {
        builder.ToTable("document", "document_management");
        builder.HasNarrative(e => e.Title, "title");
        builder.HasNarrative(e => e.Description, "description");

        // indexing-strategy.md I-31: SCR-121 Project Documents, SCR-120 under project scope. I-32: SCR-120 under ALL, SCR-122.
        builder.HasIndex(e => new { e.ProjectId, e.UpdatedAt, e.Id });
        builder.HasIndex(e => new { e.UpdatedAt, e.Id });

        builder.HasOne<MasterDataItem>().WithMany().HasForeignKey(e => e.DocumentTypeItemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<MasterDataItem>().WithMany().HasForeignKey(e => e.DataClassificationItemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ProjectEntity>().WithMany().HasForeignKey(e => e.ProjectId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.OwnerUserId).OnDelete(DeleteBehavior.Restrict);

        builder.HasRowVersion();
    }
}
