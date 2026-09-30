namespace PMPlatform.Application.Features.DocumentManagement.Contracts;

/// <summary>One page of documents (R-29).</summary>
public sealed record DocumentPage(IReadOnlyList<DocumentSummary> Items, int Page, int PageSize, int TotalCount);
