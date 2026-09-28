namespace PMPlatform.Domain.Approval;

/// <summary>ERD <c>approval_instance.status</c>. Every state but PENDING is terminal.</summary>
public enum ApprovalInstanceStatus
{
    Pending = 1,
    Approved = 2,
    Rejected = 3,
    Returned = 4,
    Withdrawn = 5,
}
