using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PMPlatform.Application.Features.IntegrationMonitoring;
using PMPlatform.Infrastructure.Hosting;

namespace PMPlatform.Infrastructure.IntegrationMonitoring;

/// <summary>
/// Runs FG-05's pass in the API process (TASK-075): due retries, abandoned attempts, then every instance's freshness and delivery rate.
/// Each item is locked for its work, so passes on two API instances never take the same retry.
/// </summary>
internal sealed class IntegrationMonitoringWorker(
    IServiceScopeFactory scopes,
    IOptions<IntegrationMonitoringWorkerOptions> options,
    TimeProvider timeProvider,
    ILogger<IntegrationMonitoringWorker> logger) : PollingWorker(scopes, timeProvider, logger)
{
    protected override TimeSpan PollInterval => options.Value.PollInterval;

    protected override int BatchSize => options.Value.BatchSize;

    protected override string PassName => "Integration monitoring";

    protected override Task<int> RunPassAsync(IServiceProvider services, int batchSize, CancellationToken cancellationToken) =>
        services.GetRequiredService<IIntegrationMonitoringMaintenance>().RunAsync(batchSize, cancellationToken);
}
