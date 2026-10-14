using PMPlatform.Application.Common.RateLimiting;
using PMPlatform.Infrastructure.RateLimiting;

namespace PMPlatform.Tests.Unit.Infrastructure.RateLimiting;

/// <summary>TASK-078: the per-instance counters — a fixed window that opens at a key's first request.</summary>
public sealed class InMemoryRateLimitStoreTests
{
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(1);

    private readonly SteppingClock _clock = new();

    [Fact]
    public async Task AWindowCountsEveryRequestOnItsKeyAndSaysWhenItCloses()
    {
        InMemoryRateLimitStore store = new(_clock);

        RateLimitCount? first = await store.IncrementAsync("SignIn:address:a", Window, CancellationToken.None);
        _clock.Advance(TimeSpan.FromSeconds(20));
        RateLimitCount? second = await store.IncrementAsync("SignIn:address:a", Window, CancellationToken.None);

        Assert.Equal(new RateLimitCount(1, Window), first);
        Assert.Equal(new RateLimitCount(2, TimeSpan.FromSeconds(40)), second);
    }

    [Fact]
    public async Task KeysAreCountedApart()
    {
        InMemoryRateLimitStore store = new(_clock);

        await store.IncrementAsync("SignIn:address:a", Window, CancellationToken.None);
        await store.IncrementAsync("SignIn:address:a", Window, CancellationToken.None);

        Assert.Equal(1, (await store.IncrementAsync("SignIn:address:b", Window, CancellationToken.None))!.Count);
        Assert.Equal(1, (await store.IncrementAsync("SessionRefresh:address:a", Window, CancellationToken.None))!.Count);
    }

    /// <summary>The window closes exactly <c>window</c> after its first request; the next request opens a new one at 1.</summary>
    [Fact]
    public async Task TheNextRequestAfterTheWindowClosesOpensANewOne()
    {
        InMemoryRateLimitStore store = new(_clock);
        for (int request = 0; request < 3; request++)
        {
            await store.IncrementAsync("k", Window, CancellationToken.None);
        }

        _clock.Advance(Window - TimeSpan.FromTicks(1));
        Assert.Equal(4, (await store.IncrementAsync("k", Window, CancellationToken.None))!.Count);
        _clock.Advance(TimeSpan.FromTicks(1));
        Assert.Equal(new RateLimitCount(1, Window), await store.IncrementAsync("k", Window, CancellationToken.None));
    }

    /// <summary>Concurrent requests on one key are each counted once: the count is the number of requests, no more and no fewer.</summary>
    [Fact]
    public async Task ConcurrentRequestsAreEachCountedOnce()
    {
        InMemoryRateLimitStore store = new(_clock);

        RateLimitCount?[] counts = await Task.WhenAll(Enumerable.Range(0, 500).Select(_ => Task.Run(() => store.IncrementAsync("k", Window, CancellationToken.None))));

        Assert.Equal(Enumerable.Range(1, 500).Select(count => (long)count), counts.Select(count => count!.Count).Order());
    }

    private sealed class SteppingClock : TimeProvider
    {
        private DateTimeOffset _now = new(2026, 10, 14, 9, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan by) => _now += by;
    }
}
