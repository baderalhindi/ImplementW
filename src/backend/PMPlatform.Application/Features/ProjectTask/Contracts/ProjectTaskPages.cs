namespace PMPlatform.Application.Features.ProjectTask.Contracts;

/// <summary>A project's tasks (R-29).</summary>
public sealed record ProjectTaskPage(IReadOnlyList<ProjectTaskDetail> Items, int Page, int PageSize, int TotalCount);

/// <summary>A project's task dependencies, oldest first (R-29).</summary>
public sealed record TaskDependencyPage(IReadOnlyList<TaskDependencyDetail> Items, int Page, int PageSize, int TotalCount);

/// <summary>A project's Activity Execution Progress (R-29).</summary>
public sealed record ActivityExecutionProgressPage(IReadOnlyList<ActivityExecutionProgressDetail> Items, int Page, int PageSize, int TotalCount);
