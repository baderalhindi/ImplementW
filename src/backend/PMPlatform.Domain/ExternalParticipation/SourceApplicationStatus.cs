namespace PMPlatform.Domain.ExternalParticipation;

/// <summary>
/// ERD <c>source_application.status</c>, as an attempt ends (TASK-066). An attempt is made and decided in one request, so the ERD's
/// PENDING, REVALIDATING and RETRY_SCHEDULED — states of an attempt still on its way — are not entered; a revalidated CONFLICT stays
/// CONFLICT with <see cref="SourceApplication.RevalidatedAt"/> set.
/// </summary>
public enum SourceApplicationStatus
{
    Applied = 1,
    Conflict = 2,
    Failed = 3,
}
