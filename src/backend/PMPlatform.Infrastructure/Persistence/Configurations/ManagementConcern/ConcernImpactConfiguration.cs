using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PMPlatform.Domain.ManagementConcern;
using PMPlatform.Domain.MasterDataConfig;
using ConcernEntity = PMPlatform.Domain.ManagementConcern.ManagementConcern;

namespace PMPlatform.Infrastructure.Persistence.Configurations.ManagementConcern;

internal sealed class ConcernImpactConfiguration : IEntityTypeConfiguration<ConcernImpact>
{
    public void Configure(EntityTypeBuilder<ConcernImpact> builder)
    {
        builder.ToTable("concern_impact", "management_concern");
        builder.HasNarrative(e => e.Rationale, "rationale");

        // ADR-011: one row per dimension of a concern.
        builder.HasIndex(e => new { e.ManagementConcernId, e.ImpactDimensionItemId }).IsUnique().HasDatabaseName("ix_concern_impact_concern_dimension");

        builder.HasCheck("impact_level", "impact_level BETWEEN 1 AND 5");

        builder.HasOne<ConcernEntity>().WithMany().HasForeignKey(e => e.ManagementConcernId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<MasterDataItem>().WithMany().HasForeignKey(e => e.ImpactDimensionItemId).OnDelete(DeleteBehavior.Restrict);
    }
}
