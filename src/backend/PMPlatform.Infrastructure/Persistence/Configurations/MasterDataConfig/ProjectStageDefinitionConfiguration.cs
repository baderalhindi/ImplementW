using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PMPlatform.Domain.MasterDataConfig;

namespace PMPlatform.Infrastructure.Persistence.Configurations.MasterDataConfig;

internal sealed class ProjectStageDefinitionConfiguration : IEntityTypeConfiguration<ProjectStageDefinition>
{
    public void Configure(EntityTypeBuilder<ProjectStageDefinition> builder)
    {
        builder.ToTable("project_stage_definition", "master_data_config");
        builder.Property(e => e.Code).HasMaxLength(50);
        builder.HasIndex(e => new { e.ConfigurationVersionId, e.Code }).IsUnique();
        builder.HasBilingualLabel(e => e.Name, "name");
        builder.Property(e => e.WeightPercent).HasPrecision(5, 2);
        builder.HasCheck("weight_percent", "weight_percent BETWEEN 0 AND 100");

        builder.HasOne<ConfigurationVersion>().WithMany().HasForeignKey(e => e.ConfigurationVersionId).OnDelete(DeleteBehavior.Restrict);
    }
}
