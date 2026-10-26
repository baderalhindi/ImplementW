using PMPlatform.Domain.Common;
using PMPlatform.Domain.MasterDataConfig;

namespace PMPlatform.Application.Features.MasterDataConfig.Contracts;

public sealed record KpiDefinitionSummary(
    Guid Id, string Code, BilingualLabel Name, Guid UnitItemId, KpiDirection Direction, GovernedLifecycleState LifecycleState);
