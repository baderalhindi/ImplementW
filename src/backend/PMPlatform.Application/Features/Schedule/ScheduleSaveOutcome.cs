namespace PMPlatform.Application.Features.Schedule;

public enum ScheduleSaveOutcome
{
    Saved = 1,

    /// <summary>The row changed since it was read (R-21).</summary>
    ConcurrencyConflict = 2,

    /// <summary>A unique key another request took first: a WBS code, a dependency, a schedule, an ACTIVE baseline, an intake's baseline.</summary>
    Duplicate = 3,
}
