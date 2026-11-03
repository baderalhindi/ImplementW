namespace PMPlatform.Application.Features.Project.Contracts;

/// <summary>
/// How a project's delivering entity has answered the update requests sent to it (هل الجهة ملتزمة بالتحديث الدوري, the 2 Nov 2026 review):
/// WF-13 counts them; the Project module decides compliance from the counts. Declared here and implemented by ExternalParticipation, so the
/// project module depends on no module that depends on it (ADR-003).
/// </summary>
public interface IProjectUpdateComplianceSource
{
    /// <summary>The counts of each project that has any request due or answered; a project with none is absent.</summary>
    public Task<IReadOnlyDictionary<Guid, UpdateComplianceCounts>> CountAsync(
        IReadOnlyCollection<Guid> projectIds, DateOnly today, CancellationToken cancellationToken);
}

/// <summary>
/// The update requests issued to the project's entity with a due date that has come, or that were answered: how many, and how many were
/// answered on or before their due date.
/// </summary>
public sealed record UpdateComplianceCounts(int Counted, int OnTime);

/// <summary>Until a module counts requests, no project has any.</summary>
internal sealed class NoUpdateComplianceSource : IProjectUpdateComplianceSource
{
    public Task<IReadOnlyDictionary<Guid, UpdateComplianceCounts>> CountAsync(
        IReadOnlyCollection<Guid> projectIds, DateOnly today, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyDictionary<Guid, UpdateComplianceCounts>>(new Dictionary<Guid, UpdateComplianceCounts>());
}
