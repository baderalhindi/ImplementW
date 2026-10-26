namespace PMPlatform.Application.Features.Risk;

/// <summary>WF-06's time-driven pass (TASK-055): risk acceptances that reach their expiry, run by a worker in the API process.</summary>
public interface IRiskMaintenance
{
    /// <summary>Expires up to <paramref name="batchSize"/> lapsed acceptances, each in its own unit of work; returns how many it expired.</summary>
    public Task<int> RunAsync(int batchSize, CancellationToken cancellationToken);
}
