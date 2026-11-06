using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PMPlatform.Application.Features.ExternalParticipation.MonthlyUpdates;
using PMPlatform.Infrastructure.Hosting;

namespace PMPlatform.Infrastructure.ExternalParticipation;

/// <summary>Bound from <c>ExternalParticipation:MonthlyUpdate</c>. An hour's pause suffices: each request gets one reminder a day, and a due batch goes out the same day.</summary>
internal sealed class MonthlyUpdateOptions
{
    public const string Section = "ExternalParticipation:MonthlyUpdate";

    public TimeSpan PollInterval { get; set; } = TimeSpan.FromHours(1);

    public int BatchSize { get; set; } = 500;
}

/// <summary>Runs the monthly update's reminders and repeating batches (the 5 Nov 2026 review §2) in the API process.</summary>
internal sealed class MonthlyUpdateWorker(
    IServiceScopeFactory scopes,
    WorkerActivity activity,
    IOptions<MonthlyUpdateOptions> options,
    TimeProvider timeProvider,
    ILogger<MonthlyUpdateWorker> logger) : PollingWorker(scopes, activity, timeProvider, logger)
{
    protected override TimeSpan PollInterval => options.Value.PollInterval;

    protected override int BatchSize => options.Value.BatchSize;

    protected override string PassName => "Monthly update";

    protected override Task<int> RunPassAsync(IServiceProvider services, int batchSize, CancellationToken cancellationToken) =>
        services.GetRequiredService<IMonthlyUpdateMaintenance>().RunAsync(batchSize, cancellationToken);
}
