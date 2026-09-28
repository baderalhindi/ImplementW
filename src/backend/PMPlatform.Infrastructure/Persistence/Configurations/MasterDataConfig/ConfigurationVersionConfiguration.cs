using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PMPlatform.Domain.MasterDataConfig;

namespace PMPlatform.Infrastructure.Persistence.Configurations.MasterDataConfig;

internal sealed class ConfigurationVersionConfiguration : IEntityTypeConfiguration<ConfigurationVersion>
{
    public void Configure(EntityTypeBuilder<ConfigurationVersion> builder)
    {
        builder.ToTable("configuration_version", "master_data_config");
        builder.HasRowVersion();
        builder.HasIndex(e => new { e.ConfigurationFamilyId, e.VersionNo }).IsUnique();
        builder.HasNarrative(e => e.ChangeSummary, "change_summary");
        builder.HasGovernedLifecycle();

        // TASK-034: a version has an effective-from exactly when it was published; a retirement's effective-to is not
        // before it (equal when withdrawn before taking effect). The order of publications and the immutability of a
        // PUBLISHED version and its content are enforced by trigger (migration TASK-034_GuardConfigurationHistory).
        builder.HasCheck("effective_from", "(published_at IS NULL) = (effective_from IS NULL)");
        builder.HasCheck("effective_to", "effective_to IS NULL OR effective_to >= effective_from");

        builder.HasOne<ConfigurationFamily>().WithMany().HasForeignKey(e => e.ConfigurationFamilyId).OnDelete(DeleteBehavior.Restrict);

        // TASK-026 (indexing-strategy.md I-10): TASK-034's as-of resolution, the latest PUBLISHED version of a family
        // effective at a date. The default name is longer than PostgreSQL's 63 characters.
        builder.HasIndex(e => new { e.ConfigurationFamilyId, e.LifecycleState, e.EffectiveFrom })
            .HasDatabaseName("ix_configuration_version_family_state_effective_from");
    }
}
