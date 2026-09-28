using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PMPlatform.Application.Common.Events;
using PMPlatform.Infrastructure.Hosting;

namespace PMPlatform.Infrastructure.Persistence.Messaging;

/// <summary>Runs the outbox dispatcher in the API process (event-conventions EV-6), every <see cref="OutboxOptions.PollInterval"/> while idle.</summary>
internal sealed class OutboxDispatchWorker(
    IServiceScopeFactory scopes,
    IOptions<OutboxOptions> options,
    TimeProvider timeProvider,
    ILogger<OutboxDispatchWorker> logger) : PollingWorker(scopes, timeProvider, logger)
{
    protected override TimeSpan PollInterval => options.Value.PollInterval;

    protected override int BatchSize => options.Value.BatchSize;

    protected override string PassName => "Outbox dispatch";

    protected override Task<int> RunPassAsync(IServiceProvider services, int batchSize, CancellationToken cancellationToken) =>
        services.GetRequiredService<IOutboxDispatcher>().DispatchDueAsync(batchSize, cancellationToken);
}
