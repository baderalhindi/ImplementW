namespace PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;

public sealed record ExternalEntityPage(IReadOnlyList<ExternalEntitySummary> Items, int Page, int PageSize, int TotalCount);
