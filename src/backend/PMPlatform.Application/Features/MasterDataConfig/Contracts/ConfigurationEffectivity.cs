namespace PMPlatform.Application.Features.MasterDataConfig.Contracts;

/// <summary>
/// Where a published version stands at a moment (ERD D-13). Derived from the effective dates of the family's versions,
/// never stored: a stored value would go stale when a date passes.
/// </summary>
public enum ConfigurationEffectivity
{
    /// <summary>Published with an effective-from still ahead.</summary>
    FutureEffective = 1,

    /// <summary>The version resolution returns now.</summary>
    Active = 2,

    /// <summary>A later version has taken effect; resolution still returns this one for dates in its window.</summary>
    Superseded = 3,

    /// <summary>Withdrawn: from its effective-to on, nothing resolves to it.</summary>
    Retired = 4,
}
