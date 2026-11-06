using System.Text.Json.Nodes;
using PMPlatform.Tests.Integration.DataImport;
using PMPlatform.Tests.Integration.Identity;

namespace PMPlatform.Tests.Integration.Schedule;

/// <summary>A project's activities from a workbook (data-import.md D-12): parents by WBS code, the form's rules, and only for the schedule's editor.</summary>
[Collection(ScheduleSuite.Name)]
public sealed class ProjectScheduleImportTests(ScheduleTestHost host)
{
    private const string Collection = "/api/v1/schedule-activities";
    private static readonly string[] Header =
        ["legacy_id", "project_formal_id", "wbs_code", "parent_wbs_code", "name", "text_language", "requested_start_date", "planned_duration_days", "sort_order"];

    [Fact]
    public async Task AManagerImportsActivitiesWithParentsNamedByWbsCode()
    {
        using HttpClient client = host.Api.CreateClient();
        string manager = (await client.SignInOrFailAsync(8)).AccessToken;
        Guid project = await host.ScheduledProjectAsync(client, manager);
        string formalId = await FormalIdAsync(project);
        string run = Guid.NewGuid().ToString("N")[..10];
        byte[] workbook = ImportApi.Workbook(Header,
            [$"{run}-1", formalId, "1", null, "Design", "en", "2026-02-01", "10", "1"],
            [$"{run}-2", formalId, "1.1", "1", "Survey", "en", "2026-02-01", "5", null]);

        using HttpResponseMessage template = await client.TemplateAsync(Collection, manager);
        Assert.Equal(Header, ImportApi.HeaderOf(await template.Content.ReadAsByteArrayAsync()));
        using HttpResponseMessage response = await client.UploadAsync(Collection, manager, workbook, commit: true);

        JsonObject report = await response.ReadObjectAsync();
        Assert.Empty(ImportApi.IssuesOf(report));
        Assert.Equal(2, report["createdCount"]!.GetValue<int>());
        Assert.Equal(["1", "1.1"], await host.Database.QueryAsync($"""
            SELECT a.wbs_code FROM schedule.schedule_activity a JOIN schedule.project_schedule s ON s.id = a.project_schedule_id
            WHERE s.project_id = '{project}' ORDER BY a.wbs_code
            """));
    }

    [Fact]
    public async Task ARowMeetsTheFormsRules()
    {
        using HttpClient client = host.Api.CreateClient();
        string manager = (await client.SignInOrFailAsync(8)).AccessToken;
        string formalId = await FormalIdAsync(await host.ScheduledProjectAsync(client, manager));
        string run = Guid.NewGuid().ToString("N")[..10];
        byte[] workbook = ImportApi.Workbook(Header,
            [$"{run}-1", formalId, "1", "NO-SUCH", "Design", "en", "2026-02-01", "10", null],
            [$"{run}-2", formalId, "2", null, "Design", "en", "2026-02-01", "0", null]);

        using HttpResponseMessage response = await client.UploadAsync(Collection, manager, workbook, commit: false);

        Assert.Equal(
            ["2 parent_wbs_code REFERENCE_NOT_FOUND", "3 planned_duration_days OUT_OF_RANGE"],
            ImportApi.IssuesOf(await response.ReadObjectAsync()));
    }

    private async Task<string> FormalIdAsync(Guid project) =>
        Assert.Single(await host.Database.QueryAsync($"SELECT formal_project_id FROM project.project WHERE id = '{project}'"));
}
