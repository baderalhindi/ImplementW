namespace PMPlatform.Domain.Schedule;

/// <summary>
/// ERD <c>project_baseline.status</c>. A candidate is DRAFT, SUBMITTED to WF-11, and ends ACTIVE, RETURNED (resubmitted
/// as the next revision), REJECTED or WITHDRAWN; an ACTIVE baseline becomes SUPERSEDED when another activates.
/// UNDER_REVIEW is in the ERD's value set but no edge reaches it: WF-11 gives the source no signal between start and
/// outcome (schedule-baseline.md F-8).
/// </summary>
public enum ProjectBaselineStatus
{
    Draft = 1,
    Submitted = 2,
    UnderReview = 3,
    Returned = 4,
    Active = 5,
    Superseded = 6,
    Rejected = 7,
    Withdrawn = 8,
}
