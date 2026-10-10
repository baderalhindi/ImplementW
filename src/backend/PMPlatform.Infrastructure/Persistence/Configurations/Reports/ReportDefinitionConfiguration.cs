using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PMPlatform.Domain.Reports;

namespace PMPlatform.Infrastructure.Persistence.Configurations.Reports;

internal sealed class ReportDefinitionConfiguration : IEntityTypeConfiguration<ReportDefinition>
{
    public const string PublishedKey = "ix_report_definition_published";

    public const string OpenVersionKey = "ix_report_definition_open_code";

    public void Configure(EntityTypeBuilder<ReportDefinition> builder)
    {
        builder.ToTable("report_definition", "reports");
        builder.HasBilingualLabel(e => e.Name, "name");
        builder.HasBilingualLabel(e => e.Description, "description");
        builder.Property(e => e.PrimaryProjectionCode).HasMaxLength(100);
        builder.HasIndex(e => new { e.Code, e.VersionNo }).IsUnique();

        // The runtime's lookup: a code's PUBLISHED version. Not unique: publishing retires the version it replaces in the same save; one
        // PUBLISHED version per code is held at commit by the guard (TASK-071 migration 3).
        builder.HasIndex(e => e.Code, PublishedKey).HasFilter("lifecycle_state = 'PUBLISHED'").HasDatabaseName(PublishedKey);

        // One version on its way per report, DRAFT or VALIDATED, for any writer.
        builder.HasIndex(e => e.Code, OpenVersionKey).IsUnique().HasFilter("lifecycle_state IN ('DRAFT', 'VALIDATED')").HasDatabaseName(OpenVersionKey);

        builder.HasCheck("version_no", "version_no >= 1");

        // A registered projection's code, never an expression (BR-RPT-046); the application refuses an unregistered one at validation.
        builder.HasCheck("primary_projection_code", "primary_projection_code ~ '^[A-Z][A-Z_]*\\.[A-Z][A-Z_]*$'");

        builder.HasGovernedLifecycle();
        builder.HasRowVersion();
    }
}
