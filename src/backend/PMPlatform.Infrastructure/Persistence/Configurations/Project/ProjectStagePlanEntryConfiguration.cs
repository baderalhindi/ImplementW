using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PMPlatform.Domain.Project;
using ProjectEntity = PMPlatform.Domain.Project.Project;

namespace PMPlatform.Infrastructure.Persistence.Configurations.Project;

internal sealed class ProjectStagePlanEntryConfiguration : IEntityTypeConfiguration<ProjectStagePlanEntry>
{
    public void Configure(EntityTypeBuilder<ProjectStagePlanEntry> builder)
    {
        builder.ToTable("project_stage_plan", "project");
        builder.Property(e => e.StageCode).HasMaxLength(50);
        builder.Property(e => e.StartingPercent).HasPrecision(5, 2);
        builder.HasCheck("dates", "planned_start_date IS NULL OR planned_end_date IS NULL OR planned_start_date <= planned_end_date");
        builder.HasCheck("starting_percent", "starting_percent IS NULL OR starting_percent BETWEEN 0 AND 100");

        // A project's plan is read whole, one row per stage, and is part of the project: deleting a draft or rolling back an import deletes it too.
        builder.HasOne<ProjectEntity>().WithMany().HasForeignKey(e => e.ProjectId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(e => new { e.ProjectId, e.StageCode }).IsUnique();
    }
}
