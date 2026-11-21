namespace PMPlatform.Application.Features.Schedule.Contracts;

/// <summary>
/// Planned milestones past their forecast date, counted per project: the Project Manager's Home (review round 8, point 13). The same rule as the
/// milestone lists' "overdue". It authorizes no one: the caller passes the projects it may read.
/// </summary>
public interface IOverdueMilestoneCounter
{
    /// <summary>Only projects with an overdue milestone appear.</summary>
    public Task<IReadOnlyDictionary<Guid, int>> CountAsync(IReadOnlyCollection<Guid> projectIds, DateOnly today, CancellationToken cancellationToken);
}
