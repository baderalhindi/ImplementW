using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PMPlatform.Domain.Project;

namespace PMPlatform.Infrastructure.Persistence.Configurations.Project;

internal sealed class ProjectImportValidationConfiguration : IEntityTypeConfiguration<ProjectImportValidation>
{
    public void Configure(EntityTypeBuilder<ProjectImportValidation> builder)
    {
        builder.ToTable("project_import_validation", "project");
        builder.HasRowVersion();
        builder.Property(e => e.FileName).HasMaxLength(255);
        builder.Property(e => e.Plan).HasColumnType("jsonb");
        builder.Property(e => e.Result).HasColumnType("jsonb");
        builder.Property(e => e.FailureCode).HasMaxLength(50);
        builder.HasCheck("result", "(status = 'COMPLETED') = (result IS NOT NULL) AND (status = 'FAILED') = (failure_code IS NOT NULL)");

        // The worker's queue, oldest first (indexing-strategy I-64).
        builder.HasIndex(e => new { e.Status, e.CreatedAt, e.Id });
    }
}
