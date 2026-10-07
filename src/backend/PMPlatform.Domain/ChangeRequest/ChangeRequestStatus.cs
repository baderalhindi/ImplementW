namespace PMPlatform.Domain.ChangeRequest;

/// <summary>
/// ERD <c>change_request.status</c> (TASK-060): Draft → Submitted → Under Review → Returned / Approved / Rejected → Implementation →
/// Implemented → Closed, and Withdrawn. REJECTED, WITHDRAWN and CLOSED are final.
/// </summary>
public enum ChangeRequestStatus
{
    Draft = 1,
    Submitted = 2,
    UnderReview = 3,
    Returned = 4,
    Approved = 5,
    Rejected = 6,
    Implementation = 7,
    Implemented = 8,
    Closed = 9,
    Withdrawn = 10,
}
