namespace PMPlatform.Domain.Common;

/// <summary>
/// Who manages a project's day-to-day record (ADR-013). Shared because a Project carries it and FG-04's
/// participation rules are keyed on it.
/// </summary>
public enum ParticipationMode
{
    EntityManaged = 1,
    AhdaManaged = 2,
}
