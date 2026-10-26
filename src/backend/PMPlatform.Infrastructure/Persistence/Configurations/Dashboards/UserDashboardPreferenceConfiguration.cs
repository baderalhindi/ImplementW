using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PMPlatform.Domain.Dashboards;
using PMPlatform.Domain.IdentityAccess;

namespace PMPlatform.Infrastructure.Persistence.Configurations.Dashboards;

internal sealed class UserDashboardPreferenceConfiguration : IEntityTypeConfiguration<UserDashboardPreference>
{
    public void Configure(EntityTypeBuilder<UserDashboardPreference> builder)
    {
        builder.ToTable("user_dashboard_preference", "dashboards");
        builder.HasIndex(e => new { e.UserId, e.DashboardDefinitionId }).IsUnique();

        builder.HasOne<DashboardDefinition>().WithMany().HasForeignKey(e => e.DashboardDefinitionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Restrict);
    }
}
