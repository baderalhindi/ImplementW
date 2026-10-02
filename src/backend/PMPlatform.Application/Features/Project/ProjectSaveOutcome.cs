namespace PMPlatform.Application.Features.Project;

public enum ProjectSaveOutcome
{
    Saved = 1,

    /// <summary>The row changed since it was read (R-21), or another request started the same revision's review first.</summary>
    ConcurrencyConflict = 2,

    /// <summary>A deleted draft is still referenced by another record.</summary>
    InUse = 3,
}
