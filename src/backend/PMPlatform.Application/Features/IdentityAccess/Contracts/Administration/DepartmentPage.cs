namespace PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;

public sealed record DepartmentPage(IReadOnlyList<DepartmentSummary> Items, int Page, int PageSize, int TotalCount);
