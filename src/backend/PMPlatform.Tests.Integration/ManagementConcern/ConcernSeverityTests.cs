using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using PMPlatform.Tests.Integration.Identity;
using PMPlatform.Tests.Integration.MasterDataConfig;
using PMPlatform.Tests.Integration.Risk;

namespace PMPlatform.Tests.Integration.ManagementConcern;

/// <summary>
/// TASK-057's first acceptance criterion: severity is computed server-side from documented rules (<c>ConcernSeverity</c>;
/// management-concern.md D-4), never accepted as raw client input — and it is pinned to the version whose rule computed it.
/// </summary>
[Collection(ConcernSuite.Name)]
public sealed class ConcernSeverityTests(ConcernTestHost host)
{
    /// <summary>
    /// The workbook's check: submit a client-supplied severity and confirm the server ignores it and stores the computed one. The
    /// client sends CRITICAL in every shape a representation has — the item, a code, the overall level, a version — with impacts whose
    /// highest level is 2; the server stores MINOR, level 2, under the scale in force, in the API and in the database. A PUT carrying a
    /// severity changes nothing either. Only a reassessment moves it, by computing it again.
    /// </summary>
    [Fact]
    public async Task AClientSuppliedSeverityIsIgnoredAndTheComputedOneStored()
    {
        using HttpClient client = host.Api.CreateClient();
        ConcernSessions sessions = await client.SignInAsync();
        Guid[] dimensions = await host.DimensionsAsync();
        Guid projectId = await host.ProjectAsync(projectManager: 8);
        string scale = await ScaleInForceAsync();

        JsonObject raised = await client.CreatedOrFailAsync(
            sessions.EntityManager, ConcernDriver.Concerns, WithSuppliedSeverity(ConcernDriver.ConcernBody(projectId, ConcernDriver.Impacts(dimensions, 1, 2, 1))));
        Guid concernId = AdministrationApi.IdOf(raised);
        Assert.Equal((ConcernTestHost.MinorId.ToString(), 2, scale), Severity(raised));
        Assert.Equal([$"{ConcernTestHost.MinorId}|2|{scale}"], await StoredSeverityAsync(concernId));

        using (HttpResponseMessage read = await client.GetAsync($"{ConcernDriver.Concerns}/{concernId}", sessions.Officer))
        {
            JsonObject edit = WithSuppliedSeverity(new
            {
                title = new { text = "Site access blocked", language = "en" },
                description = new { text = "The access road is closed.", language = "en" },
                categoryItemId = ConcernTestHost.CategoryId,
                priorityItemId = ConcernTestHost.LowPriorityId,
            });
            using HttpResponseMessage put = await client.PutAsync($"{ConcernDriver.Concerns}/{concernId}", sessions.Officer, edit, AdministrationApi.ETagOf(read));
            Assert.Equal(HttpStatusCode.OK, put.StatusCode);
            JsonObject edited = await put.ReadObjectAsync();
            Assert.Equal((ConcernTestHost.MinorId.ToString(), 2, scale), Severity(edited));

            // Priority is people's and moves alone: the severity did not set it, and it does not set the severity (BR-ISS-011).
            Assert.Equal(ConcernTestHost.LowPriorityId.ToString(), edited.Text("priorityItemId"));
        }

        JsonObject reassessed = await client.CommandOrFailAsync(sessions.Officer, concernId, "assess", new { impacts = ConcernDriver.Impacts(dimensions, 5, 1) });
        Assert.Equal((ConcernTestHost.CriticalId.ToString(), 5, scale), Severity(reassessed));
        Assert.Equal(ConcernTestHost.LowPriorityId.ToString(), reassessed.Text("priorityItemId"));
        Assert.Equal([$"{ConcernTestHost.CriticalId}|5|{scale}"], await StoredSeverityAsync(concernId));
        Assert.Equal(2, reassessed["impacts"]!.AsArray().Count);
    }

    /// <summary>
    /// A severity is pinned to the scale whose rule computed it: publishing a version that maps level 4 to CRITICAL leaves a concern
    /// recorded MAJOR under the earlier version as it was; a reassessment of the same impacts is computed by, and pins, the new one.
    /// </summary>
    [Fact]
    public async Task ARepublishedScaleChangesNoRecordedSeverity()
    {
        using HttpClient client = host.Api.CreateClient();
        ConcernSessions sessions = await client.SignInAsync();
        Crew crew = await client.SignInCrewAsync();
        Guid[] dimensions = await host.DimensionsAsync();
        Guid concernId = await client.RaiseAsync(sessions.InternalManager, await host.ProjectAsync(), ConcernDriver.Impacts(dimensions, 4));
        string before = await ScaleInForceAsync();

        Guid stricter = await host.PublishScaleAsync(client, crew, new Dictionary<short, string>(ConcernDriver.DefaultMapping) { [4] = "TEST_CRITICAL" });
        try
        {
            Assert.Equal((ConcernTestHost.MajorId.ToString(), 4, before), Severity(await client.ConcernAsync(sessions.Officer, concernId)));
            Assert.Equal([$"{ConcernTestHost.MajorId}|4|{before}"], await StoredSeverityAsync(concernId));

            JsonObject reassessed = await client.CommandOrFailAsync(sessions.Officer, concernId, "assess", new { impacts = ConcernDriver.Impacts(dimensions, 4) });
            Assert.Equal((ConcernTestHost.CriticalId.ToString(), 4, stricter.ToString()), Severity(reassessed));
        }
        finally
        {
            // The collection's other tests expect the host's mapping in force.
            await host.PublishScaleAsync(client, crew, ConcernDriver.DefaultMapping);
        }
    }

    /// <summary>Impacts that miss the scale are refused with every miss named, and nothing is recorded; a concern raised without impacts has no severity yet.</summary>
    [Fact]
    public async Task ImpactsOffTheScaleAreRefusedAndAnUnassessedConcernHasNoSeverity()
    {
        using HttpClient client = host.Api.CreateClient();
        ConcernSessions sessions = await client.SignInAsync();
        Guid[] dimensions = await host.DimensionsAsync();
        Guid projectId = await host.ProjectAsync();
        object[] offTheScale =
        [
            new { impactDimensionItemId = dimensions[0], impactLevel = 2 },
            new { impactDimensionItemId = dimensions[0], impactLevel = 3 },
            new { impactDimensionItemId = ConcernTestHost.CategoryId, impactLevel = 1 },
        ];

        using (HttpResponseMessage refused = await client.PostAsync(ConcernDriver.Concerns, sessions.InternalManager, ConcernDriver.ConcernBody(projectId, offTheScale)))
        {
            Assert.Equal(HttpStatusCode.UnprocessableEntity, refused.StatusCode);
            JsonObject problem = await refused.ReadObjectAsync();
            Assert.Equal("CONCERN_IMPACT_INVALID", problem.Text("code"));
            Assert.Equal(["impacts[1].impactDimensionItemId DUPLICATE", "impacts[2].impactDimensionItemId NOT_ALLOWED"], problem["errors"]!.AsArray().Select(e => $"{e!["field"]} {e["code"]}"));
        }

        Assert.Equal(["0"], await host.Database.QueryAsync($"SELECT count(*)::text FROM management_concern.management_concern WHERE project_id = '{projectId}'"));

        JsonObject unassessed = await client.CreatedOrFailAsync(sessions.InternalManager, ConcernDriver.Concerns, ConcernDriver.ConcernBody(projectId));
        Assert.Null(unassessed["severityItemId"]);
        Assert.Null(unassessed["overallImpactLevel"]);
        Assert.Empty(unassessed["impacts"]!.AsArray());
    }

    /// <summary>A body as a client that tries to set the severity sends it: every severity-shaped field, all saying CRITICAL at level 5.</summary>
    private static JsonObject WithSuppliedSeverity(object body)
    {
        JsonObject json = JsonSerializer.SerializeToNode(body, SessionApi.Json)!.AsObject();
        json["severityItemId"] = ConcernTestHost.CriticalId.ToString();
        json["severity"] = "TEST_CRITICAL";
        json["overallImpactLevel"] = 5;
        json["severityConfigurationVersionId"] = Guid.NewGuid().ToString();
        return json;
    }

    private static (string? SeverityItemId, int? OverallImpactLevel, string? VersionId) Severity(JsonObject concern) =>
        (concern["severityItemId"]?.GetValue<string>(), concern["overallImpactLevel"]?.GetValue<int>(), concern["severityConfigurationVersionId"]?.GetValue<string>());

    private Task<IReadOnlyList<string>> StoredSeverityAsync(Guid concernId) =>
        host.Database.QueryAsync($"SELECT severity_item_id || '|' || overall_impact_level || '|' || severity_configuration_version_id FROM management_concern.management_concern WHERE id = '{concernId}'");

    /// <summary>The RISK_MATRIX version in force now: the newest published, as resolution chooses it.</summary>
    private async Task<string> ScaleInForceAsync() =>
        Assert.Single(await host.Database.QueryAsync("""
            SELECT v.id::text FROM master_data_config.configuration_version v JOIN master_data_config.configuration_family f ON f.id = v.configuration_family_id
            WHERE f.code = 'RISK_MATRIX' AND v.lifecycle_state = 'PUBLISHED' AND v.effective_from <= now() ORDER BY v.effective_from DESC LIMIT 1
            """));
}
