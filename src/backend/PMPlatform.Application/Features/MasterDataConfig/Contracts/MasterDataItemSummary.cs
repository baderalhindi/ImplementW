using PMPlatform.Domain.Common;

namespace PMPlatform.Application.Features.MasterDataConfig.Contracts;

public sealed record MasterDataItemSummary(
    Guid Id,
    Guid CatalogueId,
    string Code,
    BilingualLabel Label,
    Guid? ParentItemId,
    int SortOrder,
    GovernedLifecycleState LifecycleState,
    bool IsSystem);
