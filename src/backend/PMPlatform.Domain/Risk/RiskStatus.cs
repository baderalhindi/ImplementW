namespace PMPlatform.Domain.Risk;

/// <summary>ERD <c>risk.status</c> (TASK-055): Identified → Assessed → Treatment/Monitoring → Closed. CLOSED is left only by a reopen.</summary>
public enum RiskStatus
{
    Identified = 1,
    Assessed = 2,
    Treatment = 3,
    Monitoring = 4,
    Closed = 5,
}
