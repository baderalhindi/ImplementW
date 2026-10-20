using System.Diagnostics;
using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PMPlatform.Infrastructure.Hosting;
using PMPlatform.Infrastructure.Persistence;
using PMPlatform.Tests.Integration.Identity;

namespace PMPlatform.Tests.Integration.Observability;

/// <summary>
/// TASK-091 acceptance criterion 1 in process: "a readiness check correctly reports unhealthy when the database is unreachable
/// (verified by a simulated DB outage)". The outage is a <see cref="DatabaseRelay"/> between the API and its database, cut while the API
/// runs. The same with the release image and a stopped database container is in health-checks-uptime-monitoring.md §5.3.
/// </summary>
[Collection(IdentitySuite.Name)]
public sealed class HealthCheckTests(IdentityTestHost host)
{
    private const string Health = "/health";
    private const string Live = "/health/live";
    private const string Ready = "/health/ready";

    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(PostgreSqlHealthCheck.TimeoutSeconds);

    /// <summary>A cached healthy result can be reused for ReuseFor; the probe after it fails within Timeout.</summary>
    private static readonly TimeSpan Detection = PostgreSqlHealthCheck.ReuseFor + Timeout + TimeSpan.FromSeconds(2);

    [Fact]
    public async Task ReadinessIsUnhealthyWhileTheDatabaseIsDownAndLivenessIsNot()
    {
        await using DatabaseRelay relay = new(host.Database.ConnectionString);
        await using WebApplicationFactory<Program> api = CreateApi(relay);
        using HttpClient client = api.CreateClient();
        await AnswersAsync(client, Ready, HttpStatusCode.OK, Detection);

        relay.Refuse();

        await AnswersAsync(client, Ready, HttpStatusCode.ServiceUnavailable, Detection);
        Assert.Equal((HttpStatusCode.ServiceUnavailable, "Unhealthy"), await GetAsync(client, Health));
        Assert.Equal((HttpStatusCode.OK, "Healthy"), await GetAsync(client, Live));

        relay.Restore();

        await AnswersAsync(client, Ready, HttpStatusCode.OK, Detection);
        Assert.Equal((HttpStatusCode.OK, "Healthy"), await GetAsync(client, Health));
    }

    /// <summary>
    /// A database host that accepts connections and never answers would hold a probe until the driver's own timeouts, longer than the
    /// uptime check waits; the readiness probe gives up after <see cref="PostgreSqlHealthCheck.TimeoutSeconds"/> and says so.
    /// </summary>
    [Fact]
    public async Task ADatabaseThatNeverAnswersIsReportedUnhealthyWithinTheProbeTimeout()
    {
        await using DatabaseRelay relay = new(host.Database.ConnectionString);
        await using WebApplicationFactory<Program> api = CreateApi(relay);
        using HttpClient client = api.CreateClient();
        await AnswersAsync(client, Ready, HttpStatusCode.OK, Detection);

        relay.Silence();

        Stopwatch waited = Stopwatch.StartNew();
        HttpStatusCode status;
        string body;
        do
        {
            Stopwatch request = Stopwatch.StartNew();
            (status, body) = await GetAsync(client, Ready);
            Assert.True(request.Elapsed < Timeout + TimeSpan.FromSeconds(1.5), $"{Ready} took {request.Elapsed}.");
        }
        while (status == HttpStatusCode.OK && waited.Elapsed < Detection);

        Assert.Equal((HttpStatusCode.ServiceUnavailable, "Unhealthy"), (status, body));
        Assert.Equal((HttpStatusCode.OK, "Healthy"), await GetAsync(client, Live));
    }

    /// <summary>
    /// A worker in one pass for longer than HealthChecks:WorkerPassTimeout fails liveness, so the platform restarts the instance; the
    /// database still answers, so readiness does not change.
    /// </summary>
    [Fact]
    public async Task AWorkerStuckInOnePassFailsLiveness()
    {
        await using WebApplicationFactory<Program> api = host.CreateApi(new Dictionary<string, string?> { ["HealthChecks:WorkerPassTimeout"] = "00:01:00" });
        using HttpClient client = api.CreateClient();
        WorkerActivity activity = api.Services.GetRequiredService<WorkerActivity>();
        Assert.Equal((HttpStatusCode.OK, "Healthy"), await GetAsync(client, Live));

        activity.PassStarted("TASK-091 stalled pass", host.Clock.GetUtcNow() - TimeSpan.FromMinutes(2));
        try
        {
            Assert.Equal((HttpStatusCode.ServiceUnavailable, "Unhealthy"), await GetAsync(client, Live));
            Assert.Equal((HttpStatusCode.ServiceUnavailable, "Unhealthy"), await GetAsync(client, Health));
            Assert.Equal((HttpStatusCode.OK, "Healthy"), await GetAsync(client, Ready));
        }
        finally
        {
            activity.PassEnded("TASK-091 stalled pass");
        }

        Assert.Equal((HttpStatusCode.OK, "Healthy"), await GetAsync(client, Live));
    }

    /// <summary>
    /// Anonymous, so the uptime monitor and the platform's probes need no credential; never cached, so no proxy answers for the instance;
    /// and nothing in the body but the status.
    /// </summary>
    [Theory]
    [InlineData(Health)]
    [InlineData(Live)]
    [InlineData(Ready)]
    public async Task EachHealthEndpointIsAnonymousAndNeverCached(string path)
    {
        using HttpClient client = host.Api.CreateClient();

        using HttpResponseMessage response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
        Assert.True(response.Headers.CacheControl?.NoStore, $"{path}: Cache-Control is '{response.Headers.CacheControl}'.");
    }

    /// <summary>The API's settings with its database behind <paramref name="relay"/>; added last, so it wins over the suite's own.</summary>
    private WebApplicationFactory<Program> CreateApi(DatabaseRelay relay) =>
        host.CreateApi().WithWebHostBuilder(builder => builder.ConfigureAppConfiguration(configuration =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?> { [PostgreSqlHealthCheck.ConnectionStringKey] = relay.ConnectionString })));

    private static async Task<(HttpStatusCode Status, string Body)> GetAsync(HttpClient client, string path)
    {
        using HttpResponseMessage response = await client.GetAsync(path);
        return (response.StatusCode, await response.Content.ReadAsStringAsync());
    }

    private static async Task AnswersAsync(HttpClient client, string path, HttpStatusCode expected, TimeSpan within)
    {
        Stopwatch waited = Stopwatch.StartNew();
        HttpStatusCode status;
        while ((status = (await GetAsync(client, path)).Status) != expected && waited.Elapsed < within)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(250));
        }

        Assert.True(status == expected, $"{path} still answered {(int)status} after {within}; expected {(int)expected}.");
    }
}
