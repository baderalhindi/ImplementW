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

    /// <summary>Set at publication, now or later, and after every earlier publication of the family (TASK-034).</summary>
    public DateTimeOffset? EffectiveFrom { get; set; }

    /// <summary>
    /// Set only when a published version is retired: the moment it stops resolving, or its <see cref="EffectiveFrom"/>
    /// if it is withdrawn before taking effect. Supersession by a later version is derived and never written here, so
    /// publishing a version changes no other (TASK-034).
    /// </summary>
    public DateTimeOffset? EffectiveTo { get; set; }

    public NarrativeText? ChangeSummary { get; set; }
}
