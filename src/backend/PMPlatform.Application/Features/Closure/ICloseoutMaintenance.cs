namespace PMPlatform.Application.Features.Closure;

/// <summary>WF-10's activation pass (TASK-063): approved completion and closure cases, run by a worker in the API process.</summary>
public interface ICloseoutMaintenance
{
    /// <summary>Activates up to <paramref name="batchSize"/> approved cases of each kind, each in its own unit of work; returns how many it effected.</summary>
    public Task<int> RunAsync(int batchSize, CancellationToken cancellationToken);
}
