namespace PMPlatform.Application.Features.Closure.Contracts;

/// <summary>A project's completion cases, most recently changed first (R-29).</summary>
public sealed record CompletionCasePage(IReadOnlyList<CompletionCaseDetail> Items, int Page, int PageSize, int TotalCount);

/// <summary>A project's closure cases, most recently changed first (R-29).</summary>
public sealed record ClosureCasePage(IReadOnlyList<ClosureCaseDetail> Items, int Page, int PageSize, int TotalCount);

/// <summary>A case's readiness records, newest first: its evaluations and waivers.</summary>
public sealed record ReadinessRecordPage(IReadOnlyList<ReadinessRecordDetail> Items, int Page, int PageSize, int TotalCount);

/// <summary>A project's post-project obligations, most recently changed first (R-29).</summary>
public sealed record PostProjectObligationPage(IReadOnlyList<PostProjectObligationDetail> Items, int Page, int PageSize, int TotalCount);
