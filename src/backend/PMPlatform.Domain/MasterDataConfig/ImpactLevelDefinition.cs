using PMPlatform.Domain.Common;

namespace PMPlatform.Domain.MasterDataConfig;

/// <summary>
/// One of the five levels of one impact dimension (ADR-011). Dimensions are IMPACT_DIMENSION master data items —
/// cost, schedule, reputation and at least one operational — shared by risks and issues. Level descriptions and
/// boundaries are outstanding (OQ-006). Delete policy: CASCADE.
/// </summary>
public sealed class ImpactLevelDefinition : AuditedEntity
{
    public Guid ConfigurationVersionId { get; set; }

    public Guid ImpactDimensionItemId { get; set; }

    /// <summary>1 to 5.</summary>
    public short Level { get; set; }

    public required BilingualLabel Label { get; set; }

    public BilingualLabel? Description { get; set; }

    /// <summary>The quantitative boundary, where the dimension has one.</summary>
    public decimal? LowerBound { get; set; }

    public decimal? UpperBound { get; set; }
}
