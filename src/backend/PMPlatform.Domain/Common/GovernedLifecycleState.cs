namespace PMPlatform.Domain.Common;

/// <summary>
/// The governed lifecycle of everything authored and published (ERD D-12; Blueprint Section 12, TASK-034).
/// A PUBLISHED row is immutable; an abandoned draft is RETIRED, never deleted.
/// </summary>
public enum GovernedLifecycleState
{
    Draft = 1,
    Validated = 2,
    Published = 3,
    Retired = 4,
}
