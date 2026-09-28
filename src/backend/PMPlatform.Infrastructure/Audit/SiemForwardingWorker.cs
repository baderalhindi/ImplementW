using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PMPlatform.Application.Features.AuditActivity;

namespace PMPlatform.Infrastructure.Audit;

/// <summary>
/// Runs the SIEM forwarder in the API process (TASK-033): a full batch is followed at once by the next, otherwise the
/// worker waits <see cref="SiemOptions.PollInterval"/>. A failed pass is logged and retried; it never stops the API.
/// Every API instance runs one, so an event may reach the SIEM twice; it carries its id for the SIEM to recognise that.
/// </summary>
internal sealed partial class SiemForwardingWorker(
    IServiceScopeFactory scopes,
    IOptions<SiemOptions> options,
    TimeProvider timeProvider,
    ILogger<SiemForwardingWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        SiemOptions siem = options.Value;
        while (!stoppingToken.IsCancellationRequested)
        {
            int forwarded = 0;
            try
            {
                await using AsyncServiceScope scope = scopes.CreateAsyncScope();
                forwarded = await scope.ServiceProvider.GetRequiredService<ISiemForwarder>()
                    .ForwardPendingAsync(siem.BatchSize, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
#pragma warning disable CA1031 // The worker outlives any one failed pass: the database or SIEM may be back on the next.
            catch (Exception exception)
#pragma warning restore CA1031
            {
                LogPassFailed(logger, exception.GetType().Name);
            }

            if (forwarded < siem.BatchSize)
            {
                try
                {
                    await Task.Delay(siem.PollInterval, timeProvider, stoppingToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "SIEM forwarding pass failed: {Reason}. It is retried on the next pass.")]
    private static partial void LogPassFailed(ILogger logger, string reason);
}
