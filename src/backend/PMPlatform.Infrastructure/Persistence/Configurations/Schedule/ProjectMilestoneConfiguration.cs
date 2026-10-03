using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PMPlatform.Domain.MasterDataConfig;
using PMPlatform.Domain.Schedule;
using ProjectEntity = PMPlatform.Domain.Project.Project;

namespace PMPlatform.Infrastructure.Persistence.Configurations.Schedule;

internal sealed class ProjectMilestoneConfiguration : IEntityTypeConfiguration<ProjectMilestone>
{
    public void Configure(EntityTypeBuilder<ProjectMilestone> builder)
    {
        builder.ToTable("project_milestone", "schedule");
        builder.HasRowVersion();
        builder.HasNarrative(e => e.Title, "title");
        builder.Property(e => e.SortOrder).HasDatabaseDefault(0);

        // TASK-026 (indexing-strategy.md I-35, I-36): SCR-046's milestones of a project; SCR-062's register of upcoming milestones.
        builder.HasIndex(e => new { e.ProjectId, e.ForecastDate });
        builder.HasIndex(e => new { e.Status, e.ForecastDate });

        builder.HasCheck("sort_order", "sort_order >= 0");

        builder.HasOne<ProjectSchedule>().WithMany().HasForeignKey(e => e.ProjectScheduleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ScheduleActivity>().WithMany().HasForeignKey(e => e.ScheduleActivityId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ProjectEntity>().WithMany().HasForeignKey(e => e.ProjectId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<MasterDataItem>().WithMany().HasForeignKey(e => e.MilestoneCategoryItemId).OnDelete(DeleteBehavior.Restrict);
    }
}
