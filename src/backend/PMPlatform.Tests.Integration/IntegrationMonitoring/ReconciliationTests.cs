using System.Net;
using System.Text.Json.Nodes;
using PMPlatform.Application.Common.Events;
using PMPlatform.Tests.Integration.Identity;
using static PMPlatform.Tests.Integration.IntegrationMonitoring.IntegrationMonitoringApi;

namespace PMPlatform.Tests.Integration.IntegrationMonitoring;

/// <summary>
/// TASK-075 acceptance criterion 2 and the workbook's second validation check: a reconciliation that finds the directory and the platform
/// disagree raises an operational alert for investigation and repairs nothing — the authoritative user rows stay exactly as they were. A
/// repeated request is the same run; the alert is not raised twice; a directory that cannot be read is retried, not guessed at.
/// </summary>
[Collection(IntegrationMonitoringSuite.Name)]
public sealed class ReconciliationTests(IntegrationMonitoringTestHost host)
{
    private const string UserRows = "SELECT md5(string_agg(u::text, '|' ORDER BY u.id))::text FROM identity_access.\"user\" u";

    [Fact]
    public async Task AMismatchRaisesAnOperationalAlertAndNeverRepairsTheAuthoritativeData()
    {
        using HttpClient administrator = await host.ClientAsync(1);
        Guid directory = await host.DefinitionIdAsync(IntegrationCodes.Directory);
        Guid r06 = IntegrationMonitoringTestHost.Person(6);
        await host.Database.ExecuteAsync($"UPDATE identity_access.\"user\" SET job_title = 'Title the directory never had' WHERE id = '{r06}'");
        string usersBefore = Assert.Single(await host.Database.QueryAsync(UserRows));

        JsonObject run = await (await administrator.SendSensitiveAsync(Reconciliations, new { integrationId = directory })).SuccessAsync();

        Assert.Equal("SUCCEEDED", run["invocation"]!["status"].Text());
        Guid syncRunId = Guid.Parse(run["invocation"]!["syncRunId"].Text());
        JsonObject syncRun = await (await administrator.GetAsync($"{SyncRuns}/{syncRunId}")).SuccessAsync();
        Assert.Equal("RECONCILIATION", syncRun["runType"].Text());
        Assert.Equal("SUCCEEDED", syncRun["status"].Text());
        Assert.Equal(5, syncRun["recordsRead"]!.GetValue<int>()); // local.r01, r02, r03, r05, r06: active, internal, linked to the directory
        Assert.Equal(0, syncRun["recordsApplied"]!.GetValue<int>());
        Assert.True(syncRun["mismatchCount"]!.GetValue<int>() >= 1);

        JsonObject alert = Assert.Single(
            (await (await administrator.GetAsync($"{Alerts}?alertType=RECONCILIATION_MISMATCH&status=OPEN,ACKNOWLEDGED&integrationId={directory}")).SuccessAsync())["items"]!.AsArray())!
            .AsObject();
        Assert.Contains($"{r06}: job_title", alert["detail"].Text(), StringComparison.Ordinal);
        Assert.DoesNotContain("Title the directory never had", alert["detail"].Text(), StringComparison.Ordinal);
        Assert.DoesNotContain("Viewer", alert["detail"].Text(), StringComparison.Ordinal);

        // Nothing was repaired: the stale title is still the platform's, and no user row changed at all.
        Assert.Equal(["Title the directory never had"], await host.Database.QueryAsync($"SELECT job_title FROM identity_access.\"user\" WHERE id = '{r06}'"));
        Assert.Equal(usersBefore, Assert.Single(await host.Database.QueryAsync(UserRows)));

        // The run is integration audit, apart from business audit.
        Assert.Contains(
            await host.Database.QueryAsync($"SELECT event_class || '|' || event_type || '|' || outcome FROM audit_activity.audit_event WHERE subject_id = '{syncRunId}'"),
            e => e == "INTEGRATION|IntegrationMonitoring.ReconciliationCompleted|SUCCESS");
    }

    [Fact]
    public async Task TheSameRequestKeyIsTheSameRunAndAStandingMismatchIsOneAlert()
    {
        using HttpClient administrator = await host.ClientAsync(1);
        Guid directory = await host.DefinitionIdAsync(IntegrationCodes.Directory);
        await host.Database.ExecuteAsync($"UPDATE identity_access.\"user\" SET job_title = 'Another stale title' WHERE id = '{IntegrationMonitoringTestHost.Person(5)}'");
        Guid key = Guid.NewGuid();

        JsonObject first = await (await administrator.SendSensitiveAsync(Reconciliations, new { integrationId = directory }, key)).SuccessAsync();
        JsonObject again = await (await administrator.SendSensitiveAsync(Reconciliations, new { integrationId = directory }, key)).SuccessAsync();
        JsonObject other = await (await administrator.SendSensitiveAsync(Reconciliations, new { integrationId = directory })).SuccessAsync();

        Assert.Equal(first["invocation"]!["id"].Text(), again["invocation"]!["id"].Text());
        Assert.Equal(first["invocation"]!["syncRunId"].Text(), again["invocation"]!["syncRunId"].Text());
        Assert.NotEqual(first["invocation"]!["idempotencyKey"].Text(), other["invocation"]!["idempotencyKey"].Text());
        Assert.Equal(
            ["1"],
            await host.Database.QueryAsync($"SELECT count(*)::text FROM integration_monitoring.invocation WHERE idempotency_key = '{first["invocation"]!["idempotencyKey"].Text()}'"));
        Assert.Single(await host.LiveAlertsAsync(IntegrationCodes.Directory), a => a.StartsWith("RECONCILIATION_MISMATCH|", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AnUnreadableDirectoryIsRetriedAndNothingIsCompared()
    {
        using HttpClient administrator = await host.ClientAsync(1);
        await using IdentityApiFactory outage = host.CreateApi(new Dictionary<string, string?> { ["AD_LDAP_URL"] = "ldap://127.0.0.1:1/dc=pmplatform,dc=local" });
        using HttpClient client = outage.CreateClient();
        client.DefaultRequestHeaders.Authorization = administrator.DefaultRequestHeaders.Authorization;
        string usersBefore = Assert.Single(await host.Database.QueryAsync(UserRows));

        JsonObject run = await (await client.SendSensitiveAsync(Reconciliations, new { integrationId = await host.DefinitionIdAsync(IntegrationCodes.Directory) })).SuccessAsync();

        JsonObject attempt = run["invocation"]!.AsObject();
        Assert.Equal(("RETRY_SCHEDULED", "TECHNICAL", "UNREACHABLE"), (attempt["status"].Text(), attempt["failureCategory"].Text(), attempt["responseCode"].Text()));
        Assert.Equal(
            DateTimeOffset.Parse(attempt["completedAt"].Text(), System.Globalization.CultureInfo.InvariantCulture).AddSeconds(60),
            DateTimeOffset.Parse(attempt["nextAttemptAt"].Text(), System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal(
            ["FAILED|0|5|0"],
            await host.Database.QueryAsync($"SELECT status || '|' || records_read || '|' || records_rejected || '|' || mismatch_count FROM integration_monitoring.sync_run WHERE id = '{attempt["syncRunId"].Text()}'"));
        Assert.Equal(usersBefore, Assert.Single(await host.Database.QueryAsync(UserRows)));
    }

    [Fact]
    public async Task OnlyAnIntegrationWithAReconciliationThatIsMonitoredCanBeReconciled()
    {
        using HttpClient administrator = await host.ClientAsync(1);
        using HttpResponseMessage sms = await administrator.SendSensitiveAsync(Reconciliations, new { integrationId = await host.DefinitionIdAsync(IntegrationCodes.Sms) });
        using HttpResponseMessage unknown = await administrator.SendSensitiveAsync(Reconciliations, new { integrationId = Guid.NewGuid() });
        using HttpResponseMessage noBody = await administrator.SendSensitiveAsync(Reconciliations, new { });

        Assert.Equal((HttpStatusCode.UnprocessableEntity, "RECONCILIATION_NOT_AVAILABLE"), (sms.StatusCode, (await sms.ReadAsync<Problem>()).Code));
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, noBody.StatusCode);

        Guid directory = await host.DefinitionIdAsync(IntegrationCodes.Directory);
        await (await administrator.SendSensitiveAsync($"{Integrations}/{directory}/disable")).SuccessAsync();
        try
        {
            using HttpResponseMessage disabled = await administrator.SendSensitiveAsync(Reconciliations, new { integrationId = directory });
            Assert.Equal((HttpStatusCode.UnprocessableEntity, "INTEGRATION_DISABLED"), (disabled.StatusCode, (await disabled.ReadAsync<Problem>()).Code));
        }
        finally
        {
            await (await administrator.SendSensitiveAsync($"{Integrations}/{directory}/enable")).SuccessAsync();
        }
    }
}
