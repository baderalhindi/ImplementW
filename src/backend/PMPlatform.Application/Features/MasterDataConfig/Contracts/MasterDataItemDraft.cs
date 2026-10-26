using PMPlatform.Domain.Common;

namespace PMPlatform.Application.Features.MasterDataConfig.Contracts;

/// <summary>A new item, created DRAFT. The catalogue and code are its identity and never change.</summary>
public sealed record MasterDataItemDraft(
    Guid CatalogueId, string Code, BilingualLabel Label, BilingualLabel? Description, Guid? ParentItemId, int SortOrder);
