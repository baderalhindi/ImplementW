using PMPlatform.Application.Common.RateLimiting;

namespace PMPlatform.Infrastructure.RateLimiting;

/// <summary>
/// Counters held by this API instance (TASK-078): the store when <c>RATE_LIMIT_STORE_CONNECTION_STRING</c> is empty, as in DEV
/// and on a developer's machine. With several instances each counts on its own, so a client spread across them gets up to
/// the instance count times the limit. Closed windows are dropped once a minute so that the table holds only open ones.
/// </summary>
internal sealed class InMemoryRateLimitStore(TimeProvider timeProvider) : IRateLimitStore
{
    private static readonly TimeSpan SweepInterval = TimeSpan.FromMinutes(1);

    private readonly Dictionary<string, Window> _windows = new(StringComparer.Ordinal);
    private readonly Lock _gate = new();
    private DateTimeOffset _nextSweep = DateTimeOffset.MinValue;

    public Task<RateLimitCount?> IncrementAsync(string key, TimeSpan window, CancellationToken cancellationToken)
    {
        DateTimeOffset now = timeProvider.GetUtcNow();
        lock (_gate)
        {
            if (now >= _nextSweep)
            {
                foreach (string closed in _windows.Where(entry => entry.Value.ClosesAt <= now).Select(entry => entry.Key).ToList())
                {
                    _windows.Remove(closed);
                }

                _nextSweep = now + SweepInterval;
            }

            Window current = _windows.TryGetValue(key, out Window? open) && open.ClosesAt > now ? open : new Window(now + window);
            current.Count++;
            _windows[key] = current;
            return Task.FromResult<RateLimitCount?>(new RateLimitCount(current.Count, current.ClosesAt - now));
        }
    }

    private sealed class Window(DateTimeOffset closesAt)
    {
        public DateTimeOffset ClosesAt { get; } = closesAt;

        public long Count { get; set; }
    }
}
