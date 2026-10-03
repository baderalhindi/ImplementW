namespace PMPlatform.Application.Features.Schedule.Contracts;

/// <summary>A project's schedule: one item once initialized (R-29).</summary>
public sealed record ProjectSchedulePage(IReadOnlyList<ProjectScheduleDetail> Items, int Page, int PageSize, int TotalCount);

/// <summary>A project's activities in work-breakdown order (R-29).</summary>
public sealed record ScheduleActivityPage(IReadOnlyList<ScheduleActivityDetail> Items, int Page, int PageSize, int TotalCount);

/// <summary>A project's dependencies (R-29).</summary>
public sealed record ScheduleDependencyPage(IReadOnlyList<ScheduleDependencyDetail> Items, int Page, int PageSize, int TotalCount);

/// <summary>A project's baselines, latest version first (R-29).</summary>
public sealed record ProjectBaselinePage(IReadOnlyList<ProjectBaselineDetail> Items, int Page, int PageSize, int TotalCount);

/// <summary>The activities a baseline froze (R-29).</summary>
public sealed record BaselineActivityPage(IReadOnlyList<BaselineActivityDetail> Items, int Page, int PageSize, int TotalCount);

/// <summary>The dependencies a baseline froze (R-29).</summary>
public sealed record BaselineDependencyPage(IReadOnlyList<BaselineDependencyDetail> Items, int Page, int PageSize, int TotalCount);

/// <summary>A project's live Schedule Health: one item once computed (R-29).</summary>
public sealed record ScheduleHealthStatusPage(IReadOnlyList<ScheduleHealthStatusDetail> Items, int Page, int PageSize, int TotalCount);
