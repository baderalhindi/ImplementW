namespace PMPlatform.Domain.ChangeRequest;

/// <summary>ERD <c>change_authorization.status</c>: ISSUED until its target module applies it, once. APPLIED, EXPIRED and REVOKED are final.</summary>
public enum ChangeAuthorizationStatus
{
    Issued = 1,
    Applied = 2,
    Expired = 3,
    Revoked = 4,
}
