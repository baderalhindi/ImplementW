using PMPlatform.Domain.Progress;

namespace PMPlatform.Application.Features.Progress;

/// <summary>
/// The review and publication workflow (TASK-044). A revision is born DRAFT, or SUBMITTED when it is the ADR-014 opening
/// position, and moves forward only: each edge is one command of <see cref="ProgressService"/>. RETURNED and PUBLISHED are
/// final. A returned period continues as revision + 1. Migration <c>TASK-044_GuardProgressHistory</c> refuses every other
/// change in the database too.
/// </summary>
internal static class ProgressWorkflow
{
    public static IReadOnlySet<(ProgressSubmissionStatus From, ProgressSubmissionStatus To)> Transitions { get; } = new HashSet<(ProgressSubmissionStatus, ProgressSubmissionStatus)>
    {
        (ProgressSubmissionStatus.Draft, ProgressSubmissionStatus.Submitted),         // submit
        (ProgressSubmissionStatus.Submitted, ProgressSubmissionStatus.UnderReview),   // start-review
        (ProgressSubmissionStatus.UnderReview, ProgressSubmissionStatus.Returned),    // return, opening revision + 1
        (ProgressSubmissionStatus.UnderReview, ProgressSubmissionStatus.Published),   // publish, writing the snapshot
    };

    public static bool Allows(ProgressSubmissionStatus from, ProgressSubmissionStatus to) => Transitions.Contains((from, to));
}
