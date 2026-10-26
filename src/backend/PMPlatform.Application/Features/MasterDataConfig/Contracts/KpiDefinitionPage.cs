namespace PMPlatform.Application.Features.MasterDataConfig.Contracts;

public sealed record KpiDefinitionPage(IReadOnlyList<KpiDefinitionSummary> Items, int Page, int PageSize, int TotalCount);
