using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PMPlatform.Application.Features.AuditActivity;
using PMPlatform.Infrastructure.Hosting;
using PMPlatform.Infrastructure.Persistence.AuditActivity;

namespace PMPlatform.Infrastructure.Audit;

/// <summary>
/// Stores the audit messages that were not ingested at commit — the API stopped, the store refused, a lock timed out — every
/// <see cref="AuditIngestionOptions.PollInterval"/> while idle (TASK-073). Every API instance runs one; a message is locked while
/// it is stored, so two instances never store it twice.
/// </summary>
internal sealed class AuditIngestionWorker(
    IServiceScopeFactory scopes,
    WorkerActivity activity,
    IOptions<AuditIngestionOptions> options,
    TimeProvider timeProvider,
    ILogger<AuditIngestionWorker> logger) : PollingWorker(scopes, activity, timeProvider, logger)
{
    protected override TimeSpan PollInterval => options.Value.PollInterval;

    protected override int BatchSize => options.Value.BatchSize;

    protected override string PassName => "Audit ingestion";

    protected override Task<int> RunPassAsync(IServiceProvider services, int batchSize, CancellationToken cancellationToken) =>
        services.GetRequiredService<IAuditIngestor>().IngestDueAsync(batchSize, cancellationToken);
}
