using PMPlatform.Domain.Common;

namespace PMPlatform.Application.Features.MasterDataConfig;

/// <summary>What a reference check needs to know of a master data item or KPI definition.</summary>
public sealed record ItemFacts(Guid Id, string CatalogueCode, GovernedLifecycleState LifecycleState);
