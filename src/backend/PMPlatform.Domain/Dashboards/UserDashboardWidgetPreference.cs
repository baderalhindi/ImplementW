using PMPlatform.Domain.Common;

namespace PMPlatform.Domain.Dashboards;

/// <summary>The hidden and ordered state of one optional widget in a user's preference (ADR-019). Delete policy: CASCADE.</summary>
public sealed class UserDashboardWidgetPreference : AuditedEntity
{
    public Guid UserDashboardPreferenceId { get; set; }

    public Guid DashboardWidgetId { get; set; }

    public bool IsHidden { get; set; }

    /// <summary>The widget's place among the user's ordered widgets, from 1; null keeps the governed order.</summary>
    public short? SortOrder { get; set; }
}
