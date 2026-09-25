using PMPlatform.Domain.Common;

namespace PMPlatform.Domain.MasterDataConfig;

/// <summary>
/// The unit of publication for one configuration family. Its typed rows become immutable with it. Effectivity
/// (FUTURE_EFFECTIVE, ACTIVE, SUPERSEDED) is derived from the dates and is not stored (ERD D-13). Delete policy: RETAIN.
/// </summary>
public sealed class ConfigurationVersion : GovernedEntity
{
    public Guid ConfigurationFamilyId { get; set; }

    public int VersionNo { get; set; }

    /// <summary>Set at publication; may be future-dated.</summary>
    public DateTimeOffset? EffectiveFrom { get; set; }

    /// <summary>Set when the next version's <see cref="EffectiveFrom"/> passes.</summary>
    public DateTimeOffset? EffectiveTo { get; set; }

    public NarrativeText? ChangeSummary { get; set; }
}
