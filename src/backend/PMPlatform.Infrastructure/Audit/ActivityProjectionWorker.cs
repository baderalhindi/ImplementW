using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PMPlatform.Application.Features.AuditActivity;
using PMPlatform.Infrastructure.Hosting;
using PMPlatform.Infrastructure.Persistence.AuditActivity;

namespace PMPlatform.Infrastructure.Audit;

/// <summary>
/// Projects the stored events the commit path did not, every <see cref="ActivityProjectionOptions.PollInterval"/> while idle
/// (TASK-073). Passes hold the projection's checkpoint one at a time, so instances never project an event twice.
/// </summary>
internal sealed class ActivityProjectionWorker(
    IServiceScopeFactory scopes,
    WorkerActivity activity,
    IOptions<ActivityProjectionOptions> options,
    TimeProvider timeProvider,
    ILogger<ActivityProjectionWorker> logger) : PollingWorker(scopes, activity, timeProvider, logger)
{
    protected override TimeSpan PollInterval => options.Value.PollInterval;

    protected override int BatchSize => options.Value.BatchSize;

    protected override string PassName => "Activity projection";

    protected override Task<int> RunPassAsync(IServiceProvider services, int batchSize, CancellationToken cancellationToken) =>
        services.GetRequiredService<IActivityProjectionBuilder>().ProjectPendingAsync(batchSize, cancellationToken);
}
