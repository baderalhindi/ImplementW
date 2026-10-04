using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PMPlatform.Domain.FinancialKpi;
using ProjectEntity = PMPlatform.Domain.Project.Project;

namespace PMPlatform.Infrastructure.Persistence.Configurations.FinancialKpi;

internal sealed class FinancialSourceModeConfiguration : IEntityTypeConfiguration<FinancialSourceMode>
{
    public void Configure(EntityTypeBuilder<FinancialSourceMode> builder)
    {
        builder.ToTable("financial_source_mode", "financial_kpi");
        builder.HasRowVersion();
        builder.HasIndex(e => new { e.ProjectId, e.FieldCode }).IsUnique();

        builder.HasOne<ProjectEntity>().WithMany().HasForeignKey(e => e.ProjectId).OnDelete(DeleteBehavior.Restrict);
    }
}
