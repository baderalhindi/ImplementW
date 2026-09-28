using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PMPlatform.Application.Features.Approval;
using PMPlatform.Infrastructure.Hosting;

namespace PMPlatform.Infrastructure.Approval;

/// <summary>
/// Runs the WF-11 time-driven pass (TASK-035) in the API process. Two instances escalating the same task at once cannot
/// both commit: each changes the run's row, and the second meets a changed row version.
/// </summary>
internal sealed class ApprovalMaintenanceWorker(
    IServiceScopeFactory scopes,
    IOptions<ApprovalMaintenanceOptions> options,
    TimeProvider timeProvider,
    ILogger<ApprovalMaintenanceWorker> logger) : PollingWorker(scopes, timeProvider, logger)
{
    protected override TimeSpan PollInterval => options.Value.PollInterval;

    protected override int BatchSize => options.Value.BatchSize;

    protected override string PassName => "Approval maintenance";

    protected override Task<int> RunPassAsync(IServiceProvider services, int batchSize, CancellationToken cancellationToken) =>
        services.GetRequiredService<IApprovalMaintenance>().RunAsync(batchSize, cancellationToken);
}
