using PMPlatform.Domain.Common;

namespace PMPlatform.Domain.MasterDataConfig;

/// <summary>A stable KPI identity in the catalogue; formulas and targets are outstanding (OQ-006). Delete policy: RETAIN.</summary>
public sealed class KpiDefinition : GovernedEntity
{
    public required string Code { get; set; }

    public required BilingualLabel Name { get; set; }

    public BilingualLabel? Description { get; set; }

    /// <summary>Master data item of catalogue KPI_UNIT.</summary>
    public Guid UnitItemId { get; set; }

    public KpiDirection Direction { get; set; }
}
