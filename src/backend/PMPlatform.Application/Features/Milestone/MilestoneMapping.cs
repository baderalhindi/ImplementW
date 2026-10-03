using PMPlatform.Application.Features.Milestone.Contracts;
using PMPlatform.Domain.Milestone;

namespace PMPlatform.Application.Features.Milestone;

internal static class MilestoneMapping
{
    /// <summary>The ACCEPTED revision is the current one: the database holds at most one per milestone.</summary>
    public static MilestoneAchievementDetail ToDetail(MilestoneAchievement a) => new(
        a.Id, a.ProjectMilestoneId, a.ProjectId, a.RevisionNo, a.Status, a.Status == MilestoneAchievementStatus.Accepted, a.ClaimedAchievementDate,
        a.AcceptedActualAchievementDate, a.Narrative, a.SubmittedByUserId, a.SubmittedAt, a.ReviewedByUserId, a.ReviewedAt, a.ReturnReason,
        a.SupersededByAchievementId, a.ProjectIntakeId, a.CreatedAt, a.CreatedBy, a.UpdatedAt, a.UpdatedBy);
}
