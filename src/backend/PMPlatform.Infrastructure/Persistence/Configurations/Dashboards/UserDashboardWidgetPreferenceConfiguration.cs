using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PMPlatform.Domain.Dashboards;

namespace PMPlatform.Infrastructure.Persistence.Configurations.Dashboards;

internal sealed class UserDashboardWidgetPreferenceConfiguration : IEntityTypeConfiguration<UserDashboardWidgetPreference>
{
    public void Configure(EntityTypeBuilder<UserDashboardWidgetPreference> builder)
    {
        builder.ToTable("user_dashboard_widget_preference", "dashboards");
        builder.HasIndex(e => new { e.UserDashboardPreferenceId, e.DashboardWidgetId }).IsUnique();
        builder.HasCheck("sort_order", "sort_order IS NULL OR sort_order >= 1");

        builder.HasOne<UserDashboardPreference>().WithMany().HasForeignKey(e => e.UserDashboardPreferenceId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<DashboardWidget>().WithMany().HasForeignKey(e => e.DashboardWidgetId).OnDelete(DeleteBehavior.Restrict);
    }
}
