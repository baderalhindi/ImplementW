using PMPlatform.Domain.Common;

namespace PMPlatform.Domain.MasterDataConfig;

/// <summary>
/// One cell of the 5×5 probability × overall-impact matrix and the rating it yields (ADR-011; mapping outstanding,
/// OQ-006). Delete policy: CASCADE.
/// </summary>
public sealed class RiskMatrixCell : AuditedEntity
{
    public Guid ConfigurationVersionId { get; set; }

    /// <summary>1 to 5.</summary>
    public short ProbabilityLevel { get; set; }

    /// <summary>1 to 5.</summary>
    public short ImpactLevel { get; set; }

    public Guid RiskRatingDefinitionId { get; set; }
}
