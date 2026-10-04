using System.Net;
using System.Text.Json.Nodes;
using PMPlatform.Application.Features.FinancialKpi.Contracts;
using PMPlatform.Tests.Integration.Identity;

namespace PMPlatform.Tests.Integration.FinancialKpi;

/// <summary>
/// TASK-052 acceptance criterion 2: a missing financial or KPI value serializes as an explicit Unknown/N/A — a JSON null beside
/// a value status that says why, and a status of UNKNOWN or NOT_APPLICABLE — never as 0 and never as GREEN, in the live view,
/// the published view, and the database.
/// </summary>
[Collection(FinancialKpiSuite.Name)]
public sealed class UnknownValueTests(FinancialKpiTestHost host)
{
    [Fact]
    public async Task AMissingFinancialValueIsUnknownNeverZero()
    {
        using HttpClient client = host.Api.CreateClient();
        Sessions sessions = await client.SignInAsync();
        Guid projectId = await host.ProjectAsync();
        await host.PeriodsAsync(projectId, 1);
        await host.ActiveBudgetAsync(client, sessions, projectId, "1000000.00");

        // A new update presumes nothing: every figure is null and MISSING.
        JsonObject started = await client.CreatedOrFailAsync(sessions.ProjectManager, FinancialKpiDriver.Updates, new { projectId });
        Guid id = AdministrationApi.IdOf(started);
        AssertNull(started, "actualExpenditureToDateSar", "forecastAtCompletionSar");
        Assert.Equal("MISSING", started.Text("valueStatus"));

        // A figure is present exactly when MEASURED: "0.00" is not a way to say "unknown", and MEASURED needs the figure.
        using (HttpResponseMessage zero = await client.PutAsync(sessions.ProjectManager, $"{FinancialKpiDriver.Updates}/{id}", FinancialKpiDriver.Update("0.00", null, "MISSING")))
        {
            Assert.Equal((HttpStatusCode.UnprocessableEntity, FinancialKpiErrorCodes.ValueStatusInvalid), await zero.RefusalAsync());
        }

        using (HttpResponseMessage empty = await client.PutAsync(sessions.ProjectManager, $"{FinancialKpiDriver.Updates}/{id}", FinancialKpiDriver.Update(null, null, "MEASURED")))
        {
            Assert.Equal((HttpStatusCode.UnprocessableEntity, FinancialKpiErrorCodes.ValueStatusInvalid), await empty.RefusalAsync());
        }

        await client.PutOrFailAsync(sessions.ProjectManager, $"{FinancialKpiDriver.Updates}/{id}", FinancialKpiDriver.Update(null, null, "STALE"));
        await client.CommandOrFailAsync(sessions.ProjectManager, $"{FinancialKpiDriver.Updates}/{id}/submit");

        // The live position shows the budget beside Unknown actuals: rated UNKNOWN, not GREEN.
        JsonObject live = Assert.Single(await client.ItemsAsync(sessions.Portfolio, FinancialKpiDriver.Positions, $"projectId={projectId}"))!.AsObject();
        Assert.Equal(("CURRENT_LIVE", "1000000.00", "STALE", "UNKNOWN"), (live.Text("semanticState"), live.Text("approvedBudgetSar"), live.Text("valueStatus"), live.Text("financialStatus")));
        AssertNull(live, "actualExpenditureToDateSar", "forecastAtCompletionSar");

        // So does the published snapshot, which keeps the thresholds it would have been rated under.
        await client.CommandOrFailAsync(sessions.Portfolio, $"{FinancialKpiDriver.Updates}/{id}/start-review");
        await client.CommandOrFailAsync(sessions.Portfolio, $"{FinancialKpiDriver.Updates}/{id}/publish");
        JsonObject snapshot = Assert.Single(await client.ItemsAsync(sessions.Portfolio, FinancialKpiDriver.Snapshots, $"projectId={projectId}"))!.AsObject();
        Assert.Equal(("PUBLISHED_OFFICIAL", "STALE", "UNKNOWN", FinancialKpiTestHost.WorkflowPolicyVersionId.ToString()),
            (snapshot.Text("semanticState"), snapshot.Text("valueStatus"), snapshot.Text("financialStatus"), snapshot.Text("thresholdConfigurationVersionId")));
        AssertNull(snapshot, "actualExpenditureToDateSar", "forecastAtCompletionSar");
        Assert.Equal([""], await host.Database.QueryAsync(
            $"SELECT coalesce(actual_expenditure_to_date_sar::text, '') FROM financial_kpi.published_financial_snapshot WHERE project_id = '{projectId}'"));

        // For any writer, the guards aside: a 0 cannot stand in for an Unknown figure, and an Unknown figure is never rated a colour.
        Assert.Equal("ck_financial_progress_update_value_status", await host.RefusedAsync(FinancialKpiDriver.Unguarded(
            $"UPDATE financial_kpi.financial_progress_update SET actual_expenditure_to_date_sar = 0 WHERE id = '{id}'")));
        Assert.Equal("ck_published_financial_snapshot_financial_status", await host.RefusedAsync(FinancialKpiDriver.Unguarded(
            $"UPDATE financial_kpi.published_financial_snapshot SET financial_status = 'GREEN' WHERE project_id = '{projectId}'")));
    }

    [Fact]
    public async Task AMissingKpiValueIsUnknownAndANotApplicableOneIsNotApplicable()
    {
        using HttpClient client = host.Api.CreateClient();
        Sessions sessions = await client.SignInAsync();
        Guid assignmentId = await client.AssignmentAsync(sessions, await host.ProjectAsync(), FinancialKpiTestHost.SafetyKpiId);
        await host.ActiveTargetAsync(client, sessions, assignmentId, 95m, 95m, 90m);
        DateOnly period = FinancialKpiDriver.Today.AddDays(-60);

        JsonObject missing = await client.PublishedMeasurementAsync(sessions, assignmentId, period, null, "MISSING");
        JsonObject notApplicable = await client.PublishedMeasurementAsync(sessions, assignmentId, period.AddDays(30), null, "NOT_APPLICABLE");

        AssertNull(missing, "measuredValue");
        Assert.Equal(("MISSING", "UNKNOWN"), (missing.Text("valueStatus"), missing.Text("ragStatus")));
        AssertNull(notApplicable, "measuredValue");
        Assert.Equal(("NOT_APPLICABLE", "NOT_APPLICABLE"), (notApplicable.Text("valueStatus"), notApplicable.Text("ragStatus")));

        using (HttpResponseMessage zero = await client.PostAsync(FinancialKpiDriver.Measurements, sessions.ProjectManager,
                   FinancialKpiDriver.Measurement(assignmentId, period.AddDays(60), 0m, "MISSING")))
        {
            Assert.Equal((HttpStatusCode.UnprocessableEntity, FinancialKpiErrorCodes.ValueStatusInvalid), await zero.RefusalAsync());
        }

        Assert.Equal("ck_kpi_measurement_rag_status", await host.RefusedAsync(FinancialKpiDriver.Unguarded(
            $"UPDATE financial_kpi.kpi_measurement SET rag_status = 'GREEN' WHERE id = '{AdministrationApi.IdOf(missing)}'")));
        Assert.Equal("ck_kpi_measurement_value_status", await host.RefusedAsync(FinancialKpiDriver.Unguarded(
            $"UPDATE financial_kpi.kpi_measurement SET measured_value = 0 WHERE id = '{AdministrationApi.IdOf(notApplicable)}'")));
    }

    /// <summary>The property is there, and it is JSON null: an explicit Unknown, not an absent or a 0.</summary>
    private static void AssertNull(JsonObject body, params string[] properties)
    {
        foreach (string property in properties)
        {
            Assert.True(body.ContainsKey(property), $"{property} is absent");
            Assert.Null(body[property]);
        }
    }
}
