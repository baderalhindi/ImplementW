using PMPlatform.Domain.Closure;

namespace PMPlatform.Application.Features.Closure;

/// <summary>
/// An obligation's state machine (TASK-063): OPEN → IN_PROGRESS, and either to SATISFIED, CANCELLED or WAIVED, which are settled and
/// final. Migration <c>TASK-063_GuardCloseoutHistory</c> refuses every other change in the database too.
/// </summary>
internal static class PostProjectObligationRules
{
    public static bool IsOpen(PostProjectObligationStatus status) => status is PostProjectObligationStatus.Open or PostProjectObligationStatus.InProgress;

    /// <summary>Where <paramref name="command"/> takes an obligation in <paramref name="from"/>; null when there is no such edge.</summary>
    public static PostProjectObligationStatus? TargetOf(ObligationCommand command, PostProjectObligationStatus from) => (command, from) switch
    {
        (ObligationCommand.Start, PostProjectObligationStatus.Open) => PostProjectObligationStatus.InProgress,
        (ObligationCommand.Satisfy, PostProjectObligationStatus.Open or PostProjectObligationStatus.InProgress) => PostProjectObligationStatus.Satisfied,
        (ObligationCommand.Cancel, PostProjectObligationStatus.Open or PostProjectObligationStatus.InProgress) => PostProjectObligationStatus.Cancelled,
        (ObligationCommand.Waive, PostProjectObligationStatus.Open or PostProjectObligationStatus.InProgress) => PostProjectObligationStatus.Waived,
        _ => null,
    };
}

internal enum ObligationCommand
{
    Start = 1,
    Satisfy = 2,
    Cancel = 3,
    Waive = 4,
}
