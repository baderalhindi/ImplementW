using System.Text.Json.Nodes;
using PMPlatform.Tests.Integration.DataImport;
using PMPlatform.Tests.Integration.Identity;

namespace PMPlatform.Tests.Integration.FinancialKpi;

/// <summary>A project's KPI figures from a workbook (data-import.md D-12): each a DRAFT on an assignment named by the KPI's code, which the team still submits for review.</summary>
[Collection(FinancialKpiSuite.Name)]
public sealed class ProjectKpiImportTests(FinancialKpiTestHost host)
{
    private const string Collection = "/api/v1/kpi-measurements";
    private static readonly string[] Header =
        ["legacy_id", "project_formal_id", "kpi_code", "period_start", "period_end", "value_status", "measured_value", "as_of_date", "narrative", "text_language"];

    [Fact]
    public async Task AFigureLandsAsADraftAndAnUnknownOneIsNeverZero()
    {
        using HttpClient client = host.Api.CreateClient();
        Sessions sessions = await client.SignInAsync();
        Guid project = await host.ProjectAsync();
        Guid assignment = await client.AssignmentAsync(sessions, project, FinancialKpiTestHost.SafetyKpiId);
        await host.ActiveTargetAsync(client, sessions, assignment, 5m, 5m, 2m);
        string code = await KpiCodeAsync(FinancialKpiTestHost.SafetyKpiId);
        string formalId = await FormalIdAsync(project);
        string today = FinancialKpiDriver.Iso(FinancialKpiDriver.Today);
        string run = Guid.NewGuid().ToString("N")[..10];
        byte[] workbook = ImportApi.Workbook(Header,
            [$"{run}-1", formalId, code, "2026-01-01", "2026-01-31", "MEASURED", "1.5", today, "Note", "en"],
            [$"{run}-2", formalId, code, "2026-02-01", "2026-02-28", "MISSING", null, today, null, "en"]);

        using HttpResponseMessage template = await client.TemplateAsync(Collection, sessions.ProjectManager);
        Assert.Equal(Header, ImportApi.HeaderOf(await template.Content.ReadAsByteArrayAsync()));
        using HttpResponseMessage response = await client.UploadAsync(Collection, sessions.ProjectManager, workbook, commit: true);

        JsonObject report = await response.ReadObjectAsync();
        Assert.Empty(ImportApi.IssuesOf(report));
        Assert.Equal(["DRAFT|1.5000", "DRAFT|"], await host.Database.QueryAsync($"""
            SELECT status || '|' || coalesce(measured_value::text, '') FROM financial_kpi.kpi_measurement
            WHERE kpi_assignment_id = '{assignment}' ORDER BY period_start
            """));
    }

    [Fact]
    public async Task AKpiNotAssignedToTheProjectAndAWrongFigureAreRefused()
    {
        using HttpClient client = host.Api.CreateClient();
        Sessions sessions = await client.SignInAsync();
        string code = await KpiCodeAsync(FinancialKpiTestHost.ReworkKpiId);
        string formalId = await FormalIdAsync(await host.ProjectAsync());
        string today = FinancialKpiDriver.Iso(FinancialKpiDriver.Today);
        string run = Guid.NewGuid().ToString("N")[..10];
        byte[] workbook = ImportApi.Workbook(Header,
            [$"{run}-1", formalId, code, "2026-01-01", "2026-01-31", "MEASURED", "1.5", today, null, "en"],
            [$"{run}-2", formalId, code, "2026-01-01", "2026-01-31", "MEASURED", "1.12345", today, null, "en"]);

        using HttpResponseMessage response = await client.UploadAsync(Collection, sessions.ProjectManager, workbook, commit: false);

        Assert.Equal(["2 kpi_code REFERENCE_NOT_FOUND", "3 measured_value OUT_OF_RANGE"], ImportApi.IssuesOf(await response.ReadObjectAsync()));
    }

    private async Task<string> FormalIdAsync(Guid project) =>
        Assert.Single(await host.Database.QueryAsync($"SELECT formal_project_id FROM project.project WHERE id = '{project}'"));

    private async Task<string> KpiCodeAsync(Guid definition) =>
        Assert.Single(await host.Database.QueryAsync($"SELECT code FROM master_data_config.kpi_definition WHERE id = '{definition}'"));
}
