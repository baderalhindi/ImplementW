namespace PMPlatform.Application.Features.Progress.Contracts;

/// <summary>
/// ICD-03 for the read side (ADR-003 §8.2 edge 29): Overall Project Health as WF-02 published it, and the current value
/// beside it. A consumer renders these and never recalculates them (M-12). It authorizes no one: the consumer decides
/// what its viewer may see.
/// </summary>
public interface IProjectHealthReader
{
    public Task<ProjectHealthView> GetAsync(Guid projectId, CancellationToken cancellationToken);
}
