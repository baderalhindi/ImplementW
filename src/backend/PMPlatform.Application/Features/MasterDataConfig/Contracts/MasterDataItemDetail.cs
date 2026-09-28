using PMPlatform.Domain.Common;

namespace PMPlatform.Application.Features.MasterDataConfig.Contracts;

/// <summary>A controlled value, with who authored, validated and published it (ERD D-12).</summary>
public sealed record MasterDataItemDetail(
    Guid Id,
    Guid CatalogueId,
    string CatalogueCode,
    string Code,
    BilingualLabel Label,
    BilingualLabel? Description,
    Guid? ParentItemId,
    int SortOrder,
    bool IsSystem,
    GovernedRecord Governance);
