using System.Net;
using System.Text.Json.Nodes;
using PMPlatform.Tests.Integration.Identity;

namespace PMPlatform.Tests.Integration.DataImport;

/// <summary>
/// TASK-102 through the API and the database, on ADM-012's register: the template names the columns; a validation writes nothing; an
/// import loads every row or none, each through the department rules and audited; loading the same workbook again creates nothing.
/// </summary>
[Collection(IdentitySuite.Name)]
public sealed class DepartmentImportTests(IdentityTestHost host)
{
    private static readonly string[] Header = ["legacy_id", "code", "name_ar", "name_en", "parent_code", "directory_reference", "is_active"];

    [Fact]
    public async Task TheTemplateNamesEveryColumnTheRegisterReads()
    {
        using HttpClient client = host.Api.CreateClient();
        Session administrator = await client.SignInOrFailAsync(1);

        using HttpResponseMessage template = await client.TemplateAsync(AdministrationApi.Departments, administrator.AccessToken);

        Assert.Equal(HttpStatusCode.OK, template.StatusCode);
        Assert.Equal("department-import-template.xlsx", template.Content.Headers.ContentDisposition!.FileName?.Trim('"'));
        Assert.Equal(Header, ImportApi.HeaderOf(await template.Content.ReadAsByteArrayAsync()));
    }

    [Fact]
    public async Task AValidationReportsEveryRowAndWritesNothing()
    {
        string run = Run();
        using HttpClient client = host.Api.CreateClient();
        Session administrator = await client.SignInOrFailAsync(1);
        byte[] workbook = ImportApi.Workbook(Header,
            [$"V{run}-1", $"IMP-{run}-A", "إدارة أ", "Department A", null, null, null],
            [$"V{run}-2", $"IMP-{run}-B", "إدارة ب", "Department B", $"IMP-{run}-A", null, "FALSE"],
            [$"V{run}-3", "lower", "إدارة ج", "Department C", "NO-SUCH-PARENT", null, "maybe"],
            [$"V{run}-4", "DEPT-IDENTITY-TEST", "إدارة د", "Department D", null, null, null]);

        using HttpResponseMessage response = await client.UploadAsync(AdministrationApi.Departments, administrator.AccessToken, workbook, commit: false);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        JsonObject report = await response.ReadObjectAsync();
        Assert.Equal(["4 code MALFORMED", "4 is_active MALFORMED", "5 code ALREADY_EXISTS"], ImportApi.IssuesOf(report));
        Assert.Equal(["READY", "READY", "REJECTED", "REJECTED"], report["rows"]!.AsArray().Select(r => r!["status"]!.GetValue<string>()));
        Assert.False(report["committed"]!.GetValue<bool>());
        Assert.Equal(["0"], await host.Database.QueryAsync($"SELECT count(*)::text FROM identity_access.department WHERE code LIKE 'IMP-{run}-%'"));
        Assert.Equal(["0"], await host.Database.QueryAsync($"SELECT count(*)::text FROM common.imported_record WHERE legacy_id LIKE 'V{run}-%'"));
    }

    [Fact]
    public async Task AnImportLoadsEveryRowOrNone()
    {
        string run = Run();
        using HttpClient client = host.Api.CreateClient();
        Session administrator = await client.SignInOrFailAsync(1);
        byte[] refused = ImportApi.Workbook(Header,
            [$"N{run}-1", $"IMP-{run}-A", "إدارة أ", "Department A", null, null, null],
            [$"N{run}-2", $"IMP-{run}-B", "إدارة ب", "Department B", "NO-SUCH-PARENT", null, null]);

        using HttpResponseMessage response = await client.UploadAsync(AdministrationApi.Departments, administrator.AccessToken, refused, commit: true);

        JsonObject report = await response.ReadObjectAsync();
        Assert.Equal(["3 parent_code REFERENCE_NOT_FOUND"], ImportApi.IssuesOf(report));
        Assert.False(report["committed"]!.GetValue<bool>());
        Assert.Equal(["0"], await host.Database.QueryAsync($"SELECT count(*)::text FROM identity_access.department WHERE code LIKE 'IMP-{run}-%'"));
        Assert.Equal(["0"], await host.Database.QueryAsync($"SELECT count(*)::text FROM common.import_batch WHERE file_name = 'migration.xlsx' AND created_at > now() - interval '1 minute' AND row_count = 2 AND created_count = 1"));
    }

    [Fact]
    public async Task AnImportCreatesTheDepartmentsInTheirLegacyStateWithLineageAndAudit()
    {
        string run = Run();
        using HttpClient client = host.Api.CreateClient();
        Session administrator = await client.SignInOrFailAsync(1);
        byte[] workbook = ImportApi.Workbook(Header,
            [$"I{run}-1", $"IMP-{run}-A", "إدارة أ", "Department A", null, $"DIR-{run}", null],
            [$"I{run}-2", $"IMP-{run}-B", "إدارة ب", "Department B", $"IMP-{run}-A", null, "FALSE"]);

        using HttpResponseMessage response = await client.UploadAsync(AdministrationApi.Departments, administrator.AccessToken, workbook, commit: true);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        JsonObject report = await response.ReadObjectAsync();
        Assert.Empty(ImportApi.IssuesOf(report));
        Assert.True(report["committed"]!.GetValue<bool>());
        Assert.Equal((2, 0), (report["createdCount"]!.GetValue<int>(), report["skippedCount"]!.GetValue<int>()));
        Assert.Equal(
            [$"IMP-{run}-A|-|true|DIR-{run}|{IdentityDatabase.UserId(1)}", $"IMP-{run}-B|IMP-{run}-A|false|-|{IdentityDatabase.UserId(1)}"],
            await host.Database.QueryAsync($"""
                SELECT d.code || '|' || coalesce(p.code, '-') || '|' || d.is_active || '|' || coalesce(d.directory_reference, '-') || '|' || d.created_by
                FROM identity_access.department d LEFT JOIN identity_access.department p ON p.id = d.parent_department_id
                WHERE d.code LIKE 'IMP-{run}-%' ORDER BY d.code
                """));
        string batchId = report["batchId"]!.GetValue<string>();
        Assert.Equal(
            [$"I{run}-1|IMP-{run}-A|2", $"I{run}-2|IMP-{run}-B|3"],
            await host.Database.QueryAsync($"""
                SELECT r.legacy_id || '|' || d.code || '|' || r.row_number FROM common.imported_record r
                JOIN identity_access.department d ON d.id = r.entity_id WHERE r.import_batch_id = '{batchId}' ORDER BY r.row_number
                """));
        Assert.Equal(["DEPARTMENT|2|2|0"], await host.Database.QueryAsync($"SELECT register_code || '|' || row_count || '|' || created_count || '|' || skipped_count FROM common.import_batch WHERE id = '{batchId}'"));
        Assert.Equal(
            ["IdentityAccess.DepartmentCreated", "IdentityAccess.DepartmentCreated", "IdentityAccess.DepartmentDeactivated", "IdentityAccess.RecordsImported"],
            await host.Database.QueryAsync($"""
                SELECT event_type FROM audit_activity.audit_event
                WHERE subject_id IN (SELECT entity_id FROM common.imported_record WHERE import_batch_id = '{batchId}') OR subject_id = '{batchId}'
                ORDER BY event_type
                """));

        using HttpResponseMessage again = await client.UploadAsync(AdministrationApi.Departments, administrator.AccessToken, workbook, commit: true);

        JsonObject second = await again.ReadObjectAsync();
        Assert.Equal((0, 2), (second["createdCount"]!.GetValue<int>(), second["skippedCount"]!.GetValue<int>()));
        Assert.Equal(["SKIPPED", "SKIPPED"], second["rows"]!.AsArray().Select(r => r!["status"]!.GetValue<string>()));
        Assert.Equal(["2"], await host.Database.QueryAsync($"SELECT count(*)::text FROM identity_access.department WHERE code LIKE 'IMP-{run}-%'"));
    }

    [Fact]
    public async Task AFileThatIsNotAWorkbookIsRefused()
    {
        using HttpClient client = host.Api.CreateClient();
        Session administrator = await client.SignInOrFailAsync(1);

        using HttpResponseMessage response = await client.UploadAsync(AdministrationApi.Departments, administrator.AccessToken, "legacy_id,code\n"u8.ToArray(), commit: false);

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
    }

    [Fact]
    public async Task OnlyAHolderOfDataImportMayImport()
    {
        using HttpClient client = host.Api.CreateClient();
        Session viewer = await client.SignInOrFailAsync(6);

        using HttpResponseMessage template = await client.TemplateAsync(AdministrationApi.Departments, viewer.AccessToken);
        using HttpResponseMessage import = await client.UploadAsync(AdministrationApi.Departments, viewer.AccessToken, ImportApi.Workbook(Header), commit: true);

        Assert.Equal((HttpStatusCode.Forbidden, HttpStatusCode.Forbidden), (template.StatusCode, import.StatusCode));
    }

    private static string Run() => Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
}
