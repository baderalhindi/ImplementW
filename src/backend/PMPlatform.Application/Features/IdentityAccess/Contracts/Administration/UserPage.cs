namespace PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;

/// <summary>One page of ADM-002 (R-29).</summary>
public sealed record UserPage(IReadOnlyList<UserSummary> Items, int Page, int PageSize, int TotalCount);
