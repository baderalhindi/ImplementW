using System.Text.Json.Nodes;
using PMPlatform.Tests.Integration.DataImport;
using PMPlatform.Tests.Integration.Identity;

namespace PMPlatform.Tests.Integration.ProjectTask;

/// <summary>A project's tasks from a workbook (data-import.md D-12): activities by WBS code, a parent by this project's legacy ID, and the form's rules.</summary>
[Collection(ProjectTaskSuite.Name)]
public sealed class ProjectTaskImportTests(ProjectTaskTestHost host)
{
    private const string Collection = "/api/v1/project-tasks";
    private static readonly string[] Header =
    [
        "legacy_id", "project_formal_id", "title", "description", "text_language", "planned_start_date", "planned_finish_date",
        "assignee_username", "priority_code", "schedule_activity_wbs", "parent_task_legacy_id",
    ];

    [Fact]
    public async Task AManagerImportsTasksAndSubtasksAgainstAnActivity()
    {
        Guid project = await host.ProjectAsync(projectManager: 8);
        await host.ActivitiesAsync(project, "1");
        string formalId = await FormalIdAsync(project);
        string run = Guid.NewGuid().ToString("N")[..10];
        using HttpClient client = host.Api.CreateClient();
        string manager = (await client.SignInOrFailAsync(8)).AccessToken;
        byte[] workbook = ImportApi.Workbook(Header,
            [$"{run}-P", formalId, "Parent", null, "en", "2026-02-01", "2026-02-10", null, "TEST_HIGH", "1", null],
            [$"{run}-C", formalId, "Child", "Detail", "en", "2026-02-02", "2026-02-05", null, null, null, $"{run}-P"]);

        using HttpResponseMessage template = await client.TemplateAsync(Collection, manager);
        Assert.Equal(Header, ImportApi.HeaderOf(await template.Content.ReadAsByteArrayAsync()));
        using HttpResponseMessage response = await client.UploadAsync(Collection, manager, workbook, commit: true);

        JsonObject report = await response.ReadObjectAsync();
        Assert.Empty(ImportApi.IssuesOf(report));
        Assert.Equal(2, report["createdCount"]!.GetValue<int>());
        // The subtask executes against its parent's activity, as the form's does.
        Assert.Equal(["1"], await host.Database.QueryAsync($"""
            SELECT count(DISTINCT t.schedule_activity_id)::text FROM project_task.project_task t WHERE t.project_id = '{project}'
            """));
    }

    [Fact]
    public async Task AParentIsLookedForInThisProjectOnlyAndARowMeetsTheFormsRules()
    {
        Guid other = await host.ProjectAsync(projectManager: 8);
        Guid project = await host.ProjectAsync(projectManager: 8);
        string run = Guid.NewGuid().ToString("N")[..10];
        using HttpClient client = host.Api.CreateClient();
        string manager = (await client.SignInOrFailAsync(8)).AccessToken;
        using (HttpResponseMessage loaded = await client.UploadAsync(Collection, manager, ImportApi.Workbook(Header,
                   [$"{run}-THERE", await FormalIdAsync(other), "Elsewhere", null, "en", "2026-02-01", "2026-02-10", null, null, null, null]), commit: true))
        {
            Assert.Empty(ImportApi.IssuesOf(await loaded.ReadObjectAsync()));
        }

        string formalId = await FormalIdAsync(project);
        byte[] workbook = ImportApi.Workbook(Header,
            [$"{run}-1", formalId, "Child", null, "en", "2026-02-01", "2026-02-10", null, null, null, $"{run}-THERE"],
            [$"{run}-2", formalId, "Backwards", null, "en", "2026-02-10", "2026-02-01", null, null, null, null],
            [$"{run}-3", formalId, "Unknown activity", null, "en", "2026-02-01", "2026-02-10", null, null, "NO-SUCH", null]);

        using HttpResponseMessage response = await client.UploadAsync(Collection, manager, workbook, commit: false);

        Assert.Equal(
            ["2 parent_task_legacy_id REFERENCE_NOT_FOUND", "3 planned_finish_date DATE_BEFORE_START", "4 schedule_activity_wbs REFERENCE_NOT_FOUND"],
            ImportApi.IssuesOf(await response.ReadObjectAsync()));
    }

    private async Task<string> FormalIdAsync(Guid project) =>
        Assert.Single(await host.Database.QueryAsync($"SELECT formal_project_id FROM project.project WHERE id = '{project}'"));
}
