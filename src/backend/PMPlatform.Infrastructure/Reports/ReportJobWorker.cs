using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PMPlatform.Application.Features.Reports;
using PMPlatform.Infrastructure.Hosting;

namespace PMPlatform.Infrastructure.Reports;

/// <summary>
/// Runs FG-02's report jobs in the API process (TASK-071; DC-RPT-01): every PDF, XLSX and CSV is generated here, apart from the request that asked for
/// it, as its requester may see when it runs. Two instances never run one job twice: each step claims the job by saving it unchanged since read.
/// </summary>
internal sealed class ReportJobWorker(
    IServiceScopeFactory scopes,
    IOptions<ReportJobOptions> options,
    TimeProvider timeProvider,
    ILogger<ReportJobWorker> logger) : PollingWorker(scopes, timeProvider, logger)
{
    protected override TimeSpan PollInterval => options.Value.PollInterval;

    protected override int BatchSize => options.Value.BatchSize;

    protected override string PassName => "Report jobs";

    protected override Task<int> RunPassAsync(IServiceProvider services, int batchSize, CancellationToken cancellationToken) =>
        services.GetRequiredService<IReportMaintenance>().RunAsync(
            new ReportMaintenancePolicy(batchSize, options.Value.OutputLifetime, options.Value.MaxExportRows, options.Value.AbandonAfter), cancellationToken);
}
