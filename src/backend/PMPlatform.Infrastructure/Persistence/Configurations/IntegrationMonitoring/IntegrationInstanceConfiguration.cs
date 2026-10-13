using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PMPlatform.Domain.IntegrationMonitoring;

namespace PMPlatform.Infrastructure.Persistence.Configurations.IntegrationMonitoring;

internal sealed class IntegrationInstanceConfiguration : IEntityTypeConfiguration<IntegrationInstance>
{
    public void Configure(EntityTypeBuilder<IntegrationInstance> builder)
    {
        builder.ToTable("integration_instance", "integration_monitoring");
        builder.Property(e => e.Code).HasMaxLength(50);
        builder.Property(e => e.EndpointConfigurationKey).HasMaxLength(200);
        builder.Property(e => e.IsEnabled).HasDatabaseDefault(true);
        builder.HasIndex(e => new { e.IntegrationDefinitionId, e.Code }).IsUnique();

        builder.HasOne<IntegrationDefinition>().WithMany().HasForeignKey(e => e.IntegrationDefinitionId).OnDelete(DeleteBehavior.Restrict);

        // Health is rewritten by each recorded attempt and by the health pass; the instance row is locked for it.
        builder.HasRowVersion();
    }
}
