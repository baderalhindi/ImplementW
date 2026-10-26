using PMPlatform.Domain.Closure;

namespace PMPlatform.Application.Features.Closure;

/// <summary>
/// Which criteria a case is evaluated against (WF-10 §6.1, §6.2) and which may be waived. Completion checks every source module's records
/// and that each open obligation has an owner. Closure on the normal path checks what may still move once the project is COMPLETED —
/// risks, issues, changes, decisions — and that every obligation is settled: the closure policy. The terminal path from SUSPENDED was
/// never reconciled by a completion, so it checks everything completion does, and the closure policy. Waivable criteria are the open-item
/// dispositions an approver may accept as exceptions (READY_WITH_CONDITIONS); a decision still to land, an open suspension or resumption
/// request and an obligation are settled where they live, never waived.
/// </summary>
internal static class ReadinessPolicy
{
    private static readonly ReadinessCheckCode[] SourceChecks =
    [
        ReadinessCheckCode.DecisionsSettled,
        ReadinessCheckCode.TasksDispositioned,
        ReadinessCheckCode.ScheduleReconciled,
        ReadinessCheckCode.MilestonesDispositioned,
        ReadinessCheckCode.RisksDispositioned,
        ReadinessCheckCode.IssuesDispositioned,
        ReadinessCheckCode.ChangesDispositioned,
        ReadinessCheckCode.SuspensionRequestsSettled,
        ReadinessCheckCode.ProgressReported,
        ReadinessCheckCode.FinancialsSettled,
    ];

    public static IReadOnlyList<ReadinessCheckCode> CompletionChecks { get; } = [.. SourceChecks, ReadinessCheckCode.ObligationsOwned];

    public static IReadOnlyList<ReadinessCheckCode> TerminalClosureChecks { get; } = [.. SourceChecks, ReadinessCheckCode.ObligationsSatisfied];

    public static IReadOnlyList<ReadinessCheckCode> ClosureChecks { get; } =
    [
        ReadinessCheckCode.DecisionsSettled,
        ReadinessCheckCode.RisksDispositioned,
        ReadinessCheckCode.IssuesDispositioned,
        ReadinessCheckCode.ChangesDispositioned,
        ReadinessCheckCode.ObligationsSatisfied,
    ];

    public static IReadOnlyList<ReadinessCheckCode> ChecksOf(CloseoutCase @case) => @case switch
    {
        CompletionCase => CompletionChecks,
        ClosureCase { CompletionCaseId: null } => TerminalClosureChecks,
        ClosureCase => ClosureChecks,
        _ => throw new ArgumentOutOfRangeException(nameof(@case), @case, "Unknown case."),
    };

    public static bool IsWaivable(ReadinessCheckCode code) =>
        code is not (ReadinessCheckCode.DecisionsSettled or ReadinessCheckCode.SuspensionRequestsSettled
            or ReadinessCheckCode.ObligationsOwned or ReadinessCheckCode.ObligationsSatisfied);
}
