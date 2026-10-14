namespace PMPlatform.Application.Common.RateLimiting;

/// <summary>
/// The counters behind per-client rate limiting (TASK-078, CTL-12). A counter is a fixed window that opens at its first
/// request: it counts every request on its key until <c>window</c> has passed since then, and the next request opens a new one.
/// With <c>RATE_LIMIT_STORE_CONNECTION_STRING</c> the counters are shared by every API instance; without it each instance
/// counts on its own.
/// </summary>
public interface IRateLimitStore
{
    /// <summary>
    /// Counts one request on <paramref name="key"/>. Null when the store cannot count: the caller lets the request through,
    /// because throttling degrades rather than fails (secret-rotation-runbook, class B).
    /// </summary>
    public Task<RateLimitCount?> IncrementAsync(string key, TimeSpan window, CancellationToken cancellationToken);
}

/// <summary>The requests counted in the key's current window, this one included, and the time until the window closes.</summary>
public sealed record RateLimitCount(long Count, TimeSpan ResetAfter);
