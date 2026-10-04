namespace PMPlatform.Domain.FinancialKpi;

/// <summary>ERD <c>kpi_measurement.status</c>: DRAFT → SUBMITTED → PUBLISHED. PUBLISHED is final.</summary>
public enum KpiMeasurementStatus
{
    Draft = 1,
    Submitted = 2,
    Published = 3,
}
