namespace PMPlatform.Application.Features.Progress.Contracts;

/// <summary>One page of a project's reporting periods, earliest first (R-29).</summary>
public sealed record ReportingCyclePage(IReadOnlyList<ReportingCycleSummary> Items, int Page, int PageSize, int TotalCount);

/// <summary>One page of a project's progress revisions, newest first (R-29).</summary>
public sealed record ProgressSubmissionPage(IReadOnlyList<ProgressSubmissionDetail> Items, int Page, int PageSize, int TotalCount);

/// <summary>One page of a project's published snapshots, latest first (R-29).</summary>
public sealed record PublishedProgressSnapshotPage(IReadOnlyList<PublishedProgressSnapshotDetail> Items, int Page, int PageSize, int TotalCount);

/// <summary>A project's live health: one item once it has been computed, none before (R-29).</summary>
public sealed record ProjectHealthStatusPage(IReadOnlyList<ProjectHealthStatusDetail> Items, int Page, int PageSize, int TotalCount);
