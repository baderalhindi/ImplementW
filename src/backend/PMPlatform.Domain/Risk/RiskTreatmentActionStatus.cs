namespace PMPlatform.Domain.Risk;

/// <summary>ERD <c>risk_treatment_action.status</c>: PLANNED → IN_PROGRESS → COMPLETED, and CANCELLED from either open state. COMPLETED and CANCELLED are final.</summary>
public enum RiskTreatmentActionStatus
{
    Planned = 1,
    InProgress = 2,
    Completed = 3,
    Cancelled = 4,
}
