namespace PMPlatform.Application.Features.MasterDataConfig.Contracts;

public sealed record ConfigurationVersionPage(IReadOnlyList<ConfigurationVersionSummary> Items, int Page, int PageSize, int TotalCount);
