using System.Net;
using System.Text.Json.Nodes;
using PMPlatform.Tests.Integration.Identity;
using static PMPlatform.Tests.Integration.ExternalParticipation.ExternalParticipationDriver;

namespace PMPlatform.Tests.Integration.ExternalParticipation;

/// <summary>
/// A request's own way (WF-13 §4.1, §5.3): drafted by AHDA, issued only once everything an issued request needs holds, answered only while
/// open, cancelled only before its answer reaches AHDA, and edited only as a DRAFT.
/// </summary>
[Collection(ExternalParticipationSuite.Name)]
public sealed class UpdateRequestLifecycleTests(ExternalParticipationTestHost host)
{
    /// <summary>
    /// WF-13 §5.3: the typed schema must exist (no generic "submit anything"), the source record must be the project's, the responder an
    /// eligible user of the addressed entity and the reviewer an eligible AHDA user; issue needs both, a due date not past, and the contribution
    /// type enabled for the project's participation mode.
    /// </summary>
    [Fact]
    public async Task ARequestIsIssuedOnlyWhenEverythingItNeedsHolds()
    {
        using HttpClient client = host.Api.CreateClient();
        ParticipationSessions sessions = await client.SignInAsync();
        Guid projectId = await host.ProjectAsync();
        Guid otherProject = await host.ProjectAsync();
        await host.GrantProjectAsync(8, projectId);
        await host.GrantProjectAsync(7, projectId);
        Guid otherProjectsTask = await client.StartedTaskAsync(sessions, otherProject, "Elsewhere");

        await AssertRefusedAsync(client, sessions, RequestBody(projectId, ExternalParticipationTestHost.EntityA, ExternalParticipationTestHost.UntypedTypeId, null, 8),
            HttpStatusCode.UnprocessableEntity, "EXTERNAL_REQUEST_SCHEMA_INVALID");
        await AssertRefusedAsync(client, sessions, RequestBody(projectId, ExternalParticipationTestHost.EntityA, ExternalParticipationTestHost.TaskProgressTypeId, null, 8),
            HttpStatusCode.UnprocessableEntity, "EXTERNAL_REQUEST_SOURCE_INVALID");
        await AssertRefusedAsync(client, sessions, RequestBody(projectId, ExternalParticipationTestHost.EntityA, ExternalParticipationTestHost.TaskProgressTypeId, otherProjectsTask, 8),
            HttpStatusCode.UnprocessableEntity, "EXTERNAL_REQUEST_SOURCE_INVALID");
        await AssertRefusedAsync(client, sessions, RequestBody(projectId, ExternalParticipationTestHost.EntityA, ExternalParticipationTestHost.InformationTypeId, Guid.NewGuid(), 8),
            HttpStatusCode.UnprocessableEntity, "EXTERNAL_REQUEST_SOURCE_INVALID");

        // The responder: of the addressed entity (local.r07 is entity B's), external (local.r06 is AHDA's), with access to the project.
        foreach (int ineligible in new[] { 7, 6 })
        {
            await AssertRefusedAsync(client, sessions, RequestBody(projectId, ExternalParticipationTestHost.EntityA, ExternalParticipationTestHost.InformationTypeId, null, ineligible),
                HttpStatusCode.UnprocessableEntity, "EXTERNAL_RESPONSIBLE_USER_INELIGIBLE");
        }

        // The reviewer: an AHDA user who could review it — not the entity's user, not the System Administrator.
        foreach (int ineligible in new[] { 8, 1 })
        {
            await AssertRefusedAsync(client, sessions, RequestBody(projectId, ExternalParticipationTestHost.EntityA, ExternalParticipationTestHost.InformationTypeId, null, 8, reviewer: ineligible),
                HttpStatusCode.UnprocessableEntity, "REVIEWER_INELIGIBLE");
        }

        Guid draft = AdministrationApi.IdOf(await client.CreatedOrFailAsync(sessions.ProjectManager, Requests,
            RequestBody(projectId, ExternalParticipationTestHost.EntityA, ExternalParticipationTestHost.InformationTypeId, null, responder: null, reviewer: null)));
        using (HttpResponseMessage incomplete = await client.PostAsync($"{Requests}/{draft}/issue", sessions.ProjectManager))
        {
            Assert.Equal((HttpStatusCode.UnprocessableEntity, "EXTERNAL_REQUEST_INCOMPLETE"), await incomplete.RefusalAsync());
            Assert.Equal(["responsibleUserId REQUIRED", "reviewerUserId REQUIRED"], await incomplete.ReadFieldErrorsAsync());
        }

        await PutAsync(client, sessions, draft, RequestBody(projectId, ExternalParticipationTestHost.EntityA, ExternalParticipationTestHost.InformationTypeId, null, 8, due: Today.AddDays(-1)));
        using (HttpResponseMessage late = await client.PostAsync($"{Requests}/{draft}/issue", sessions.ProjectManager))
        {
            Assert.Equal((HttpStatusCode.UnprocessableEntity, "EXTERNAL_REQUEST_DUE_DATE_INVALID"), await late.RefusalAsync());
        }

        await PutAsync(client, sessions, draft, RequestBody(projectId, ExternalParticipationTestHost.EntityA, ExternalParticipationTestHost.InformationTypeId, null, 8, due: Today));
        JsonObject issued = await client.OkOrFailAsync(sessions.ProjectManager, $"{Requests}/{draft}/issue");
        Assert.Equal(("ISSUED", "DUE", ExternalParticipationTestHost.ParticipationVersionId.ToString()),
            (issued.Text("status"), issued.Text("dueCondition"), issued.Text("participationConfigurationVersionId")));

        using HttpResponseMessage read = await client.GetAsync($"{Requests}/{draft}", sessions.ProjectManager);
        using HttpResponseMessage edit = await client.PutAsync($"{Requests}/{draft}", sessions.ProjectManager,
            RequestBody(projectId, ExternalParticipationTestHost.EntityA, ExternalParticipationTestHost.InformationTypeId, null, 8), AdministrationApi.ETagOf(read));
        Assert.Equal((HttpStatusCode.Conflict, "EXTERNAL_REQUEST_NOT_EDITABLE"), await edit.RefusalAsync());
    }

    /// <summary>ADR-013: PARTICIPATION decides which contribution types each participation mode takes; PROJECT_INFORMATION is entity-managed projects' only.</summary>
    [Fact]
    public async Task AContributionTypeIsIssuedOnlyWhereThePublishedParticipationEnablesIt()
    {
        using HttpClient client = host.Api.CreateClient();
        ParticipationSessions sessions = await client.SignInAsync();
        Guid ahdaManaged = await host.ProjectAsync(participation: "AHDA_MANAGED");
        await host.GrantProjectAsync(8, ahdaManaged);
        Guid draft = AdministrationApi.IdOf(await client.CreatedOrFailAsync(sessions.ProjectManager, Requests,
            RequestBody(ahdaManaged, ExternalParticipationTestHost.EntityA, ExternalParticipationTestHost.InformationTypeId, null, 8)));

        using HttpResponseMessage issued = await client.PostAsync($"{Requests}/{draft}/issue", sessions.ProjectManager);
        Assert.Equal((HttpStatusCode.UnprocessableEntity, "EXTERNAL_CONTRIBUTION_TYPE_NOT_ENABLED"), await issued.RefusalAsync());
    }

    /// <summary>
    /// The entity answers only while the request is open, one revision at a time, and AHDA cancels only before the answer reaches it; a
    /// cancelled request's draft answer stays, never submitted.
    /// </summary>
    [Fact]
    public async Task ARequestIsAnsweredWhileOpenAndCancelledBeforeItsAnswerArrives()
    {
        using HttpClient client = host.Api.CreateClient();
        ParticipationSessions sessions = await client.SignInAsync();
        Guid projectId = await host.ProjectAsync();
        await host.GrantProjectAsync(8, projectId);
        Guid requestId = await client.IssuedRequestAsync(sessions, projectId, ExternalParticipationTestHost.EntityA, ExternalParticipationTestHost.InformationTypeId, null, responder: 8);

        Guid contributionId = AdministrationApi.IdOf(await client.CreatedOrFailAsync(sessions.EntityA, Contributions,
            new { externalUpdateRequestId = requestId, fields = Information("Work restarts on Sunday.") }));
        using (HttpResponseMessage second = await client.PostAsync(Contributions, sessions.EntityA, new { externalUpdateRequestId = requestId, fields = Information("Again.") }))
        {
            Assert.Equal((HttpStatusCode.Conflict, "EXTERNAL_REQUEST_NOT_OPEN"), await second.RefusalAsync());
        }

        await client.OkOrFailAsync(sessions.EntityA, $"{Contributions}/{contributionId}/submit");

        using (HttpResponseMessage responded = await client.PostAsync($"{Requests}/{requestId}/cancel", sessions.ProjectManager,
                   new { reason = new { text = "No longer needed.", language = "en" } }))
        {
            Assert.Equal((HttpStatusCode.Conflict, "EXTERNAL_REQUEST_RESPONDED"), await responded.RefusalAsync());
        }

        Guid cancelled = await client.IssuedRequestAsync(sessions, projectId, ExternalParticipationTestHost.EntityA, ExternalParticipationTestHost.InformationTypeId, null, responder: 8);
        Guid drafted = AdministrationApi.IdOf(await client.CreatedOrFailAsync(sessions.EntityA, Contributions,
            new { externalUpdateRequestId = cancelled, fields = Information("Half an answer.") }));
        JsonObject cancel = await client.OkOrFailAsync(sessions.ProjectManager, $"{Requests}/{cancelled}/cancel", new { reason = new { text = "Superseded.", language = "en" } });
        Assert.Equal("CANCELLED", cancel.Text("status"));
        using (HttpResponseMessage late = await client.PostAsync($"{Contributions}/{drafted}/submit", sessions.EntityA))
        {
            Assert.Equal((HttpStatusCode.Conflict, "EXTERNAL_REQUEST_NOT_OPEN"), await late.RefusalAsync());
        }

        Assert.Equal("DRAFT", (await client.GetOrFailAsync(sessions.ProjectManager, $"{Contributions}/{drafted}")).Text("status"));
    }

    /// <summary>A value outside the typed schema never reaches storage (BR-EXT-020): refused, not ignored, and the stored revision is unchanged.</summary>
    [Fact]
    public async Task AnAnswerHoldsTheSchemasFieldsAndNothingElse()
    {
        using HttpClient client = host.Api.CreateClient();
        ParticipationSessions sessions = await client.SignInAsync();
        Guid projectId = await host.ProjectAsync();
        await host.GrantProjectAsync(8, projectId);
        Guid taskId = await client.StartedTaskAsync(sessions, projectId, "Piling");
        Guid requestId = await client.IssuedRequestAsync(sessions, projectId, ExternalParticipationTestHost.EntityA, ExternalParticipationTestHost.TaskProgressTypeId, taskId, responder: 8);

        object[] injectedFields = [new { fieldCode = "actualPercentComplete", value = "50" }, new { fieldCode = "assigneeUserId", value = Person(8).ToString() }];
        using (HttpResponseMessage injected = await client.PostAsync(Contributions, sessions.EntityA, new { externalUpdateRequestId = requestId, fields = injectedFields }))
        {
            Assert.Equal((HttpStatusCode.UnprocessableEntity, "EXTERNAL_FIELD_ACCESS_DENIED"), await injected.RefusalAsync());
            Assert.Equal(["fields[1].fieldCode NOT_ALLOWED"], await injected.ReadFieldErrorsAsync());
        }

        using (HttpResponseMessage outOfRange = await client.PostAsync(Contributions, sessions.EntityA,
                   new { externalUpdateRequestId = requestId, fields = new object[] { new { fieldCode = "actualPercentComplete", value = "140" } } }))
        {
            Assert.Equal((HttpStatusCode.UnprocessableEntity, "CONTRIBUTION_VALIDATION_FAILED"), await outOfRange.RefusalAsync());
        }

        Guid contributionId = AdministrationApi.IdOf(await client.CreatedOrFailAsync(sessions.EntityA, Contributions,
            new { externalUpdateRequestId = requestId, fields = new object[] { new { fieldCode = "progressNote", value = "Rig arrives Monday.", language = "en" } } }));
        using (HttpResponseMessage incomplete = await client.PostAsync($"{Contributions}/{contributionId}/submit", sessions.EntityA))
        {
            Assert.Equal((HttpStatusCode.UnprocessableEntity, "CONTRIBUTION_REQUIRED_ITEM_MISSING"), await incomplete.RefusalAsync());
        }

        Assert.Equal(["progressNote"], await host.Database.QueryAsync(
            $"SELECT field_code FROM external_participation.external_contribution_field WHERE external_contribution_id = '{contributionId}'"));
    }

    private static async Task AssertRefusedAsync(HttpClient client, ParticipationSessions sessions, object body, HttpStatusCode status, string code)
    {
        using HttpResponseMessage response = await client.PostAsync(Requests, sessions.ProjectManager, body);
        Assert.Equal((status, code), await response.RefusalAsync());
    }

    private static async Task PutAsync(HttpClient client, ParticipationSessions sessions, Guid requestId, object body)
    {
        using HttpResponseMessage read = await client.GetAsync($"{Requests}/{requestId}", sessions.ProjectManager);
        using HttpResponseMessage put = await client.PutAsync($"{Requests}/{requestId}", sessions.ProjectManager, body, AdministrationApi.ETagOf(read));
        Assert.True(put.StatusCode == HttpStatusCode.OK, $"PUT: {(int)put.StatusCode} {await put.Content.ReadAsStringAsync()}");
    }
}
