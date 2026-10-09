using System.Net;
using System.Text.Json.Nodes;
using Npgsql;
using PMPlatform.Tests.Integration.Identity;
using static PMPlatform.Tests.Integration.ExternalParticipation.ExternalParticipationDriver;

namespace PMPlatform.Tests.Integration.ExternalParticipation;

/// <summary>
/// TASK-066 acceptance criterion 2 and its validation check: a reviewer cannot silently edit a submitted contribution's values — the only
/// paths are Accept, Return or Reject, and a return produces a new revision (WF-13 EXT-P-10, BR-EXT-010 to BR-EXT-012). The reviewer is the
/// request's assigned AHDA user, never anyone else by role alone (EXT-CC-10, BR-EXT-046).
/// </summary>
[Collection(ExternalParticipationSuite.Name)]
public sealed class ContributionReviewTests(ExternalParticipationTestHost host)
{
    /// <summary>
    /// The workbook's check: the reviewer tries to edit the submitted values — refused, as is the responder now; the database refuses a direct
    /// write too. Returning opens revision 2 with the same values for the entity to correct; revision 1 stays exactly as submitted. Revision
    /// 2, corrected and resubmitted, is accepted; neither revision's values ever changed after its submission.
    /// </summary>
    [Fact]
    public async Task AReviewerCannotEditASubmittedRevisionOnlyAcceptReturnOrReject()
    {
        using HttpClient client = host.Api.CreateClient();
        ParticipationSessions sessions = await client.SignInAsync();
        Guid projectId = await host.ProjectAsync();
        await host.GrantProjectAsync(8, projectId);
        Guid requestId = await client.IssuedRequestAsync(sessions, projectId, ExternalParticipationTestHost.EntityA, ExternalParticipationTestHost.InformationTypeId, null, responder: 8);
        JsonObject submitted = await client.AnswerAsync(sessions.EntityA, requestId, Information("Handover on 3 November."));
        Guid first = AdministrationApi.IdOf(submitted);
        await client.OkOrFailAsync(sessions.ProjectManager, $"{Contributions}/{first}/start-review");

        using (HttpResponseMessage reviewerEdit = await client.PutAsync($"{Contributions}/{first}", sessions.ProjectManager, new { fields = Information("Handover on 30 November.") }, null))
        {
            Assert.Equal((HttpStatusCode.Forbidden, "PERMISSION_DENIED"), await reviewerEdit.RefusalAsync());
        }

        using (HttpResponseMessage read = await client.GetAsync($"{Contributions}/{first}", sessions.EntityA))
        using (HttpResponseMessage responderEdit = await client.PutAsync($"{Contributions}/{first}", sessions.EntityA, new { fields = Information("Handover on 30 November.") },
                   AdministrationApi.ETagOf(read)))
        {
            Assert.Equal((HttpStatusCode.Conflict, "CONTRIBUTION_NOT_EDITABLE"), await responderEdit.RefusalAsync());
        }

        PostgresException refused = await Assert.ThrowsAsync<PostgresException>(() => host.Database.ExecuteAsync(
            $"UPDATE external_participation.external_contribution_field SET proposed_value = 'Handover on 30 November.' WHERE external_contribution_id = '{first}'"));
        Assert.Equal(PostgresErrorCodes.RestrictViolation, refused.SqlState);

        JsonObject returned = await client.OkOrFailAsync(sessions.ProjectManager, $"{Contributions}/{first}/return",
            new { reason = new { text = "Which handover: the site or the building?", language = "en" } });
        Assert.Equal(("RETURNED", "Handover on 3 November."), (returned.Text("status"), returned["fields"]![0]!.Text("value")));

        JsonArray revisions = (await client.GetOrFailAsync(sessions.EntityA, $"{Contributions}?externalUpdateRequestId={requestId}"))["items"]!.AsArray();
        JsonNode second = revisions[0]!;
        Assert.Equal((2, "DRAFT", first.ToString(), "Handover on 3 November."),
            (second["revisionNo"]!.GetValue<int>(), second.Text("status"), second.Text("previousRevisionId"), second["fields"]![0]!.Text("value")));
        Assert.Equal("IN_PROGRESS", (await client.GetOrFailAsync(sessions.EntityA, $"{Requests}/{requestId}")).Text("status"));

        Guid secondId = Guid.Parse(second.Text("id"));
        using (HttpResponseMessage read = await client.GetAsync($"{Contributions}/{secondId}", sessions.EntityA))
        using (HttpResponseMessage corrected = await client.PutAsync($"{Contributions}/{secondId}", sessions.EntityA,
                   new { fields = Information("Site handover on 3 November; building on 20 November.") }, AdministrationApi.ETagOf(read)))
        {
            Assert.Equal(HttpStatusCode.OK, corrected.StatusCode);
        }

        await client.OkOrFailAsync(sessions.EntityA, $"{Contributions}/{secondId}/submit");
        JsonObject accepted = await client.DecideAsync(sessions.ProjectManager, secondId, "accept");
        Assert.Equal("APPLIED", accepted.Text("status"));
        Assert.Equal("CLOSED", (await client.GetOrFailAsync(sessions.EntityA, $"{Requests}/{requestId}")).Text("status"));

        Assert.Equal(
            ["1|RETURNED|Handover on 3 November.", "2|APPLIED|Site handover on 3 November; building on 20 November."],
            await host.Database.QueryAsync($"""
                SELECT c.revision_no || '|' || c.status || '|' || f.proposed_value FROM external_participation.external_contribution c
                JOIN external_participation.external_contribution_field f ON f.external_contribution_id = c.id
                WHERE c.external_update_request_id = '{requestId}' ORDER BY c.revision_no
                """));
        Assert.Equal(
            ["ExternalParticipation.RequestCreated", "ExternalParticipation.RequestIssued", "ExternalParticipation.ContributionStarted",
             "ExternalParticipation.ContributionSubmitted", "ExternalParticipation.ReviewStarted", "ExternalParticipation.ContributionReturned",
             "ExternalParticipation.ContributionChanged", "ExternalParticipation.ContributionSubmitted", "ExternalParticipation.ReviewStarted",
             "ExternalParticipation.ContributionAccepted"],
            await host.AuditEventsAsync(requestId));
    }

    /// <summary>
    /// Only the request's assigned AHDA reviewer decides: not the System Administrator by role (BR-EXT-046), not a Department Manager who could
    /// review but is not assigned, not the entity, and not anyone once decided — the decision is the revision's one review.
    /// </summary>
    [Fact]
    public async Task OnlyTheAssignedReviewerDecidesAndOnlyOnce()
    {
        using HttpClient client = host.Api.CreateClient();
        ParticipationSessions sessions = await client.SignInAsync();
        Guid projectId = await host.ProjectAsync();
        await host.GrantProjectAsync(8, projectId);
        Guid requestId = await client.IssuedRequestAsync(sessions, projectId, ExternalParticipationTestHost.EntityA, ExternalParticipationTestHost.InformationTypeId, null, responder: 8);
        Guid contributionId = AdministrationApi.IdOf(await client.AnswerAsync(sessions.EntityA, requestId, Information("Design approved by the municipality.")));

        foreach ((string who, HttpStatusCode status) in new[]
                 {
                     (sessions.Administrator, HttpStatusCode.Forbidden), (sessions.EntityA, HttpStatusCode.Forbidden), (sessions.DepartmentManager, HttpStatusCode.Forbidden),
                 })
        {
            using HttpResponseMessage start = await client.PostAsync($"{Contributions}/{contributionId}/start-review", who);
            Assert.Equal(status, start.StatusCode);
        }

        // Named reviewer, the Department Manager reviews; the Project Manager, no longer named, does not.
        await client.OkOrFailAsync(sessions.ProjectManager, $"{Requests}/{requestId}/assign-reviewer", new { reviewerUserId = Person(3) });
        using (HttpResponseMessage notNamed = await client.PostAsync($"{Contributions}/{contributionId}/start-review", sessions.ProjectManager))
        {
            Assert.Equal(HttpStatusCode.Forbidden, notNamed.StatusCode);
        }

        JsonObject rejected = await client.DecideAsync(sessions.DepartmentManager, contributionId, "reject", reason: "Not the document we asked for.");
        Assert.Equal(("REJECTED", "Not the document we asked for."), (rejected.Text("status"), rejected["reviewReason"]!.Text("text")));
        Assert.Equal("CLOSED", (await client.GetOrFailAsync(sessions.ProjectManager, $"{Requests}/{requestId}")).Text("status"));

        foreach (string decision in new[] { "accept", "return", "reject" })
        {
            using HttpResponseMessage again = await client.PostAsync($"{Contributions}/{contributionId}/{decision}", sessions.DepartmentManager,
                new { reason = new { text = "Second thoughts.", language = "en" } });
            Assert.Equal((HttpStatusCode.Conflict, "CONTRIBUTION_ALREADY_DECIDED"), await again.RefusalAsync());
        }

        using HttpResponseMessage reasonless = await client.PostAsync($"{Contributions}/{contributionId}/return", sessions.DepartmentManager, new { });
        Assert.Equal((HttpStatusCode.BadRequest, "VALIDATION_FAILED"), await reasonless.RefusalAsync());
    }

    /// <summary>
    /// ADR-013 and the gate decision: AHDA issues requests and reviews them. An entity user holding R04 on a project — the entity Project Manager
    /// — is refused drafting a request whatever they hold, and the refusal is audited.
    /// </summary>
    [Fact]
    public async Task AnEntityProjectManagerNeitherRequestsNorReviews()
    {
        using HttpClient client = host.Api.CreateClient();
        ParticipationSessions sessions = await client.SignInAsync();
        Guid entityProjectId = Guid.Parse(IdentityDatabase.EntityProjectId);

        using HttpResponseMessage drafted = await client.PostAsync(Requests, sessions.EntityA,
            RequestBody(entityProjectId, ExternalParticipationTestHost.EntityA, ExternalParticipationTestHost.InformationTypeId, null, responder: null));
        Assert.Equal((HttpStatusCode.Forbidden, "PERMISSION_DENIED"), await drafted.RefusalAsync());
        Assert.Contains("EXTERNAL_USER", await host.Database.QueryAsync($"""
            SELECT a.new_value FROM audit_activity.audit_event e JOIN audit_activity.audit_event_attribute a ON a.audit_event_id = e.id
            WHERE e.event_type = 'ExternalParticipation.AuthorityRefused' AND e.actor_user_id = '{Person(8)}' AND a.attribute_name = 'reason'
            """));
    }
}
