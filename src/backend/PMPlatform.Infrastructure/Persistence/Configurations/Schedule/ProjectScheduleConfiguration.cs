using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PMPlatform.Domain.MasterDataConfig;
using PMPlatform.Domain.Schedule;
using ProjectEntity = PMPlatform.Domain.Project.Project;

namespace PMPlatform.Infrastructure.Persistence.Configurations.Schedule;

internal sealed class ProjectScheduleConfiguration : IEntityTypeConfiguration<ProjectSchedule>
{
    public void Configure(EntityTypeBuilder<ProjectSchedule> builder)
    {
        builder.ToTable("project_schedule", "schedule");
        builder.HasRowVersion();
        builder.HasIndex(e => e.ProjectId).IsUnique();

        builder.HasOne<ProjectEntity>().WithMany().HasForeignKey(e => e.ProjectId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<MasterDataItem>().WithMany().HasForeignKey(e => e.CalendarItemId).OnDelete(DeleteBehavior.Restrict);
    }
}
