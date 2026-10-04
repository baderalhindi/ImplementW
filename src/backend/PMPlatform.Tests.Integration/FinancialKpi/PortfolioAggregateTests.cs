using System.Text.Json.Nodes;

namespace PMPlatform.Tests.Integration.FinancialKpi;

/// <summary>
/// TASK-052 acceptance criterion 3: a portfolio aggregate is computed only when currency or unit compatibility is verified;
/// otherwise — or when a figure is missing, or a project is not the caller's to see — the aggregate is explicitly partial and says
/// what it left out.
/// </summary>
[Collection(FinancialKpiSuite.Name)]
public sealed class PortfolioAggregateTests(FinancialKpiTestHost host)
{
    [Fact]
    public async Task FinancialTotalsAddOnlyVerifiedMeasuredSarFiguresAndSayWhenPartial()
    {
        using HttpClient client = host.Api.CreateClient();
        Sessions sessions = await client.SignInAsync();
        (Guid measured, Guid missing, Guid unrelated) = (await host.ProjectAsync(), await host.ProjectAsync(), await host.ProjectAsync(FinancialKpiTestHost.OtherEntityId));
        foreach (Guid project in new[] { measured, missing })
        {
            await host.PeriodsAsync(project, 1);
        }

        await host.ActiveBudgetAsync(client, sessions, measured, "1000000.00");
        await host.ActiveBudgetAsync(client, sessions, missing, "500000.00");
        await client.PublishedUpdateAsync(sessions, measured, FinancialKpiDriver.Update("400000.00", "1020000.00", "MEASURED"));
        await client.PublishedUpdateAsync(sessions, missing, FinancialKpiDriver.Update(null, null, "MISSING"));

        // Both of local.r02's projects: one counted, one left out as not measured — partial, and the total is the counted one's, not one with a 0 added.
        JsonObject partial = await client.GetOrFailAsync(sessions.Portfolio, $"{FinancialKpiDriver.FinancialAggregates}?projectId={measured}&projectId={missing}");
        Assert.Equal((true, "PARTIAL", 2, 1, "SAR"), (partial["isPartial"]!.GetValue<bool>(), partial.Text("coverage"), partial["requestedProjectCount"]!.GetValue<int>(),
            partial["includedProjectCount"]!.GetValue<int>(), partial.Text("currencyCode")));
        Assert.Equal(("1000000.00", "400000.00", "1020000.00"), (partial.Text("totalApprovedBudgetSar"), partial.Text("totalActualExpenditureToDateSar"), partial.Text("totalForecastAtCompletionSar")));
        Assert.Equal([(missing.ToString(), "VALUE_NOT_MEASURED")], Exclusions(partial));

        // Only the measured one: complete.
        JsonObject complete = await client.GetOrFailAsync(sessions.Portfolio, $"{FinancialKpiDriver.FinancialAggregates}?projectId={measured}");
        Assert.Equal((false, "COMPLETE"), (complete["isPartial"]!.GetValue<bool>(), complete.Text("coverage")));

        // The entity Project Manager names a project of another entity: it is not theirs to see, so it is left out, and nothing is counted from it.
        JsonObject entity = await client.GetOrFailAsync(sessions.ProjectManager, $"{FinancialKpiDriver.FinancialAggregates}?projectId={measured}&projectId={unrelated}");
        Assert.Equal((true, 1), (entity["isPartial"]!.GetValue<bool>(), entity["includedProjectCount"]!.GetValue<int>()));
        Assert.Equal([(unrelated.ToString(), "NOT_AVAILABLE")], Exclusions(entity));

        // Nothing counted: every total is null, never "0.00".
        JsonObject none = await client.GetOrFailAsync(sessions.Portfolio, $"{FinancialKpiDriver.FinancialAggregates}?projectId={missing}&semanticState=CURRENT_LIVE");
        Assert.Equal(("NONE", "CURRENT_LIVE"), (none.Text("coverage"), none.Text("semanticState")));
        Assert.Null(none["totalApprovedBudgetSar"]);
        Assert.Null(none["totalActualExpenditureToDateSar"]);
    }

    [Fact]
    public async Task KpiValuesAreCombinedOnlyWithinOneUnit()
    {
        using HttpClient client = host.Api.CreateClient();
        Sessions sessions = await client.SignInAsync();
        (Guid first, Guid second) = (await host.ProjectAsync(), await host.ProjectAsync());
        DateOnly period = FinancialKpiDriver.Today.AddDays(-45);
        foreach ((Guid project, Guid kpi, decimal value) in new[]
                 {
                     (first, FinancialKpiTestHost.SafetyKpiId, 96m), (second, FinancialKpiTestHost.SafetyKpiId, 92m),
                     (first, FinancialKpiTestHost.ReworkKpiId, 4m), (second, FinancialKpiTestHost.DelayKpiId, 20m),
                 })
        {
            Guid assignmentId = await client.AssignmentAsync(sessions, project, kpi);
            await host.ActiveTargetAsync(client, sessions, assignmentId, 95m, kpi == FinancialKpiTestHost.SafetyKpiId ? 95m : 5m, kpi == FinancialKpiTestHost.SafetyKpiId ? 90m : 10m);
            await client.PublishedMeasurementAsync(sessions, assignmentId, period, value);
        }

        // Two KPIs measured in percent, on two projects: one unit, verified, combined — the rework KPI is not assigned to the second project.
        JsonObject percent = await client.GetOrFailAsync(sessions.Portfolio,
            $"{FinancialKpiDriver.KpiAggregates}?kpiDefinitionId={FinancialKpiTestHost.SafetyKpiId}&kpiDefinitionId={FinancialKpiTestHost.ReworkKpiId}&projectId={first}&projectId={second}");
        Assert.Equal((true, FinancialKpiTestHost.PercentUnitId.ToString(), 3), (percent["isUnitCompatible"]!.GetValue<bool>(), percent.Text("unitItemId"), percent["measuredCount"]!.GetValue<int>()));
        Assert.Equal(64m, percent["meanValue"]!.GetValue<decimal>());
        Assert.Equal([(second.ToString(), "NO_PUBLISHED_FIGURE")], Exclusions(percent));

        // Percent and days do not combine: no value is computed, the aggregate is partial, and the unitless RAG is still counted.
        JsonObject mixed = await client.GetOrFailAsync(sessions.Portfolio,
            $"{FinancialKpiDriver.KpiAggregates}?kpiDefinitionId={FinancialKpiTestHost.SafetyKpiId}&kpiDefinitionId={FinancialKpiTestHost.DelayKpiId}&projectId={first}&projectId={second}");
        Assert.Equal((false, true, "NONE"), (mixed["isUnitCompatible"]!.GetValue<bool>(), mixed["isPartial"]!.GetValue<bool>(), mixed.Text("coverage")));
        Assert.Null(mixed["meanValue"]);
        Assert.Null(mixed["unitItemId"]);
        JsonObject rag = mixed["ragCounts"]!.AsObject();
        Assert.Equal((1, 1, 1), (rag["green"]!.GetValue<int>(), rag["amber"]!.GetValue<int>(), rag["red"]!.GetValue<int>()));
    }

    private static IEnumerable<(string, string)> Exclusions(JsonObject aggregate) =>
        aggregate["exclusions"]!.AsArray().Select(e => (e!.Text("projectId"), e!.Text("reason")));
}
