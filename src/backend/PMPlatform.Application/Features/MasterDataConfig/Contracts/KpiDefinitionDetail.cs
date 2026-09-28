using PMPlatform.Domain.Common;
using PMPlatform.Domain.MasterDataConfig;

namespace PMPlatform.Application.Features.MasterDataConfig.Contracts;

/// <summary>A stable KPI identity. Its calculation rule and thresholds are versioned KPI_POLICY configuration (OQ-006).</summary>
public sealed record KpiDefinitionDetail(
    Guid Id,
    string Code,
    BilingualLabel Name,
    BilingualLabel? Description,
    Guid UnitItemId,
    KpiDirection Direction,
    GovernedRecord Governance);
