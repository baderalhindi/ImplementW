using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PMPlatform.Domain.Reports;

namespace PMPlatform.Infrastructure.Persistence.Configurations.Reports;

internal sealed class ReportParameterValueConfiguration : IEntityTypeConfiguration<ReportParameterValue>
{
    public void Configure(EntityTypeBuilder<ReportParameterValue> builder)
    {
        builder.ToTable("report_parameter_value", "reports");
        builder.Property(e => e.ParameterCode).HasMaxLength(50);
        builder.Property(e => e.ValueText).HasMaxLength(500);
        builder.HasIndex(e => new { e.SavedViewId, e.ParameterCode }).IsUnique();

        builder.HasOne<SavedView>().WithMany().HasForeignKey(e => e.SavedViewId).OnDelete(DeleteBehavior.Cascade);
    }
}
