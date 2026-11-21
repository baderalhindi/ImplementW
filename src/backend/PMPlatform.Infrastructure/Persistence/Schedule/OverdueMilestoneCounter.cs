using Microsoft.EntityFrameworkCore;
using PMPlatform.Application.Features.Schedule.Contracts;
using PMPlatform.Domain.Schedule;

namespace PMPlatform.Infrastructure.Persistence.Schedule;

/// <summary>Overdue milestones per project, in one query (review round 8, point 13): Schedule's own read of the shared milestone row (ICD-04).</summary>
internal sealed class OverdueMilestoneCounter(PMPlatformDbContext context) : IOverdueMilestoneCounter
{
    public async Task<IReadOnlyDictionary<Guid, int>> CountAsync(IReadOnlyCollection<Guid> projectIds, DateOnly today, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(projectIds);
        return await context.Set<ProjectMilestone>().AsNoTracking()
            .Where(m => projectIds.Contains(m.ProjectId) && m.DeletedAt == null && m.Status == ProjectMilestoneStatus.Planned && m.ForecastDate < today)
            .GroupBy(m => m.ProjectId)
            .Select(g => new { ProjectId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.ProjectId, g => g.Count, cancellationToken).ConfigureAwait(false);
    }
}
