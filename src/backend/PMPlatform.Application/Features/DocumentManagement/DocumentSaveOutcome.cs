namespace PMPlatform.Application.Features.DocumentManagement;

public enum DocumentSaveOutcome
{
    Saved = 1,

    /// <summary>The row changed since it was read (R-21).</summary>
    ConcurrencyConflict = 2,

    /// <summary>Another version of the document took the same number first.</summary>
    DuplicateVersion = 3,

    /// <summary>Another request linked the same document, target and role first.</summary>
    DuplicateLink = 4,

    /// <summary>Another request designated the same link, version and type first.</summary>
    DuplicateEvidence = 5,
}
