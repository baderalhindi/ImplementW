using PMPlatform.Application.Common.Auditing;
using PMPlatform.Application.Features.Closure.Contracts;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.Project.Contracts;
using PMPlatform.Domain.Closure;
using PMPlatform.Domain.Project;

namespace PMPlatform.Application.Features.Closure;

/// <summary>
/// When a project admits a case, and what a case needs (WF-10 §4.3, §8, CLO-CC-04). A completion case is of an ACTIVE project
/// (BR-CLO-002). A closure case follows its project's state: on the normal path the project is COMPLETED and the case follows its effected
/// completion case; on the terminal path it is SUSPENDED and the case names none (BR-CLO-003). The project's state is read again by every
/// command but withdrawal, so a case raised while the project was eligible is refused once it is not (BR-CLO-034). That a project has at
/// most one open case of each kind is held by a unique key, whose violation the save answers with 409 CLOSURE_CASE_ALREADY_OPEN.
/// </summary>
internal static class CloseoutEligibility
{
    public static AdministrationError? ProjectRefused(CloseoutCase @case, ProjectFacts project)
    {
        ArgumentNullException.ThrowIfNull(project);
        bool eligible = @case switch
        {
            CompletionCase => project.Status == ProjectLifecycleState.Active,
            ClosureCase { CompletionCaseId: null } => project.Status == ProjectLifecycleState.Suspended,
            ClosureCase => project.Status == ProjectLifecycleState.Completed,
            _ => throw new ArgumentOutOfRangeException(nameof(@case), @case, "Unknown case."),
        };
        return eligible ? null : AdministrationError.Rule(ClosureErrorCodes.ProjectNotEligible);
    }

    /// <summary>
    /// An actual completion date, when given, is not after today and not before the project was activated: the official date is the
    /// approved business date delivery ended, never a future one, and cannot predate execution (WF-10 §8; BR-CLO-008 leaves backdating to
    /// AHDA's policy, TBC-CLO-007).
    /// </summary>
    public static AdministrationError? CompletionFieldsRefused(CompletionCaseChanges fields, ProjectFacts project, DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(fields);
        ArgumentNullException.ThrowIfNull(project);
        return fields.ActualProjectCompletionDate is not { } date ? null
            : date > today ? AdministrationError.Rule(ClosureErrorCodes.CompletionDateInvalid, new FieldIssue("actualProjectCompletionDate", FieldIssue.NotAllowed))
            : project.ActivatedAt is { } activated && date < DateOnly.FromDateTime(activated.UtcDateTime)
                ? AdministrationError.Rule(ClosureErrorCodes.CompletionDateInvalid, new FieldIssue("actualProjectCompletionDate", FieldIssue.BeforeStart))
            : null;
    }

    /// <summary>
    /// What review needs: a completion case's actual completion date and narrative — the Project Manager's recommendation (WF-10 §6.1
    /// PM_RECOMMENDATION) — and a closure case's narrative, the final summary or the justification for stopping (§6.2 LESSONS, §9).
    /// </summary>
    public static AdministrationError? SubmissionRefused(CloseoutCase @case, ProjectFacts project, DateOnly today) => @case switch
    {
        CompletionCase { ActualProjectCompletionDate: null } => AdministrationError.Rule(ClosureErrorCodes.Incomplete, new FieldIssue("actualProjectCompletionDate", FieldIssue.Required)),
        CompletionCase { CompletionNarrative: null } => AdministrationError.Rule(ClosureErrorCodes.Incomplete, new FieldIssue("completionNarrative", FieldIssue.Required)),
        CompletionCase completion => CompletionFieldsRefused(new CompletionCaseChanges(completion.ActualProjectCompletionDate, completion.CompletionNarrative), project, today),
        ClosureCase { ClosureNarrative: null } => AdministrationError.Rule(ClosureErrorCodes.Incomplete, new FieldIssue("closureNarrative", FieldIssue.Required)),
        ClosureCase => null,
        _ => throw new ArgumentOutOfRangeException(nameof(@case), @case, "Unknown case."),
    };

    /// <summary>422 CLOSURE_BLOCKER_EXISTS when the readiness is NOT_READY, naming each failing criterion; null otherwise.</summary>
    public static AdministrationError? BlockerOf(ReadinessDetail readiness)
    {
        ArgumentNullException.ThrowIfNull(readiness);
        return readiness.Status == ReadinessStatus.NotReady
            ? AdministrationError.Rule(
                ClosureErrorCodes.BlockerExists,
                [.. readiness.Checks.Where(c => c.Result == ReadinessResult.Fail).Select(c => new FieldIssue(AuditValue.Format(c.CheckCode)!, FieldIssue.NotAllowed))])
            : null;
    }
}
