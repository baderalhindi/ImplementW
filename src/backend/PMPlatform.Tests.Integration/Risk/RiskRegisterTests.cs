using System.Net;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using PMPlatform.Application.Common.Events;
using PMPlatform.Tests.Integration.Identity;

namespace PMPlatform.Tests.Integration.Risk;

/// <summary>
/// The risk register through the API: who registers and updates risks (ADR-013), on which projects (ADR-015 and the project's
/// state), with which references, and how the register reads back.
/// </summary>
[Collection(RiskSuite.Name)]
public sealed class RiskRegisterTests(RiskTestHost host)
{
    /// <summary>
    /// ADR-013's amendment to TASK-055: entity Project Managers may register and update risks on their own project. local.r08
    /// registers one IDENTIFIED with no rating, edits it with its ETag, and reaches no project they do not manage.
    /// </summary>
    [Fact]
    public async Task TheEntityProjectManagerRegistersAndUpdatesRisksOnTheirOwnProjectOnly()
    {
        using HttpClient client = host.Api.CreateClient();
        RiskSessions sessions = await client.SignInAsync();
        Guid projectId = await host.ProjectAsync(projectManager: 8);

        using HttpResponseMessage registered = await client.PostAsync(RiskDriver.Risks, sessions.EntityManager, RiskDriver.RiskBody(projectId, ownerUserId: RiskDriver.Person(8)));
        Assert.Equal(HttpStatusCode.Created, registered.StatusCode);
        JsonObject risk = await registered.ReadObjectAsync();
        Assert.Equal(("IDENTIFIED", RiskDriver.Person(8).ToString()), (risk.Text("status"), risk.Text("ownerUserId")));
        Assert.Null(risk["currentAssessment"]);
        Guid riskId = AdministrationApi.IdOf(risk);

        object changed = new
        {
            title = new { text = "Contractor mobilisation delay (main works)", language = "en" },
            description = new { text = "Mobilisation depends on the site permit.", language = "en" },
            riskCategoryItemId = RiskTestHost.CategoryId,
            identifiedDate = RiskDriver.Iso(RiskDriver.Today),
            nextReviewDate = RiskDriver.Iso(RiskDriver.Today.AddDays(14)),
        };
        using (HttpResponseMessage noVersion = await client.PutAsync($"{RiskDriver.Risks}/{riskId}", sessions.EntityManager, changed, ifMatch: null))
        {
            Assert.Equal(HttpStatusCode.PreconditionRequired, noVersion.StatusCode);
        }

        using (HttpResponseMessage updated = await client.PutAsync($"{RiskDriver.Risks}/{riskId}", sessions.EntityManager, changed, AdministrationApi.ETagOf(registered)))
        {
            Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
            JsonObject body = await updated.ReadObjectAsync();
            Assert.Equal(RiskDriver.Iso(RiskDriver.Today.AddDays(14)), body.Text("nextReviewDate"));
            Assert.Null(body["ownerUserId"]);
        }

        Guid othersProject = await host.ProjectAsync(projectManager: 5);
        using (HttpResponseMessage elsewhere = await client.PostAsync(RiskDriver.Risks, sessions.EntityManager, RiskDriver.RiskBody(othersProject)))
        {
            Assert.Equal(HttpStatusCode.NotFound, elsewhere.StatusCode);
        }

        Guid othersRisk = await client.RiskAsync(sessions.InternalManager, othersProject);
        using (HttpResponseMessage hidden = await client.GetAsync($"{RiskDriver.Risks}/{othersRisk}", sessions.EntityManager))
        {
            Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);
        }

        Assert.Empty((await client.GetOrFailAsync(sessions.EntityManager, $"{RiskDriver.Risks}?projectId={othersProject}"))["items"]!.AsArray());
        Assert.Equal(["Risk.RiskRegistered", "Risk.RiskChanged"], await host.AuditEventsAsync(riskId));
    }

    /// <summary>ADR-015: risk management is required for the Standard and Full profiles; a Light project carries issues only.</summary>
    [Fact]
    public async Task ALightProjectCarriesNoRisks()
    {
        using HttpClient client = host.Api.CreateClient();
        RiskSessions sessions = await client.SignInAsync();

        using HttpResponseMessage light = await client.PostAsync(RiskDriver.Risks, sessions.EntityManager, RiskDriver.RiskBody(await host.ProjectAsync(profile: "LIGHT")));
        Assert.Equal((HttpStatusCode.UnprocessableEntity, "RISK_NOT_IN_PROFILE"), await light.RefusalAsync());
        await client.RiskAsync(sessions.EntityManager, await host.ProjectAsync(profile: "FULL"));
    }

    /// <summary>A risk names a published category and an owner with a role over the project, on an approved project, identified by today.</summary>
    [Fact]
    public async Task ARiskNamesPublishedReferencesOnAnApprovedProject()
    {
        using HttpClient client = host.Api.CreateClient();
        RiskSessions sessions = await client.SignInAsync();
        Guid projectId = await host.ProjectAsync();

        (object Body, HttpStatusCode Status, string Code)[] refusals =
        [
            (RiskDriver.RiskBody(projectId, category: RiskTestHost.DraftCategoryId), HttpStatusCode.UnprocessableEntity, "RISK_CATEGORY_INVALID"),
            (RiskDriver.RiskBody(projectId, ownerUserId: RiskDriver.Person(4)), HttpStatusCode.UnprocessableEntity, "RISK_OWNER_NOT_ELIGIBLE"),
            (RiskDriver.RiskBody(projectId, identified: RiskDriver.Today.AddDays(2)), HttpStatusCode.UnprocessableEntity, "RISK_IDENTIFIED_DATE_INVALID"),
            (RiskDriver.RiskBody(await host.ProjectAsync(state: "APPROVED_PLANNED")), HttpStatusCode.Created, "-"),
            (new { projectId, title = new { text = "No description", language = "en" } }, HttpStatusCode.BadRequest, "VALIDATION_FAILED"),
        ];
        foreach ((object body, HttpStatusCode status, string code) in refusals)
        {
            using HttpResponseMessage response = await client.PostAsync(RiskDriver.Risks, sessions.Officer, body);
            Assert.Equal(status, response.StatusCode);
            if (status != HttpStatusCode.Created)
            {
                Assert.Equal(code, (await response.RefusalAsync()).Code);
            }
        }

        using (HttpResponseMessage submitted = await client.PostAsync(RiskDriver.Risks, sessions.Officer, RiskDriver.RiskBody(await host.ProjectAsync(state: "SUBMITTED"))))
        {
            Assert.Equal((HttpStatusCode.UnprocessableEntity, "RISK_PROJECT_NOT_ELIGIBLE"), await submitted.RefusalAsync());
        }

        // A CLOSED project is terminal and read-only (TASK-063): the refusal says so.
        using HttpResponseMessage closed = await client.PostAsync(RiskDriver.Risks, sessions.Officer, RiskDriver.RiskBody(await host.ProjectAsync(state: "CLOSED")));
        Assert.Equal((HttpStatusCode.Conflict, "PROJECT_CLOSED"), await closed.RefusalAsync());
    }

    /// <summary>
    /// SCR-080: the project's risks, most recently changed first, each with its current rating, filtered by status and owner. The
    /// Notifications seam reads a risk's status as its column holds it, for a reminder's revalidation.
    /// </summary>
    [Fact]
    public async Task TheRegisterListsTheProjectsRisksWithTheirRating()
    {
        using HttpClient client = host.Api.CreateClient();
        RiskSessions sessions = await client.SignInAsync();
        Guid projectId = await host.ProjectAsync();
        Guid identified = await client.RiskAsync(sessions.EntityManager, projectId);
        Guid assessed = await client.RiskAsync(sessions.EntityManager, projectId);
        await client.CommandOrFailAsync(sessions.Officer, assessed, "assess", RiskDriver.Assessment(await host.DimensionsAsync(), 1, 1));

        JsonArray all = (await client.GetOrFailAsync(sessions.EntityManager, $"{RiskDriver.Risks}?projectId={projectId}"))["items"]!.AsArray();
        Assert.Equal([assessed, identified], all.Select(r => AdministrationApi.IdOf(r!.AsObject())));
        Assert.Equal("LOW", all[0]!["currentAssessment"]!["rating"]!.Text("code"));

        JsonArray rated = (await client.GetOrFailAsync(sessions.EntityManager, $"{RiskDriver.Risks}?projectId={projectId}&status=ASSESSED,TREATMENT"))["items"]!.AsArray();
        Assert.Equal([assessed], rated.Select(r => AdministrationApi.IdOf(r!.AsObject())));
        using (HttpResponseMessage bad = await client.GetAsync($"{RiskDriver.Risks}?projectId={projectId}&status=OPEN", sessions.EntityManager))
        {
            Assert.Equal(["status ENUM_VALUE"], await bad.ReadFieldErrorsAsync());
        }

        await using AsyncServiceScope scope = host.Api.Services.CreateAsyncScope();
        INotificationConditionSource source = Assert.Single(scope.ServiceProvider.GetServices<INotificationConditionSource>(), s => s.SourceModule == "Risk");
        Assert.Equal("IDENTIFIED", await source.FindStatusAsync("Risk", identified, CancellationToken.None));
        Assert.Null(await source.FindStatusAsync("Risk", Guid.NewGuid(), CancellationToken.None));
    }
}
