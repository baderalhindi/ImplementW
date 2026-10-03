using PMPlatform.Domain.Schedule;

namespace PMPlatform.Application.Features.Schedule.Contracts;

/// <summary>What WF-05 needs to know about a shared milestone (ICD-04): its project, its category, its status and its forecast date.</summary>
public sealed record ProjectMilestoneFacts(Guid Id, Guid ProjectId, Guid MilestoneCategoryItemId, ProjectMilestoneStatus Status, DateOnly ForecastDate);
