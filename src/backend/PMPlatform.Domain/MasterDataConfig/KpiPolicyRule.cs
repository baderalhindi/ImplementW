using PMPlatform.Domain.Common;

namespace PMPlatform.Domain.MasterDataConfig;

/// <summary>The versioned calculation rule and RAG thresholds of a KPI (OQ-006). Delete policy: CASCADE.</summary>
public sealed class KpiPolicyRule : AuditedEntity
{
    public Guid ConfigurationVersionId { get; set; }

    public Guid KpiDefinitionId { get; set; }

    /// <summary>An allowlisted expression; outstanding.</summary>
    public string? CalculationExpression { get; set; }

    public decimal? GreenThreshold { get; set; }

    public decimal? AmberThreshold { get; set; }
}
