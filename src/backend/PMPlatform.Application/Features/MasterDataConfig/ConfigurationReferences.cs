using PMPlatform.Domain.Common;

namespace PMPlatform.Application.Features.MasterDataConfig;

/// <summary>
/// What content may reference, read once per check: the master data items and KPI definitions it names, the roles, and
/// the PUBLISHED items of the catalogues whose every value a family must cover.
/// </summary>
internal sealed record ConfigurationReferences(
    IReadOnlyDictionary<Guid, ItemFacts> Items,
    IReadOnlyDictionary<Guid, GovernedLifecycleState> KpiDefinitions,
    IReadOnlySet<Guid> RoleIds,
    IReadOnlyDictionary<string, IReadOnlyList<Guid>> PublishedItemsByCatalogue)
{
    public IReadOnlyList<Guid> PublishedItemsOf(string catalogueCode) => PublishedItemsByCatalogue.GetValueOrDefault(catalogueCode) ?? [];
}
