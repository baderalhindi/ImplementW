using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PMPlatform.Application.Features.Project.Contracts;
using PMPlatform.Infrastructure.Hosting;

namespace PMPlatform.Infrastructure.DataImport;

/// <summary>
/// Configuration section <c>ProjectImport:Validation</c>: how often queued project import validations are run, and how many a pass takes. A
/// validation of 1,500 rows takes seconds, so a pass takes few.
/// </summary>
internal sealed class ProjectImportValidationOptions
{
    public const string Section = "ProjectImport:Validation";

    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(5);

    public int BatchSize { get; set; } = 2;
}

/// <summary>
/// Validates the project import workbooks of more than 200 rows in the API process (the 2 Nov 2026 review §4, step 3), apart from the request
/// that uploaded them, as their importer may import when it runs; the importer is notified when each is done.
/// </summary>
internal sealed class ProjectImportValidationWorker(
    IServiceScopeFactory scopes,
    WorkerActivity activity,
    IOptions<ProjectImportValidationOptions> options,
    TimeProvider timeProvider,
    ILogger<ProjectImportValidationWorker> logger) : PollingWorker(scopes, activity, timeProvider, logger)
{
    protected override TimeSpan PollInterval => options.Value.PollInterval;

    protected override int BatchSize => options.Value.BatchSize;

    protected override string PassName => "Project import validations";

    protected override Task<int> RunPassAsync(IServiceProvider services, int batchSize, CancellationToken cancellationToken) =>
        services.GetRequiredService<IProjectImportValidationRunner>().RunAsync(batchSize, cancellationToken);
}
