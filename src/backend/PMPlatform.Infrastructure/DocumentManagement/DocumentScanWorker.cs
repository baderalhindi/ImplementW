using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PMPlatform.Application.Features.DocumentManagement;
using PMPlatform.Infrastructure.Hosting;

namespace PMPlatform.Infrastructure.DocumentManagement;

/// <summary>
/// Runs the malware scan (TASK-037) in the API process. Two instances deciding one version at once cannot both commit:
/// the version's row version lets only the first.
/// </summary>
internal sealed class DocumentScanWorker(
    IServiceScopeFactory scopes,
    IOptions<DocumentScanOptions> options,
    TimeProvider timeProvider,
    ILogger<DocumentScanWorker> logger) : PollingWorker(scopes, timeProvider, logger)
{
    protected override TimeSpan PollInterval => options.Value.PollInterval;

    protected override int BatchSize => options.Value.BatchSize;

    protected override string PassName => "Document malware scan";

    protected override Task<int> RunPassAsync(IServiceProvider services, int batchSize, CancellationToken cancellationToken) =>
        services.GetRequiredService<IDocumentScanning>().RunAsync(batchSize, cancellationToken);
}
