using System.Net;
using System.Text.Json.Nodes;
using PMPlatform.Application.Common.Events;
using PMPlatform.Tests.Integration.Identity;
using static PMPlatform.Tests.Integration.IntegrationMonitoring.IntegrationMonitoringApi;

namespace PMPlatform.Tests.Integration.IntegrationMonitoring;

/// <summary>
/// Who may see and operate FG-05 (FG-05 §12, INT-CC-01 to -07): R01 by its shipped grants, nobody else by default; never an external entity
/// user (BR-INT-022, SEC-INT-09). What is shown names each endpoint by its configuration key and never carries a secret (INT-CC-07), and
/// availability and freshness are two indicators, beside activation (Blueprint §17.1, BR-INT-013).
/// </summary>
[Collection(IntegrationMonitoringSuite.Name)]
public sealed class IntegrationAccessTests(IntegrationMonitoringTestHost host)
{
    private static readonly string[] Reads = [Integrations, Invocations, SyncRuns, Alerts];
    private static readonly string[] Availabilities = ["UP", "DOWN", "UNKNOWN"];
    private static readonly string[] Freshnesses = ["FRESH", "STALE", "UNKNOWN"];

    [Fact]
    public async Task TheCatalogueShowsEachInstancesAvailabilityAndFreshnessApartWithItsEnvironmentAndNoSecret()
    {
        using HttpClient administrator = await host.ClientAsync(1);

        using HttpResponseMessage response = await administrator.GetAsync(Integrations);
        string text = await response.Content.ReadAsStringAsync();
        JsonObject catalogue = JsonNode.Parse(text)!.AsObject();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Development", catalogue["environment"].Text());
        Assert.Equal(
            ["DIRECTORY", "EXCHANGE_EMAIL", "MFA", "NAFATH", "SMS", "SSO", TestOperation.Code],
            catalogue["items"]!.AsArray().Select(i => i!["code"].Text()));
        JsonObject directory = catalogue["items"]!.AsArray().Single(i => i!["code"].Text() == IntegrationCodes.Directory)!.AsObject();
        JsonObject instance = directory["instances"]!.AsArray().Single()!.AsObject();
        Assert.Equal(("PRIMARY", "AD_LDAP_URL", true), (instance["code"].Text(), instance["endpointConfigurationKey"].Text(), instance["isEnabled"]!.GetValue<bool>()));
        Assert.Contains(instance["availability"].Text(), Availabilities);
        Assert.Contains(instance["freshness"].Text(), Freshnesses);
        Assert.DoesNotContain("health", instance.Select(p => p.Key));
        Assert.DoesNotContain(TestDirectory.BindPassword, text, StringComparison.Ordinal);
        Assert.DoesNotContain(TestMultiFactorProvider.ApiKey, text, StringComparison.Ordinal);
        Assert.DoesNotContain(TestDirectory.Url, text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheDetailShowsThePolicyTheOperationsAndADRFoursMeasuresWithoutASecret()
    {
        using HttpClient administrator = await host.ClientAsync(1);
        Guid directory = await host.DefinitionIdAsync(IntegrationCodes.Directory);

        using HttpResponseMessage response = await administrator.GetAsync($"{Integrations}/{directory}");
        string text = await response.Content.ReadAsStringAsync();
        JsonObject detail = JsonNode.Parse(text)!.AsObject();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(response.Headers.ETag);
        Assert.Equal((3, 60), (detail["maxAttempts"]!.GetValue<int>(), detail["retryBackoffSeconds"]!.GetValue<int>()));
        Assert.Equal(["RECONCILE"], detail["operations"]!.AsArray().Select(o => o.Text()));
        JsonObject metrics = detail["metrics"]!.AsArray().Single()!.AsObject();
        Assert.All(["deliveryRate", "failureRate", "medianLatencyMilliseconds", "p95LatencyMilliseconds", "medianReceiptLatencyMilliseconds"], m => Assert.True(metrics.ContainsKey(m)));
        Assert.DoesNotContain(TestDirectory.BindPassword, text, StringComparison.Ordinal);
    }

    /// <summary>SEC-INT-09: an external entity user reaches no FG-05 route; nor does an internal role without a grant (R06), nor R01 by role alone for what it is not granted.</summary>
    [Theory]
    [InlineData(8)]
    [InlineData(6)]
    public async Task OnlyAHolderOfTheGrantsReachesFg05(int person)
    {
        using HttpClient client = await host.ClientAsync(person);
        Guid directory = await host.DefinitionIdAsync(IntegrationCodes.Directory);

        foreach (string path in Reads.Append($"{Integrations}/{directory}"))
        {
            using HttpResponseMessage read = await client.GetAsync(path);
            Assert.True(read.StatusCode == HttpStatusCode.Forbidden, $"{path}: {(int)read.StatusCode}");
        }

        foreach (string path in new[] { Reconciliations, $"{Integrations}/{directory}/disable", $"{Invocations}/{Guid.NewGuid()}/retry", $"{Alerts}/{Guid.NewGuid()}/acknowledge" })
        {
            using HttpResponseMessage write = await client.SendSensitiveAsync(path, new { integrationId = directory });
            Assert.True(write.StatusCode == HttpStatusCode.Forbidden, $"{path}: {(int)write.StatusCode}");
        }
    }

    [Fact]
    public async Task WithoutATokenEveryRouteIsUnauthorized()
    {
        using HttpClient anonymous = host.Api.CreateClient();
        foreach (string path in Reads)
        {
            using HttpResponseMessage read = await anonymous.GetAsync(path);
            Assert.Equal(HttpStatusCode.Unauthorized, read.StatusCode);
        }
    }

    [Fact]
    public async Task DisablingAnIntegrationIsAudited()
    {
        Guid correlationId = Guid.NewGuid();
        using HttpClient administrator = await host.ClientAsync(1, correlationId);
        Guid nafath = await host.DefinitionIdAsync(IntegrationCodes.Nafath);

        JsonObject disabled = await (await administrator.SendSensitiveAsync($"{Integrations}/{nafath}/disable")).SuccessAsync();
        using HttpResponseMessage twice = await administrator.SendSensitiveAsync($"{Integrations}/{nafath}/disable");
        using HttpResponseMessage stale = await administrator.SendSensitiveAsync($"{Integrations}/{nafath}/enable", ifMatch: "\"1\"");
        JsonObject enabled = await (await administrator.SendSensitiveAsync($"{Integrations}/{nafath}/enable")).SuccessAsync();

        Assert.Equal("DISABLED", disabled["integration"]!["status"].Text());
        Assert.Equal(HttpStatusCode.Conflict, twice.StatusCode);
        Assert.Equal(HttpStatusCode.PreconditionFailed, stale.StatusCode);
        Assert.Equal("ACTIVE", enabled["integration"]!["status"].Text());
        Assert.Equal(
            ["CONFIGURATION_CHANGE|IntegrationMonitoring.IntegrationDisabled", "CONFIGURATION_CHANGE|IntegrationMonitoring.IntegrationEnabled"],
            await host.Database.QueryAsync($"SELECT event_class || '|' || event_type FROM audit_activity.audit_event WHERE subject_id = '{nafath}' AND correlation_id = '{correlationId}' ORDER BY recorded_at"));
    }
}
