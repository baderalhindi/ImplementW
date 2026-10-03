namespace PMPlatform.Domain.Milestone;

/// <summary>
/// ERD <c>milestone_achievement.status</c> (TASK-050). A revision is born DRAFT and SUBMITTED to WF-11; the outcome makes it
/// ACCEPTED or RETURNED. An ACCEPTED revision becomes SUPERSEDED when a later revision of the same milestone is accepted.
/// RETURNED and SUPERSEDED are final: a correction is always a new revision, never an edit.
/// </summary>
public enum MilestoneAchievementStatus
{
    Draft = 1,
    Submitted = 2,
    Returned = 3,
    Accepted = 4,
    Superseded = 5,
}
