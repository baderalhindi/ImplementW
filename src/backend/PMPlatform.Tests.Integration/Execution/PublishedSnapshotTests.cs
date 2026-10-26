using System.Net;
using System.Text.Json.Nodes;
using PMPlatform.Application.Features.FinancialKpi.Contracts;
using PMPlatform.Tests.Integration.FinancialKpi;
using PMPlatform.Tests.Integration.Identity;

namespace PMPlatform.Tests.Integration.Execution;

/// <summary>
/// Separation 3 (financial-kpi.md D-9, D-13; erd.md F-062): what WF-14 published for a period — the financial snapshot and the
/// KPI measurement — stays as published after every later correction the design allows: a new Approved Budget version, the next
/// period's restated to-date figures, and a new KPI target version. A published figure itself has no correction (F-12): its API
/// refuses the edit. Each is read back whole, through the API and from the database.
/// </summary>
[Collection(FinancialKpiSuite.Name)]
public sealed class PublishedSnapshotTests(FinancialKpiTestHost host)
{
    [Fact]
    public async Task APublishedPeriodIsUnchangedByEveryLaterCorrection()
    {
        using HttpClient client = host.Api.CreateClient();
        Sessions sessions = await client.SignInAsync();
        Guid projectId = await host.ProjectAsync();
        await host.PeriodsAsync(projectId, 2);
        await host.ActiveBudgetAsync(client, sessions, projectId, "1000000.00");
        Guid assignmentId = await client.AssignmentAsync(sessions, projectId, FinancialKpiTestHost.SafetyKpiId);
        await host.ActiveTargetAsync(client, sessions, assignmentId, 95m, 95m, 90m);
        DateOnly month = FinancialKpiDriver.Today.AddDays(-30);

        // The period is published: 8% over budget is AMBER; 92% safe hours against a 95% target is AMBER.
        Guid updateId = await client.PublishedUpdateAsync(sessions, projectId, FinancialKpiDriver.Update("300000.00", "1080000.00", "MEASURED", "Invoice register 03"));
        JsonObject snapshot = Assert.Single(await client.ItemsAsync(sessions.Portfolio, FinancialKpiDriver.Snapshots, $"projectId={projectId}"))!.AsObject();
        Guid snapshotId = AdministrationApi.IdOf(snapshot);
        Guid measurementId = AdministrationApi.IdOf(await client.PublishedMeasurementAsync(sessions, assignmentId, month, 92m));
        Assert.Equal(("1000000.00", "AMBER"), (snapshot.Text("approvedBudgetSar"), snapshot.Text("financialStatus")));
        Published before = await PublishedAsync(client, sessions, projectId, snapshotId, updateId, measurementId);
        Assert.Equal("AMBER", before.Measurement.Text("ragStatus"));

        // A published figure is not corrected in place: its update and its measurement refuse the edit and a second publication.
        using (HttpResponseMessage edit = await client.PutAsync(sessions.ProjectManager, $"{FinancialKpiDriver.Updates}/{updateId}", FinancialKpiDriver.Update("1.00", "1.00", "MEASURED")))
        {
            Assert.Equal((HttpStatusCode.Conflict, FinancialKpiErrorCodes.NotEditable), await edit.RefusalAsync());
        }

        using (HttpResponseMessage edit = await client.PutAsync(sessions.ProjectManager, $"{FinancialKpiDriver.Measurements}/{measurementId}",
                   new { measuredValue = 99m, valueStatus = "MEASURED", asOfDate = FinancialKpiDriver.Iso(FinancialKpiDriver.Today) }))
        {
            Assert.Equal((HttpStatusCode.Conflict, FinancialKpiErrorCodes.NotEditable), await edit.RefusalAsync());
        }

        using (HttpResponseMessage again = await client.PostAsync($"{FinancialKpiDriver.Updates}/{updateId}/publish", sessions.Portfolio))
        {
            Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        }

        using (HttpResponseMessage second = await client.PostAsync(FinancialKpiDriver.Measurements, sessions.ProjectManager, FinancialKpiDriver.Measurement(assignmentId, month, 99m)))
        {
            Assert.Equal((HttpStatusCode.Conflict, FinancialKpiErrorCodes.MeasurementExists), await second.RefusalAsync());
        }

        // The corrections the design allows: a new budget, the next period restating the to-date figures, a new KPI target.
        await host.ActiveBudgetAsync(client, sessions, projectId, "1200000.00");
        await client.PublishedUpdateAsync(sessions, projectId, FinancialKpiDriver.Update("350000.00", "1150000.00", "MEASURED", "Invoice register 03, restated"));
        await host.ActiveTargetAsync(client, sessions, assignmentId, 90m, 90m, 85m);

        // They took effect: the live position and a new publication show them.
        JsonObject live = Assert.Single(await client.ItemsAsync(sessions.Portfolio, FinancialKpiDriver.Positions, $"projectId={projectId}"))!.AsObject();
        Assert.Equal(("1200000.00", "350000.00", "GREEN"), (live.Text("approvedBudgetSar"), live.Text("actualExpenditureToDateSar"), live.Text("financialStatus")));
        Assert.Equal(2, (await client.ItemsAsync(sessions.Portfolio, FinancialKpiDriver.Snapshots, $"projectId={projectId}")).Count);

        // The published period is exactly as it was, whole, through the API and in the database.
        Published after = await PublishedAsync(client, sessions, projectId, snapshotId, updateId, measurementId);
        Assert.True(JsonNode.DeepEquals(before.Snapshot, after.Snapshot), $"The published snapshot changed:\n{before.Snapshot}\n{after.Snapshot}");
        Assert.True(JsonNode.DeepEquals(before.Update, after.Update), $"The published update changed:\n{before.Update}\n{after.Update}");
        Assert.True(JsonNode.DeepEquals(before.Measurement, after.Measurement), $"The published measurement changed:\n{before.Measurement}\n{after.Measurement}");
        Assert.Equal(before.Rows, after.Rows);

        // For any writer, the snapshot and the measurement are never changed or removed.
        Assert.NotNull(await host.RefusedAsync($"UPDATE financial_kpi.published_financial_snapshot SET financial_status = 'GREEN' WHERE id = '{snapshotId}'"));
        Assert.NotNull(await host.RefusedAsync($"DELETE FROM financial_kpi.published_financial_snapshot WHERE id = '{snapshotId}'"));
        Assert.NotNull(await host.RefusedAsync($"UPDATE financial_kpi.kpi_measurement SET rag_status = 'GREEN' WHERE id = '{measurementId}'"));
        Assert.NotNull(await host.RefusedAsync($"DELETE FROM financial_kpi.kpi_measurement WHERE id = '{measurementId}'"));
        Assert.Equal(before.Rows, (await PublishedAsync(client, sessions, projectId, snapshotId, updateId, measurementId)).Rows);
    }

    /// <summary>The period as published: the snapshot, its update and the measurement as the API serves them, and their rows.</summary>
    private async Task<Published> PublishedAsync(HttpClient client, Sessions sessions, Guid projectId, Guid snapshotId, Guid updateId, Guid measurementId) => new(
        (await client.ItemsAsync(sessions.Portfolio, FinancialKpiDriver.Snapshots, $"projectId={projectId}")).Single(s => s!.Text("id") == snapshotId.ToString())!.AsObject(),
        await client.GetOrFailAsync(sessions.Portfolio, $"{FinancialKpiDriver.Updates}/{updateId}"),
        await client.GetOrFailAsync(sessions.Portfolio, $"{FinancialKpiDriver.Measurements}/{measurementId}"),
        [
            await host.RowAsync("published_financial_snapshot", snapshotId),
            await host.RowAsync("financial_progress_update", updateId),
            await host.RowAsync("kpi_measurement", measurementId),
        ]);

    private sealed record Published(JsonObject Snapshot, JsonObject Update, JsonObject Measurement, string[] Rows);
}
