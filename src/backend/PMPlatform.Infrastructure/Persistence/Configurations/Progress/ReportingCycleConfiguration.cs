using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PMPlatform.Domain.Progress;
using ProjectEntity = PMPlatform.Domain.Project.Project;

namespace PMPlatform.Infrastructure.Persistence.Configurations.Progress;

internal sealed class ReportingCycleConfiguration : IEntityTypeConfiguration<ReportingCycle>
{
    public void Configure(EntityTypeBuilder<ReportingCycle> builder)
    {
        builder.ToTable("reporting_cycle", "progress");
        builder.HasIndex(e => new { e.ProjectId, e.PeriodStart }).IsUnique();
        builder.HasCheck("period", "period_end >= period_start AND due_date >= period_start");

        builder.HasOne<ProjectEntity>().WithMany().HasForeignKey(e => e.ProjectId).OnDelete(DeleteBehavior.Restrict);
    }
}
