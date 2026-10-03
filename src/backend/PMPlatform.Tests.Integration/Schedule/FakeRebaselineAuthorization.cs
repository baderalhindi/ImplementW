using System.Collections.Concurrent;
using PMPlatform.Application.Features.Schedule;

namespace PMPlatform.Tests.Integration.Schedule;

/// <summary>Stands in for WF-08's change authorisations (schedule-baseline.md F-1): a test declares which are applicable to which project.</summary>
internal sealed class FakeRebaselineAuthorization : IRebaselineAuthorization
{
    private readonly ConcurrentDictionary<Guid, Guid> _applicable = new();

    public Task<bool> IsApplicableAsync(Guid projectId, Guid changeAuthorizationId, CancellationToken cancellationToken) =>
        Task.FromResult(_applicable.TryGetValue(changeAuthorizationId, out Guid project) && project == projectId);

    /// <summary>A new authorisation, applicable to the project.</summary>
    public Guid Authorize(Guid projectId)
    {
        Guid id = Guid.NewGuid();
        _applicable[id] = projectId;
        return id;
    }
}
