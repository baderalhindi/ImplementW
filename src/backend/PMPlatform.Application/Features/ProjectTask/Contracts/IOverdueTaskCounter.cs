namespace PMPlatform.Application.Features.ProjectTask.Contracts;

/// <summary>
/// Open tasks (not started, in progress or blocked) past their planned finish, counted per project: the Project Manager's Home (review round 8,
/// point 13). The same rule as the task lists' "overdue". It authorizes no one: the caller passes the projects it may read.
/// </summary>
public interface IOverdueTaskCounter
{
    /// <summary>Only projects with an overdue task appear.</summary>
    public Task<IReadOnlyDictionary<Guid, int>> CountAsync(IReadOnlyCollection<Guid> projectIds, DateOnly today, CancellationToken cancellationToken);
}
