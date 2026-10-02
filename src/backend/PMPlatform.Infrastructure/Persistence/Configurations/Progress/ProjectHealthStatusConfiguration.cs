using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PMPlatform.Domain.MasterDataConfig;
using PMPlatform.Domain.Progress;
using ProjectEntity = PMPlatform.Domain.Project.Project;

namespace PMPlatform.Infrastructure.Persistence.Configurations.Progress;

internal sealed class ProjectHealthStatusConfiguration : IEntityTypeConfiguration<ProjectHealthStatus>
{
    public void Configure(EntityTypeBuilder<ProjectHealthStatus> builder)
    {
        builder.ToTable("project_health_status", "progress");
        builder.Property(e => e.ActualPercent).HasPrecision(18, 4);
        builder.Property(e => e.PlannedPercent).HasPrecision(18, 4);
        builder.HasIndex(e => e.ProjectId).IsUnique();
        builder.HasCheck("percent", "(actual_percent IS NULL OR actual_percent BETWEEN 0 AND 100) AND (planned_percent IS NULL OR planned_percent BETWEEN 0 AND 100)");

        builder.HasOne<ProjectEntity>().WithMany().HasForeignKey(e => e.ProjectId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ConfigurationVersion>().WithMany().HasForeignKey(e => e.HealthRuleConfigurationVersionId).OnDelete(DeleteBehavior.Restrict);
    }
}
