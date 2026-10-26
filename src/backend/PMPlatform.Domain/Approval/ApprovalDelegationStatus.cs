namespace PMPlatform.Domain.Approval;

/// <summary>ERD <c>approval_delegation.status</c>. An ACTIVE delegation conveys authority only inside its period.</summary>
public enum ApprovalDelegationStatus
{
    Active = 1,
    Revoked = 2,
    Expired = 3,
}
