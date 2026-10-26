namespace PMPlatform.Application.Features.Schedule.Contracts;

/// <summary>
/// ADR-003 §8.2 edge 10, WF-03 → WF-05 query (ICD-04): the shared milestone an achievement claims. WF-05 names the row by its
/// identifier and holds no copy of it. It authorizes no one: the consumer decides what its caller may see, on the project's
/// anchors.
/// </summary>
public interface IProjectMilestoneReader
{
    /// <summary>Null when there is no such milestone.</summary>
    public Task<ProjectMilestoneFacts?> FindAsync(Guid projectMilestoneId, CancellationToken cancellationToken);
}
