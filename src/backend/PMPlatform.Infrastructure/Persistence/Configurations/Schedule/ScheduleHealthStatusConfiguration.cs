using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PMPlatform.Domain.MasterDataConfig;
using PMPlatform.Domain.Schedule;
using ProjectEntity = PMPlatform.Domain.Project.Project;

namespace PMPlatform.Infrastructure.Persistence.Configurations.Schedule;

internal sealed class ScheduleHealthStatusConfiguration : IEntityTypeConfiguration<ScheduleHealthStatus>
{
    public void Configure(EntityTypeBuilder<ScheduleHealthStatus> builder)
    {
        builder.ToTable("schedule_health_status", "schedule");
        builder.HasIndex(e => e.ProjectId).IsUnique();

        // A variance needs the baseline it was measured against.
        builder.HasCheck("variance", "finish_variance_days IS NULL OR project_baseline_id IS NOT NULL");

        builder.HasOne<ProjectBaseline>().WithMany().HasForeignKey(e => e.ProjectBaselineId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ProjectEntity>().WithMany().HasForeignKey(e => e.ProjectId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ConfigurationVersion>().WithMany().HasForeignKey(e => e.HealthRuleConfigurationVersionId).OnDelete(DeleteBehavior.Restrict);
    }
}
