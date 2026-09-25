using PMPlatform.Domain.Common;

namespace PMPlatform.Domain.MasterDataConfig;

/// <summary>One of the five probability levels (PTBC-017). Delete policy: CASCADE.</summary>
public sealed class ProbabilityLevelDefinition : AuditedEntity
{
    public Guid ConfigurationVersionId { get; set; }

    /// <summary>1 to 5.</summary>
    public short Level { get; set; }

    public required BilingualLabel Label { get; set; }

    public decimal? LowerPct { get; set; }

    public decimal? UpperPct { get; set; }
}
