namespace PMPlatform.Application.Common.Governance;

/// <summary>What a <see cref="GovernedLifecycle"/> step came to.</summary>
public enum GovernedTransitionOutcome
{
    Applied = 1,

    /// <summary>The step is not allowed from the row's current state.</summary>
    InvalidTransition = 2,

    /// <summary>The row is RETIRED and takes no step.</summary>
    TerminalState = 3,

    /// <summary>The actor already holds another part of the row's lifecycle: author, reviewer and publisher differ.</summary>
    SeparationOfDuties = 4,
}
