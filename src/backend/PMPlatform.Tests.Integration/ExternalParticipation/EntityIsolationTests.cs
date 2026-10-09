using System.Net;
using System.Text.Json.Nodes;
using PMPlatform.Tests.Integration.Identity;
using static PMPlatform.Tests.Integration.ExternalParticipation.ExternalParticipationDriver;

namespace PMPlatform.Tests.Integration.ExternalParticipation;

/// <summary>
/// TASK-066 acceptance criterion 1 and its validation check: an R08 user can never query or infer data outside their explicit ENTITY/Project
/// grant. Two entities answer requests on the same project, each through its own user's per-project assignment; each user then tries the
/// other's records by direct API call — read, list, filter, answer, edit, submit — and every attempt answers as for a record that does not
/// exist (R-47), so not even existence leaks (WF-13 EXT-P-14, BR-EXT-033, BR-EXT-034).
/// </summary>
[Collection(ExternalParticipationSuite.Name)]
public sealed class EntityIsolationTests(ExternalParticipationTestHost host)
{
    [Fact]
    public async Task AnEntityUserCannotReachAnotherEntitysRequestsOrContributionsByAnyCall()
    {
        using HttpClient client = host.Api.CreateClient();
        ParticipationSessions sessions = await client.SignInAsync();
        Guid projectId = await host.ProjectAsync();
        await host.GrantProjectAsync(8, projectId);
        await host.GrantProjectAsync(7, projectId);

        Guid ownRequest = await client.IssuedRequestAsync(sessions, projectId, ExternalParticipationTestHost.EntityA, ExternalParticipationTestHost.InformationTypeId, null, responder: 8);
        Guid otherRequest = await client.IssuedRequestAsync(sessions, projectId, ExternalParticipationTestHost.EntityB, ExternalParticipationTestHost.InformationTypeId, null, responder: 7);
        JsonObject otherContribution = await client.AnswerAsync(sessions.EntityB, otherRequest, Information("Entity B's site team answers."));
        Guid otherContributionId = AdministrationApi.IdOf(otherContribution);
        string notFound = $"{Requests}/{Guid.NewGuid()}";

        // A record of the other entity answers exactly as one that does not exist: 404 NOT_FOUND, the same body shape.
        Assert.Equal((HttpStatusCode.NotFound, "NOT_FOUND"), await RefusalOfAsync(client.GetAsync(notFound, sessions.EntityA)));
        Assert.Equal((HttpStatusCode.NotFound, "NOT_FOUND"), await RefusalOfAsync(client.GetAsync($"{Requests}/{otherRequest}", sessions.EntityA)));
        Assert.Equal((HttpStatusCode.NotFound, "NOT_FOUND"), await RefusalOfAsync(client.GetAsync($"{Contributions}/{otherContributionId}", sessions.EntityA)));
        Assert.Equal((HttpStatusCode.NotFound, "NOT_FOUND"), await RefusalOfAsync(client.PostAsync(Contributions, sessions.EntityA,
            new { externalUpdateRequestId = otherRequest, fields = Information("Entity A answers for entity B.") })));
        Assert.Equal((HttpStatusCode.NotFound, "NOT_FOUND"), await RefusalOfAsync(client.PostAsync($"{Contributions}/{otherContributionId}/submit", sessions.EntityA)));
        Assert.Equal((HttpStatusCode.NotFound, "NOT_FOUND"), await RefusalOfAsync(client.PutAsync($"{Contributions}/{otherContributionId}", sessions.EntityA,
            new { fields = Information("Overwritten by entity A.") }, "\"1\"")));

        // Collections hold nothing of the other entity, whatever the filter asks for; no count betrays it.
        Assert.Equal([ownRequest.ToString()], await client.IdsAsync(sessions.EntityA, $"{Requests}?projectId={projectId}"));
        JsonObject filtered = await client.GetOrFailAsync(sessions.EntityA, $"{Requests}?externalEntityId={ExternalParticipationTestHost.EntityB}");
        Assert.Equal((0, 0), (filtered["items"]!.AsArray().Count, filtered["totalCount"]!.GetValue<int>()));
        JsonObject revisions = await client.GetOrFailAsync(sessions.EntityA, $"{Contributions}?externalUpdateRequestId={otherRequest}");
        Assert.Equal((0, 0), (revisions["items"]!.AsArray().Count, revisions["totalCount"]!.GetValue<int>()));
        Assert.Empty(await client.IdsAsync(sessions.EntityA, $"{Applications}?externalContributionId={otherContributionId}"));

        // The other entity's user sees its own and not entity A's; AHDA's Project Manager sees both.
        Assert.Equal([otherRequest.ToString()], await client.IdsAsync(sessions.EntityB, $"{Requests}?projectId={projectId}"));
        Assert.Equal((HttpStatusCode.NotFound, "NOT_FOUND"), await RefusalOfAsync(client.GetAsync($"{Requests}/{ownRequest}", sessions.EntityB)));
        Assert.Equal(
            new[] { ownRequest.ToString(), otherRequest.ToString() }.Order(),
            (await client.IdsAsync(sessions.ProjectManager, $"{Requests}?projectId={projectId}")).Order());

        // The other entity's answer is exactly as entity B submitted it: nothing entity A sent reached it.
        JsonObject unchanged = await client.GetOrFailAsync(sessions.EntityB, $"{Contributions}/{otherContributionId}");
        Assert.Equal(("SUBMITTED", "Entity B's site team answers."), (unchanged.Text("status"), unchanged["fields"]![0]!.Text("value")));
        Assert.Equal(["1"], await host.Database.QueryAsync($"SELECT count(*)::text FROM external_participation.external_contribution WHERE external_update_request_id = '{otherRequest}'"));
    }

    /// <summary>
    /// The explicit Project grant: the same entity's request on a project the user's assignment does not cover is out of reach, as is any
    /// request once the assignment has ended (WF-13 EXT-P-13: access expiry blocks new actions, history stays).
    /// </summary>
    [Fact]
    public async Task AnEntityUserReachesOnlyTheProjectsItIsAssignedToAndOnlyWhileAssigned()
    {
        using HttpClient client = host.Api.CreateClient();
        ParticipationSessions sessions = await client.SignInAsync();
        Guid assigned = await host.ProjectAsync();
        Guid unassigned = await host.ProjectAsync();
        await host.GrantProjectAsync(8, assigned);

        Guid reachable = await client.IssuedRequestAsync(sessions, assigned, ExternalParticipationTestHost.EntityA, ExternalParticipationTestHost.InformationTypeId, null, responder: 8);
        using (HttpResponseMessage ineligible = await client.PostAsync(Requests, sessions.ProjectManager,
                   RequestBody(unassigned, ExternalParticipationTestHost.EntityA, ExternalParticipationTestHost.InformationTypeId, null, responder: 8)))
        {
            Assert.Equal((HttpStatusCode.UnprocessableEntity, "EXTERNAL_RESPONSIBLE_USER_INELIGIBLE"), await ineligible.RefusalAsync());
        }

        Guid unreachable = AdministrationApi.IdOf(await client.CreatedOrFailAsync(sessions.ProjectManager, Requests,
            RequestBody(unassigned, ExternalParticipationTestHost.EntityA, ExternalParticipationTestHost.InformationTypeId, null, responder: null)));
        Assert.Equal((HttpStatusCode.NotFound, "NOT_FOUND"), await RefusalOfAsync(client.GetAsync($"{Requests}/{unreachable}", sessions.EntityA)));
        Assert.Equal([reachable.ToString()], await client.IdsAsync(sessions.EntityA, $"{Requests}?projectId={assigned}"));
        Assert.Empty(await client.IdsAsync(sessions.EntityA, $"{Requests}?projectId={unassigned}"));
        Assert.DoesNotContain(unreachable.ToString(), await client.IdsAsync(sessions.EntityA, $"{Requests}?pageSize=200"));

        Guid contributionId = AdministrationApi.IdOf(await client.CreatedOrFailAsync(sessions.EntityA, Contributions,
            new { externalUpdateRequestId = reachable, fields = Information("Drafted before the assignment ended.") }));
        await host.Database.ExecuteAsync($"""
            UPDATE identity_access.access_relationship SET status = 'ENDED', ends_at = now(), end_reason = 'MANUAL'
            WHERE user_id = '{Person(8)}' AND project_id = '{assigned}'
            """);

        Assert.Equal((HttpStatusCode.NotFound, "NOT_FOUND"), await RefusalOfAsync(client.PostAsync($"{Contributions}/{contributionId}/submit", sessions.EntityA)));
        Assert.Empty(await client.IdsAsync(sessions.EntityA, $"{Requests}?projectId={assigned}"));
        JsonObject kept = await client.GetOrFailAsync(sessions.ProjectManager, $"{Contributions}/{contributionId}");
        Assert.Equal(("DRAFT", "Drafted before the assignment ended."), (kept.Text("status"), kept["fields"]![0]!.Text("value")));
    }

    /// <summary>A DRAFT request is AHDA's: it does not exist for the entity it will be addressed to (WF-13 §4.1).</summary>
    [Fact]
    public async Task ADraftRequestDoesNotExistForTheEntity()
    {
        using HttpClient client = host.Api.CreateClient();
        ParticipationSessions sessions = await client.SignInAsync();
        Guid projectId = await host.ProjectAsync();
        await host.GrantProjectAsync(8, projectId);
        Guid draft = AdministrationApi.IdOf(await client.CreatedOrFailAsync(sessions.ProjectManager, Requests,
            RequestBody(projectId, ExternalParticipationTestHost.EntityA, ExternalParticipationTestHost.InformationTypeId, null, responder: 8)));

        Assert.Equal((HttpStatusCode.NotFound, "NOT_FOUND"), await RefusalOfAsync(client.GetAsync($"{Requests}/{draft}", sessions.EntityA)));
        Assert.Empty(await client.IdsAsync(sessions.EntityA, $"{Requests}?projectId={projectId}"));
        Assert.Equal((HttpStatusCode.NotFound, "NOT_FOUND"), await RefusalOfAsync(client.PostAsync(Contributions, sessions.EntityA,
            new { externalUpdateRequestId = draft, fields = Information("Too early.") })));
        Assert.Equal("DRAFT", (await client.GetOrFailAsync(sessions.ProjectManager, $"{Requests}/{draft}")).Text("status"));
    }

    /// <summary>
    /// WF-13 §8.2, EXT-P-12: the external projection carries no internal-only field — not the reviewer, the issuer, the internal note or the
    /// source version — and names each in <c>maskedFields</c>; the internal note's text appears nowhere in anything the entity can read.
    /// </summary>
    [Fact]
    public async Task TheEntitysProjectionCarriesNoInternalOnlyField()
    {
        using HttpClient client = host.Api.CreateClient();
        ParticipationSessions sessions = await client.SignInAsync();
        Guid projectId = await host.ProjectAsync();
        await host.GrantProjectAsync(8, projectId);
        Guid requestId = await client.IssuedRequestAsync(sessions, projectId, ExternalParticipationTestHost.EntityA, ExternalParticipationTestHost.InformationTypeId, null, responder: 8);
        Guid contributionId = AdministrationApi.IdOf(await client.AnswerAsync(sessions.EntityA, requestId, Information("The access road opens on Sunday.")));
        const string InternalNote = "Cross-check with the contractor's own schedule before believing this.";
        await client.DecideAsync(sessions.ProjectManager, contributionId, "return", reason: "Say which access road.", note: InternalNote);

        JsonObject internalRequest = await client.GetOrFailAsync(sessions.ProjectManager, $"{Requests}/{requestId}");
        JsonObject externalRequest = await client.GetOrFailAsync(sessions.EntityA, $"{Requests}/{requestId}");
        Assert.Equal(("INTERNAL", "EXTERNAL"), (internalRequest.Text("projection"), externalRequest.Text("projection")));
        string[] withheld = ["reviewerUserId", "participationConfigurationVersionId", "issuedByUserId", "createdBy", "updatedBy"];
        Assert.All(withheld, field => Assert.True(internalRequest.ContainsKey(field), field));
        Assert.All(withheld, field => Assert.False(externalRequest.ContainsKey(field), field));
        Assert.Equal(withheld, externalRequest["maskedFields"]!.AsArray().Select(f => f!.GetValue<string>()));
        Assert.Equal(
            internalRequest.Select(p => p.Key).Except(withheld).Except(["maskedFields", "projection"]).Order(),
            externalRequest.Select(p => p.Key).Except(["maskedFields", "projection"]).Order());

        JsonObject internalRevision = await client.GetOrFailAsync(sessions.ProjectManager, $"{Contributions}/{contributionId}");
        Assert.Equal(InternalNote, internalRevision["reviewInternalNote"]!.Text("text"));
        foreach (string path in new[] { $"{Contributions}/{contributionId}", $"{Contributions}?externalUpdateRequestId={requestId}", $"{Requests}/{requestId}", Requests })
        {
            using HttpResponseMessage read = await client.GetAsync(path, sessions.EntityA);
            string body = await read.Content.ReadAsStringAsync();
            Assert.Equal(HttpStatusCode.OK, read.StatusCode);
            Assert.DoesNotContain(InternalNote, body, StringComparison.Ordinal);
            Assert.DoesNotContain("reviewInternalNote\":", body, StringComparison.Ordinal);
            Assert.DoesNotContain("reviewedByUserId\":", body, StringComparison.Ordinal);
        }

        JsonObject externalRevision = await client.GetOrFailAsync(sessions.EntityA, $"{Contributions}/{contributionId}");
        Assert.Equal(("RETURNED", "Say which access road."), (externalRevision.Text("status"), externalRevision["reviewReason"]!.Text("text")));
    }

    private static async Task<(HttpStatusCode, string?)> RefusalOfAsync(Task<HttpResponseMessage> call)
    {
        using HttpResponseMessage response = await call;
        return await response.RefusalAsync();
    }
}
