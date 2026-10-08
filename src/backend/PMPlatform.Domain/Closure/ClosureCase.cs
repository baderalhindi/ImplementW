using PMPlatform.Domain.Common;

namespace PMPlatform.Domain.Closure;

/// <summary>
/// The request to close a project (WF-10 §4.2, TASK-063): on the normal path a COMPLETED project, following its effected completion case;
/// on the terminal path a SUSPENDED project that will not resume, which is closed without being completed and so never has an actual
/// completion date (WF-10 §9, DCL-CLO-13). Once effected the project is CLOSED: terminal and read-only (BR-CLO-020).
/// </summary>
public sealed class ClosureCase : CloseoutCase
{
    /// <summary>The effected completion case the closure follows; null on the terminal path from SUSPENDED.</summary>
    public Guid? CompletionCaseId { get; set; }

    /// <summary>
    /// The final project summary — on the terminal path, the justification for stopping without completion — stored as entered
    /// (ADR-012). Required to submit.
    /// </summary>
    public NarrativeText? ClosureNarrative { get; set; }

    /// <summary>How the project ends: completed, or terminated without completion (BR-CLO-026, BR-CLO-039). Derived, not stored.</summary>
    public ProjectOutcome Outcome => CompletionCaseId is null ? ProjectOutcome.TerminatedWithoutCompletion : ProjectOutcome.Completed;
}
