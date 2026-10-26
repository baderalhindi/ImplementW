namespace PMPlatform.Application.Features.Progress;

public enum ProgressSaveOutcome
{
    Saved = 1,

    /// <summary>The row changed since it was read (R-21).</summary>
    ConcurrencyConflict = 2,

    /// <summary>Another request wrote the same period, revision or opening position first.</summary>
    Duplicate = 3,
}
