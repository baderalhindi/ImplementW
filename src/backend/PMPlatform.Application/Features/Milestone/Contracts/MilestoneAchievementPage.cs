namespace PMPlatform.Application.Features.Milestone.Contracts;

/// <summary>Achievement revisions, newest first (R-29).</summary>
public sealed record MilestoneAchievementPage(IReadOnlyList<MilestoneAchievementDetail> Items, int Page, int PageSize, int TotalCount);
