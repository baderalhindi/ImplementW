namespace PMPlatform.Domain.ExternalParticipation;

/// <summary>
/// ERD <c>external_contribution.status</c> (TASK-066): DRAFT → SUBMITTED → UNDER_REVIEW → RETURNED, REJECTED or
/// ACCEPTED_PENDING_APPLICATION, which ends APPLIED or APPLICATION_FAILED. A reference-only answer is APPLIED on its acceptance, as
/// nothing is applied to a source (WF-13 §4.5 step 8). RETURNED, REJECTED, APPLIED and APPLICATION_FAILED are final for the revision.
/// The specification's Conditional WITHDRAWN is not entered.
/// </summary>
public enum ExternalContributionStatus
{
    Draft = 1,
    Submitted = 2,
    UnderReview = 3,
    Returned = 4,
    Rejected = 5,
    AcceptedPendingApplication = 6,
    Applied = 7,
    ApplicationFailed = 8,
}
