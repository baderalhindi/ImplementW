using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using PMPlatform.Tests.Integration.Identity;

namespace PMPlatform.Tests.Integration.Reports;

/// <summary>
/// TASK-071's acceptance criteria and validation checks against the API: SCR-138's query surface is the REPORT_RULES allowlist — a field outside it,
/// asked for directly, is refused before anything is read; and a report job's output is authorised at download time on the same rules as the data
/// it holds, not only when it was generated. Beside them, ADR-019: an external entity user is given no report; and the amounts a viewer does not
/// hold are withheld (shipped-permission-matrix.md D-4, D-7).
/// </summary>
[Collection(ReportSuite.Name)]
public sealed class ReportAcceptanceTests(ReportTestHost host)
{
    private const string FinancialBudget = "FINANCIAL_KPI_FINANCIAL_POSITION";

    /// <summary>Acceptance criterion 1 and the workbook's check: a field outside the allowlist, requested by a direct API call, is refused.</summary>
    [Fact]
    public async Task AFieldOutsideTheAllowlistIsRefusedBeforeAnythingIsRead()
    {
        using HttpClient client = host.Api.CreateClient();
        string composer = (await client.SignInOrFailAsync(2)).AccessToken;
        await host.ReportProjectAsync();

        // A composition of allowlisted fields runs.
        using (HttpResponseMessage allowed = await client.PostAsync($"{ReportDriver.Explorer}/run", composer, new
        {
            columns = new[] { Field("PROJECT", "TITLE"), Field("PROJECT_LIFECYCLE_STATE", "LIFECYCLE_STATE"), Field(FinancialBudget, "FINANCIAL_CONDITION") },
            filters = new[] { new { sourceEntityCode = "PROJECT_LIFECYCLE_STATE", fieldCode = "LIFECYCLE_STATE", @operator = "EQ", value = "ACTIVE" } },
        }))
        {
            Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
        }

        // The live Approved Budget is a field of the register, of a projection the allowlist names, but not itself allowlisted.
        await AssertRefusedAsync(client, composer, new { columns = new[] { Field("PROJECT", "TITLE"), Field(FinancialBudget, "APPROVED_BUDGET") } },
            "REPORT_COLUMN_NOT_SUPPORTED", "columns[1] FIELD_NOT_ALLOWLISTED");

        // A projection the allowlist does not name is a join it does not allow; a field the register does not have is refused the same way.
        await AssertRefusedAsync(client, composer, new { columns = new[] { Field("PROGRESS_PUBLISHED_PROGRESS_HISTORY", "OVERALL_HEALTH") } },
            "REPORT_COLUMN_NOT_SUPPORTED", "columns[0] JOIN_NOT_ALLOWLISTED");
        await AssertRefusedAsync(client, composer, new { columns = new[] { Field("PROJECT", "REGISTRATION_BUDGET_SAR") } },
            "REPORT_COLUMN_NOT_SUPPORTED", "columns[0] FIELD_NOT_ALLOWLISTED");
        await AssertRefusedAsync(client, composer, new { columns = new[] { Field("PROJECT_PROJECT", "ID") } },
            "REPORT_COLUMN_NOT_SUPPORTED", "columns[0] JOIN_NOT_ALLOWLISTED");

        // A filter on an allowlisted field the allowlist does not make filterable, or with an operator its type does not take.
        await AssertRefusedAsync(client, composer,
            new { columns = new[] { Field("PROJECT", "TITLE") }, filters = new[] { new { sourceEntityCode = "PROJECT", fieldCode = "EXTERNAL_ENTITY", @operator = "EQ", value = ReportTestHost.EntityId } } },
            "REPORT_FILTER_NOT_SUPPORTED", "filters[0] NOT_ALLOWED");
        await AssertRefusedAsync(client, composer,
            new { columns = new[] { Field("PROJECT", "TITLE") }, filters = new[] { new { sourceEntityCode = "PROJECT_LIFECYCLE_STATE", fieldCode = "LIFECYCLE_STATE", @operator = "GT", value = "ACTIVE" } } },
            "REPORT_FILTER_NOT_SUPPORTED", "filters[0].operator OPERATOR_NOT_SUPPORTED");

        // Nothing that is not a code reaches the module at all: no query text, no expression.
        using (HttpResponseMessage injected = await client.PostAsync($"{ReportDriver.Explorer}/run", composer,
                   new { columns = new[] { Field("PROJECT", "TITLE; DROP TABLE project.project") } }))
        {
            await AssertRefusalAsync(injected, HttpStatusCode.BadRequest, "VALIDATION_FAILED", "columns[0].fieldCode MALFORMED");
        }

        // An export of an out-of-allowlist field is refused the same way, and no job is made.
        string jobsBefore = Assert.Single(await host.Database.QueryAsync($"SELECT count(*)::text FROM reports.report_job WHERE requested_by_user_id = '{ReportDriver.Person(2)}'"));
        using (HttpResponseMessage export = await client.ExportAsync(composer, $"{ReportDriver.Explorer}/export",
                   new { columns = new[] { Field(FinancialBudget, "APPROVED_BUDGET") }, format = "CSV", language = "en" }))
        {
            await AssertRefusalAsync(export, HttpStatusCode.UnprocessableEntity, "REPORT_COLUMN_NOT_SUPPORTED", "columns[0] FIELD_NOT_ALLOWLISTED");
        }

        Assert.Equal([jobsBefore], await host.Database.QueryAsync($"SELECT count(*)::text FROM reports.report_job WHERE requested_by_user_id = '{ReportDriver.Person(2)}'"));

        // The allowlist the explorer offers is exactly the configuration's, and the database admits no other field into a saved composition.
        using HttpResponseMessage offered = await client.GetAsync($"{ReportDriver.Allowlist}?pageSize=200", composer);
        JsonObject page = await offered.ReadObjectAsync();
        Assert.Equal(ReportTestHost.AllowlistVersionId.ToString(), page["configurationVersionId"]!.GetValue<string>());
        Assert.DoesNotContain(page["items"]!.AsArray(), e => $"{e!["sourceEntityCode"]}.{e["fieldCode"]}" == ReportTestHost.NotAllowlisted[0]);
        Assert.Equal(
            (await host.Database.QueryAsync($"SELECT source_entity_code || '.' || field_code FROM master_data_config.report_allowlist_entry WHERE configuration_version_id = '{ReportTestHost.AllowlistVersionId}'"))
                .Order(StringComparer.Ordinal),
            page["items"]!.AsArray().Select(e => $"{e!["sourceEntityCode"]}.{e["fieldCode"]}").Order(StringComparer.Ordinal));
        string? violated = null;
        try
        {
            await host.Database.ExecuteAsync($"""
                INSERT INTO reports.saved_view (id, owner_user_id, view_type, name, name_lang, created_at, created_by, updated_at, updated_by)
                VALUES ('{Guid.NewGuid()}', '{ReportDriver.Person(2)}', 'EXPLORER_COMPOSITION', 'Bypass', 'en', now(), '{ReportDriver.Person(2)}', now(), '{ReportDriver.Person(2)}');
                INSERT INTO reports.saved_view_column (id, saved_view_id, report_allowlist_entry_id, sort_order, created_at, created_by, updated_at, updated_by)
                SELECT '{Guid.NewGuid()}', v.id, '{Guid.NewGuid()}', 1, now(), v.owner_user_id, now(), v.owner_user_id FROM reports.saved_view v WHERE v.name = 'Bypass';
                """);
        }
        catch (Npgsql.PostgresException exception)
        {
            violated = exception.ConstraintName;
        }

        Assert.Equal("fk_saved_view_column_report_allowlist_entry_report_allowlist_e", violated);
    }

    /// <summary>
    /// Acceptance criterion 2 and the workbook's check: an output generated for one person is refused to another, unauthorised person's session; and
    /// its own requester is refused once they may no longer see what it holds — authorised at download time, not only at generation.
    /// </summary>
    [Fact]
    public async Task AnOutputIsAuthorisedAgainAtDownloadTimeOnTheRulesOfItsData()
    {
        using HttpClient client = host.Api.CreateClient();
        string departmentManager = (await client.SignInOrFailAsync(3)).AccessToken;
        string viewer = (await client.SignInOrFailAsync(6)).AccessToken;
        string portfolio = (await client.SignInOrFailAsync(2)).AccessToken;
        Guid projectId = await host.ReportProjectAsync(title: "Download authorisation project");

        Guid jobId = await client.ExportOrFailAsync(departmentManager, $"{ReportDriver.Reports}/PROJECT_REGISTER/export", new
        {
            parameters = new[] { new { code = "DEPARTMENT", value = ReportTestHost.DepartmentId.ToString() } },
            format = "CSV",
            language = "en",
        });
        Assert.Equal("REQUESTED", (await client.JobOrFailAsync(departmentManager, jobId))["status"]!.GetValue<string>());
        await host.RunJobsAsync();
        JsonObject job = await client.JobOrFailAsync(departmentManager, jobId);
        Assert.Equal("COMPLETED", job["status"]!.GetValue<string>());

        // SCR-140 names who generated it and when: the requester, in their job and in their Export History.
        Assert.Equal(ReportDriver.Person(3).ToString(), job["requestedByUserId"]!.GetValue<string>());
        using (HttpResponseMessage history = await client.GetAsync($"{ReportDriver.Jobs}?pageSize=200", departmentManager))
        {
            JsonNode listed = (await history.ReadObjectAsync())["items"]!.AsArray().Single(j => j!["id"]!.GetValue<string>() == jobId.ToString())!;
            Assert.Equal(ReportDriver.Person(3).ToString(), listed["requestedByUserId"]!.GetValue<string>());
            Assert.Equal(job["completedAt"]!.GetValue<string>(), listed["completedAt"]!.GetValue<string>());
        }

        // Its requester downloads it: the bytes they were given, matching the hash recorded at generation.
        byte[] content;
        using (HttpResponseMessage download = await client.DownloadAsync(departmentManager, jobId))
        {
            Assert.Equal(HttpStatusCode.OK, download.StatusCode);
            Assert.Equal("application/octet-stream", download.Content.Headers.ContentType!.MediaType);
            Assert.Equal("no-store", download.Headers.CacheControl!.ToString());
            content = await download.Content.ReadAsByteArrayAsync();
        }

        Assert.Equal(job["output"]!["checksumSha256"]!.GetValue<string>(), Convert.ToHexStringLower(SHA256.HashData(content)));
        Assert.Contains("Download authorisation project", Encoding.UTF8.GetString(content), StringComparison.Ordinal);

        // Another person's session: one with no data access, and one with access to the same data — the output is its requester's alone (R-47).
        foreach (string other in new[] { viewer, portfolio })
        {
            using HttpResponseMessage refused = await client.DownloadAsync(other, jobId);
            Assert.Equal(HttpStatusCode.NotFound, refused.StatusCode);
            using HttpResponseMessage hidden = await client.GetAsync($"{ReportDriver.Jobs}/{jobId}", other);
            Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);
        }

        // The project leaves the requester's department: the same requester, the same output, is refused now — and the refusal is audited.
        await host.Database.ExecuteAsync($"BEGIN; SET LOCAL session_replication_role = replica; UPDATE project.project SET department_id = '{ReportTestHost.OtherDepartmentId}' WHERE id = '{projectId}'; COMMIT;");
        try
        {
            using HttpResponseMessage moved = await client.DownloadAsync(departmentManager, jobId);
            await AssertRefusalAsync(moved, HttpStatusCode.Forbidden, "PERMISSION_DENIED");
            Assert.Equal(["Reports.OutputAccessDenied|DENIED"], await host.Database.QueryAsync(
                $"SELECT event_type || '|' || outcome FROM audit_activity.audit_event WHERE subject_id = '{jobId}' AND event_type = 'Reports.OutputAccessDenied'"));
        }
        finally
        {
            await host.Database.ExecuteAsync($"BEGIN; SET LOCAL session_replication_role = replica; UPDATE project.project SET department_id = '{ReportTestHost.DepartmentId}' WHERE id = '{projectId}'; COMMIT;");
        }

        // Back in scope, the download is allowed again; then the export permission itself is withdrawn, and it is refused again.
        using (HttpResponseMessage restored = await client.DownloadAsync(departmentManager, jobId))
        {
            Assert.Equal(HttpStatusCode.OK, restored.StatusCode);
        }

        string grant = $"""
            permission_profile_version_id = '{IdentityDatabase.ProfileVersionId(3)}' AND permission_id = (SELECT id FROM identity_access.permission WHERE code = 'REPORT_EXPORT')
            """;
        await host.Database.ExecuteAsync($"BEGIN; SET LOCAL session_replication_role = replica; UPDATE identity_access.permission_profile_grant SET data_scope = 'OWN' WHERE {grant}; COMMIT;");
        try
        {
            using HttpResponseMessage withdrawn = await client.DownloadAsync(departmentManager, jobId);
            Assert.Equal(HttpStatusCode.Forbidden, withdrawn.StatusCode);
        }
        finally
        {
            await host.Database.ExecuteAsync($"BEGIN; SET LOCAL session_replication_role = replica; UPDATE identity_access.permission_profile_grant SET data_scope = 'DEPT' WHERE {grant}; COMMIT;");
        }
    }

    /// <summary>
    /// Build Pack 06 §A item 6 and ADR-019 (superseding ADR-013's entity report set): an external entity user is given no report — the catalogue,
    /// a report, its run and export, the jobs, the explorer, the allowlist and the definitions all answer 404, as if the routes did not exist —
    /// the gate's refusal included (shipped-permission-matrix.md D-7).
    /// </summary>
    [Fact]
    public async Task AnEntityUserIsGivenNoReport()
    {
        using HttpClient client = host.Api.CreateClient();
        string entity = (await client.SignInOrFailAsync(8)).AccessToken;
        await host.ReportProjectAsync(title: "Entity's own project");

        foreach (HttpResponseMessage response in new[]
                 {
                     await client.GetAsync(ReportDriver.Reports, entity),
                     await client.GetAsync($"{ReportDriver.Reports}/PROJECT_REGISTER", entity),
                     await client.RunAsync(entity, "PROJECT_REGISTER", new { }),
                     await client.ExportAsync(entity, $"{ReportDriver.Reports}/PROJECT_REGISTER/export", new { format = "CSV", language = "en" }),
                     await client.GetAsync(ReportDriver.Jobs, entity),
                     await client.PostAsync($"{ReportDriver.Explorer}/run", entity, new { columns = new[] { Field("PROJECT", "TITLE") } }),
                     await client.GetAsync(ReportDriver.Allowlist, entity),
                     await client.GetAsync(ReportDriver.Definitions, entity),
                 })
        {
            using (response)
            {
                Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            }
        }
    }

    /// <summary>
    /// Review C2 (shipped-permission-matrix.md D-4): the Viewer sees its department's financial condition with every amount withheld, and no other
    /// department's project; the Portfolio Manager, who holds the amounts, sees them.
    /// </summary>
    [Fact]
    public async Task TheViewerSeesTheFinancialConditionOfItsDepartmentWithTheAmountsWithheld()
    {
        using HttpClient client = host.Api.CreateClient();
        string viewer = (await client.SignInOrFailAsync(6)).AccessToken;
        string portfolio = (await client.SignInOrFailAsync(2)).AccessToken;
        Guid own = await host.ReportProjectAsync(title: "Viewer's department project");
        Guid other = await host.ReportProjectAsync(departmentId: ReportTestHost.OtherDepartmentId, title: "Another department's project");
        foreach (Guid project in new[] { own, other })
        {
            Guid cycle = await host.ReportingPeriodAsync(project, ReportDriver.Today.AddDays(-14), ReportDriver.Today.AddDays(-7), "CLOSED");
            await host.PublishedFinancialsAsync(project, cycle, "1000000.00", "400000.00", "990000.00");
        }

        JsonObject seen = await client.RunOrFailAsync(viewer, "FINANCIAL_PERFORMANCE", new { });
        Assert.Contains(own, seen.ProjectIds());
        Assert.DoesNotContain(other, seen.ProjectIds());
        Assert.Equal("GREEN", seen.Cell(own, "FINANCIAL_KPI_PUBLISHED_FINANCIAL_SNAPSHOT", "FINANCIAL_CONDITION")["value"]!.GetValue<string>());
        foreach (string amount in new[] { "APPROVED_BUDGET", "ACTUAL_EXPENDITURE_TO_DATE", "FORECAST_AT_COMPLETION" })
        {
            JsonObject cell = seen.Cell(own, "FINANCIAL_KPI_PUBLISHED_FINANCIAL_SNAPSHOT", amount);
            Assert.Equal((true, "RESTRICTED"), (cell["isMasked"]!.GetValue<bool>(), cell["unknownReason"]!.GetValue<string>()));
            Assert.Null(cell["value"]);
        }

        JsonObject revealed = await client.RunOrFailAsync(portfolio, "FINANCIAL_PERFORMANCE", new { });
        Assert.Equal("1000000.00", revealed.Cell(own, "FINANCIAL_KPI_PUBLISHED_FINANCIAL_SNAPSHOT", "APPROVED_BUDGET")["value"]!.GetValue<string>());
    }

    private static object Field(string entity, string field) => new { sourceEntityCode = entity, fieldCode = field };

    private static async Task AssertRefusedAsync(HttpClient client, string token, object body, string code, params string[] errors)
    {
        using HttpResponseMessage response = await client.PostAsync($"{ReportDriver.Explorer}/run", token, body);
        await AssertRefusalAsync(response, HttpStatusCode.UnprocessableEntity, code, errors);
    }

    private static async Task AssertRefusalAsync(HttpResponseMessage response, HttpStatusCode status, string code, params string[] errors)
    {
        (HttpStatusCode actualStatus, string? actualCode, string[] actualErrors) = await response.RefusalAsync();
        Assert.Equal(status, actualStatus);
        Assert.Equal(code, actualCode);
        Assert.Equal(errors, actualErrors);
    }
}
