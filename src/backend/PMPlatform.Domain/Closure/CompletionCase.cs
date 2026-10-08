using PMPlatform.Domain.Common;

namespace PMPlatform.Domain.Closure;

/// <summary>
/// The readiness-gated request to complete an ACTIVE project (WF-10 §4.1, TASK-063): its readiness is evaluated against the source
/// modules, it is decided through WF-11, and once effected the project is COMPLETED. No progress figure, task or milestone completes a
/// project: only an effected completion case does (BR-CLO-009, CLO-CC-16). At most one per project is open and one is effected.
/// </summary>
public sealed class CompletionCase : CloseoutCase
{
    /// <summary>
    /// The official business date the project's delivery ended (ERD F-044): proposed by the requester, required to submit, reviewed and
    /// approved with the case, and the project's completion date once the case is effected — never inferred from progress or from a
    /// task's or milestone's date (BR-CLO-006).
    /// </summary>
    public DateOnly? ActualProjectCompletionDate { get; set; }

    /// <summary>The Project Manager's completion recommendation and outcome narrative, stored as entered (ADR-012). Required to submit.</summary>
    public NarrativeText? CompletionNarrative { get; set; }
}
