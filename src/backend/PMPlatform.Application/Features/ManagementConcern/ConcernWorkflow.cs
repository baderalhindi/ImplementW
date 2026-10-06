using PMPlatform.Domain.ManagementConcern;

namespace PMPlatform.Application.Features.ManagementConcern;

/// <summary>
/// A concern's state machine (TASK-057): OPEN → ASSIGNED → IN_PROGRESS → PENDING_VALIDATION → RESOLVED → CLOSED. WF-11's approval
/// takes PENDING_VALIDATION to RESOLVED; its return, rejection or withdrawal takes it back to IN_PROGRESS, as the next revision.
/// Reassignment, assessment and review keep the state. Migration <c>TASK-057_GuardManagementConcernHistory</c> refuses every other
/// change of status in the database too.
/// </summary>
internal static class ConcernWorkflow
{
    public static IReadOnlySet<(ConcernStatus From, ConcernStatus To)> Transitions { get; } = new HashSet<(ConcernStatus, ConcernStatus)>
    {
        (ConcernStatus.Open, ConcernStatus.Assigned),                    // assign
        (ConcernStatus.Assigned, ConcernStatus.InProgress),              // start
        (ConcernStatus.InProgress, ConcernStatus.PendingValidation),     // submit-resolution, starting the WF-11 run
        (ConcernStatus.PendingValidation, ConcernStatus.Resolved),       // WF-11 approved
        (ConcernStatus.PendingValidation, ConcernStatus.InProgress),     // WF-11 returned, rejected or withdrawn: the next revision
        (ConcernStatus.Resolved, ConcernStatus.Closed),                  // close
    };

    /// <summary>Where <paramref name="command"/> takes a concern in <paramref name="from"/>; null when the state machine has no such edge.</summary>
    public static ConcernStatus? TargetOf(ConcernCommand command, ConcernStatus from) => (command, from) switch
    {
        (ConcernCommand.Assess, _) when IsEditable(from) => from,
        (ConcernCommand.Assign, ConcernStatus.Open) => ConcernStatus.Assigned,
        (ConcernCommand.Assign, ConcernStatus.Assigned or ConcernStatus.InProgress) => from,
        (ConcernCommand.Start, ConcernStatus.Assigned) => ConcernStatus.InProgress,
        (ConcernCommand.SubmitResolution, ConcernStatus.InProgress) => ConcernStatus.PendingValidation,
        (ConcernCommand.Review, _) when IsActive(from) => from,
        (ConcernCommand.Close, ConcernStatus.Resolved) => ConcernStatus.Closed,
        _ => null,
    };

    /// <summary>Its fields and impacts change until the resolution goes to validation.</summary>
    public static bool IsEditable(ConcernStatus status) => status is ConcernStatus.Open or ConcernStatus.Assigned or ConcernStatus.InProgress;

    /// <summary>Not yet resolved: reviewed on its cadence, and escalated when it needs to be.</summary>
    public static bool IsActive(ConcernStatus status) => IsEditable(status) || status == ConcernStatus.PendingValidation;

    /// <summary>
    /// Why a command has no edge from <paramref name="from"/>: a CLOSED concern changes no more; an edit of one with validation or
    /// validated is not an edit any longer; anything else is a step the state machine does not take.
    /// </summary>
    public static (bool Closed, bool NotEditable) RefusalOf(ConcernCommand command, ConcernStatus from) =>
        (from == ConcernStatus.Closed, command is ConcernCommand.Assess or ConcernCommand.Assign && !IsEditable(from));
}

/// <summary>The commands that move a concern along <see cref="ConcernWorkflow"/>, one per edge family (api-conventions R-4).</summary>
internal enum ConcernCommand
{
    Assess = 1,
    Assign = 2,
    Start = 3,
    SubmitResolution = 4,
    Review = 5,
    Close = 6,
}
