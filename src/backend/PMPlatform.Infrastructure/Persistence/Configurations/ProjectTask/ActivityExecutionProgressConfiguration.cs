using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PMPlatform.Domain.ProjectTask;
using PMPlatform.Domain.Schedule;
using ProjectEntity = PMPlatform.Domain.Project.Project;

namespace PMPlatform.Infrastructure.Persistence.Configurations.ProjectTask;

internal sealed class ActivityExecutionProgressConfiguration : IEntityTypeConfiguration<ActivityExecutionProgress>
{
    public void Configure(EntityTypeBuilder<ActivityExecutionProgress> builder)
    {
        builder.ToTable("activity_execution_progress", "project_task");
        builder.Property(e => e.ActualPercentComplete).HasPrecision(18, 4);
        builder.HasIndex(e => e.ScheduleActivityId).IsUnique();

        builder.HasCheck("percent", "actual_percent_complete BETWEEN 0 AND 100");

        builder.HasOne<ProjectEntity>().WithMany().HasForeignKey(e => e.ProjectId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ScheduleActivity>().WithMany().HasForeignKey(e => e.ScheduleActivityId).OnDelete(DeleteBehavior.Restrict);
    }
}
