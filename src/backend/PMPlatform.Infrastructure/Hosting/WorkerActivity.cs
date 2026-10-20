using System.Collections.Concurrent;

namespace PMPlatform.Infrastructure.Hosting;

/// <summary>
/// When each <see cref="PollingWorker"/>'s current pass began, for <see cref="WorkerLivenessHealthCheck"/> (TASK-091). A worker between
/// passes has no entry. One per process.
/// </summary>
internal sealed class WorkerActivity
{
    private readonly ConcurrentDictionary<string, DateTimeOffset> _passesInProgress = new(StringComparer.Ordinal);

    public void PassStarted(string worker, DateTimeOffset at) => _passesInProgress[worker] = at;

    public void PassEnded(string worker) => _passesInProgress.TryRemove(worker, out _);

    /// <summary>The workers in a pass, with when it began.</summary>
    public IReadOnlyDictionary<string, DateTimeOffset> PassesInProgress() => new Dictionary<string, DateTimeOffset>(_passesInProgress, StringComparer.Ordinal);
}
