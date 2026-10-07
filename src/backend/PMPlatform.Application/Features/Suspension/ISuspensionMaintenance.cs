namespace PMPlatform.Application.Features.Suspension;

/// <summary>WF-09's time-driven pass (TASK-062): approved requests whose effective date has come, run by a worker in the API process.</summary>
public interface ISuspensionMaintenance
{
    /// <summary>Activates up to <paramref name="batchSize"/> due requests, each in its own unit of work; returns how many it effected.</summary>
    public Task<int> RunAsync(int batchSize, CancellationToken cancellationToken);
}
