using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PMPlatform.Domain.IdentityAccess;
using PMPlatform.Domain.IntegrationMonitoring;

namespace PMPlatform.Infrastructure.Persistence.Configurations.IntegrationMonitoring;

internal sealed class IntegrationDefinitionConfiguration : IEntityTypeConfiguration<IntegrationDefinition>
{
    public void Configure(EntityTypeBuilder<IntegrationDefinition> builder)
    {
        builder.ToTable("integration_definition", "integration_monitoring");
        builder.Property(e => e.Code).HasMaxLength(50);
        builder.HasIndex(e => e.Code).IsUnique();
        builder.HasBilingualLabel(e => e.Name, "name");
        builder.Property(e => e.MaxAttempts).HasDatabaseDefault((short)3);
        builder.Property(e => e.RetryBackoffSeconds).HasDatabaseDefault(60);
        builder.HasCheck("max_attempts", "max_attempts >= 1");
        builder.HasCheck("retry_backoff_seconds", "retry_backoff_seconds >= 0");

        builder.HasOne<Role>().WithMany().HasForeignKey(e => e.OwnerRoleId).OnDelete(DeleteBehavior.Restrict);

        // Enabling and disabling (MOD-101) are commands on the row; two at once cannot both commit.
        builder.HasRowVersion();
    }
}
