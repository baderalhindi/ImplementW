namespace PMPlatform.Application.Features.ChangeRequest.Contracts;

/// <summary>A project's change requests, most recently changed first (R-29; indexing-strategy P-2, I-26).</summary>
public sealed record ChangeRequestPage(IReadOnlyList<ChangeRequestDetail> Items, int Page, int PageSize, int TotalCount);

/// <summary>Change authorisations, most recently issued first.</summary>
public sealed record ChangeAuthorizationPage(IReadOnlyList<ChangeAuthorizationDetail> Items, int Page, int PageSize, int TotalCount);
