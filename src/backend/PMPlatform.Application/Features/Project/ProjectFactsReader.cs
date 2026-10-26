using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Features.Project.Contracts;
using ProjectEntity = PMPlatform.Domain.Project.Project;

namespace PMPlatform.Application.Features.Project;

internal sealed class ProjectFactsReader(IProjectRepository repository) : IProjectFactsReader
{
    public async Task<ProjectFacts?> FindAsync(Guid projectId, CancellationToken cancellationToken) =>
        await repository.ReadAsync(projectId, cancellationToken).ConfigureAwait(false) is { } p ? FactsOf(p) : null;

    public async Task<IReadOnlyList<ProjectFacts>> ListReachedAsync(RecordScope scope, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);
        return scope.IsEmpty ? [] : [.. (await repository.ListReachedAsync(scope, cancellationToken).ConfigureAwait(false)).Select(FactsOf)];
    }

    public static ProjectFacts FactsOf(ProjectEntity p)
    {
        ArgumentNullException.ThrowIfNull(p);
        return new ProjectFacts(
            p.Id, p.FormalProjectId, p.DepartmentId, p.ExternalEntityId, p.ProjectManagerUserId, p.LifecycleState, p.GovernanceProfileItemId,
            p.LegacyIntakeDate, p.ActivatedAt, p.ParticipationMode, p.Title);
    }
}
