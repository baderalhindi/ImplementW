using System.Net;
using System.Text.Json.Nodes;
using PMPlatform.Application.Features.Approval.Contracts;
using PMPlatform.Tests.Integration.Identity;
using PMPlatform.Tests.Integration.MasterDataConfig;

namespace PMPlatform.Tests.Integration.Project;

/// <summary>The WF-01 endpoints as the SPA calls them (R-16, R-21, R-23, R-31, R-36): validation, references, concurrency and the register.</summary>
[Collection(ProjectSuite.Name)]
public sealed class ProjectEndpointTests(ProjectTestHost host)
{
    [Fact]
    public async Task ARegistrationIsShapeCheckedBeforeAnythingIsWritten()
    {
        using HttpClient client = host.Api.CreateClient();
        string token = (await client.SignInOrFailAsync(3)).AccessToken;

        using (HttpResponseMessage noKey = await client.SendAsync(HttpMethod.Post, ProjectDriver.Projects, token, host.Registration(), idempotencyKey: ""))
        {
            Assert.Equal((HttpStatusCode.BadRequest, "IDEMPOTENCY_KEY_REQUIRED"), (noKey.StatusCode, await noKey.CodeOfAsync()));
        }

        using (HttpResponseMessage empty = await client.PostAsync(ProjectDriver.Projects, token, new JsonObject()))
        {
            Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);
            Assert.Equal(
                ["title REQUIRED", "classificationItemId REQUIRED", "departmentId REQUIRED", "governanceProfileItemId REQUIRED", "participationMode REQUIRED"],
                await empty.ReadFieldErrorsAsync());
        }

        JsonObject malformed = host.Registration(change: r =>
        {
            r["participationMode"] = "SHARED";
            r["registrationBudgetSar"] = "12500000.5";
            r["plannedEndDate"] = "2020-01-01";
            r["latitude"] = 91;
            r["longitude"] = -181;
        });
        using (HttpResponseMessage invalid = await client.PostAsync(ProjectDriver.Projects, token, malformed))
        {
            Assert.Equal(
                ["participationMode ENUM_VALUE", "registrationBudgetSar MALFORMED", "plannedEndDate DATE_BEFORE_START", "latitude OUT_OF_RANGE", "longitude OUT_OF_RANGE"],
                await invalid.ReadFieldErrorsAsync());
        }

        using HttpResponseMessage negative = await client.PostAsync(ProjectDriver.Projects, token, host.Registration(change: r => r["registrationBudgetSar"] = "-1.00"));
        Assert.Equal(["registrationBudgetSar OUT_OF_RANGE"], await negative.ReadFieldErrorsAsync());
    }

    /// <summary>
    /// The names a registration uses must be usable now: PUBLISHED items of the right catalogue, an active entity, a city in
    /// the region named with it (core-platform-schema N-1), and an entity for an entity-managed project (ADR-013).
    /// </summary>
    [Fact]
    public async Task ARegistrationNamesOnlyUsableReferences()
    {
        using HttpClient client = host.Api.CreateClient();
        string token = (await client.SignInOrFailAsync(3)).AccessToken;

        JsonObject unusable = host.Registration(change: r =>
        {
            r["classificationItemId"] = ProjectTestHost.DraftClassificationId.ToString();
            r["governanceProfileItemId"] = ProjectTestHost.RegionId.ToString();
            r["regionItemId"] = ProjectTestHost.OtherRegionId.ToString();
            r["externalEntityId"] = ProjectTestHost.SuspendedEntityId.ToString();
        });
        using (HttpResponseMessage refused = await client.PostAsync(ProjectDriver.Projects, token, unusable))
        {
            Assert.Equal((HttpStatusCode.UnprocessableEntity, "PROJECT_REFERENCE_INVALID"), (refused.StatusCode, await refused.CodeOfAsync()));
            Assert.Equal(
                ["classificationItemId NOT_FOUND", "governanceProfileItemId NOT_FOUND", "cityItemId NOT_ALLOWED", "externalEntityId NOT_FOUND"],
                await refused.ReadFieldErrorsAsync());
        }

        using (HttpResponseMessage noEntity = await client.PostAsync(ProjectDriver.Projects, token, host.Registration(change: r => r["externalEntityId"] = null)))
        {
            Assert.Equal((HttpStatusCode.UnprocessableEntity, "PROJECT_PARTICIPATION_INVALID"), (noEntity.StatusCode, await noEntity.CodeOfAsync()));
            Assert.Equal(["externalEntityId REQUIRED"], await noEntity.ReadFieldErrorsAsync());
        }

        // A client cannot name the Formal Project ID: AHDA issues it on approval (ADR-013).
        JsonObject created = await client.CreateOrFailAsync(token, host.Registration(change: r => r["formalProjectId"] = "PRJ-999999"));
        Assert.Null(created.FormalProjectId());
    }

    /// <summary>Review needs the budget and planned dates the governance profile is assigned on (TASK-105).</summary>
    [Fact]
    public async Task ADraftWithoutItsBudgetAndDatesIsNotSubmitted()
    {
        using HttpClient client = host.Api.CreateClient();
        string token = (await client.SignInOrFailAsync(3)).AccessToken;
        JsonObject sparse = host.Registration(change: r =>
        {
            r.Remove("registrationBudgetSar");
            r.Remove("plannedStartDate");
            r.Remove("plannedEndDate");
        });
        Guid projectId = AdministrationApi.IdOf(await client.CreateOrFailAsync(token, sparse));

        using HttpResponseMessage refused = await client.PostAsync($"{ProjectDriver.Projects}/{projectId}/submit", token, new { projectManagerUserId = ProjectDriver.Person(5) });

        Assert.Equal((HttpStatusCode.UnprocessableEntity, "PROJECT_INCOMPLETE"), (refused.StatusCode, await refused.CodeOfAsync()));
        Assert.Equal(["registrationBudgetSar REQUIRED", "plannedStartDate REQUIRED", "plannedEndDate REQUIRED"], await refused.ReadFieldErrorsAsync());
        Assert.Equal("DRAFT", (await host.RowAsync(projectId))["lifecycle_state"]!.GetValue<string>());
    }

    [Fact]
    public async Task AnEditNeedsTheCurrentVersion()
    {
        using HttpClient client = host.Api.CreateClient();
        string token = (await client.SignInOrFailAsync(3)).AccessToken;
        Guid projectId = AdministrationApi.IdOf(await client.CreateOrFailAsync(token, host.Registration()));
        using HttpResponseMessage read = await client.GetAsync($"{ProjectDriver.Projects}/{projectId}", token);
        string eTag = AdministrationApi.ETagOf(read);

        using (HttpResponseMessage noIfMatch = await client.PutAsync($"{ProjectDriver.Projects}/{projectId}", token, host.Registration(), ifMatch: null))
        {
            Assert.Equal((HttpStatusCode.PreconditionRequired, "PRECONDITION_REQUIRED"), (noIfMatch.StatusCode, await noIfMatch.CodeOfAsync()));
        }

        using (HttpResponseMessage edited = await client.PutAsync($"{ProjectDriver.Projects}/{projectId}", token, host.Registration(change: r => r["title"]!["text"] = "Coastal road upgrade"), eTag))
        {
            Assert.Equal(HttpStatusCode.OK, edited.StatusCode);
            Assert.NotEqual(eTag, AdministrationApi.ETagOf(edited));
            Assert.Equal("Coastal road upgrade", (await edited.ReadObjectAsync())["title"]!["text"]!.GetValue<string>());
        }

        using (HttpResponseMessage stale = await client.PutAsync($"{ProjectDriver.Projects}/{projectId}", token, host.Registration(), eTag))
        {
            Assert.Equal((HttpStatusCode.PreconditionFailed, "PRECONDITION_FAILED"), (stale.StatusCode, await stale.CodeOfAsync()));
        }

        using (HttpResponseMessage staleCommand = await client.PostAsync($"{ProjectDriver.Projects}/{projectId}/submit", token, new { projectManagerUserId = ProjectDriver.Person(5) }, eTag))
        {
            Assert.Equal(HttpStatusCode.PreconditionFailed, staleCommand.StatusCode);
        }

        Assert.Contains("Project.ProjectChanged", await host.AuditEventsAsync(projectId));
    }

    /// <summary>SCR-025: filtered by state and found by Formal Project ID; an unknown state is a 400 (R-31).</summary>
    [Fact]
    public async Task TheRegisterFiltersByStateAndFindsAFormalProjectId()
    {
        using HttpClient client = host.Api.CreateClient();
        Sessions sessions = await client.SignInAsync();
        Guid projectId = await host.UnderReviewAsync(client, sessions);
        await host.DecideAndDeliverAsync(projectId, ApprovalTaskDecision.Approve);
        string formalProjectId = (await host.RowAsync(projectId))["formal_project_id"]!.GetValue<string>();

        using HttpResponseMessage byId = await client.GetAsync($"{ProjectDriver.Projects}?q={formalProjectId}", sessions.Approver);
        JsonObject found = Assert.Single((await byId.ReadObjectAsync())["items"]!.AsArray())!.AsObject();
        Assert.Equal((projectId.ToString(), "APPROVED_PLANNED"), (found["id"]!.GetValue<string>(), found.Status()));

        using HttpResponseMessage planned = await client.GetAsync($"{ProjectDriver.Projects}?status=APPROVED_PLANNED,ACTIVE&pageSize=200", sessions.Approver);
        Assert.All((await planned.ReadObjectAsync())["items"]!.AsArray(), p => Assert.True(p!["status"]!.GetValue<string>() is "APPROVED_PLANNED" or "ACTIVE"));

        using HttpResponseMessage unknown = await client.GetAsync($"{ProjectDriver.Projects}?status=CANCELLED", sessions.Approver);
        Assert.Equal(["status ENUM_VALUE"], await unknown.ReadFieldErrorsAsync());
    }

    /// <summary>
    /// SCR-026 My Projects: the projects a person manages, within what they may see. A draft names no manager yet, and the
    /// filter never shows a project outside the caller's scope.
    /// </summary>
    [Fact]
    public async Task MyProjectsAreTheOnesTheCallerManagesWithinTheirScope()
    {
        using HttpClient client = host.Api.CreateClient();
        Sessions sessions = await client.SignInAsync();
        Guid managed = await host.UnderReviewAsync(client, sessions);
        Guid draft = AdministrationApi.IdOf(await client.CreateOrFailAsync(sessions.Entity, host.Registration()));
        Guid manager = ProjectDriver.Person(8);

        using HttpResponseMessage mine = await client.GetAsync($"{ProjectDriver.Projects}?projectManagerUserId={manager}&pageSize=200", sessions.Entity);
        List<string> ids = [.. (await mine.ReadObjectAsync())["items"]!.AsArray().Select(p => p!["id"]!.GetValue<string>())];
        Assert.Contains(managed.ToString(), ids);
        Assert.DoesNotContain(draft.ToString(), ids);

        using HttpResponseMessage someoneElse = await client.GetAsync($"{ProjectDriver.Projects}?projectManagerUserId={ProjectDriver.Person(5)}&pageSize=200", sessions.Entity);
        JsonArray others = (await someoneElse.ReadObjectAsync())["items"]!.AsArray();
        Assert.DoesNotContain(others, p => p!["id"]!.GetValue<string>() == managed.ToString());
        Assert.All(others, p => Assert.Equal(ProjectDriver.Person(5).ToString(), p!["projectManagerUserId"]!.GetValue<string>()));

        using HttpResponseMessage malformed = await client.GetAsync($"{ProjectDriver.Projects}?projectManagerUserId=me", sessions.Entity);
        Assert.Equal(HttpStatusCode.BadRequest, malformed.StatusCode);
    }
}
