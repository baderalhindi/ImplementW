namespace PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;

public sealed record PermissionProfilePage(IReadOnlyList<PermissionProfileSummary> Items, int Page, int PageSize, int TotalCount);
