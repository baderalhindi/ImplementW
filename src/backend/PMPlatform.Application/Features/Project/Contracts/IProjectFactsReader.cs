using PMPlatform.Application.Common.Authorization;

namespace PMPlatform.Application.Features.Project.Contracts;

/// <summary>
/// The query of ADR-003 §8.2 edges 1–6, and of edge 47 for FG-01's dashboard population (TASK-069). It authorizes no one: the
/// calling module decides access on the anchors it is given, through the authorization engine, as it does for its own records.
/// </summary>
public interface IProjectFactsReader
{
    /// <summary>Null when there is no such project.</summary>
    public Task<ProjectFacts?> FindAsync(Guid projectId, CancellationToken cancellationToken);

    /// <summary>
    /// Every project <paramref name="scope"/> reaches, as the authorization engine built it for the caller (edge 47): the population an
    /// FG-01 aggregate is counted over, so authorization is applied before aggregation (DSH-CC-04). Empty for an empty scope.
    /// </summary>
    public Task<IReadOnlyList<ProjectFacts>> ListReachedAsync(RecordScope scope, CancellationToken cancellationToken);
}
