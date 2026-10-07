using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PMPlatform.Application.Features.Suspension;
using PMPlatform.Infrastructure.Hosting;

namespace PMPlatform.Infrastructure.Suspension;

/// <summary>
/// Runs WF-09's scheduled activation (TASK-062) in the API process: approved suspension and resumption requests whose effective date has
/// come are activated, each in its own transaction, apart from the approval that decided them. Two instances activating the same request
/// cannot both commit: each changes the request's and the project's rows.
/// </summary>
internal sealed class SuspensionActivationWorker(
    IServiceScopeFactory scopes,
    IOptions<SuspensionActivationOptions> options,
    TimeProvider timeProvider,
    ILogger<SuspensionActivationWorker> logger) : PollingWorker(scopes, timeProvider, logger)
{
    protected override TimeSpan PollInterval => options.Value.PollInterval;

    protected override int BatchSize => options.Value.BatchSize;

    protected override string PassName => "Suspension activation";

    protected override Task<int> RunPassAsync(IServiceProvider services, int batchSize, CancellationToken cancellationToken) =>
        services.GetRequiredService<ISuspensionMaintenance>().RunAsync(batchSize, cancellationToken);
}
