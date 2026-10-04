namespace PMPlatform.Domain.FinancialKpi;

/// <summary>
/// ERD <c>kpi_measurement.rag_status</c>, rated against the pinned target version. UNKNOWN when there is no figure or no
/// thresholds to rate it by, NOT_APPLICABLE when the KPI does not apply to the period: neither is ever coerced to a colour.
/// </summary>
public enum KpiRagStatus
{
    Green = 1,
    Amber = 2,
    Red = 3,
    Unknown = 4,
    NotApplicable = 5,
}
