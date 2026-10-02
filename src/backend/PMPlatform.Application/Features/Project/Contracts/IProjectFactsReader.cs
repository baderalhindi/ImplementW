namespace PMPlatform.Application.Features.Project.Contracts;

/// <summary>
/// The query of ADR-003 §8.2 edges 1–6. It authorizes no one: the calling module decides access on the anchors it is
/// given, through the authorization engine, as it does for its own records.
/// </summary>
public interface IProjectFactsReader
{
    /// <summary>Null when there is no such project.</summary>
    public Task<ProjectFacts?> FindAsync(Guid projectId, CancellationToken cancellationToken);
}
