namespace PMPlatform.Application.Features.MasterDataConfig.Contracts.Resolution;

/// <summary>Why resolution failed closed (Blueprint Section 12). Each is answered 422 <c>CONFIGURATION_MISSING</c>.</summary>
public enum ConfigurationMissingReason
{
    /// <summary>No configuration family or master data catalogue has this code.</summary>
    UnknownCode = 1,

    /// <summary>No published version of the family is effective at the date asked about.</summary>
    NoEffectiveVersion = 2,

    /// <summary>More than one version claims the same moment. Publication prevents it; resolution refuses to pick.</summary>
    AmbiguousVersions = 3,

    /// <summary>The version asked for by id (a pin, ERD D-13) does not exist or was never published.</summary>
    VersionNotPublished = 4,

    /// <summary>The effective version does not hold the entry the operation needs.</summary>
    EntryMissing = 5,

    /// <summary>The entry exists but not as the operation reads it, e.g. a TEXT value read as an integer.</summary>
    EntryInvalid = 6,

    /// <summary>A master data item that does not exist, is of another catalogue, or is not PUBLISHED.</summary>
    ItemUnavailable = 7,
}
