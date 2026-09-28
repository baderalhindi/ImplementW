using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PMPlatform.Application.Features.AuditActivity;
using PMPlatform.Infrastructure.Hosting;

namespace PMPlatform.Infrastructure.Audit;

/// <summary>
/// Runs the SIEM forwarder in the API process (TASK-033), every <see cref="SiemOptions.PollInterval"/> while idle.
/// Every API instance runs one, so an event may reach the SIEM twice; it carries its id for the SIEM to recognise that.
/// </summary>
internal sealed class SiemForwardingWorker(
    IServiceScopeFactory scopes,
    IOptions<SiemOptions> options,
    TimeProvider timeProvider,
    ILogger<SiemForwardingWorker> logger) : PollingWorker(scopes, timeProvider, logger)
{
    protected override TimeSpan PollInterval => options.Value.PollInterval;

    protected override int BatchSize => options.Value.BatchSize;

    protected override string PassName => "SIEM forwarding";

    protected override Task<int> RunPassAsync(IServiceProvider services, int batchSize, CancellationToken cancellationToken) =>
        services.GetRequiredService<ISiemForwarder>().ForwardPendingAsync(batchSize, cancellationToken);
}
