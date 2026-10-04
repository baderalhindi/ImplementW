namespace PMPlatform.Domain.FinancialKpi;

/// <summary>ERD <c>kpi_assignment.status</c>: ACTIVE and SUSPENDED move both ways; RETIRED is final.</summary>
public enum KpiAssignmentStatus
{
    Active = 1,
    Suspended = 2,
    Retired = 3,
}
