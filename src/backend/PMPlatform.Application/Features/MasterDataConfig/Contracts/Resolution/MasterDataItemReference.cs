using PMPlatform.Domain.Common;

namespace PMPlatform.Application.Features.MasterDataConfig.Contracts.Resolution;

public sealed record MasterDataItemReference(Guid Id, string CatalogueCode, string Code, BilingualLabel Label, Guid? ParentItemId, int SortOrder);
