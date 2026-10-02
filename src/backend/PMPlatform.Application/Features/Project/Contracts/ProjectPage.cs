namespace PMPlatform.Application.Features.Project.Contracts;

/// <summary>One page of projects (R-29).</summary>
public sealed record ProjectPage(IReadOnlyList<ProjectSummary> Items, int Page, int PageSize, int TotalCount);
