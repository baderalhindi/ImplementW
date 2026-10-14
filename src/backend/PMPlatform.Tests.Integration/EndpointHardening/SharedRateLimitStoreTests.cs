using System.Net;
using PMPlatform.Tests.Integration.Identity;

namespace PMPlatform.Tests.Integration.EndpointHardening;

/// <summary>
/// TASK-078: <c>RATE_LIMIT_STORE_CONNECTION_STRING</c>'s verification method — "throttling behaves consistently across multiple API
/// instances". Two APIs share one Redis, as two Cloud Run instances do, and a burst spread across both is throttled at the limit,
/// not at twice it. The store is a real Redis named by <c>RATE_LIMIT_TEST_REDIS</c> (CI's service container; locally e.g.
/// <c>localhost:6379</c>), never the variable itself, which would put every other suite's API on it too.
/// </summary>
[Collection(IdentitySuite.Name)]
public sealed class SharedRateLimitStoreTests(IdentityTestHost host)
{
    private const int Limit = 4;

    private static string Redis => Environment.GetEnvironmentVariable("RATE_LIMIT_TEST_REDIS") is { Length: > 0 } redis
        ? redis
        : throw new InvalidOperationException("RATE_LIMIT_TEST_REDIS is not set: these tests need a Redis server, e.g. localhost:6379.");

    [Fact]
    public async Task TwoApiInstancesSharingTheStoreThrottleABurstAtTheLimit()
    {
        IReadOnlyDictionary<string, string?> settings = HardeningApi.Settings(
            ("RATE_LIMIT_STORE_CONNECTION_STRING", Redis), ("RateLimiting:Policies:SignIn:PermitLimit", $"{Limit}"));
        await using IdentityApiFactory first = host.CreateApi(settings);
        await using IdentityApiFactory second = host.CreateApi(settings);
        using HttpClient one = first.CreateClient();
        using HttpClient two = second.CreateClient();
        string attacker = HardeningApi.NewClientAddress();

        List<HttpStatusCode> statuses = [];
        for (int attempt = 0; attempt < 2 * Limit; attempt++)
        {
            HttpClient instance = attempt % 2 == 0 ? one : two;
            statuses.Add((await instance.SignInFromAsync(attacker, "local.r02", "wrong").OutcomeAsync()).Status);
        }

        Assert.Equal([.. Enumerable.Repeat(HttpStatusCode.Unauthorized, Limit), .. Enumerable.Repeat(HttpStatusCode.TooManyRequests, Limit)], statuses);
    }

    /// <summary>
    /// A store that cannot be reached does not take sign-in down with it: requests are let through, unthrottled, and the API says so
    /// in its log (secret-rotation-runbook class B, "throttling degrades rather than fails"). The log names neither the store nor its password.
    /// </summary>
    [Fact]
    public async Task AnUnreachableStoreLetsRequestsThroughAndSaysSo()
    {
        await using IdentityApiFactory api = host.CreateApi(HardeningApi.Settings(
            ("RATE_LIMIT_STORE_CONNECTION_STRING", "rate-limit-store.invalid:6379,password=not-a-secret,connectTimeout=200"), ("RateLimiting:Policies:SignIn:PermitLimit", "1")));
        using HttpClient client = api.CreateClient();
        string address = HardeningApi.NewClientAddress();

        HttpStatusCode[] statuses =
        [
            (await client.SignInFromAsync(address, "local.r02", "wrong").OutcomeAsync()).Status,
            (await client.SignInFromAsync(address, "local.r02", "wrong").OutcomeAsync()).Status,
            (await client.SignInFromAsync(address, "local.r02", "wrong").OutcomeAsync()).Status,
        ];

        Assert.All(statuses, status => Assert.Equal(HttpStatusCode.Unauthorized, status));
        Assert.Contains("The rate-limit store could not count a request (RedisConnectionException); the request is let through.", host.Logs.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("rate-limit-store.invalid", host.Logs.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("not-a-secret", host.Logs.Text, StringComparison.Ordinal);
    }
}
