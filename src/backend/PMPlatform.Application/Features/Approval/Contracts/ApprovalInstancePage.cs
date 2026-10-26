namespace PMPlatform.Application.Features.Approval.Contracts;

/// <summary>One page of approval runs (R-29).</summary>
public sealed record ApprovalInstancePage(IReadOnlyList<ApprovalInstanceSummary> Items, int Page, int PageSize, int TotalCount);
