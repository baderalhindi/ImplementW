using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PMPlatform.Domain.DocumentManagement;
using PMPlatform.Domain.IdentityAccess;

namespace PMPlatform.Infrastructure.Persistence.Configurations.DocumentManagement;

internal sealed class BusinessLinkConfiguration : IEntityTypeConfiguration<BusinessLink>
{
    /// <summary>Named: the generated name exceeds PostgreSQL's 63 characters. ERD: one link per document, target and role.</summary>
    public const string TargetKey = "ix_business_link_document_target_role";

    public void Configure(EntityTypeBuilder<BusinessLink> builder)
    {
        builder.ToTable("business_link", "document_management");
        builder.Property(e => e.TargetModule).HasMaxLength(50);
        builder.Property(e => e.TargetType).HasMaxLength(100);
        builder.HasCheck("unlinked", "(unlinked_at IS NULL) = (unlinked_by_user_id IS NULL)");

        builder.HasIndex(e => new { e.DocumentId, e.TargetModule, e.TargetType, e.TargetId, e.LinkRole }).IsUnique().HasDatabaseName(TargetKey);

        // A target's attachments and the evidence it holds (TASK-050, TASK-066): a module lookup, not a register (indexing-strategy.md §2).
        builder.HasIndex(e => new { e.TargetModule, e.TargetType, e.TargetId }).HasDatabaseName("ix_business_link_target");

        builder.HasOne<Document>().WithMany().HasForeignKey(e => e.DocumentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.LinkedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.UnlinkedByUserId).OnDelete(DeleteBehavior.Restrict);

        builder.HasRowVersion();
    }
}
