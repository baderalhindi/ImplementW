using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PMPlatform.Domain.IdentityAccess;
using PMPlatform.Domain.Reports;

namespace PMPlatform.Infrastructure.Persistence.Configurations.Reports;

internal sealed class ReportAudienceRoleConfiguration : IEntityTypeConfiguration<ReportAudienceRole>
{
    public void Configure(EntityTypeBuilder<ReportAudienceRole> builder)
    {
        builder.ToTable("report_audience_role", "reports");
        builder.HasIndex(e => new { e.ReportDefinitionId, e.RoleId }).IsUnique();

        builder.HasOne<ReportDefinition>().WithMany().HasForeignKey(e => e.ReportDefinitionId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Role>().WithMany().HasForeignKey(e => e.RoleId).OnDelete(DeleteBehavior.Restrict);
    }
}
