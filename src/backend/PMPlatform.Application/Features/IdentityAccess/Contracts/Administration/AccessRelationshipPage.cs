namespace PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;

public sealed record AccessRelationshipPage(IReadOnlyList<AccessRelationshipSummary> Items, int Page, int PageSize, int TotalCount);
