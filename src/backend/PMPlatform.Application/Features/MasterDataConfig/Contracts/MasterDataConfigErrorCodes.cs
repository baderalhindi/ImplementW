namespace PMPlatform.Application.Features.MasterDataConfig.Contracts;

/// <summary>The MasterDataConfig module's error codes (api-conventions R-27). A code, once shipped, keeps its meaning.</summary>
public static class MasterDataConfigErrorCodes
{
    /// <summary>409: a code another item, KPI definition or version already holds; <c>errors[]</c> names the field.</summary>
    public const string DuplicateKey = "MASTER_DATA_CONFIG_DUPLICATE_KEY";

    /// <summary>422: a referenced catalogue, item, family, version or KPI definition does not exist or may not be used.</summary>
    public const string ReferenceInvalid = "MASTER_DATA_CONFIG_REFERENCE_INVALID";

    /// <summary>422: the author edits a draft; a reviewer and a publisher who are two other people validate and publish it (ERD D-12).</summary>
    public const string SeparationOfDuties = "MASTER_DATA_CONFIG_SEPARATION_OF_DUTIES";

    /// <summary>422: the field is fixed once the row is PUBLISHED.</summary>
    public const string PublishedImmutable = "MASTER_DATA_CONFIG_PUBLISHED_IMMUTABLE";

    /// <summary>422: a shipped (<c>is_system</c>) item is never retired.</summary>
    public const string SystemRow = "MASTER_DATA_CONFIG_SYSTEM_ROW";

    /// <summary>422: the content breaks a rule of its family; <c>errors[]</c> names every entry and why.</summary>
    public const string ContentInvalid = "MASTER_DATA_CONFIG_CONTENT_INVALID";

    /// <summary>422: the content lacks what its family requires before it may be validated or published.</summary>
    public const string ContentIncomplete = "MASTER_DATA_CONFIG_CONTENT_INCOMPLETE";

    /// <summary>422: a version takes effect now or later, and after every version of its family published before it.</summary>
    public const string EffectiveFromInvalid = "MASTER_DATA_CONFIG_EFFECTIVE_FROM_INVALID";
}
