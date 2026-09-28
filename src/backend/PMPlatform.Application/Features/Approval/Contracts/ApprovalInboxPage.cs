namespace PMPlatform.Application.Features.Approval.Contracts;

/// <summary>One page of SCR-100, earliest due first (R-29).</summary>
public sealed record ApprovalInboxPage(IReadOnlyList<ApprovalInboxItem> Items, int Page, int PageSize, int TotalCount);
