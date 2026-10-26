using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PMPlatform.Domain.IdentityAccess;
using ProjectEntity = PMPlatform.Domain.Project.Project;

namespace PMPlatform.Infrastructure.Persistence.Configurations.IdentityAccess;

internal sealed class AccessRelationshipConfiguration : IEntityTypeConfiguration<AccessRelationship>
{
    public void Configure(EntityTypeBuilder<AccessRelationship> builder)
    {
        builder.ToTable("access_relationship", "identity_access");

        builder.HasOne<User>().WithMany().HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PermissionProfileVersion>().WithMany().HasForeignKey(e => e.PermissionProfileVersionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Department>().WithMany().HasForeignKey(e => e.DepartmentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ExternalEntity>().WithMany().HasForeignKey(e => e.ExternalEntityId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.SponsorUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ProjectEntity>().WithMany().HasForeignKey(e => e.ProjectId).OnDelete(DeleteBehavior.Restrict);

        // TASK-026 (indexing-strategy.md I-06, I-07): a user's active grants, read on every authorised request, and a
        // project's active grants, ended on closure. Each leads with its foreign key and replaces its single-column index.
        builder.HasIndex(e => new { e.UserId, e.Status });
        builder.HasIndex(e => new { e.ProjectId, e.Status });
    }
}
