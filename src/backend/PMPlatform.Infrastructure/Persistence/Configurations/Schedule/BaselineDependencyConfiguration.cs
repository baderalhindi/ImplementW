using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PMPlatform.Domain.Schedule;

namespace PMPlatform.Infrastructure.Persistence.Configurations.Schedule;

internal sealed class BaselineDependencyConfiguration : IEntityTypeConfiguration<BaselineDependency>
{
    public void Configure(EntityTypeBuilder<BaselineDependency> builder)
    {
        builder.ToTable("baseline_dependency", "schedule");
        builder.IsAppendOnly();
        builder.HasIndex(e => new { e.ProjectBaselineId, e.PredecessorActivityId, e.SuccessorActivityId }).IsUnique();
        builder.HasCheck("ends", "predecessor_activity_id <> successor_activity_id");
        builder.HasCheck("lag_days", "lag_days >= 0");

        builder.HasOne<ProjectBaseline>().WithMany().HasForeignKey(e => e.ProjectBaselineId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ScheduleActivity>().WithMany().HasForeignKey(e => e.PredecessorActivityId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ScheduleActivity>().WithMany().HasForeignKey(e => e.SuccessorActivityId).OnDelete(DeleteBehavior.Restrict);
    }
}
