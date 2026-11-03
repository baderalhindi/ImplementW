using PMPlatform.Application.Features.Project.Contracts;

namespace PMPlatform.Application.Features.ExternalParticipation;

/// <summary>
/// WF-13's count of how an entity answers its update requests (the 2 Nov 2026 review): a request is counted once it is issued with a due date
/// that has come, or once it is answered; it is on time when its first contribution was submitted by the end of its due date.
/// </summary>
internal sealed class UpdateComplianceSource(IExternalParticipationRepository repository) : IProjectUpdateComplianceSource
{
    public async Task<IReadOnlyDictionary<Guid, UpdateComplianceCounts>> CountAsync(
        IReadOnlyCollection<Guid> projectIds, DateOnly today, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(projectIds);
        return (await repository.ListTimelinessAsync(projectIds, cancellationToken).ConfigureAwait(false))
            .Where(r => r.FirstSubmittedAt is not null || r.DueDate < today)
            .GroupBy(r => r.ProjectId)
            .ToDictionary(
                g => g.Key,
                g => new UpdateComplianceCounts(g.Count(), g.Count(r => r.FirstSubmittedAt is { } at && DateOnly.FromDateTime(at.UtcDateTime) <= r.DueDate)));
    }
}
