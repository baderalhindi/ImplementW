using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PMPlatform.Domain.FinancialKpi;
using PMPlatform.Domain.IdentityAccess;
using PMPlatform.Domain.MasterDataConfig;
using ProjectEntity = PMPlatform.Domain.Project.Project;

namespace PMPlatform.Infrastructure.Persistence.Configurations.FinancialKpi;

internal sealed class KpiAssignmentConfiguration : IEntityTypeConfiguration<KpiAssignment>
{
    public void Configure(EntityTypeBuilder<KpiAssignment> builder)
    {
        builder.ToTable("kpi_assignment", "financial_kpi");
        builder.HasRowVersion();
        builder.HasIndex(e => new { e.ProjectId, e.KpiDefinitionId }).IsUnique();

        builder.HasOne<ProjectEntity>().WithMany().HasForeignKey(e => e.ProjectId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<KpiDefinition>().WithMany().HasForeignKey(e => e.KpiDefinitionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.OwnerUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<MasterDataItem>().WithMany().HasForeignKey(e => e.MeasurementFrequencyItemId).OnDelete(DeleteBehavior.Restrict);
    }
}
