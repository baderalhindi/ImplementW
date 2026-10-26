namespace PMPlatform.Domain.Progress;

/// <summary>
/// The review and publication workflow of one revision of a period's progress (TASK-044). RETURNED and PUBLISHED are
/// final: a returned revision is followed by revision + 1.
/// </summary>
public enum ProgressSubmissionStatus
{
    Draft = 1,
    Submitted = 2,
    UnderReview = 3,
    Returned = 4,
    Published = 5,
}
