using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PMPlatform.Application.Features.Closure;
using PMPlatform.Infrastructure.Hosting;

namespace PMPlatform.Infrastructure.Closure;

/// <summary>
/// Runs WF-10's activation (TASK-063) in the API process: approved completion and closure cases are activated, each in its own transaction,
/// apart from the approval that decided them (WF-10 P3). Two instances activating the same case cannot both commit: each changes the case's
/// and the project's rows.
/// </summary>
internal sealed class CloseoutActivationWorker(
    IServiceScopeFactory scopes,
    IOptions<CloseoutActivationOptions> options,
    TimeProvider timeProvider,
    ILogger<CloseoutActivationWorker> logger) : PollingWorker(scopes, timeProvider, logger)
{
    protected override TimeSpan PollInterval => options.Value.PollInterval;

    protected override int BatchSize => options.Value.BatchSize;

    protected override string PassName => "Closeout activation";

    protected override Task<int> RunPassAsync(IServiceProvider services, int batchSize, CancellationToken cancellationToken) =>
        services.GetRequiredService<ICloseoutMaintenance>().RunAsync(batchSize, cancellationToken);
}
