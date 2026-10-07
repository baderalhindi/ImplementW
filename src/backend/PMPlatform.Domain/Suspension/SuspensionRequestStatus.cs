namespace PMPlatform.Domain.Suspension;

/// <summary>
/// ERD <c>suspension_request.status</c> (TASK-062): Draft → Submitted → Under Review → Returned / Approved / Rejected, Withdrawn, and
/// Approved → Effected. APPROVED is WF-11's decision; EFFECTED is the project's lifecycle transition, a later and separate step
/// (WF-09 APPROVED_PENDING_ACTIVATION and ACTIVATED). REJECTED, WITHDRAWN and EFFECTED are final.
/// </summary>
public enum SuspensionRequestStatus
{
    Draft = 1,
    Submitted = 2,
    UnderReview = 3,
    Returned = 4,
    Approved = 5,
    Rejected = 6,
    Withdrawn = 7,
    Effected = 8,
}
