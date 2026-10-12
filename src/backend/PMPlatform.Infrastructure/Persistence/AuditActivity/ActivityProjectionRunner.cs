using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PMPlatform.Application.Features.AuditActivity;

namespace PMPlatform.Infrastructure.Persistence.AuditActivity;

/// <summary>Brings the Activity projection up to the store's head, outside any caller's unit of work (TASK-073).</summary>
internal interface IActivityProjectionRunner
{
    public Task ProjectPendingAsync(CancellationToken cancellationToken);
}

/// <summary>Runs projection passes, each in a scope of its own, until one reads less than a full batch.</summary>
internal sealed class ActivityProjectionRunner(IServiceScopeFactory scopes, IOptions<ActivityProjectionOptions> options) : IActivityProjectionRunner
{
    public async Task ProjectPendingAsync(CancellationToken cancellationToken)
    {
        int batchSize = options.Value.BatchSize;
        int read;
        do
        {
            await using AsyncServiceScope scope = scopes.CreateAsyncScope();
            read = await scope.ServiceProvider.GetRequiredService<IActivityProjectionBuilder>().ProjectPendingAsync(batchSize, cancellationToken).ConfigureAwait(false);
        }
        while (read == batchSize);
    }
}

/// <summary>Configuration section <c>Audit:Activity</c>: how often and how much the Activity projection reads (TASK-073).</summary>
internal sealed class ActivityProjectionOptions
{
    public const string Section = "Audit:Activity";

    /// <summary>How long the worker waits after a pass that found nothing more to project. Most events are projected at commit.</summary>
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(5);

    public int BatchSize { get; set; } = 500;
}
