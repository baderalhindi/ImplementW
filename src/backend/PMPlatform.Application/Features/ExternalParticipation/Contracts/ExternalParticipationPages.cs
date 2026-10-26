namespace PMPlatform.Application.Features.ExternalParticipation.Contracts;

/// <summary>The requests the caller's data scope reaches, most recently changed first (R-29; indexing-strategy P-2).</summary>
public sealed record ExternalUpdateRequestPage(IReadOnlyList<ExternalUpdateRequestDetail> Items, int Page, int PageSize, int TotalCount);

/// <summary>A request's revisions, newest first.</summary>
public sealed record ExternalContributionPage(IReadOnlyList<ExternalContributionDetail> Items, int Page, int PageSize, int TotalCount);

/// <summary>A revision's application attempts, newest first.</summary>
public sealed record SourceApplicationPage(IReadOnlyList<SourceApplicationDetail> Items, int Page, int PageSize, int TotalCount);
