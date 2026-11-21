using System.Net;
using System.Text.Json.Nodes;
using PMPlatform.Application.Features.Approval.Contracts;
using PMPlatform.Tests.Integration.Identity;
using PMPlatform.Tests.Integration.MasterDataConfig;

namespace PMPlatform.Tests.Integration.Project;

/// <summary>
/// Review round 7, item 1: the stage plan of a registration. It is pre-filled from the PROJECT_STAGE version in force, required to submit, reviewed
/// with the registration, and from approval on changed only with an approved schedule change's authorisation, except once on a project that has none.
/// </summary>
[Collection(ProjectStagePlanSuite.Name)]
public sealed class ProjectStagePlanTests(ProjectTestHost host) : IAsyncLifetime
{
    private const string StagePlans = "/api/v1/project-stage-plans";

    public Task InitializeAsync() => StageFixture.PublishAsync(host.Database);

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task ThePlanIsPrefilledRequiredToSubmitReviewedWithTheRegistrationAndLockedOnceApproved()
    {
        using HttpClient client = host.Api.CreateClient();
        Sessions sessions = await client.SignInAsync();
        JsonObject draft = await client.CreateOrFailAsync(sessions.Entity, host.Registration());
        Guid projectId = AdministrationApi.IdOf(draft);
        DateOnly start = DateOnly.Parse(draft["plannedStartDate"]!.GetValue<string>(), System.Globalization.CultureInfo.InvariantCulture);

        (JsonObject plan, string tag) = await GetPlanAsync(client, sessions.Entity, projectId);
        Assert.Equal(0, plan["revisionNo"]!.GetValue<int>());
        Assert.False(plan["requiresChangeAuthorization"]!.GetValue<bool>());
        Assert.False(plan["acceptsStartingPercent"]!.GetValue<bool>());
        Assert.Equal(
            StageFixture.Stages.Select(s => (s.Code, s.Weight, true, (string?)null)),
            plan["stages"]!.AsArray().Select(s => (s!["stageCode"]!.GetValue<string>(), s["weightPercent"]!.GetValue<decimal>(), s["applies"]!.GetValue<bool>(),
                s["plannedStartDate"]?.GetValue<string>())));

        using (HttpResponseMessage unplanned = await client.PostAsync($"{ProjectDriver.Projects}/{projectId}/submit", sessions.Entity, new { projectManagerUserId = ProjectDriver.Person(8) }))
        {
            Assert.Equal((HttpStatusCode.UnprocessableEntity, "PROJECT_INCOMPLETE"), (unplanned.StatusCode, await unplanned.CodeOfAsync()));
            Assert.Equal(["stagePlan REQUIRED"], await unplanned.ReadFieldErrorsAsync());
        }

        // The last stage ends a day after the project; a new registration has no starting point.
        using (HttpResponseMessage outside = await client.PutAsync(
                   $"{StagePlans}/{projectId}", sessions.Entity, StageFixture.Plan(start, days: 62, startingPercent: 10m), tag))
        {
            Assert.Equal((HttpStatusCode.UnprocessableEntity, "PROJECT_STAGE_PLAN_INVALID"), (outside.StatusCode, await outside.CodeOfAsync()));
            string[] errors = await outside.ReadFieldErrorsAsync();
            Assert.Contains("stages[5].plannedEndDate OUTSIDE_RANGE", errors);
            Assert.Contains("stages[0].startingPercent NOT_ALLOWED", errors);
        }

        using (HttpResponseMessage saved = await client.PutAsync($"{StagePlans}/{projectId}", sessions.Entity, StageFixture.Plan(start, skipped: "DESIGN"), tag))
        {
            Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
            JsonObject body = await saved.ReadObjectAsync();
            Assert.Equal(1, body["revisionNo"]!.GetValue<int>());
            Assert.False(body["stages"]![1]!["applies"]!.GetValue<bool>());
        }

        await client.CommandOrFailAsync(sessions.Entity, projectId, "submit", new { projectManagerUserId = ProjectDriver.Person(8) });
        (_, tag) = await GetPlanAsync(client, sessions.Entity, projectId);
        using (HttpResponseMessage underReview = await client.PutAsync($"{StagePlans}/{projectId}", sessions.Entity, StageFixture.Plan(start), tag))
        {
            Assert.Equal((HttpStatusCode.Conflict, "PROJECT_STAGE_PLAN_UNDER_REVIEW"), (underReview.StatusCode, await underReview.CodeOfAsync()));
        }

        await client.CommandOrFailAsync(sessions.Reviewer, projectId, "start-review");
        await host.DecideAndDeliverAsync(projectId, ApprovalTaskDecision.Approve);

        (plan, tag) = await GetPlanAsync(client, sessions.Entity, projectId);
        Assert.True(plan["requiresChangeAuthorization"]!.GetValue<bool>());
        using (HttpResponseMessage unauthorised = await client.PutAsync($"{StagePlans}/{projectId}", sessions.Entity, StageFixture.Plan(start), tag))
        {
            Assert.Equal((HttpStatusCode.UnprocessableEntity, "PROJECT_STAGE_PLAN_CHANGE_REQUIRED"), (unauthorised.StatusCode, await unauthorised.CodeOfAsync()));
            Assert.Equal(["changeAuthorizationId REQUIRED"], await unauthorised.ReadFieldErrorsAsync());
        }

        using (HttpResponseMessage unknown = await client.PutAsync(
                   $"{StagePlans}/{projectId}", sessions.Entity, StageFixture.Plan(start, changeAuthorizationId: Guid.NewGuid()), tag))
        {
            Assert.Equal((HttpStatusCode.UnprocessableEntity, "PROJECT_STAGE_PLAN_CHANGE_REQUIRED"), (unknown.StatusCode, await unknown.CodeOfAsync()));
            Assert.Equal(["changeAuthorizationId NOT_ALLOWED"], await unknown.ReadFieldErrorsAsync());
        }

        // The same plan again changes nothing, so it needs no authorisation.
        using (HttpResponseMessage same = await client.PutAsync($"{StagePlans}/{projectId}", sessions.Entity, StageFixture.Plan(start, skipped: "DESIGN"), tag))
        {
            Assert.Equal(HttpStatusCode.OK, same.StatusCode);
            Assert.Equal(1, (await same.ReadObjectAsync())["revisionNo"]!.GetValue<int>());
        }

        Assert.Single(await host.Database.QueryAsync(
            $"SELECT id::text FROM audit_activity.audit_event WHERE event_type = 'Project.StagePlanChanged' AND subject_id = '{projectId}'"));
    }

    /// <summary>A project approved before stages existed, or imported, sets its plan once without a change request; the next change needs one.</summary>
    [Fact]
    public async Task AnApprovedProjectWithoutAPlanSetsItOnceAndAnInFlightOneMayGiveStartingPoints()
    {
        using HttpClient client = host.Api.CreateClient();
        Sessions sessions = await client.SignInAsync();
        JsonObject draft = await client.CreateOrFailAsync(sessions.Entity, host.Registration());
        Guid projectId = AdministrationApi.IdOf(draft);
        DateOnly start = DateOnly.Parse(draft["plannedStartDate"]!.GetValue<string>(), System.Globalization.CultureInfo.InvariantCulture);
        string tag = (await GetPlanAsync(client, sessions.Entity, projectId)).Tag;
        using (HttpResponseMessage planned = await client.PutAsync($"{StagePlans}/{projectId}", sessions.Entity, StageFixture.Plan(start), tag))
        {
            Assert.Equal(HttpStatusCode.OK, planned.StatusCode);
        }

        await client.CommandOrFailAsync(sessions.Entity, projectId, "submit", new { projectManagerUserId = ProjectDriver.Person(8) });
        await client.CommandOrFailAsync(sessions.Reviewer, projectId, "start-review");
        await host.DecideAndDeliverAsync(projectId, ApprovalTaskDecision.Approve);

        // As a project registered before round 7 stands: approved, imported, with no plan.
        await host.Database.ExecuteAsync($"""
            DELETE FROM project.project_stage_plan WHERE project_id = '{projectId}';
            UPDATE project.project SET stage_plan_revision_no = 0, legacy_intake_date = current_date WHERE id = '{projectId}';
            """);

        (JsonObject plan, tag) = await GetPlanAsync(client, sessions.Entity, projectId);
        Assert.False(plan["requiresChangeAuthorization"]!.GetValue<bool>());
        Assert.True(plan["acceptsStartingPercent"]!.GetValue<bool>());
        using (HttpResponseMessage once = await client.PutAsync(
                   $"{StagePlans}/{projectId}", sessions.Entity, StageFixture.Plan(start, startingPercent: 25m), tag))
        {
            Assert.Equal(HttpStatusCode.OK, once.StatusCode);
            JsonObject body = await once.ReadObjectAsync();
            Assert.Equal((1, 25m), (body["revisionNo"]!.GetValue<int>(), body["stages"]![0]!["startingPercent"]!.GetValue<decimal>()));
            Assert.True(body["requiresChangeAuthorization"]!.GetValue<bool>());
            tag = AdministrationApi.ETagOf(once);
        }

        using HttpResponseMessage again = await client.PutAsync($"{StagePlans}/{projectId}", sessions.Entity, StageFixture.Plan(start, days: 20), tag);
        Assert.Equal((HttpStatusCode.UnprocessableEntity, "PROJECT_STAGE_PLAN_CHANGE_REQUIRED"), (again.StatusCode, await again.CodeOfAsync()));
    }

    private static async Task<(JsonObject Plan, string Tag)> GetPlanAsync(HttpClient client, string token, Guid projectId)
    {
        using HttpResponseMessage response = await client.GetAsync($"{StagePlans}/{projectId}", token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.ReadObjectAsync(), AdministrationApi.ETagOf(response));
    }
}

/// <summary>A database of its own: with stages published, every registration in it needs a stage plan.</summary>
[CollectionDefinition(Name)]
public sealed class ProjectStagePlanSuite : ICollectionFixture<ProjectTestHost>
{
    public const string Name = "ProjectStagePlan";
}
