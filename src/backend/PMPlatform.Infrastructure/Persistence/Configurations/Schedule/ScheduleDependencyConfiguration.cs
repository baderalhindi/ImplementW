using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PMPlatform.Domain.Schedule;

namespace PMPlatform.Infrastructure.Persistence.Configurations.Schedule;

internal sealed class ScheduleDependencyConfiguration : IEntityTypeConfiguration<ScheduleDependency>
{
    public void Configure(EntityTypeBuilder<ScheduleDependency> builder)
    {
        builder.ToTable("schedule_dependency", "schedule");
        builder.HasRowVersion();
        builder.Property(e => e.LagDays).HasDatabaseDefault(0);
        builder.HasIndex(e => new { e.PredecessorActivityId, e.SuccessorActivityId }).IsUnique();

        // VAL-SCH-007, BR-SCH-025: two distinct activities, a non-negative lag. Acyclicity is the guard trigger's (TASK-046_GuardScheduleHistory).
        builder.HasCheck("ends", "predecessor_activity_id <> successor_activity_id");
        builder.HasCheck("lag_days", "lag_days >= 0");

        builder.HasOne<ScheduleActivity>().WithMany().HasForeignKey(e => e.PredecessorActivityId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ScheduleActivity>().WithMany().HasForeignKey(e => e.SuccessorActivityId).OnDelete(DeleteBehavior.Restrict);
    }
}
