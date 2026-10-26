using PMPlatform.Domain.Common;

namespace PMPlatform.Domain.Dashboards;

/// <summary>
/// One version of a governed dashboard (FG-01 §13.1, ERD <c>dashboards.dashboard_definition</c>): its audience, its layout and the
/// registered source projections its widgets present. Under the governed lifecycle (DRAFT → VALIDATED → PUBLISHED → RETIRED; author,
/// reviewer and publisher differ). A PUBLISHED version is immutable; a change is a new version, and publishing it retires the one it
/// replaces. No column here holds a business value: a widget names the projection its value comes from (ERD §5.19). Delete
/// policy: RETAIN.
/// </summary>
public sealed class DashboardDefinition : GovernedEntity
{
    public DashboardCode Code { get; set; }

    /// <summary>1 for a code's first version, one more for each after it.</summary>
    public int VersionNo { get; set; }

    public required BilingualLabel Name { get; set; }

    public BilingualLabel? Description { get; set; }

    /// <summary>ADR-019: the Portfolio Dashboard alone lets its users hide and reorder the widgets marked optional.</summary>
    public bool AllowsPersonalization { get; set; }
}
