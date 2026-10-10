using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PMPlatform.Domain.Dashboards;

namespace PMPlatform.Infrastructure.Persistence.Configurations.Dashboards;

internal sealed class DashboardDefinitionConfiguration : IEntityTypeConfiguration<DashboardDefinition>
{
    public const string PublishedKey = "ix_dashboard_definition_published";

    public const string OpenVersionKey = "ix_dashboard_definition_open_code";

    public void Configure(EntityTypeBuilder<DashboardDefinition> builder)
    {
        builder.ToTable("dashboard_definition", "dashboards");
        builder.HasBilingualLabel(e => e.Name, "name");
        builder.HasBilingualLabel(e => e.Description, "description");
        builder.HasIndex(e => new { e.Code, e.VersionNo }).IsUnique();

        // The runtime's lookup: a code's PUBLISHED version. Not unique: publishing retires the version it replaces in the same save,
        // and a unique index would refuse that save whenever the new row happened to be written first. One PUBLISHED version per
        // code is held at commit by the guard (TASK-069 migration 3).
        builder.HasIndex(e => e.Code, PublishedKey).HasFilter("lifecycle_state = 'PUBLISHED'").HasDatabaseName(PublishedKey);

        // One version on its way per dashboard, DRAFT or VALIDATED, for any writer.
        builder.HasIndex(e => e.Code, OpenVersionKey).IsUnique().HasFilter("lifecycle_state IN ('DRAFT', 'VALIDATED')").HasDatabaseName(OpenVersionKey);

        builder.HasCheck("version_no", "version_no >= 1");

        // ADR-019: personalisation is the Portfolio Dashboard's alone.
        builder.HasCheck("allows_personalization", "NOT allows_personalization OR code = 'PORTFOLIO'");

        builder.HasGovernedLifecycle();
        builder.HasRowVersion();
    }
}
