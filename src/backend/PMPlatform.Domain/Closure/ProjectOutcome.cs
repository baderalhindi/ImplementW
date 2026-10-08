namespace PMPlatform.Domain.Closure;

/// <summary>
/// How a closed project ended (WF-10 §8 ProjectOutcomeType, BR-CLO-038): through an effected completion, or closed from SUSPENDED without
/// one. A terminated project is never reported as completed (CLO-CC-27, BR-CLO-039).
/// </summary>
public enum ProjectOutcome
{
    Completed = 1,
    TerminatedWithoutCompletion = 2,
}
