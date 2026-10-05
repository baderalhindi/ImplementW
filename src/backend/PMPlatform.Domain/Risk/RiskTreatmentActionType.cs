namespace PMPlatform.Domain.Risk;

/// <summary>ERD <c>risk_treatment_action.action_type</c>. Accepting a risk is not an action: it is a <see cref="RiskAcceptance"/>.</summary>
public enum RiskTreatmentActionType
{
    Mitigate = 1,
    Avoid = 2,
    Transfer = 3,
    Contingency = 4,
}
