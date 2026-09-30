using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PMPlatform.Application.Features.Notifications;
using PMPlatform.Infrastructure.Hosting;

namespace PMPlatform.Infrastructure.Notifications;

/// <summary>
/// Runs the WF-15 pass (TASK-039) in the API process. Every instance runs one; each intent and delivery is claimed with
/// a row lock that another instance skips, so none is routed or sent twice at once.
/// </summary>
internal sealed class NotificationWorker(
    IServiceScopeFactory scopes,
    IOptions<NotificationWorkerOptions> options,
    TimeProvider timeProvider,
    ILogger<NotificationWorker> logger) : PollingWorker(scopes, timeProvider, logger)
{
    protected override TimeSpan PollInterval => options.Value.PollInterval;

    protected override int BatchSize => options.Value.BatchSize;

    protected override string PassName => "Notification";

    protected override Task<int> RunPassAsync(IServiceProvider services, int batchSize, CancellationToken cancellationToken) =>
        services.GetRequiredService<INotificationProcessing>().RunAsync(batchSize, cancellationToken);
}
