using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PMPlatform.Domain.Schedule;

namespace PMPlatform.Infrastructure.Persistence.Configurations.Schedule;

internal sealed class ScheduleActivityConfiguration : IEntityTypeConfiguration<ScheduleActivity>
{
    public void Configure(EntityTypeBuilder<ScheduleActivity> builder)
    {
        builder.ToTable("schedule_activity", "schedule");
        builder.HasRowVersion();
        builder.Property(e => e.WbsCode).HasMaxLength(50);
        builder.HasNarrative(e => e.Name, "name");
        builder.Property(e => e.SortOrder).HasDatabaseDefault(0);
        builder.HasIndex(e => new { e.ProjectScheduleId, e.WbsCode }).IsUnique();

        builder.HasCheck("hierarchy", "parent_activity_id IS NULL OR parent_activity_id <> id");
        builder.HasCheck("planned", "planned_duration_days >= 1 AND planned_finish_date >= planned_start_date");
        builder.HasCheck("forecast", "forecast_finish_date >= forecast_start_date");
        builder.HasCheck("actual", "actual_finish_date IS NULL OR (actual_start_date IS NOT NULL AND actual_finish_date >= actual_start_date)");

        builder.HasOne<ProjectSchedule>().WithMany().HasForeignKey(e => e.ProjectScheduleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ScheduleActivity>().WithMany().HasForeignKey(e => e.ParentActivityId).OnDelete(DeleteBehavior.Restrict);
    }
}
