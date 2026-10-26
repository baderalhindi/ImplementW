using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PMPlatform.Domain.Schedule;

namespace PMPlatform.Infrastructure.Persistence.Configurations.Schedule;

internal sealed class BaselineActivityConfiguration : IEntityTypeConfiguration<BaselineActivity>
{
    public void Configure(EntityTypeBuilder<BaselineActivity> builder)
    {
        builder.ToTable("baseline_activity", "schedule");
        builder.IsAppendOnly();
        builder.HasIndex(e => new { e.ProjectBaselineId, e.ScheduleActivityId }).IsUnique();
        builder.HasCheck("planned", "planned_duration_days >= 1 AND planned_finish_date >= planned_start_date");

        builder.HasOne<ProjectBaseline>().WithMany().HasForeignKey(e => e.ProjectBaselineId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ScheduleActivity>().WithMany().HasForeignKey(e => e.ScheduleActivityId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ScheduleActivity>().WithMany().HasForeignKey(e => e.ParentActivityId).OnDelete(DeleteBehavior.Restrict);
    }
}
