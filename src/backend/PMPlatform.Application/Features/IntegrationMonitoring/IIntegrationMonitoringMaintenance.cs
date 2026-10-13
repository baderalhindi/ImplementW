namespace PMPlatform.Application.Features.IntegrationMonitoring;

/// <summary>FG-05's background pass: due retries, abandoned attempts, then every instance's time-driven health (TASK-075).</summary>
public interface IIntegrationMonitoringMaintenance
{
    /// <summary>Returns how many retries and abandoned attempts it handled, so a full batch is followed at once by another pass.</summary>
    public Task<int> RunAsync(int batchSize, CancellationToken cancellationToken);
}

internal sealed class IntegrationMonitoringMaintenance(IntegrationInvocationEngine engine, IntegrationHealthEvaluation evaluation) : IIntegrationMonitoringMaintenance
{
    public async Task<int> RunAsync(int batchSize, CancellationToken cancellationToken)
    {
        int handled = await engine.RunDueRetriesAsync(batchSize, cancellationToken).ConfigureAwait(false);
        handled += await engine.EndAbandonedAttemptsAsync(batchSize, cancellationToken).ConfigureAwait(false);
        await evaluation.EvaluateAsync(cancellationToken).ConfigureAwait(false);
        return handled;
    }
}
