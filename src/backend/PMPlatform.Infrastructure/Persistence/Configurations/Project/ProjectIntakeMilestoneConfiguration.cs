using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PMPlatform.Domain.Project;

namespace PMPlatform.Infrastructure.Persistence.Configurations.Project;

internal sealed class ProjectIntakeMilestoneConfiguration : IEntityTypeConfiguration<ProjectIntakeMilestone>
{
    public void Configure(EntityTypeBuilder<ProjectIntakeMilestone> builder)
    {
        builder.ToTable("project_intake_milestone", "project");
        builder.HasNarrative(e => e.Title, "title");
        builder.IsAppendOnly();

        builder.HasOne<ProjectIntake>().WithMany().HasForeignKey(e => e.ProjectIntakeId).OnDelete(DeleteBehavior.Restrict);
    }
}
