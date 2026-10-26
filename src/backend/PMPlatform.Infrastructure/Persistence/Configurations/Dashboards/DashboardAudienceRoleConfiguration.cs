using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PMPlatform.Domain.Dashboards;
using PMPlatform.Domain.IdentityAccess;

namespace PMPlatform.Infrastructure.Persistence.Configurations.Dashboards;

internal sealed class DashboardAudienceRoleConfiguration : IEntityTypeConfiguration<DashboardAudienceRole>
{
    public void Configure(EntityTypeBuilder<DashboardAudienceRole> builder)
    {
        builder.ToTable("dashboard_audience_role", "dashboards");
        builder.HasIndex(e => new { e.DashboardDefinitionId, e.RoleId }).IsUnique();

        builder.HasOne<DashboardDefinition>().WithMany().HasForeignKey(e => e.DashboardDefinitionId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Role>().WithMany().HasForeignKey(e => e.RoleId).OnDelete(DeleteBehavior.Restrict);
    }
}
