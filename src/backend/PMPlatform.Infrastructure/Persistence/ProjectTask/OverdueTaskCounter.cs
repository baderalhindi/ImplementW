using Microsoft.EntityFrameworkCore;
using PMPlatform.Application.Features.ProjectTask.Contracts;
using PMPlatform.Domain.ProjectTask;
using ProjectTaskEntity = PMPlatform.Domain.ProjectTask.ProjectTask;

namespace PMPlatform.Infrastructure.Persistence.ProjectTask;

/// <summary>Overdue tasks per project, in one query (review round 8, point 13).</summary>
internal sealed class OverdueTaskCounter(PMPlatformDbContext context) : IOverdueTaskCounter
{
    private static readonly ProjectTaskStatus[] Open = [ProjectTaskStatus.NotStarted, ProjectTaskStatus.InProgress, ProjectTaskStatus.Blocked];

    public async Task<IReadOnlyDictionary<Guid, int>> CountAsync(IReadOnlyCollection<Guid> projectIds, DateOnly today, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(projectIds);
        return await context.Set<ProjectTaskEntity>().AsNoTracking()
            .Where(t => projectIds.Contains(t.ProjectId) && t.DeletedAt == null && Open.Contains(t.Status) && t.PlannedFinishDate < today)
            .GroupBy(t => t.ProjectId)
            .Select(g => new { ProjectId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.ProjectId, g => g.Count, cancellationToken).ConfigureAwait(false);
    }
}
