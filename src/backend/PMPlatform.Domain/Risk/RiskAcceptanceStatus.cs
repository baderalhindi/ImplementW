namespace PMPlatform.Domain.Risk;

/// <summary>ERD <c>risk_acceptance.status</c>: ACTIVE until it EXPIRES on its date or is REVOKED; both are final.</summary>
public enum RiskAcceptanceStatus
{
    Active = 1,
    Expired = 2,
    Revoked = 3,
}
