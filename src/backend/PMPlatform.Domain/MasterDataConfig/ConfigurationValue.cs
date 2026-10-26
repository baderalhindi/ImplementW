using PMPlatform.Domain.Common;

namespace PMPlatform.Domain.MasterDataConfig;

/// <summary>
/// A scalar policy value inside a configuration version: update cadence, reminder offsets, freshness thresholds,
/// override tolerance. The key catalogue is fixed per family in code. Delete policy: CASCADE.
/// </summary>
public sealed class ConfigurationValue : AuditedEntity
{
    public Guid ConfigurationVersionId { get; set; }

    public required string ValueKey { get; set; }

    public required string ValueText { get; set; }

    public ConfigurationValueType ValueType { get; set; }
}
