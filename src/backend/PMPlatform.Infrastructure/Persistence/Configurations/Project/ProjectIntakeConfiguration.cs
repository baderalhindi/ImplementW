using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PMPlatform.Domain.IdentityAccess;
using PMPlatform.Domain.Project;
using ProjectEntity = PMPlatform.Domain.Project.Project;

namespace PMPlatform.Infrastructure.Persistence.Configurations.Project;

internal sealed class ProjectIntakeConfiguration : IEntityTypeConfiguration<ProjectIntake>
{
    public void Configure(EntityTypeBuilder<ProjectIntake> builder)
    {
        builder.ToTable("project_intake", "project");
        builder.HasIndex(e => e.ProjectId).IsUnique();
        builder.HasNarrative(e => e.DeclaredScope, "declared_scope");
        builder.Property(e => e.OpeningPercentComplete).HasPrecision(18, 4);
        builder.IsAppendOnly();

        builder.HasOne<ProjectEntity>().WithMany().HasForeignKey(e => e.ProjectId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.RecordedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
