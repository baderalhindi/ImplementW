using System.Text.Json.Nodes;
using PMPlatform.Tests.Integration.DataImport;
using PMPlatform.Tests.Integration.Identity;

namespace PMPlatform.Tests.Integration.Milestone;

/// <summary>A project's milestones from a workbook (data-import.md D-12): each PLANNED through the form's own command; an achievement is still claimed on screen.</summary>
[Collection(MilestoneSuite.Name)]
public sealed class ProjectMilestoneImportTests(MilestoneTestHost host)
{
    private const string Collection = "/api/v1/project-milestones";
    private static readonly string[] Header =
        ["legacy_id", "project_formal_id", "title", "text_language", "milestone_category_code", "forecast_date", "schedule_activity_wbs", "sort_order"];

    [Fact]
    public async Task AManagerImportsMilestonesAndAnUnknownCategoryIsRefused()
    {
        using HttpClient client = host.Api.CreateClient();
        string manager = (await client.SignInOrFailAsync(8)).AccessToken;
        Guid project = await host.ScheduledProjectAsync(client, manager);
        string formalId = Assert.Single(await host.Database.QueryAsync($"SELECT formal_project_id FROM project.project WHERE id = '{project}'"));
        string run = Guid.NewGuid().ToString("N")[..10];

        using HttpResponseMessage template = await client.TemplateAsync(Collection, manager);
        Assert.Equal(Header, ImportApi.HeaderOf(await template.Content.ReadAsByteArrayAsync()));
        using HttpResponseMessage refused = await client.UploadAsync(Collection, manager, ImportApi.Workbook(Header,
            [$"{run}-0", formalId, "Handover", "en", "NO_SUCH", "2026-06-01", null, null]), commit: true);
        Assert.Equal(["2 milestone_category_code REFERENCE_NOT_FOUND"], ImportApi.IssuesOf(await refused.ReadObjectAsync()));
        using HttpResponseMessage response = await client.UploadAsync(Collection, manager, ImportApi.Workbook(Header,
            [$"{run}-1", formalId, "Handover", "en", "TEST_HANDOVER", "2026-06-01", null, "1"]), commit: true);

        JsonObject report = await response.ReadObjectAsync();
        Assert.Empty(ImportApi.IssuesOf(report));
        Assert.Equal(1, report["createdCount"]!.GetValue<int>());
        Assert.Equal(["PLANNED"], await host.Database.QueryAsync($"""
            SELECT m.status FROM schedule.project_milestone m JOIN schedule.project_schedule s ON s.id = m.project_schedule_id WHERE s.project_id = '{project}'
            """));
    }
}
