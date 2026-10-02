using PMPlatform.Application.Features.Project.Contracts;

namespace PMPlatform.Application.Features.Project;

internal sealed class ProjectFactsReader(IProjectRepository repository) : IProjectFactsReader
{
    public async Task<ProjectFacts?> FindAsync(Guid projectId, CancellationToken cancellationToken) =>
        await repository.ReadAsync(projectId, cancellationToken).ConfigureAwait(false) is { } p
            ? new ProjectFacts(
                p.Id, p.FormalProjectId, p.DepartmentId, p.ExternalEntityId, p.ProjectManagerUserId, p.LifecycleState, p.GovernanceProfileItemId,
                p.LegacyIntakeDate, p.ActivatedAt)
            : null;
}
