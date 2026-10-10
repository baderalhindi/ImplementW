using System.IO.Compression;
using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using PMPlatform.Application.Features.Reports;
using PMPlatform.Application.Features.Reports.Contracts;
using PMPlatform.Tests.Integration.Identity;

namespace PMPlatform.Tests.Integration.Reports;

/// <summary>
/// FG-02 at runtime beside its acceptance criteria: the catalogue is ADR-006's ten, held by the database; missing is never zero and stale is never
/// current; parameters narrow and never widen; the three formats of ADR-005 are generated without a formula a spreadsheet could run; a job moves only
/// along its lifecycle, idempotently, and leaves no file when it fails, is cancelled or expires.
/// </summary>
[Collection(ReportSuite.Name)]
public sealed class ReportRuntimeTests(ReportTestHost host)
{
    /// <summary>ADR-006 and TASK-002: ten PUBLISHED reports, an eleventh code refused by the database, every seeded version valid against the register.</summary>
    [Fact]
    public async Task TheCatalogueIsTheTenReportsOfAdr006()
    {
        Assert.Equal(
            ["FINANCIAL_PERFORMANCE", "GOVERNANCE_CHANGE", "KPI_PERFORMANCE", "PORTFOLIO_SUMMARY", "PROGRESS_HISTORY", "PROGRESS_REPORTING", "PROJECT_REGISTER", "PROJECT_REPORT",
                "RISK_ISSUE", "SCHEDULE_DELIVERY"],
            (await host.Database.QueryAsync("SELECT code FROM reports.report_definition WHERE lifecycle_state = 'PUBLISHED'")).Order(StringComparer.Ordinal));

        string? refused = null;
        try
        {
            await host.Database.ExecuteAsync($"""
                INSERT INTO reports.report_definition (id, code, version_no, name_ar, name_en, audience_family, primary_projection_code, allows_saved_views, lifecycle_state,
                                                       created_at, created_by, updated_at, updated_by)
                VALUES ('{Guid.NewGuid()}', 'RISK_REGISTER', 1, 'سجل', 'Register', 'RISK_ISSUE', 'RISK.RISK_EXPOSURE', false, 'DRAFT', now(), '{ReportDriver.Person(1)}', now(), '{ReportDriver.Person(1)}')
                """);
        }
        catch (Npgsql.PostgresException exception)
        {
            refused = exception.ConstraintName;
        }

        Assert.Equal("ck_report_definition_code", refused);

        // Every seeded version passes ADM-037's validation against the register in force; each OPTION's catalogue entry is one its report absorbs.
        await using AsyncServiceScope scope = host.Api.Services.CreateAsyncScope();
        IReportDefinitionService definitions = scope.ServiceProvider.GetRequiredService<IReportDefinitionService>();
        ReportFields fields = scope.ServiceProvider.GetRequiredService<ReportFields>();
        foreach (string id in await host.Database.QueryAsync("SELECT id::text FROM reports.report_definition WHERE lifecycle_state = 'PUBLISHED'"))
        {
            ReportDefinitionDetail detail = (await definitions.GetAsync(Guid.Parse(id), CancellationToken.None)).Value!.Value;
            ReportDefinitionContent content = new(
                detail.Name, detail.Description, detail.AudienceFamily, detail.PrimaryProjectionCode, detail.AllowsSavedViews, detail.AudienceRoleCodes, detail.Parameters, detail.Columns);
            Assert.True(ReportDefinitionRules.Check(detail.Code, content, fields).Count == 0, $"{detail.Code}: {string.Join(", ", ReportDefinitionRules.Check(detail.Code, content, fields))}");
            Assert.All(detail.Parameters.SelectMany(p => p.Options).Select(o => o.CatalogueEntryReference).OfType<string>(), r => Assert.Contains(r, detail.CatalogueEntries));
        }

        // A portfolio manager is of every report's audience; the System Administrator of none (BR-RPT-011).
        using HttpClient client = host.Api.CreateClient();
        using HttpResponseMessage portfolio = await client.GetAsync($"{ReportDriver.Reports}?pageSize=50", (await client.SignInOrFailAsync(2)).AccessToken);
        Assert.Equal(10, (await portfolio.ReadObjectAsync())["totalCount"]!.GetValue<int>());
        using HttpResponseMessage administrator = await client.GetAsync(ReportDriver.Reports, (await client.SignInOrFailAsync(1)).AccessToken);
        Assert.Equal(0, (await administrator.ReadObjectAsync())["totalCount"]!.GetValue<int>());
    }

    /// <summary>FG-02 §7.1, BR-RPT-019, -020: a missing value is MISSING with no value, never 0; a stale one keeps its value and its own as-of, labelled STALE.</summary>
    [Fact]
    public async Task MissingIsNeverZeroAndStaleIsNeverCurrent()
    {
        using HttpClient client = host.Api.CreateClient();
        string portfolio = (await client.SignInOrFailAsync(2)).AccessToken;
        Guid projectId = await host.ReportProjectAsync(title: "Stale and missing project");
        DateTimeOffset publishedAt = DateTimeOffset.UtcNow.AddDays(-20);
        Guid closed = await host.ReportingPeriodAsync(projectId, ReportDriver.Today.AddDays(-28), ReportDriver.Today.AddDays(-21), "CLOSED");
        await host.ReportingPeriodAsync(projectId, ReportDriver.Today.AddDays(-14), ReportDriver.Today.AddDays(-3), "OPEN");
        await host.PublishedSnapshotAsync(projectId, closed, "GREEN", 40m, publishedAt);

        JsonObject result = await client.RunOrFailAsync(portfolio, "PROJECT_REPORT", new { parameters = new[] { new { code = "PROJECT", value = projectId.ToString() } } });

        JsonObject published = result.Cell(projectId, "PROGRESS_PUBLISHED_PROGRESS_SNAPSHOT", "OVERALL_HEALTH");
        Assert.Equal(("GREEN", "STALE"), (published["value"]!.GetValue<string>(), published["freshness"]!.GetValue<string>()));
        Assert.Equal(publishedAt.ToUnixTimeMilliseconds(), published["asOf"]!.GetValue<DateTimeOffset>().ToUnixTimeMilliseconds());

        JsonObject live = result.Cell(projectId, "PROGRESS_PROJECT_HEALTH_STATUS", "OVERALL_HEALTH");
        Assert.Equal(("UNKNOWN", "MISSING"), (live["freshness"]!.GetValue<string>(), live["unknownReason"]!.GetValue<string>()));
        Assert.Null(live["value"]);

        // A source row that is there but holds no value for a field: WF-02 computed the health with no actual progress. The health is shown; the
        // percentage is MISSING, never 0.
        Guid partial = await host.ReportProjectAsync(title: "Health without actual progress");
        await host.Database.ExecuteAsync($"""
            BEGIN; SET LOCAL session_replication_role = replica;
            INSERT INTO progress.project_health_status (id, project_id, overall_health, actual_percent, planned_percent, computed_at, health_rule_configuration_version_id,
                                                        created_at, created_by, updated_at, updated_by)
            VALUES ('{Guid.NewGuid()}', '{partial}', 'AMBER', NULL, 30, now() - interval '1 hour', '{Guid.NewGuid()}', now(), '{IdentityDatabase.SeedPrincipalId}', now(), '{IdentityDatabase.SeedPrincipalId}');
            COMMIT;
            """);
        using (HttpResponseMessage explored = await client.PostAsync($"{ReportDriver.Explorer}/run?pageSize=200", portfolio, new
        {
            columns = new[]
            {
                new { sourceEntityCode = "PROJECT", fieldCode = "TITLE" },
                new { sourceEntityCode = "PROGRESS_PROJECT_HEALTH_STATUS", fieldCode = "OVERALL_HEALTH" },
                new { sourceEntityCode = "PROGRESS_PROJECT_HEALTH_STATUS", fieldCode = "ACTUAL_PERCENT" },
                new { sourceEntityCode = "PROGRESS_PROJECT_HEALTH_STATUS", fieldCode = "PLANNED_PERCENT" },
            },
        }))
        {
            JsonObject explorer = await explored.ReadObjectAsync();
            Assert.Equal(("AMBER", "FRESH"), (explorer.Cell(partial, "PROGRESS_PROJECT_HEALTH_STATUS", "OVERALL_HEALTH")["value"]!.GetValue<string>(),
                explorer.Cell(partial, "PROGRESS_PROJECT_HEALTH_STATUS", "OVERALL_HEALTH")["freshness"]!.GetValue<string>()));
            JsonObject actual = explorer.Cell(partial, "PROGRESS_PROJECT_HEALTH_STATUS", "ACTUAL_PERCENT");
            Assert.Equal(("UNKNOWN", "MISSING"), (actual["freshness"]!.GetValue<string>(), actual["unknownReason"]!.GetValue<string>()));
            Assert.Null(actual["value"]);
            Assert.Equal("30.0000", explorer.Cell(partial, "PROGRESS_PROJECT_HEALTH_STATUS", "PLANNED_PERCENT")["value"]!.GetValue<string>());
        }

        // A count the register keeps is the register's answer: no open risk is 0 risks, which is a value, not an absence (FG-01 F-13).
        Assert.Equal("0", result.Cell(projectId, "RISK_RISK_EXPOSURE", "OPEN_RISKS")["value"]!.GetValue<string>());

        JsonObject section = result["sections"]!.AsArray().Single(s => s!["projectionCode"]!.GetValue<string>() == "PROGRESS.PUBLISHED_PROGRESS_SNAPSHOT")!.AsObject();
        Assert.Equal(("PUBLISHED_OFFICIAL", "STALE"), (section["projection"]!["semanticState"]!.GetValue<string>(), section["projection"]!["freshness"]!.GetValue<string>()));

        // In the file: the missing value is MISSING, never empty and never 0, and the stale one is named as such.
        Guid jobId = await client.ExportOrFailAsync(portfolio, $"{ReportDriver.Reports}/PROJECT_REPORT/export", new
        {
            parameters = new[] { new { code = "PROJECT", value = projectId.ToString() } },
            format = "CSV",
            language = "en",
        });
        await host.RunJobsAsync();
        using HttpResponseMessage download = await client.DownloadAsync(portfolio, jobId);
        string csv = await download.Content.ReadAsStringAsync();
        string[] lines = csv.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        string[] header = lines[0].TrimStart('﻿').Split(',');
        string[] row = lines[1].Split(',');
        Assert.Equal("MISSING", row[Array.IndexOf(header, "Current Overall Project Health")]);
        Assert.Equal("GREEN", row[Array.IndexOf(header, "Published Overall Project Health")]);
        Assert.Contains("STALE: Published Overall Project Health", lines[1], StringComparison.Ordinal);
    }

    /// <summary>BR-RPT-007, -010: a parameter narrows the rows and never widens them; a value the caller may not know is refused like one that does not exist.</summary>
    [Fact]
    public async Task ParametersNarrowAndNeverWiden()
    {
        using HttpClient client = host.Api.CreateClient();
        string department = (await client.SignInOrFailAsync(3)).AccessToken;
        Guid ours = await host.ReportProjectAsync(title: "Our department's project");
        Guid suspended = await host.ReportProjectAsync(state: "SUSPENDED", title: "Our suspended project");
        Guid theirs = await host.ReportProjectAsync(departmentId: ReportTestHost.OtherDepartmentId, title: "Another department's project");

        // Offered its own department only; the rows are its department's, whatever is asked.
        using (HttpResponseMessage view = await client.GetAsync($"{ReportDriver.Reports}/PROJECT_REGISTER", department))
        {
            JsonObject parameter = (await view.ReadObjectAsync())["parameters"]!.AsArray().Single(p => p!["code"]!.GetValue<string>() == "DEPARTMENT")!.AsObject();
            Assert.Equal([ReportTestHost.DepartmentId.ToString()], parameter["options"]!.AsArray().Select(o => o!["value"]!.GetValue<string>()));
        }

        JsonObject all = await client.RunOrFailAsync(department, "PROJECT_REGISTER", new { }, "?pageSize=200");
        Assert.Contains(ours, all.ProjectIds());
        Assert.DoesNotContain(theirs, all.ProjectIds());

        JsonObject filtered = await client.RunOrFailAsync(department, "PROJECT_REGISTER", new { parameters = new[] { new { code = "LIFECYCLE_STATE", value = "SUSPENDED" } } }, "?pageSize=200");
        Assert.Contains(suspended, filtered.ProjectIds());
        Assert.DoesNotContain(ours, filtered.ProjectIds());

        foreach ((string code, object body, string error, string[] fields) in new (string, object, string, string[])[]
                 {
                     ("PROJECT_REGISTER", new { parameters = new[] { new { code = "DEPARTMENT", value = ReportTestHost.OtherDepartmentId.ToString() } } },
                         "REPORT_PARAMETER_UNAUTHORIZED", ["parameters[0].value NOT_ALLOWED"]),
                     ("PROJECT_REGISTER", new { parameters = new[] { new { code = "DEPARTMENT", value = Guid.NewGuid().ToString() } } },
                         "REPORT_PARAMETER_UNAUTHORIZED", ["parameters[0].value NOT_ALLOWED"]),
                     ("PROJECT_REPORT", new { parameters = new[] { new { code = "PROJECT", value = theirs.ToString() } } },
                         "REPORT_PARAMETER_UNAUTHORIZED", ["parameters[0].value NOT_ALLOWED"]),
                     ("PROJECT_REPORT", new { }, "REPORT_PARAMETER_REQUIRED", ["parameters.PROJECT REQUIRED"]),
                     ("PROJECT_REGISTER", new { parameters = new[] { new { code = "LIFECYCLE_STATE", value = "DELETED" } } }, "REPORT_PARAMETER_INVALID", ["parameters[0].value VALUE_INVALID"]),
                     ("PROJECT_REGISTER", new { parameters = new[] { new { code = "BUDGET", value = "1" } } }, "REPORT_PARAMETER_INVALID", ["parameters[0].code NOT_FOUND"]),
                     ("PROJECT_REGISTER", new { columns = new[] { new { sourceEntityCode = "FINANCIAL_KPI_FINANCIAL_POSITION", fieldCode = "APPROVED_BUDGET" } } },
                         "REPORT_COLUMN_NOT_SUPPORTED", ["columns[0] FIELD_NOT_IN_REPORT"]),
                     ("PROJECT_REGISTER", new { sort = new[] { new { sourceEntityCode = "PROJECT", fieldCode = "DEPARTMENT", direction = "ASC" } } },
                         "REPORT_SORT_NOT_SUPPORTED", ["sort[0] NOT_ALLOWED"]),
                 })
        {
            using HttpResponseMessage refused = await client.RunAsync(department, code, body);
            (HttpStatusCode status, string? actual, string[] errors) = await refused.RefusalAsync();
            Assert.Equal((HttpStatusCode.UnprocessableEntity, error), (status, actual));
            Assert.Equal(fields, errors);
        }
    }

    /// <summary>
    /// ADR-005 and FG-02 §9.2: PDF, XLSX and CSV, generated from the full authorised rows; text a spreadsheet could run as a formula is never written as
    /// one; an output that reveals a financial amount is classified SENSITIVE (§9.3), whatever the report's baseline.
    /// </summary>
    [Fact]
    public async Task TheThreeFormatsAreGeneratedWithoutAFormulaASpreadsheetCouldRun()
    {
        using HttpClient client = host.Api.CreateClient();
        string portfolio = (await client.SignInOrFailAsync(2)).AccessToken;
        Guid injected = await host.ReportProjectAsync(title: "=HYPERLINK(\"http://evil.example\",\"x\")");
        object register = new
        {
            parameters = new[] { new { code = "LIFECYCLE_STATE", value = "ACTIVE" } },
            columns = new[] { new { sourceEntityCode = "PROJECT", fieldCode = "FORMAL_PROJECT_ID" }, new { sourceEntityCode = "PROJECT", fieldCode = "TITLE" } },
        };

        Dictionary<string, Guid> jobs = [];
        foreach (string format in new[] { "PDF", "XLSX", "CSV" })
        {
            jobs[format] = await client.ExportOrFailAsync(portfolio, $"{ReportDriver.Reports}/PROJECT_REGISTER/export", Merge(register, format));
        }

        await host.RunJobsAsync();
        Dictionary<string, byte[]> files = [];
        foreach ((string format, Guid jobId) in jobs)
        {
            JsonObject job = await client.JobOrFailAsync(portfolio, jobId);
            Assert.Equal(("COMPLETED", "STANDARD"), (job["status"]!.GetValue<string>(), job["output"]!["sensitivity"]!.GetValue<string>()));
            using HttpResponseMessage download = await client.DownloadAsync(portfolio, jobId);
            files[format] = await download.Content.ReadAsByteArrayAsync();
            Assert.EndsWith("." + format.ToLowerInvariant(), download.Content.Headers.ContentDisposition!.FileName!.Trim('"'), StringComparison.Ordinal);
        }

        // CSV: the title quoted with a leading apostrophe; nothing begins with '='.
        string csv = Encoding.UTF8.GetString(files["CSV"]);
        Assert.Contains("\"'=HYPERLINK(\"\"http://evil.example\"\",\"\"x\"\")\"", csv, StringComparison.Ordinal);
        Assert.DoesNotContain(",=HYPERLINK", csv, StringComparison.Ordinal);

        // XLSX: no cell is a formula; the title is an inline string with the quote-prefixed style.
        using (ZipArchive xlsx = new(new MemoryStream(files["XLSX"])))
        {
            string sheet = new StreamReader(xlsx.GetEntry("xl/worksheets/sheet1.xml")!.Open()).ReadToEnd();
            Assert.DoesNotContain("<f>", sheet, StringComparison.Ordinal);
            Assert.Contains("s=\"2\" t=\"inlineStr\"><is><t xml:space=\"preserve\">=HYPERLINK(", sheet, StringComparison.Ordinal);
            Assert.NotNull(xlsx.GetEntry("xl/worksheets/sheet2.xml"));
        }

        // PDF: a complete document with its font embedded.
        string pdf = Encoding.Latin1.GetString(files["PDF"]);
        Assert.StartsWith("%PDF-1.7", pdf, StringComparison.Ordinal);
        Assert.EndsWith("%%EOF\n", pdf, StringComparison.Ordinal);
        Assert.Contains("/FontFile2", pdf, StringComparison.Ordinal);
        Assert.Contains("/ToUnicode", pdf, StringComparison.Ordinal);

        Assert.Contains(await host.Database.QueryAsync($"""
            SELECT a.new_value FROM audit_activity.audit_event e JOIN audit_activity.audit_event_attribute a ON a.audit_event_id = e.id
            WHERE e.subject_id = '{jobs["CSV"]}' AND e.event_type = 'Reports.ExportCompleted' AND a.attribute_name = 'neutralized_cell_count'
            """), value => value != "0");

        // A report revealing financial amounts is SENSITIVE.
        Guid financial = await client.ExportOrFailAsync(portfolio, $"{ReportDriver.Reports}/FINANCIAL_PERFORMANCE/export", new { format = "XLSX", language = "ar" });
        Guid cycle = await host.ReportingPeriodAsync(injected, ReportDriver.Today.AddDays(-14), ReportDriver.Today.AddDays(-7), "CLOSED");
        await host.PublishedFinancialsAsync(injected, cycle, "250000.00", "100000.00", "240000.00");
        await host.RunJobsAsync();
        Assert.Equal("SENSITIVE", (await client.JobOrFailAsync(portfolio, financial))["output"]!["sensitivity"]!.GetValue<string>());
    }

    /// <summary>
    /// FG-02 §10.1, US-RPT-SYS-036 to -038: a repeated key answers with its job; a cancelled job leaves no file; a requester who loses the export
    /// permission before the job runs gets a FAILED job and no file; an output past its expiry is purged and refused, its record kept.
    /// </summary>
    [Fact]
    public async Task AJobMovesOnlyAlongItsLifecycleAndLeavesNoFileWhenItDoesNotComplete()
    {
        using HttpClient client = host.Api.CreateClient();
        string portfolio = (await client.SignInOrFailAsync(2)).AccessToken;
        await host.ReportProjectAsync();
        string path = $"{ReportDriver.Reports}/PROJECT_REGISTER/export";
        object csv = new { format = "CSV", language = "en" };

        // The same key, the same request: the same job, replayed. The same key with another request is refused.
        string key = Guid.NewGuid().ToString();
        Guid first;
        using (HttpResponseMessage once = await client.ExportAsync(portfolio, path, csv, key))
        {
            first = Guid.Parse((await once.ReadObjectAsync())["id"]!.GetValue<string>());
        }

        using (HttpResponseMessage twice = await client.ExportAsync(portfolio, path, csv, key))
        {
            Assert.Equal(HttpStatusCode.Accepted, twice.StatusCode);
            Assert.Equal("true", twice.Headers.GetValues("Idempotent-Replayed").Single());
            Assert.Equal(first.ToString(), (await twice.ReadObjectAsync())["id"]!.GetValue<string>());
        }

        using (HttpResponseMessage reused = await client.ExportAsync(portfolio, path, new { format = "PDF", language = "en" }, key))
        {
            Assert.Equal((HttpStatusCode.UnprocessableEntity, "REPORT_DUPLICATE_REQUEST"), ((await reused.RefusalAsync()).Status, (await reused.RefusalAsync()).Code));
        }

        // Cancelled before it runs: it stays CANCELLED and no file is ever made; it cannot be cancelled twice.
        using (HttpResponseMessage cancel = await client.PostAsync($"{ReportDriver.Jobs}/{first}/cancel", portfolio))
        {
            Assert.Equal("CANCELLED", (await cancel.ReadObjectAsync())["status"]!.GetValue<string>());
        }

        await host.RunJobsAsync();
        Assert.Equal("CANCELLED", (await client.JobOrFailAsync(portfolio, first))["status"]!.GetValue<string>());
        Assert.Equal(["0"], await host.Database.QueryAsync($"SELECT count(*)::text FROM reports.generated_output WHERE report_job_id = '{first}'"));
        using (HttpResponseMessage again = await client.PostAsync($"{ReportDriver.Jobs}/{first}/cancel", portfolio))
        {
            Assert.Equal((HttpStatusCode.Conflict, "REPORT_JOB_NOT_CANCELLABLE"), ((await again.RefusalAsync()).Status, (await again.RefusalAsync()).Code));
        }

        using (HttpResponseMessage none = await client.DownloadAsync(portfolio, first))
        {
            Assert.Equal((HttpStatusCode.Conflict, "REPORT_OUTPUT_NOT_AVAILABLE"), ((await none.RefusalAsync()).Status, (await none.RefusalAsync()).Code));
        }

        // The requester loses the export permission before the job runs: checked again then, it fails and keeps no file.
        Guid withdrawn = await client.ExportOrFailAsync(portfolio, path, csv);
        string profile = IdentityDatabase.ProfileVersionId(2);
        string grantId = Assert.Single(await host.Database.QueryAsync($"""
            SELECT g.id::text FROM identity_access.permission_profile_grant g JOIN identity_access.permission p ON p.id = g.permission_id
            WHERE g.permission_profile_version_id = '{profile}' AND p.code = 'REPORT_EXPORT'
            """));
        string exportId = Assert.Single(await host.Database.QueryAsync("SELECT id::text FROM identity_access.permission WHERE code = 'REPORT_EXPORT'"));
        await host.Database.ExecuteAsync($"""
            BEGIN; SET LOCAL session_replication_role = replica;
            UPDATE identity_access.permission_profile_grant SET permission_id = (
                SELECT p.id FROM identity_access.permission p
                WHERE NOT EXISTS (SELECT 1 FROM identity_access.permission_profile_grant g WHERE g.permission_profile_version_id = '{profile}' AND g.permission_id = p.id)
                ORDER BY p.code LIMIT 1)
            WHERE id = '{grantId}';
            COMMIT;
            """);
        try
        {
            await host.RunJobsAsync();
        }
        finally
        {
            await host.Database.ExecuteAsync($"BEGIN; SET LOCAL session_replication_role = replica; UPDATE identity_access.permission_profile_grant SET permission_id = '{exportId}' WHERE id = '{grantId}'; COMMIT;");
        }

        JsonObject failed = await client.JobOrFailAsync(portfolio, withdrawn);
        Assert.Equal(("FAILED", "REPORT_EXPORT_NOT_PERMITTED"), (failed["status"]!.GetValue<string>(), failed["failureCode"]!.GetValue<string>()));
        Assert.Null(failed["output"]);

        // More rows than an export may hold: FAILED with its code, no file.
        Guid large = await client.ExportOrFailAsync(portfolio, path, csv);
        await host.RunJobsAsync(maxExportRows: 1);
        Assert.Equal("REPORT_EXPORT_SIZE_LIMIT_EXCEEDED", (await client.JobOrFailAsync(portfolio, large))["failureCode"]!.GetValue<string>());

        // An output past its expiry: purged, its job EXPIRED, the download refused, the records kept.
        Guid expiring = await client.ExportOrFailAsync(portfolio, path, csv);
        await host.RunJobsAsync();
        string storageKey = Assert.Single(await host.Database.QueryAsync($"SELECT storage_object_key FROM reports.generated_output WHERE report_job_id = '{expiring}'"));
        host.Identity.Clock.Advance(TimeSpan.FromDays(2));
        try
        {
            await host.RunJobsAsync();
        }
        finally
        {
            host.Identity.Clock.Reset();
        }

        portfolio = (await client.SignInOrFailAsync(2)).AccessToken;
        JsonObject expired = await client.JobOrFailAsync(portfolio, expiring);
        Assert.Equal(("EXPIRED", "PURGED"), (expired["status"]!.GetValue<string>(), expired["output"]!["status"]!.GetValue<string>()));
        Assert.Equal(["0"], await host.Database.QueryAsync($"SELECT count(*)::text FROM reports.report_output_content WHERE storage_object_key = '{storageKey}'"));
        using HttpResponseMessage gone = await client.DownloadAsync(portfolio, expiring);
        Assert.Equal((HttpStatusCode.Conflict, "REPORT_OUTPUT_EXPIRED"), ((await gone.RefusalAsync()).Status, (await gone.RefusalAsync()).Code));
    }

    /// <summary>
    /// BR-RPT-013: view never implies export. A job's rows are the projects its requester reaches under <c>REPORT_EXPORT</c> as well as under each
    /// projection's own permission, so an export scope narrower than the view scope narrows the file, whatever the screen shows.
    /// </summary>
    [Fact]
    public async Task AnExportHoldsOnlyTheProjectsItsRequesterMayExport()
    {
        using HttpClient client = host.Api.CreateClient();
        string portfolio = (await client.SignInOrFailAsync(2)).AccessToken;
        Guid projectId = await host.ReportProjectAsync(title: "Viewable, not exportable");
        Assert.Contains(projectId, (await client.RunOrFailAsync(portfolio, "PROJECT_REGISTER", new { }, "?pageSize=200")).ProjectIds());

        // R02's export grant narrowed from ALL to OWN: it manages no project, so it may export none, though it still views them all.
        static string Rescope(string scope) => $"""
            BEGIN; SET LOCAL session_replication_role = replica;
            UPDATE identity_access.permission_profile_grant SET data_scope = '{scope}'
            WHERE permission_profile_version_id = '{IdentityDatabase.ProfileVersionId(2)}'
              AND permission_id = (SELECT id FROM identity_access.permission WHERE code = 'REPORT_EXPORT');
            COMMIT;
            """;
        await host.Database.ExecuteAsync(Rescope("OWN"));
        try
        {
            Guid jobId = await client.ExportOrFailAsync(portfolio, $"{ReportDriver.Reports}/PROJECT_REGISTER/export", new { format = "CSV", language = "en" });
            await host.RunJobsAsync();
            JsonObject job = await client.JobOrFailAsync(portfolio, jobId);
            Assert.Equal(("COMPLETED", 0), (job["status"]!.GetValue<string>(), job["output"]!["rowCount"]!.GetValue<int>()));
            using HttpResponseMessage download = await client.DownloadAsync(portfolio, jobId);
            Assert.DoesNotContain("Viewable, not exportable", await download.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        }
        finally
        {
            await host.Database.ExecuteAsync(Rescope("ALL"));
        }
    }

    /// <summary>BR-RPT-029: a historical report's rows are the source's own snapshots, oldest first, each with its own as-of; never rebuilt from current data.</summary>
    [Fact]
    public async Task AHistoricalReportIsTheSourcesOwnSnapshots()
    {
        using HttpClient client = host.Api.CreateClient();
        string portfolio = (await client.SignInOrFailAsync(2)).AccessToken;
        Guid projectId = await host.ReportProjectAsync(title: "Historical project");
        Guid earlier = await host.ReportingPeriodAsync(projectId, ReportDriver.Today.AddDays(-28), ReportDriver.Today.AddDays(-21), "CLOSED");
        Guid later = await host.ReportingPeriodAsync(projectId, ReportDriver.Today.AddDays(-14), ReportDriver.Today.AddDays(-7), "CLOSED");
        await host.PublishedSnapshotAsync(projectId, earlier, "RED", 10m, DateTimeOffset.UtcNow.AddDays(-20));
        await host.PublishedSnapshotAsync(projectId, later, "AMBER", 25m, DateTimeOffset.UtcNow.AddDays(-6));

        JsonObject result = await client.RunOrFailAsync(portfolio, "PROGRESS_HISTORY", new { parameters = new[] { new { code = "PROJECT", value = projectId.ToString() } } });

        JsonArray rows = result["items"]!.AsArray();
        Assert.Equal([0, 1], rows.Select(r => r!["snapshotIndex"]!.GetValue<int>()));
        int health = result["columns"]!.AsArray().Select((c, i) => (c, i)).Single(x => x.c!["fieldCode"]!.GetValue<string>() == "OVERALL_HEALTH").i;
        Assert.Equal(["RED", "AMBER"], rows.Select(r => r!["cells"]!.AsArray()[health]!["value"]!.GetValue<string>()));
        Assert.Equal("HISTORICAL_SNAPSHOT", Assert.Single(result["sections"]!.AsArray())!["projection"]!["semanticState"]!.GetValue<string>());
    }

    /// <summary>FG-02 §8.2: a filter matches only values the caller is shown, so it cannot be used to learn a restricted one; restricted cells are never values.</summary>
    [Fact]
    public async Task AFilterMatchesOnlyWhatTheCallerIsShown()
    {
        using HttpClient client = host.Api.CreateClient();
        string department = (await client.SignInOrFailAsync(3)).AccessToken;
        Guid projectId = await host.ReportProjectAsync(title: "Restricted risk project");
        await host.Database.ExecuteAsync($"""
            BEGIN; SET LOCAL session_replication_role = replica;
            INSERT INTO risk.risk (id, project_id, title, title_lang, description, description_lang, risk_category_item_id, status, identified_date, reopened_count,
                                   created_at, created_by, updated_at, updated_by)
            VALUES ('{Guid.NewGuid()}', '{projectId}', 'A risk', 'en', 'A risk.', 'en', '{Guid.NewGuid()}', 'IDENTIFIED', '{ReportDriver.Iso(ReportDriver.Today.AddDays(-5))}', 0,
                    now(), '{IdentityDatabase.SeedPrincipalId}', now(), '{IdentityDatabase.SeedPrincipalId}');
            COMMIT;
            """);
        object risks = new { columns = new[] { new { sourceEntityCode = "PROJECT", fieldCode = "TITLE" }, new { sourceEntityCode = "RISK_RISK_EXPOSURE", fieldCode = "OPEN_RISKS" } } };

        // R03 views projects in its department but not risks: the count is RESTRICTED, not 1 and not 0.
        using (HttpResponseMessage shown = await client.PostAsync($"{ReportDriver.Explorer}/run?pageSize=200", department, risks))
        {
            JsonObject result = await shown.ReadObjectAsync();
            JsonObject cell = result.Cell(projectId, "RISK_RISK_EXPOSURE", "OPEN_RISKS");
            Assert.Equal("RESTRICTED", cell["unknownReason"]!.GetValue<string>());
            Assert.Null(cell["value"]);
        }

        // Filtering on it yields nothing, whatever the bound: a restricted value matches no filter.
        foreach (string op in new[] { "GTE", "LT" })
        {
            using HttpResponseMessage filtered = await client.PostAsync($"{ReportDriver.Explorer}/run?pageSize=200", department, new
            {
                columns = new[] { new { sourceEntityCode = "PROJECT", fieldCode = "TITLE" } },
                filters = new[] { new { sourceEntityCode = "RISK_RISK_EXPOSURE", fieldCode = "OPEN_RISKS", @operator = op, value = "1" } },
            });
            Assert.DoesNotContain(projectId, (await filtered.ReadObjectAsync()).ProjectIds());
        }
    }

    /// <summary>RPT-EVT-003: a run that reveals financial amounts is audited; one that does not is not (FG-02 §24.1).</summary>
    [Fact]
    public async Task ARunThatRevealsFinancialAmountsIsAudited()
    {
        using HttpClient client = host.Api.CreateClient();
        string portfolio = (await client.SignInOrFailAsync(2)).AccessToken;
        Guid projectId = await host.ReportProjectAsync();
        Guid cycle = await host.ReportingPeriodAsync(projectId, ReportDriver.Today.AddDays(-14), ReportDriver.Today.AddDays(-7), "CLOSED");
        await host.PublishedFinancialsAsync(projectId, cycle, "500000.00", "200000.00", "480000.00");
        static int Before(IReadOnlyList<string> counts) => int.Parse(counts[0], System.Globalization.CultureInfo.InvariantCulture);
        string count = $"SELECT count(*)::text FROM audit_activity.audit_event WHERE event_type = 'Reports.SensitiveReportExecuted' AND actor_user_id = '{ReportDriver.Person(2)}'";
        int before = Before(await host.Database.QueryAsync(count));

        await client.RunOrFailAsync(portfolio, "PROJECT_REGISTER", new { });
        Assert.Equal(before, Before(await host.Database.QueryAsync(count)));

        JsonObject result = await client.RunOrFailAsync(portfolio, "FINANCIAL_PERFORMANCE", new { }, "?pageSize=200");
        Assert.Equal("500000.00", result.Cell(projectId, "FINANCIAL_KPI_PUBLISHED_FINANCIAL_SNAPSHOT", "APPROVED_BUDGET")["value"]!.GetValue<string>());
        Assert.Equal(before + 1, Before(await host.Database.QueryAsync(count)));
    }

    private static Dictionary<string, object?> Merge(object body, string format)
    {
        Dictionary<string, object?> merged = JsonSerializerShape(body);
        merged["format"] = format;
        merged["language"] = "en";
        return merged;
    }

    private static Dictionary<string, object?> JsonSerializerShape(object body) =>
        System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object?>>(System.Text.Json.JsonSerializer.Serialize(body, SessionApi.Json), SessionApi.Json)!;
}
