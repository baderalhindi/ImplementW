namespace PMPlatform.Application.Features.IdentityAccess.Administration;

public enum SaveOutcome
{
    Saved = 1,

    /// <summary>The row changed since it was read at the version the caller expected (R-21).</summary>
    ConcurrencyConflict = 2,

    /// <summary>A unique key is already held; <see cref="SaveResult.DuplicateField"/> names it.</summary>
    DuplicateKey = 3,
}
