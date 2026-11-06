using System.Net;
using System.Text.Json.Nodes;
using PMPlatform.Tests.Integration.DataImport;
using PMPlatform.Tests.Integration.Identity;

namespace PMPlatform.Tests.Integration.Risk;

/// <summary>
/// A project register's import (data-import.md D-12, D-13), on risks and issues: whoever may create the record imports it, on the projects they
/// reach and no other; a row meets the form's rules; legacy IDs are each project's own, and nothing of another project leaks through them.
/// </summary>
[Collection(RiskSuite.Name)]
public sealed class ProjectRiskImportTests(RiskTestHost host)
{
    private const string Collection = RiskDriver.Risks;
    private const string Category = "TEST_DELIVERY";
    private static readonly string[] Header =
        ["legacy_id", "project_formal_id", "title", "description", "text_language", "risk_category_code", "owner_username", "identified_date", "next_review_date"];

    [Fact]
    public async Task TheTemplateNamesEveryColumnAndOnlyTheProjectsThisPersonReaches()
    {
        Guid mine = await host.ProjectAsync(projectManager: 5);
        Guid other = await host.ProjectAsync(projectManager: 8);
        using HttpClient client = host.Api.CreateClient();
        Session manager = await client.SignInOrFailAsync(5);

        using HttpResponseMessage template = await client.TemplateAsync(Collection, manager.AccessToken);

        Assert.Equal(HttpStatusCode.OK, template.StatusCode);
        byte[] workbook = await template.Content.ReadAsByteArrayAsync();
        Assert.Equal(Header, ImportApi.HeaderOf(workbook));
        string references = ImportApi.AllText(workbook);
        Assert.Contains(await FormalIdAsync(mine), references, StringComparison.Ordinal);
        Assert.DoesNotContain(await FormalIdAsync(other), references, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SomeoneWhoCannotCreateRisksIsRefusedTheTemplateAndTheImport()
    {
        Guid project = await host.ProjectAsync(projectManager: 5);
        using HttpClient client = host.Api.CreateClient();
        Session outsider = await client.SignInOrFailAsync(6);

        using HttpResponseMessage template = await client.TemplateAsync(Collection, outsider.AccessToken);
        using HttpResponseMessage upload = await client.UploadAsync(Collection, outsider.AccessToken, Workbook(await FormalIdAsync(project), "OUT-1"), commit: true);

        Assert.Equal(HttpStatusCode.Forbidden, template.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, upload.StatusCode);
        Assert.Equal(["0"], await CountAsync(project));
    }

    [Fact]
    public async Task AManagerImportsIntoTheirOwnProjectAsIfTyped()
    {
        Guid project = await host.ProjectAsync(projectManager: 5);
        string run = Run();
        using HttpClient client = host.Api.CreateClient();
        Session manager = await client.SignInOrFailAsync(5);

        using HttpResponseMessage response = await client.UploadAsync(Collection, manager.AccessToken, Workbook(await FormalIdAsync(project), $"{run}-1", $"{run}-2"), commit: true);

        JsonObject report = await response.ReadObjectAsync();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(ImportApi.IssuesOf(report));
        Assert.True(report["committed"]!.GetValue<bool>());
        Assert.Equal(2, report["createdCount"]!.GetValue<int>());
        Assert.Equal(["IDENTIFIED", "IDENTIFIED"], await host.Database.QueryAsync($"SELECT status FROM risk.risk WHERE project_id = '{project}'"));
        // The batch reads as a change to the team's own data, not as an administrator's privileged act; each row as a registration on screen does.
        Assert.Equal(["DATA_CHANGE"], await host.Database.QueryAsync($"""
            SELECT event_class FROM audit_activity.audit_event
            WHERE event_type = 'Risk.RecordsImported' AND subject_id = '{report["batchId"]!.GetValue<string>()}'
            """));
        Assert.Equal(["2"], await host.Database.QueryAsync($"""
            SELECT count(*)::text FROM audit_activity.audit_event
            WHERE event_type = 'Risk.RiskRegistered' AND subject_id IN (SELECT id FROM risk.risk WHERE project_id = '{project}')
            """));
    }

    [Fact]
    public async Task AProjectOutOfReachReadsAsUnknownAndTheWholeWorkbookIsRefused()
    {
        Guid mine = await host.ProjectAsync(projectManager: 5);
        Guid other = await host.ProjectAsync(projectManager: 8);
        string run = Run();
        using HttpClient client = host.Api.CreateClient();
        Session manager = await client.SignInOrFailAsync(5);
        byte[] workbook = ImportApi.Workbook(Header,
            Row($"{run}-1", await FormalIdAsync(mine)),
            Row($"{run}-2", await FormalIdAsync(other)),
            Row($"{run}-3", "PRJ-NO-SUCH"));

        using HttpResponseMessage response = await client.UploadAsync(Collection, manager.AccessToken, workbook, commit: true);

        JsonObject report = await response.ReadObjectAsync();
        Assert.Equal(["3 project_formal_id REFERENCE_NOT_FOUND", "4 project_formal_id REFERENCE_NOT_FOUND"], ImportApi.IssuesOf(report));
        Assert.False(report["committed"]!.GetValue<bool>());
        Assert.Equal(["0"], await CountAsync(mine));
        Assert.Equal(["0"], await CountAsync(other));
    }

    [Fact]
    public async Task TwoProjectsMayUseTheSameLegacyIdAndOneCannotLearnOfTheOthersRecord()
    {
        Guid mine = await host.ProjectAsync(projectManager: 5);
        Guid other = await host.ProjectAsync(projectManager: 8);
        string legacyId = $"{Run()}-SHARED";
        using HttpClient client = host.Api.CreateClient();
        Session manager = await client.SignInOrFailAsync(5);
        Session entityManager = await client.SignInOrFailAsync(8);

        using HttpResponseMessage first = await client.UploadAsync(Collection, entityManager.AccessToken, Workbook(await FormalIdAsync(other), legacyId), commit: true);
        Assert.Empty(ImportApi.IssuesOf(await first.ReadObjectAsync()));

        // The same ID in a project of one's own is a new record, not a skip that hands back the other project's id.
        using HttpResponseMessage second = await client.UploadAsync(Collection, manager.AccessToken, Workbook(await FormalIdAsync(mine), legacyId), commit: true);
        JsonObject created = await second.ReadObjectAsync();
        Assert.Equal(1, created["createdCount"]!.GetValue<int>());
        Assert.Equal(0, created["skippedCount"]!.GetValue<int>());

        // Naming the other project with the ID already loaded there is refused, not reported as skipped with the record's id.
        using HttpResponseMessage probe = await client.UploadAsync(Collection, manager.AccessToken, Workbook(await FormalIdAsync(other), legacyId), commit: false);
        JsonObject refused = await probe.ReadObjectAsync();
        Assert.Equal(["2 project_formal_id REFERENCE_NOT_FOUND"], ImportApi.IssuesOf(refused));
        Assert.Null(refused["rows"]![0]!["entityId"]?.GetValue<string>());
        Assert.Equal(0, refused["skippedCount"]!.GetValue<int>());
    }

    [Fact]
    public async Task LoadingTheSameWorkbookAgainCreatesNothing()
    {
        Guid project = await host.ProjectAsync(projectManager: 5);
        string run = Run();
        using HttpClient client = host.Api.CreateClient();
        Session manager = await client.SignInOrFailAsync(5);
        byte[] workbook = Workbook(await FormalIdAsync(project), $"{run}-1");

        using HttpResponseMessage first = await client.UploadAsync(Collection, manager.AccessToken, workbook, commit: true);
        using HttpResponseMessage second = await client.UploadAsync(Collection, manager.AccessToken, workbook, commit: true);

        Assert.Equal(1, (await first.ReadObjectAsync())["createdCount"]!.GetValue<int>());
        Assert.Equal(1, (await second.ReadObjectAsync())["skippedCount"]!.GetValue<int>());
        Assert.Equal(["1"], await CountAsync(project));
    }

    [Fact]
    public async Task ARowMeetsTheFormsRules()
    {
        Guid project = await host.ProjectAsync(projectManager: 5);
        string run = Run();
        using HttpClient client = host.Api.CreateClient();
        Session manager = await client.SignInOrFailAsync(5);
        string formalId = await FormalIdAsync(project);
        byte[] workbook = ImportApi.Workbook(Header,
            [$"{run}-1", formalId, "Title", "Description", "en", "TEST_DRAFT", null, "2026-01-10", null],
            [$"{run}-2", formalId, "Title", "Description", "en", Category, "no.such.person", "2026-01-10", null],
            [$"{run}-3", formalId, "Title", "Description", "en", Category, null, "2026-01-10", "2026-01-09"]);

        using HttpResponseMessage response = await client.UploadAsync(Collection, manager.AccessToken, workbook, commit: false);

        Assert.Equal(
            ["2 risk_category_code REFERENCE_NOT_FOUND", "3 owner_username REFERENCE_NOT_FOUND", "4 next_review_date DATE_BEFORE_START"],
            ImportApi.IssuesOf(await response.ReadObjectAsync()));
    }

    [Fact]
    public async Task AClosedProjectRefusesARowAsItRefusesTheForm()
    {
        Guid project = await host.ProjectAsync(state: "CLOSED", projectManager: 5);
        using HttpClient client = host.Api.CreateClient();
        Session manager = await client.SignInOrFailAsync(5);

        using HttpResponseMessage response = await client.UploadAsync(Collection, manager.AccessToken, Workbook(await FormalIdAsync(project), $"{Run()}-1"), commit: true);

        JsonObject report = await response.ReadObjectAsync();
        Assert.False(report["committed"]!.GetValue<bool>());
        Assert.Equal(["2 PROJECT_CLOSED"], ImportApi.IssuesOf(report));
        Assert.Equal(["0"], await CountAsync(project));
    }

    [Fact]
    public async Task IssuesAndChallengesImportThroughRaisingAConcern()
    {
        Guid project = await host.ProjectAsync(projectManager: 5);
        string run = Run();
        using HttpClient client = host.Api.CreateClient();
        Session manager = await client.SignInOrFailAsync(5);
        string[] header = ["legacy_id", "project_formal_id", "concern_type", "title", "description", "text_language", "category_code", "priority_code", "target_resolution_date"];
        string formalId = await FormalIdAsync(project);
        byte[] workbook = ImportApi.Workbook(header,
            [$"{run}-1", formalId, "ISSUE", "An issue", "What happened", "en", "TEST_CONCERN", "TEST_URGENT", null],
            [$"{run}-2", formalId, "CHALLENGE", "A challenge", "What may happen", "en", "TEST_CONCERN", "TEST_URGENT", null]);

        using HttpResponseMessage template = await client.TemplateAsync("/api/v1/management-concerns", manager.AccessToken);
        Assert.Equal(header, ImportApi.HeaderOf(await template.Content.ReadAsByteArrayAsync()));
        using HttpResponseMessage response = await client.UploadAsync("/api/v1/management-concerns", manager.AccessToken, workbook, commit: true);

        JsonObject report = await response.ReadObjectAsync();
        Assert.Empty(ImportApi.IssuesOf(report));
        Assert.Equal(2, report["createdCount"]!.GetValue<int>());
        Assert.Equal(
            ["CHALLENGE", "ISSUE"],
            await host.Database.QueryAsync($"SELECT concern_type FROM management_concern.management_concern WHERE project_id = '{project}' ORDER BY concern_type"));
    }

    private static string Run() => Guid.NewGuid().ToString("N")[..10];

    private static byte[] Workbook(string formalId, params string[] legacyIds) =>
        ImportApi.Workbook(Header, [.. legacyIds.Select(id => Row(id, formalId))]);

    private static string?[] Row(string legacyId, string formalId) =>
        [legacyId, formalId, "Contractor delay", "The contractor mobilises late", "en", Category, null, "2026-01-10", null];

    private async Task<string> FormalIdAsync(Guid project) =>
        Assert.Single(await host.Database.QueryAsync($"SELECT formal_project_id FROM project.project WHERE id = '{project}'"));

    private Task<IReadOnlyList<string>> CountAsync(Guid project) =>
        host.Database.QueryAsync($"SELECT count(*)::text FROM risk.risk WHERE project_id = '{project}'");
}
