using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace PMPlatform.Infrastructure.Hosting;

/// <summary>
/// A worker in the API process that runs one pass after another, each in its own scope: a pass that filled its batch
/// is followed at once by the next, otherwise the worker waits <see cref="PollInterval"/>. A failed pass is logged and
/// retried; it never stops the API. Every API instance runs one, so a pass must be safe to run twice at once.
/// </summary>
internal abstract partial class PollingWorker(IServiceScopeFactory scopes, TimeProvider timeProvider, ILogger logger) : BackgroundService
{
    protected abstract TimeSpan PollInterval { get; }

    protected abstract int BatchSize { get; }

    /// <summary>What the pass is, for the log.</summary>
    protected abstract string PassName { get; }

    /// <summary>One pass; returns how many items it handled.</summary>
    protected abstract Task<int> RunPassAsync(IServiceProvider services, int batchSize, CancellationToken cancellationToken);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            int handled = 0;
            try
            {
                await using AsyncServiceScope scope = scopes.CreateAsyncScope();
                handled = await RunPassAsync(scope.ServiceProvider, BatchSize, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
#pragma warning disable CA1031 // The worker outlives any one failed pass: the database or the remote end may be back on the next.
            catch (Exception exception)
#pragma warning restore CA1031
            {
                LogPassFailed(logger, PassName, exception.GetType().Name);
            }

            if (handled < BatchSize)
            {
                try
                {
                    await Task.Delay(PollInterval, timeProvider, stoppingToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "{Pass} pass failed: {Reason}. It is retried on the next pass.")]
    private static partial void LogPassFailed(ILogger logger, string pass, string reason);
}
