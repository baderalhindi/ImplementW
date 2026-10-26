namespace PMPlatform.Domain.Dashboards;

/// <summary>
/// ERD <c>dashboard_widget.widget_type</c>: the allowlisted visualisations of FG-01 §10. A widget is a presentation of a registered
/// projection, never code: no type here runs a query, a formula or a script (BR-DSH-030). MAP is Conditional (§10) and not offered.
/// </summary>
public enum DashboardWidgetType
{
    /// <summary>A single value or state, with its unit, semantic state and freshness.</summary>
    MetricCard = 1,

    /// <summary>A count by a governed categorical dimension.</summary>
    StatusDistribution = 2,

    BarColumn = 3,

    /// <summary>A source-provided historical series; never reconstructed from current records (BR-DSH-018).</summary>
    LineTrend = 4,

    /// <summary>A small categorical composition; the SPA offers its table alternative.</summary>
    DonutPie = 5,

    /// <summary>A source-owned percentage or ratio only; never an invented denominator.</summary>
    ProgressIndicator = 6,
}
