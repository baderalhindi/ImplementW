using System.Text.Json.Nodes;
using PMPlatform.Tests.Integration.Identity;
using PMPlatform.Tests.Integration.Project;
using static PMPlatform.Tests.Integration.ExternalParticipation.ExternalParticipationDriver;

namespace PMPlatform.Tests.Integration.ExternalParticipation;

/// <summary>
/// Review round 7, item 1: the entity's monthly update of a project with a stage plan asks for the current stage and its % complete, with each stage's
/// planned % beside it, and accepting it weighs the overall figure from the plan.
/// </summary>
[Collection(MonthlyStageUpdateSuite.Name)]
public sealed class MonthlyStageUpdateTests(ExternalParticipationTestHost host)
{
    [Fact]
    public async Task AStageAnswerIsWeighedFromThePlanWhenAccepted()
    {
        await StageFixture.PublishAsync(host.Database);
        using HttpClient client = host.Api.CreateClient();
        ParticipationSessions sessions = await client.SignInAsync();
        Guid projectId = await host.ProjectAsync();
        await host.GrantProjectAsync(8, projectId);
        await host.Database.ExecuteAsync($"""
            INSERT INTO project.project_stage_plan (id, project_id, stage_code, applies, planned_start_date, planned_end_date, created_at, created_by, updated_at, updated_by)
            SELECT gen_random_uuid(), '{projectId}', s.code, true, s.starts, s.ends, now(), '{IdentityDatabase.SeedPrincipalId}', now(), '{IdentityDatabase.SeedPrincipalId}'
            FROM (VALUES ('BUSINESS_CASE', current_date - 400, current_date - 300), ('DESIGN', current_date - 300, current_date - 200),
                         ('TENDERING', current_date - 200, current_date - 100), ('EXECUTION', current_date - 100, current_date + 100),
                         ('TESTING', current_date + 100, current_date + 150), ('HANDOVER', current_date + 150, current_date + 200)) AS s (code, starts, ends);
            UPDATE project.project SET stage_plan_revision_no = 1 WHERE id = '{projectId}';
            """);

        await client.CreatedOrFailAsync(sessions.DepartmentManager, "/api/v1/update-request-batches", new
        {
            items = new[] { new { projectId, responsibleUserId = (Guid?)Person(8) } },
            dueDate = Iso(Today.AddDays(5)),
            repeatMonthly = false,
            repeatDay = (int?)null,
            instructions = "Update November",
            reviewerUserId = Person(3),
        });
        JsonNode request = (await client.GetOrFailAsync(sessions.EntityA, "/api/v1/my-update-requests"))["items"]!.AsArray()
            .First(i => Guid.Parse(i!.Text("projectId")) == projectId)!;
        JsonArray stages = request["stages"]!.AsArray();
        Assert.Equal(StageFixture.Stages.Select(s => s.Code), stages.Select(s => s!.Text("stageCode")));
        Assert.Equal(50m, stages[3]!["plannedPercent"]!.GetValue<decimal>());

        Guid contribution = AdministrationApi.IdOf(await client.CreatedOrFailAsync(sessions.EntityA, Contributions, new
        {
            externalUpdateRequestId = Guid.Parse(request.Text("requestId")),
            fields = new object[] { new { fieldCode = "currentStageCode", value = "EXECUTION" }, new { fieldCode = "currentStagePercent", value = "40" } },
        }));
        await client.OkOrFailAsync(sessions.EntityA, $"{Contributions}/{contribution}/submit");
        await client.DecideAsync(sessions.DepartmentManager, contribution, "accept");

        JsonObject project = JsonNode.Parse(Assert.Single(await host.Database.QueryAsync(
            $"SELECT to_jsonb(p)::text FROM project.project p WHERE p.id = '{projectId}'")))!.AsObject();
        Assert.Equal(52m, project["declared_actual_percent"]!.GetValue<decimal>());
        Assert.Equal(57.5m, project["declared_planned_percent"]!.GetValue<decimal>());
        Assert.Equal(("EXECUTION", 40m), (project["current_stage_code"]!.GetValue<string>(), project["current_stage_percent"]!.GetValue<decimal>()));
    }
}

/// <summary>A database of its own: with stages published, every registration there needs a stage plan.</summary>
[CollectionDefinition(Name)]
public sealed class MonthlyStageUpdateSuite : ICollectionFixture<ExternalParticipationTestHost>
{
    public const string Name = "MonthlyStageUpdate";
}
