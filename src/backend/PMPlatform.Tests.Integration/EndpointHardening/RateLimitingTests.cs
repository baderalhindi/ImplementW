using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using PMPlatform.Api.Authorization;
using PMPlatform.Api.Middleware;
using PMPlatform.Tests.Integration.Identity;

namespace PMPlatform.Tests.Integration.EndpointHardening;

/// <summary>
/// TASK-078 (CTL-12), acceptance criterion 1: a scripted burst of login attempts beyond the configured threshold is throttled with a
/// standard 429. Each test runs its own API with a small limit, so the burst is short; the shipped limits are in appsettings.json.
/// </summary>
[Collection(IdentitySuite.Name)]
public sealed class RateLimitingTests(IdentityTestHost host)
{
    private const int Limit = 5;

    private static readonly IReadOnlyDictionary<string, string?> SignInLimit = HardeningApi.Settings(
        ("RateLimiting:Policies:SignIn:PermitLimit", $"{Limit}"), ("RateLimiting:Policies:SignIn:Window", "00:01:00"));

    /// <summary>
    /// The burst: the first five wrong passwords are the directory's 401s; the sixth attempt — even with the right password — is a 429
    /// in the R-23 envelope with Retry-After, and is not checked against the directory. Another client is unaffected.
    /// </summary>
    [Fact]
    public async Task ASignInBurstBeyondTheLimitIsThrottledWith429AndRetryAfter()
    {
        await using IdentityApiFactory api = host.CreateApi(SignInLimit);
        using HttpClient client = api.CreateClient();
        string attacker = HardeningApi.NewClientAddress();

        for (int attempt = 1; attempt <= Limit; attempt++)
        {
            Assert.Equal((HttpStatusCode.Unauthorized, "AUTHENTICATION_REQUIRED"),
                await client.SignInFromAsync(attacker, "local.r02", $"wrong-{attempt}").OutcomeAsync());
        }

        using HttpResponseMessage throttled = await client.SignInFromAsync(attacker, "local.r02", TestDirectory.PersonPassword);
        Assert.Equal(HttpStatusCode.TooManyRequests, throttled.StatusCode);
        Assert.Equal("application/problem+json", throttled.Content.Headers.ContentType?.MediaType);
        Assert.Equal(
            """{"type":"urn:pmplatform:problem:rate-limited","title":"Too many requests.","status":429,"instance":"/api/v1/sessions","code":"RATE_LIMITED","idempotencyKey":null}""",
            await throttled.ReadProblemShapeAsync());
        TimeSpan retryAfter = Assert.IsType<TimeSpan>(throttled.Headers.RetryAfter?.Delta);
        Assert.InRange(retryAfter, TimeSpan.FromSeconds(1), TimeSpan.FromMinutes(1));

        using HttpResponseMessage otherClient = await client.SignInFromAsync(HardeningApi.NewClientAddress(), "local.r02", TestDirectory.PersonPassword);
        Assert.NotEqual(HttpStatusCode.TooManyRequests, otherClient.StatusCode);
        Assert.Contains("Rate limit SignIn reached by address:" + attacker, host.Logs.Text, StringComparison.Ordinal);
    }

    /// <summary>The counter is the client's, whatever it writes into X-Forwarded-For itself: only the load balancer's entry is believed.</summary>
    [Fact]
    public async Task ASpoofedForwardedForDoesNotEscapeTheLimit()
    {
        await using IdentityApiFactory api = host.CreateApi(SignInLimit);
        using HttpClient client = api.CreateClient();
        string attacker = HardeningApi.NewClientAddress();

        List<HttpStatusCode> statuses = [];
        for (int attempt = 0; attempt <= Limit; attempt++)
        {
            statuses.Add((await client.SignInFromAsync(attacker, "local.r02", "wrong", spoofed: HardeningApi.NewClientAddress()).OutcomeAsync()).Status);
        }

        Assert.Equal([.. Enumerable.Repeat(HttpStatusCode.Unauthorized, Limit), HttpStatusCode.TooManyRequests], statuses);
    }

    /// <summary>Every step of a sign-in counts against the same limit: the MFA challenge and code included, not only the password.</summary>
    [Fact]
    public async Task EverySignInStepCountsAgainstTheSameLimit()
    {
        await using IdentityApiFactory api = host.CreateApi(SignInLimit);
        using HttpClient client = api.CreateClient();
        string attacker = HardeningApi.NewClientAddress();
        string[] steps = [SessionApi.Sessions, SessionApi.MfaChallenge, SessionApi.MfaSessions, SessionApi.SsoSessions, SessionApi.StepUp];

        List<HttpStatusCode> statuses = [];
        foreach (string step in steps.Append(SessionApi.MfaChallenge))
        {
            using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, step) { Content = System.Net.Http.Json.JsonContent.Create(new { }) }.From(attacker);
            using HttpResponseMessage response = await client.SendAsync(request);
            statuses.Add(response.StatusCode);
        }

        Assert.All(statuses.Take(Limit), status => Assert.NotEqual(HttpStatusCode.TooManyRequests, status));
        Assert.Equal(HttpStatusCode.TooManyRequests, statuses[^1]);
    }

    /// <summary>The window opens at the client's first request; once it has passed, the client may try again.</summary>
    [Fact]
    public async Task TheClientMayTryAgainOnceTheWindowHasPassed()
    {
        await using IdentityApiFactory api = host.CreateApi(SignInLimit);
        using HttpClient client = api.CreateClient();
        string client1 = HardeningApi.NewClientAddress();
        try
        {
            for (int attempt = 0; attempt < Limit; attempt++)
            {
                await client.SignInFromAsync(client1, "local.r02", "wrong").OutcomeAsync();
            }

            Assert.Equal(HttpStatusCode.TooManyRequests, (await client.SignInFromAsync(client1, "local.r02", "wrong").OutcomeAsync()).Status);
            host.Clock.Advance(TimeSpan.FromSeconds(61));
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.SignInFromAsync(client1, "local.r02", "wrong").OutcomeAsync()).Status);
        }
        finally
        {
            host.Clock.Reset();
        }
    }

    /// <summary>The refresh has its own counter: a tab refreshing its session does not use up the sign-in attempts, and the reverse.</summary>
    [Fact]
    public async Task TheRefreshIsCountedApartFromSignIn()
    {
        await using IdentityApiFactory api = host.CreateApi(HardeningApi.Settings(
            ("RateLimiting:Policies:SignIn:PermitLimit", "1"), ("RateLimiting:Policies:SessionRefresh:PermitLimit", "2")));
        using HttpClient client = api.CreateClient();
        string address = HardeningApi.NewClientAddress();

        async Task<HttpStatusCode> RefreshAsync()
        {
            using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, SessionApi.Refresh)
            { Content = System.Net.Http.Json.JsonContent.Create(new { refreshToken = "not-a-token" }) }.From(address);
            using HttpResponseMessage response = await client.SendAsync(request);
            return response.StatusCode;
        }

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.SignInFromAsync(address, "local.r02", "wrong").OutcomeAsync()).Status);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await client.SignInFromAsync(address, "local.r02", "wrong").OutcomeAsync()).Status);
        Assert.Equal([HttpStatusCode.Unauthorized, HttpStatusCode.Unauthorized, HttpStatusCode.TooManyRequests],
            [await RefreshAsync(), await RefreshAsync(), await RefreshAsync()]);
    }

    /// <summary>
    /// A sensitive write is counted per user, wherever the user is: one user's burst from two addresses is throttled, and another
    /// user behind the same address is not — the corporate-egress case (network-security.md N-F4).
    /// </summary>
    [Fact]
    public async Task SensitiveWritesAreCountedPerUser()
    {
        await using IdentityApiFactory api = host.CreateApi(HardeningApi.Settings(("RateLimiting:Policies:SensitiveWrite:PermitLimit", "3")));
        using HttpClient client = api.CreateClient();
        string office = HardeningApi.NewClientAddress();
        Session burst = await client.SignInOrFailAsync(2);
        Session colleague = await client.SignInOrFailAsync(3);

        List<HttpStatusCode> statuses = [];
        foreach (string address in new[] { office, HardeningApi.NewClientAddress(), office, HardeningApi.NewClientAddress() })
        {
            statuses.Add((await client.SensitiveWriteAsync(burst.AccessToken, address).OutcomeAsync()).Status);
        }

        Assert.All(statuses.Take(3), status => Assert.NotEqual(HttpStatusCode.TooManyRequests, status));
        Assert.Equal((HttpStatusCode.TooManyRequests, "RATE_LIMITED"), await client.SensitiveWriteAsync(burst.AccessToken, office).OutcomeAsync());
        Assert.Equal(HttpStatusCode.TooManyRequests, statuses[^1]);
        Assert.NotEqual(HttpStatusCode.TooManyRequests, (await client.SensitiveWriteAsync(colleague.AccessToken, office).OutcomeAsync()).Status);
    }

    /// <summary>
    /// What is limited is declared, not left to chance: every anonymous action is a sign-in step or the refresh, and every sensitive write
    /// (R-35) is under the sensitive-write policy. A new anonymous endpoint without a policy fails here.
    /// </summary>
    [Fact]
    public void EveryAnonymousActionAndEverySensitiveWriteIsRateLimited()
    {
        Endpoint[] actions = [.. host.Api.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .Where(endpoint => endpoint.Metadata.GetMetadata<ControllerActionDescriptor>() is not null)];
        static string Name(Endpoint endpoint) => endpoint.Metadata.GetMetadata<IEndpointNameMetadata>()!.EndpointName;

        Endpoint[] anonymous = [.. actions.Where(endpoint => endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null)];
        Assert.Equal(10, anonymous.Length);
        Assert.All(anonymous, endpoint => Assert.Equal(
            Name(endpoint) == "IdentityAccess_RefreshSession" ? RateLimitPolicy.SessionRefresh : RateLimitPolicy.SignIn, RateLimiting.PolicyOf(endpoint)));

        Endpoint[] sensitive = [.. actions.Where(endpoint => endpoint.Metadata.GetMetadata<SensitiveWriteAttribute>() is not null)];
        Assert.NotEmpty(sensitive);
        Assert.All(sensitive, endpoint => Assert.Equal(RateLimitPolicy.SensitiveWrite, RateLimiting.PolicyOf(endpoint)));

        Assert.Equal(anonymous.Length + sensitive.Length, actions.Select(RateLimiting.PolicyOf).Count(policy => policy is not null));
    }

    /// <summary>A policy without a positive limit and window stops the API at start-up: no endpoint is silently unlimited.</summary>
    [Theory]
    [InlineData("RateLimiting:Policies:SignIn:PermitLimit", "0")]
    [InlineData("RateLimiting:Policies:SensitiveWrite:Window", "00:00:00")]
    public void AnIncompletePolicyStopsTheApi(string key, string value)
    {
        using IdentityApiFactory api = host.CreateApi(new Dictionary<string, string?> { [key] = value });

        Exception failure = Assert.ThrowsAny<Exception>(api.CreateClient);
        Assert.Contains("RateLimiting:Policies needs a positive PermitLimit and Window", failure.ToString(), StringComparison.Ordinal);
    }

    /// <summary>Retry-After is whole seconds, rounded up and never 0, which would invite an immediate retry that is refused again.</summary>
    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 1)]
    [InlineData(1000, 1)]
    [InlineData(1001, 2)]
    [InlineData(59_999, 60)]
    public void RetryAfterIsTheWindowsRemainderInWholeSeconds(int milliseconds, long seconds) =>
        Assert.Equal(seconds, RateLimiting.RetryAfterSeconds(TimeSpan.FromMilliseconds(milliseconds)));
}
