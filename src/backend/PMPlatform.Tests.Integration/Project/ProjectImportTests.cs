using System.Net;
using System.Net.Http.Headers;
using System.Text.Json.Nodes;
using PMPlatform.Tests.Integration.DataImport;
using PMPlatform.Tests.Integration.Identity;

namespace PMPlatform.Tests.Integration.Project;

/// <summary>
/// Project import from RPMO's workbook (the 2 Nov 2026 review, R3-4), through the API: a workbook validated row by row against the names it
/// carries, a word the importer maps, a submitted batch a second person approves, a re-upload that changes nothing, a department outside the
/// importer's scope, and a rollback that removes what the batch created.
/// </summary>
[Collection(ProjectSuite.Name)]
public sealed class ProjectImportTests(ProjectTestHost host)
{
    private const string Batches = "/api/v1/project-import-batches";

    private static readonly string[] Keys =
    [
        "legacy_ref", "name_ar", "owner_entity", "department", "sector", "portfolio", "initiative", "project_manager", "platform_status",
        "governance_level", "priority", "budget_sar", "start_date", "end_date", "region", "city", "description", "planned_progress",
        "actual_progress", "current_obstacle", "notes", "entity_type",
    ];

    [Fact]
    public async Task AnApprovedBatchCreatesItsProjectsAndARollbackRemovesThem()
    {
        using HttpClient client = host.Api.CreateClient();
        Sessions sessions = await client.SignInAsync();
        string reference = $"AHP-{Guid.NewGuid():N}"[..20];
        byte[] workbook = await WorkbookAsync([await RowAsync(reference, status: "قيد التنفيذ")]);

        JsonObject preview = await ValidateAsync(client, sessions.Reviewer, workbook);
        Assert.True(preview["submittable"]!.GetValue<bool>(), preview.ToJsonString());
        Assert.Equal(1, preview["createCount"]!.GetValue<int>());
        JsonObject row = preview["rows"]![0]!.AsObject();
        Assert.Equal("CREATE", row["action"]!.GetValue<string>());
        Assert.Contains(row["issues"]!.AsArray(), i => i!["code"]!.GetValue<string>() == "KEPT_BLANK" || i["code"]!.GetValue<string>() == "PERCENT_SCALED");

        // The importer submits; they cannot approve their own batch.
        using HttpResponseMessage submitted = await SubmitAsync(client, sessions.Reviewer, workbook, "{\"mappings\":[],\"skippedRows\":[]}");
        Assert.Equal(HttpStatusCode.Created, submitted.StatusCode);
        Guid batchId = AdministrationApi.IdOf(JsonNode.Parse(await submitted.Content.ReadAsStringAsync())!.AsObject());
        using HttpResponseMessage own = await PostAsync(client, sessions.Reviewer, $"{Batches}/{batchId}/approve");
        Assert.Equal(HttpStatusCode.Forbidden, own.StatusCode);

        using HttpResponseMessage approved = await PostAsync(client, sessions.Approver, $"{Batches}/{batchId}/approve");
        Assert.Equal(HttpStatusCode.OK, approved.StatusCode);
        JsonObject batch = JsonNode.Parse(await approved.Content.ReadAsStringAsync())!.AsObject();
        Assert.Equal("APPROVED", batch["status"]!.GetValue<string>());
        Assert.Equal(1, batch["createCount"]!.GetValue<int>());

        JsonObject project = JsonNode.Parse(Assert.Single(await host.Database.QueryAsync(
            $"SELECT to_jsonb(p)::text FROM project.project p WHERE p.legacy_reference = '{reference}'")))!.AsObject();
        Assert.Equal("ACTIVE", project["lifecycle_state"]!.GetValue<string>());
        Assert.Equal(batchId.ToString(), project["project_import_batch_id"]!.GetValue<string>());
        Assert.NotNull(project["formal_project_id"]!.GetValue<string>());
        Assert.Equal(83m, project["declared_actual_percent"]!.GetValue<decimal>());
        Assert.Equal("IMPORT", project["declared_source"]!.GetValue<string>());
        Assert.Null(project["declared_actual_spend_percent"]?.GetValue<decimal?>());

        // The same file again matches the project by its legacy reference and changes nothing: no duplicate.
        JsonObject again = await ValidateAsync(client, sessions.Reviewer, workbook);
        Assert.Equal(0, again["createCount"]!.GetValue<int>());
        Assert.Equal("SKIP", again["rows"]![0]!["action"]!.GetValue<string>());

        using HttpResponseMessage rolledBack = await PostAsync(client, sessions.Approver, $"{Batches}/{batchId}/rollback");
        Assert.Equal(HttpStatusCode.OK, rolledBack.StatusCode);
        Assert.Equal("ROLLED_BACK", JsonNode.Parse(await rolledBack.Content.ReadAsStringAsync())!["status"]!.GetValue<string>());
        Assert.Empty(await host.Database.QueryAsync($"SELECT id::text FROM project.project WHERE legacy_reference = '{reference}'"));
        Assert.Contains("Project.ImportBatchRolledBack", await host.Database.QueryAsync(
            $"SELECT event_type FROM audit_activity.audit_event WHERE subject_id = '{batchId}'"));
    }

    [Fact]
    public async Task AStatusToChooseBlocksTheRowUntilTheImporterMapsIt()
    {
        using HttpClient client = host.Api.CreateClient();
        Sessions sessions = await client.SignInAsync();
        byte[] workbook = await WorkbookAsync([await RowAsync($"AHP-{Guid.NewGuid():N}"[..20], status: "أخرى")]);

        JsonObject blocked = await ValidateAsync(client, sessions.Reviewer, workbook);
        Assert.False(blocked["submittable"]!.GetValue<bool>());
        Assert.Contains(blocked["rows"]![0]!["issues"]!.AsArray(), i => i!["code"]!.GetValue<string>() == "STATUS_TO_CHOOSE");
        Assert.Contains(blocked["unmatched"]!.AsArray(), u => u!["dimension"]!.GetValue<string>() == "STATUS");

        JsonObject mapped = await ValidateAsync(client, sessions.Reviewer, workbook,
            "{\"mappings\":[{\"dimension\":\"STATUS\",\"sourceText\":\"أخرى\",\"targetValue\":\"ACTIVE\"}],\"skippedRows\":[]}");
        Assert.True(mapped["submittable"]!.GetValue<bool>(), mapped.ToJsonString());
    }

    [Fact]
    public async Task ADepartmentManagerCannotImportARowOfAnotherDepartment()
    {
        using HttpClient client = host.Api.CreateClient();
        Sessions sessions = await client.SignInAsync();
        byte[] workbook = await WorkbookAsync([await RowAsync($"AHP-{Guid.NewGuid():N}"[..20], status: "نشط", department: ProjectTestHost.OtherDepartmentId)]);

        JsonObject preview = await ValidateAsync(client, sessions.Reviewer, workbook);
        Assert.Contains(preview["rows"]![0]!["issues"]!.AsArray(), i => i!["code"]!.GetValue<string>() == "OUTSIDE_SCOPE");
    }

    [Fact]
    public async Task AnInitiativeOfAnotherPortfolioIsAnError()
    {
        using HttpClient client = host.Api.CreateClient();
        Sessions sessions = await client.SignInAsync();
        string?[] row = await RowAsync($"AHP-{Guid.NewGuid():N}"[..20], status: "نشط");
        row[Array.IndexOf(Keys, "portfolio")] = "PF-T2 — Other portfolio";
        JsonObject preview = await ValidateAsync(client, sessions.Approver, await WorkbookAsync([row]));

        Assert.Contains(preview["rows"]![0]!["issues"]!.AsArray(), i => i!["code"]!.GetValue<string>() == "INITIATIVE_OUTSIDE_PORTFOLIO");
    }

    [Fact]
    public async Task WithoutTheImportPermissionTheTemplateIsForbidden()
    {
        using HttpClient client = host.Api.CreateClient();
        Sessions sessions = await client.SignInAsync();
        using HttpRequestMessage request = new(HttpMethod.Get, $"{Batches}/import-template/content");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", sessions.Entity);
        using HttpResponseMessage refused = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
    }

    internal static Task<JsonObject> ValidateAsync(HttpClient client, string token, byte[] workbook, string plan = "") =>
        ReadAsync(SendFormAsync(client, token, $"{Batches}/validations", workbook, plan));

    internal static Task<HttpResponseMessage> SendValidationAsync(HttpClient client, string token, byte[] workbook) =>
        SendFormAsync(client, token, $"{Batches}/validations", workbook, string.Empty);

    internal static Task<HttpResponseMessage> SubmitAsync(HttpClient client, string token, byte[] workbook, string plan) =>
        SendFormAsync(client, token, Batches, workbook, plan, note: "دفعة اختبار");

    internal static Task<HttpResponseMessage> PostAsync(HttpClient client, string token, string path, string? json = null)
    {
        HttpRequestMessage request = new(HttpMethod.Post, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.TryAddWithoutValidation("Idempotency-Key", Guid.NewGuid().ToString());
        request.Content = new StringContent(json ?? "{}", System.Text.Encoding.UTF8, "application/json");
        return client.SendAsync(request);
    }

    /// <summary>A workbook as RPMO fills it: its row of field keys, then the rows.</summary>
    internal static Task<byte[]> WorkbookAsync(string?[][] rows) => Task.FromResult(ImportApi.Workbook(Keys, rows));

    /// <summary>One project row naming the fixture's department, entity, sector, portfolio, initiative, place and local.r05 as manager.</summary>
    internal async Task<string?[]> RowAsync(string reference, string status, Guid? department = null)
    {
        string departmentCode = Assert.Single(await host.Database.QueryAsync(
            $"SELECT code FROM identity_access.department WHERE id = '{department ?? ProjectTestHost.DepartmentId}'"));
        string entityCode = Assert.Single(await host.Database.QueryAsync($"SELECT code FROM identity_access.external_entity WHERE id = '{ProjectTestHost.EntityId}'"));
        string sectorCode = Assert.Single(await host.Database.QueryAsync($"SELECT code FROM master_data_config.master_data_item WHERE id = '{ProjectTestHost.ClassificationId}'"));
        return
        [
            reference, $"مشروع {reference}", entityCode, departmentCode, sectorCode, "PF-T1 — اختبار", "SI-T1 — اختبار", "local.r05", status,
            "قياسي", "TEST_HIGH", "250000000", "2025-01-01", "2027-12-31", "TEST_REGION", "TEST_CITY", "وصف مختصر", "0.9", "0.83",
            "تأخر التسليم", "ملاحظة متابعة", "جهة حكومية",
        ];
    }

    private static async Task<HttpResponseMessage> SendFormAsync(HttpClient client, string token, string path, byte[] workbook, string plan, string? note = null)
    {
        HttpRequestMessage request = new(HttpMethod.Post, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.TryAddWithoutValidation("Idempotency-Key", Guid.NewGuid().ToString());
        ByteArrayContent file = new(workbook);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        MultipartFormDataContent form = new() { { file, "file", "RPMO_Projects_Import.xlsx" } };
        if (plan.Length > 0)
        {
            form.Add(new StringContent(plan), "plan");
        }

        if (note is not null)
        {
            form.Add(new StringContent(note), "note");
            form.Add(new StringContent("ar"), "noteLanguage");
        }

        request.Content = form;
        return await client.SendAsync(request);
    }

    private static async Task<JsonObject> ReadAsync(Task<HttpResponseMessage> send)
    {
        using HttpResponseMessage response = await send;
        string body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, body);
        return JsonNode.Parse(body)!.AsObject();
    }
}
