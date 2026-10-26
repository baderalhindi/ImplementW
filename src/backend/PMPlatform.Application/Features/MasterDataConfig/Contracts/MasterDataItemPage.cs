namespace PMPlatform.Application.Features.MasterDataConfig.Contracts;

public sealed record MasterDataItemPage(IReadOnlyList<MasterDataItemSummary> Items, int Page, int PageSize, int TotalCount);
