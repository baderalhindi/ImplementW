namespace PMPlatform.Domain.FinancialKpi;

/// <summary>
/// ERD <c>value_status</c> on financial updates and KPI measurements: whether there is a figure, and if not, why. A figure is
/// present exactly when MEASURED; MISSING, STALE and NOT_APPLICABLE carry none and are never read as zero (TASK-052).
/// </summary>
public enum ValueStatus
{
    Measured = 1,
    Missing = 2,
    Stale = 3,
    NotApplicable = 4,
}
