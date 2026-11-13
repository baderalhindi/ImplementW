using System.Net;
using System.Text.Json.Nodes;
using PMPlatform.Tests.Integration.Identity;
using PMPlatform.Tests.Integration.Project;

namespace PMPlatform.Tests.Integration.Progress;

/// <summary>
/// Review round 7, item 1: AHDA changing the weights publishes a new PROJECT_STAGE version. A published period keeps the version of its day and its
/// figures; the project moves to the new version from its next period, which says which version it used.
/// </summary>
[Collection(StageVersionChangeSuite.Name)]
public sealed class StageVersionChangeTests(ProgressTestHost host)
{
    private static readonly Guid SecondVersionId = new("00000000-0712-4000-8000-000000000002");

    [Fact]
    public async Task NewWeightsNeverRecalculateAPublishedPeriodAndApplyFromTheNext()
    {
        await StageFixture.PublishAsync(host.Database);
        using HttpClient client = host.Api.CreateClient();
        ProgressSessions sessions = await client.SignInAsync();
        Guid projectId = await host.ProjectAsync(activatedDaysAgo: 20);
        await host.Database.ExecuteAsync($"""
            INSERT INTO project.project_stage_plan (id, project_id, stage_code, applies, planned_start_date, planned_end_date, created_at, created_by, updated_at, updated_by)
            SELECT gen_random_uuid(), '{projectId}', s.code, true, current_date - 100, current_date + 100, now(), '{IdentityDatabase.SeedPrincipalId}', now(), '{IdentityDatabase.SeedPrincipalId}'
            FROM unnest(ARRAY['BUSINESS_CASE', 'DESIGN', 'TENDERING', 'EXECUTION', 'TESTING', 'HANDOVER']) AS s (code);
            UPDATE project.project SET stage_plan_revision_no = 1 WHERE id = '{projectId}';
            """);
        DateOnly started = ProgressDriver.Today.AddDays(-50);
        object entries = new
        {
            narrative = (object?)null,
            @override = (object?)null,
            stages = StageFixture.Stages.Select((s, i) => new
            {
                stageCode = s.Code,
                percentComplete = i < 3 ? 100m : i == 3 ? 40m : 0m,
                actualStartDate = i < 4 ? started : (DateOnly?)null,
                actualFinishDate = i < 3 ? started.AddDays(10) : (DateOnly?)null,
            }).ToArray(),
        };
        JsonObject first = await client.PublishedPeriodAsync(sessions.ProjectManager, sessions.Reviewer, projectId, entries);
        Assert.Equal(52m, first.Percent("actualPercent"));

        // AHDA moves weight from execution to design: 5/25/10/45/10/5.
        await host.Database.ExecuteAsync($"""
            INSERT INTO master_data_config.configuration_version (id, configuration_family_id, version_no, lifecycle_state, created_at, created_by, updated_at, updated_by)
            SELECT '{SecondVersionId}', f.id, 1 + (SELECT count(*) FROM master_data_config.configuration_version v WHERE v.configuration_family_id = f.id), 'DRAFT', now(), '{IdentityDatabase.SeedPrincipalId}', now(), '{IdentityDatabase.SeedPrincipalId}'
            FROM master_data_config.configuration_family f WHERE f.code = 'PROJECT_STAGE';
            INSERT INTO master_data_config.project_stage_definition (id, configuration_version_id, code, sort_order, weight_percent, is_active, name_ar, name_en, created_at, created_by, updated_at, updated_by)
            SELECT gen_random_uuid(), '{SecondVersionId}', d.code, d.sort_order, CASE d.code WHEN 'DESIGN' THEN 25 WHEN 'EXECUTION' THEN 45 ELSE d.weight_percent END, true, d.name_ar, d.name_en,
                   now(), '{IdentityDatabase.SeedPrincipalId}', now(), '{IdentityDatabase.SeedPrincipalId}'
            FROM master_data_config.project_stage_definition d WHERE d.configuration_version_id = '{StageFixture.VersionId}';
            UPDATE master_data_config.configuration_version SET lifecycle_state = 'PUBLISHED', published_at = now(), effective_from = now() WHERE id = '{SecondVersionId}';
            """);

        using (HttpResponseMessage again = await client.GetAsync($"{ProgressDriver.Submissions}/{first["id"]}", sessions.ProjectManager))
        {
            JsonObject reread = await again.ReadObjectAsync();
            Assert.Equal((52m, StageFixture.VersionId.ToString(), 15m), (
                reread.Percent("actualPercent"), reread["stageConfigurationVersionId"]!.GetValue<string>(), reread["stages"]![1]!.Percent("weightPercent")));
        }

        Assert.Equal(52m, Assert.Single(await client.ItemsAsync(sessions.ProjectManager, ProgressDriver.Snapshots, projectId))!.Percent("actualPercent"));

        JsonObject next = await client.StartOrFailAsync(sessions.ProjectManager, projectId);
        Assert.Equal((first["stageVersionNo"]!.GetValue<int>() + 1, 25m), (next["stageVersionNo"]!.GetValue<int>(), next["stages"]![1]!.Percent("weightPercent")));
        Assert.Equal(HttpStatusCode.OK, (await client.EditAsync(sessions.ProjectManager, AdministrationApi.IdOf(next), entries)).StatusCode);
        Assert.Equal(58m, (await client.CommandOrFailAsync(sessions.ProjectManager, AdministrationApi.IdOf(next), "submit")).Percent("actualPercent"));
    }
}

/// <summary>A database of its own: the test publishes a second stage version, which every later period there would weigh by.</summary>
[CollectionDefinition(Name)]
public sealed class StageVersionChangeSuite : ICollectionFixture<ProgressTestHost>
{
    public const string Name = "StageVersionChange";
}
