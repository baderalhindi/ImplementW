using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PMPlatform.Application.Features.Risk;
using PMPlatform.Infrastructure.Hosting;

namespace PMPlatform.Infrastructure.Risk;

/// <summary>
/// Runs WF-06's time-driven pass (TASK-055) in the API process: acceptances whose expiry has come are expired and their risks
/// returned for review. Two instances expiring the same acceptance cannot both commit: each changes the risk's row.
/// </summary>
internal sealed class RiskMaintenanceWorker(
    IServiceScopeFactory scopes,
    IOptions<RiskMaintenanceOptions> options,
    TimeProvider timeProvider,
    ILogger<RiskMaintenanceWorker> logger) : PollingWorker(scopes, timeProvider, logger)
{
    protected override TimeSpan PollInterval => options.Value.PollInterval;

    protected override int BatchSize => options.Value.BatchSize;

    protected override string PassName => "Risk acceptance expiry";

    protected override Task<int> RunPassAsync(IServiceProvider services, int batchSize, CancellationToken cancellationToken) =>
        services.GetRequiredService<IRiskMaintenance>().RunAsync(batchSize, cancellationToken);
}
