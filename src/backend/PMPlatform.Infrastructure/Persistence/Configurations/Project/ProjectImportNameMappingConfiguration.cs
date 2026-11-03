using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PMPlatform.Domain.Project;

namespace PMPlatform.Infrastructure.Persistence.Configurations.Project;

internal sealed class ProjectImportNameMappingConfiguration : IEntityTypeConfiguration<ProjectImportNameMapping>
{
    public void Configure(EntityTypeBuilder<ProjectImportNameMapping> builder)
    {
        builder.ToTable("project_import_name_mapping", "project");
        builder.Property(e => e.Dimension).HasMaxLength(50);
        builder.Property(e => e.SourceKey).HasMaxLength(200);
        builder.Property(e => e.TargetValue).HasMaxLength(50);
        builder.HasIndex(e => new { e.Dimension, e.SourceKey }).IsUnique();
    }
}
