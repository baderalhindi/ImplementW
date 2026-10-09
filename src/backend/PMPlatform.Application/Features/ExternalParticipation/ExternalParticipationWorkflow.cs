using PMPlatform.Application.Features.ExternalParticipation.Contracts;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Domain.ExternalParticipation;

namespace PMPlatform.Application.Features.ExternalParticipation;

/// <summary>
/// The request's and the revision's state machines (TASK-066). Migration <c>TASK-066_GuardExternalParticipationHistory</c> refuses every
/// other change of status in the database too.
/// </summary>
internal static class ExternalParticipationWorkflow
{
    /// <summary>
    /// The request: DRAFT → ISSUED → IN_PROGRESS (an answer drafted) → RESPONDED (submitted) → IN_PROGRESS again on a return, or CLOSED on a
    /// rejection or once its answer is applied or found impossible to apply; ISSUED or IN_PROGRESS → CANCELLED.
    /// </summary>
    public static IReadOnlySet<(ExternalUpdateRequestStatus From, ExternalUpdateRequestStatus To)> RequestTransitions { get; } =
        new HashSet<(ExternalUpdateRequestStatus, ExternalUpdateRequestStatus)>
        {
            (ExternalUpdateRequestStatus.Draft, ExternalUpdateRequestStatus.Issued),
            (ExternalUpdateRequestStatus.Issued, ExternalUpdateRequestStatus.InProgress),
            (ExternalUpdateRequestStatus.InProgress, ExternalUpdateRequestStatus.Responded),
            (ExternalUpdateRequestStatus.Responded, ExternalUpdateRequestStatus.InProgress),
            (ExternalUpdateRequestStatus.Responded, ExternalUpdateRequestStatus.Closed),
            (ExternalUpdateRequestStatus.Issued, ExternalUpdateRequestStatus.Cancelled),
            (ExternalUpdateRequestStatus.InProgress, ExternalUpdateRequestStatus.Cancelled),
        };

    /// <summary>
    /// A revision: DRAFT → SUBMITTED → UNDER_REVIEW → RETURNED, REJECTED, ACCEPTED_PENDING_APPLICATION, or APPLIED at once for a reference-only
    /// answer; ACCEPTED_PENDING_APPLICATION → APPLIED or APPLICATION_FAILED. No edge leaves a decided revision, and none returns to DRAFT.
    /// </summary>
    public static IReadOnlySet<(ExternalContributionStatus From, ExternalContributionStatus To)> ContributionTransitions { get; } =
        new HashSet<(ExternalContributionStatus, ExternalContributionStatus)>
        {
            (ExternalContributionStatus.Draft, ExternalContributionStatus.Submitted),
            (ExternalContributionStatus.Submitted, ExternalContributionStatus.UnderReview),
            (ExternalContributionStatus.UnderReview, ExternalContributionStatus.Returned),
            (ExternalContributionStatus.UnderReview, ExternalContributionStatus.Rejected),
            (ExternalContributionStatus.UnderReview, ExternalContributionStatus.AcceptedPendingApplication),
            (ExternalContributionStatus.UnderReview, ExternalContributionStatus.Applied),
            (ExternalContributionStatus.AcceptedPendingApplication, ExternalContributionStatus.Applied),
            (ExternalContributionStatus.AcceptedPendingApplication, ExternalContributionStatus.ApplicationFailed),
        };

    public static bool IsFinal(ExternalUpdateRequestStatus status) => status is ExternalUpdateRequestStatus.Closed or ExternalUpdateRequestStatus.Cancelled;

    /// <summary>Waiting on the entity: a due date is due only then.</summary>
    public static bool AwaitsEntity(ExternalUpdateRequestStatus status) => status is ExternalUpdateRequestStatus.Issued or ExternalUpdateRequestStatus.InProgress;

    /// <summary>Its responder and reviewer may be replaced while it is issued and not final.</summary>
    public static bool IsAssignable(ExternalUpdateRequestStatus status) => !IsFinal(status) && status != ExternalUpdateRequestStatus.Draft;

    /// <summary>Where <paramref name="command"/> takes a revision in <paramref name="from"/>, or why it does not.</summary>
    public static AdministrationResult<StatusOf> TargetOf(ContributionCommand command, ExternalContributionStatus from, ContributionApplicationMode mode) =>
        (command, from) switch
        {
            (ContributionCommand.Submit, ExternalContributionStatus.Draft) => new StatusOf(ExternalContributionStatus.Submitted),
            (ContributionCommand.Submit, _) => AdministrationError.Conflict(ExternalParticipationErrorCodes.ContributionAlreadySubmitted),
            (ContributionCommand.StartReview, ExternalContributionStatus.Submitted) => new StatusOf(ExternalContributionStatus.UnderReview),
            (ContributionCommand.Accept, ExternalContributionStatus.UnderReview) => new StatusOf(mode == ContributionApplicationMode.ReferenceOnly
                ? ExternalContributionStatus.Applied
                : ExternalContributionStatus.AcceptedPendingApplication),
            (ContributionCommand.Return, ExternalContributionStatus.UnderReview) => new StatusOf(ExternalContributionStatus.Returned),
            (ContributionCommand.Reject, ExternalContributionStatus.UnderReview) => new StatusOf(ExternalContributionStatus.Rejected),
            (_, ExternalContributionStatus.Draft or ExternalContributionStatus.Submitted or ExternalContributionStatus.UnderReview) => AdministrationError.InvalidTransition,
            _ => AdministrationError.Conflict(ExternalParticipationErrorCodes.ContributionAlreadyDecided),
        };
}

/// <summary>A revision's next status, as a reference type for <see cref="AdministrationResult{T}"/>.</summary>
internal sealed record StatusOf(ExternalContributionStatus Status);

/// <summary>The commands that move a revision along its state machine, one per edge family (api-conventions R-4).</summary>
internal enum ContributionCommand
{
    Submit = 1,
    StartReview = 2,
    Accept = 3,
    Return = 4,
    Reject = 5,
}
