namespace PMPlatform.Application.Features.ManagementConcern.Contracts;

/// <summary>A project's concerns, most recently changed first (R-29; indexing-strategy P-2).</summary>
public sealed record ConcernPage(IReadOnlyList<ConcernDetail> Items, int Page, int PageSize, int TotalCount);

/// <summary>A concern's escalations, newest first.</summary>
public sealed record ConcernEscalationPage(IReadOnlyList<ConcernEscalationDetail> Items, int Page, int PageSize, int TotalCount);
