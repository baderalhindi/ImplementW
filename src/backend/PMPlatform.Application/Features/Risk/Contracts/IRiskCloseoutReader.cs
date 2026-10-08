namespace PMPlatform.Application.Features.Risk.Contracts;

/// <summary>
/// ADR-003 §8.2 edge 42, Closure → Risk (query; TASK-063): what WF-10's readiness needs of a project's risks. WF-10 never closes or accepts
/// a risk (BR-CLO-013); residual risk is accepted through WF-06's own acceptance (BR-CLO-022). It authorizes no one.
/// </summary>
public interface IRiskCloseoutReader
{
    /// <summary>The project's risks that are not CLOSED and carry no ACTIVE acceptance.</summary>
    public Task<int> CountOpenAsync(Guid projectId, CancellationToken cancellationToken);
}
