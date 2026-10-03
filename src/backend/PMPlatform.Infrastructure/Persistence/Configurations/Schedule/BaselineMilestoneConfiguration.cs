using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PMPlatform.Domain.Schedule;

namespace PMPlatform.Infrastructure.Persistence.Configurations.Schedule;

internal sealed class BaselineMilestoneConfiguration : IEntityTypeConfiguration<BaselineMilestone>
{
    public void Configure(EntityTypeBuilder<BaselineMilestone> builder)
    {
        builder.ToTable("baseline_milestone", "schedule");
        builder.IsAppendOnly();
        builder.HasIndex(e => new { e.ProjectBaselineId, e.ProjectMilestoneId }).IsUnique();

        builder.HasOne<ProjectBaseline>().WithMany().HasForeignKey(e => e.ProjectBaselineId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ProjectMilestone>().WithMany().HasForeignKey(e => e.ProjectMilestoneId).OnDelete(DeleteBehavior.Restrict);
    }
}
