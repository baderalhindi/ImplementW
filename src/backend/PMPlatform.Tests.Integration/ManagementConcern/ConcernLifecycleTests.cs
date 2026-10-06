using System.Net;
using System.Text.Json.Nodes;
using PMPlatform.Tests.Integration.Identity;
using PMPlatform.Tests.Integration.Risk;

namespace PMPlatform.Tests.Integration.ManagementConcern;

/// <summary>
/// The row's lifecycle — Open → Assigned → In Progress → Pending Validation → Resolved → Closed — with the resolution validated
/// through WF-11, an issue and a challenge alike; ADR-013's intake and visibility; and ADR-015's review cadence.
/// </summary>
[Collection(ConcernSuite.Name)]
public sealed class ConcernLifecycleTests(ConcernTestHost host)
{
    /// <summary>
    /// A challenge from OPEN to CLOSED. Its resolution goes to WF-11 and is decided by local.r02 (R02, the route) — not by its
    /// submitter: a return sends it back to IN_PROGRESS as revision 2 with the resolution kept for correction; the approval of the
    /// resubmitted resolution makes it RESOLVED. While it is with validation its fields are fixed; once CLOSED it takes no command.
    /// </summary>
    [Fact]
    public async Task AResolutionIsValidatedThroughWf11BeforeTheConcernCloses()
    {
        using HttpClient client = host.Api.CreateClient();
        ConcernSessions sessions = await client.SignInAsync();
        Guid projectId = await host.ProjectAsync();
        Guid concernId = AdministrationApi.IdOf(await client.CreatedOrFailAsync(
            sessions.InternalManager, ConcernDriver.Concerns, ConcernDriver.ConcernBody(projectId, type: "CHALLENGE", target: ConcernDriver.Today.AddDays(30))));

        await client.StartAsync(sessions, concernId);
        JsonObject submitted = await client.CommandOrFailAsync(sessions.InternalManager, concernId, "submit-resolution", ConcernDriver.Resolution("Temporary access agreed with the utility owner."));
        Assert.Equal(("PENDING_VALIDATION", 1, "CHALLENGE"), (submitted.Text("status"), submitted["revisionNo"]!.GetValue<int>(), submitted.Text("concernType")));

        using (HttpResponseMessage reassess = await client.CommandAsync(sessions.Officer, concernId, "assess", new { impacts = ConcernDriver.Impacts(await host.DimensionsAsync(), 2) }))
        {
            await reassess.AssertStatusAsync(HttpStatusCode.Conflict, "CONCERN_NOT_EDITABLE");
        }

        await host.DecideAsync(client, sessions, concernId, "return");
        JsonObject returned = await client.ConcernAsync(sessions.InternalManager, concernId);
        Assert.Equal(("IN_PROGRESS", 2), (returned.Text("status"), returned["revisionNo"]!.GetValue<int>()));
        Assert.Equal("Temporary access agreed with the utility owner.", returned["resolution"]!.Text("text"));

        await client.CommandOrFailAsync(sessions.InternalManager, concernId, "submit-resolution", ConcernDriver.Resolution("Permanent access road built and handed over."));
        await host.DecideAsync(client, sessions, concernId, "approve");
        JsonObject resolved = await client.ConcernAsync(sessions.InternalManager, concernId);
        Assert.Equal(("RESOLVED", 2), (resolved.Text("status"), resolved["revisionNo"]!.GetValue<int>()));
        Assert.NotNull(resolved["resolvedAt"]);

        JsonObject closed = await client.CommandOrFailAsync(sessions.InternalManager, concernId, "close");
        Assert.Equal("CLOSED", closed.Text("status"));
        using (HttpResponseMessage review = await client.CommandAsync(sessions.InternalManager, concernId, "review"))
        {
            await review.AssertStatusAsync(HttpStatusCode.Conflict, "CONCERN_CLOSED");
        }

        Assert.Equal(
            ["ManagementConcern.ConcernRaised", "ManagementConcern.ConcernAssigned", "ManagementConcern.WorkStarted", "ManagementConcern.ResolutionSubmitted",
             "ManagementConcern.ResolutionReturned", "ManagementConcern.ResolutionSubmitted", "ManagementConcern.ResolutionValidated", "ManagementConcern.ConcernClosed"],
            await host.AuditEventsAsync(concernId));
        Assert.Equal(["RETURNED|1", "APPROVED|2"], await host.Database.QueryAsync(
            $"SELECT status || '|' || subject_revision_no FROM approval.approval_instance WHERE subject_id = '{concernId}' ORDER BY subject_revision_no"));
    }

    /// <summary>A step out of order is refused: no start before an assignment, no close before validation; an OPEN escalation keeps a resolved concern open.</summary>
    [Fact]
    public async Task AConcernMovesOnlyAlongItsStateMachine()
    {
        using HttpClient client = host.Api.CreateClient();
        ConcernSessions sessions = await client.SignInAsync();
        Guid concernId = await client.RaiseAsync(sessions.InternalManager, await host.ProjectAsync());

        using (HttpResponseMessage start = await client.CommandAsync(sessions.InternalManager, concernId, "start"))
        {
            await start.AssertStatusAsync(HttpStatusCode.Conflict, "INVALID_TRANSITION");
        }

        using (HttpResponseMessage unknown = await client.CommandAsync(sessions.InternalManager, concernId, "assign", new { assigneeUserId = Guid.NewGuid() }))
        {
            await unknown.AssertStatusAsync(HttpStatusCode.UnprocessableEntity, "CONCERN_ASSIGNEE_NOT_ELIGIBLE");
        }

        await client.StartAsync(sessions, concernId);
        using (HttpResponseMessage close = await client.CommandAsync(sessions.InternalManager, concernId, "close"))
        {
            await close.AssertStatusAsync(HttpStatusCode.Conflict, "INVALID_TRANSITION");
        }

        await client.CommandOrFailAsync(sessions.InternalManager, concernId, "submit-resolution", ConcernDriver.Resolution("Done."));
        using (HttpResponseMessage escalated = await client.EscalateAsync(sessions.InternalManager, concernId, Guid.NewGuid()))
        {
            Assert.Equal(HttpStatusCode.Created, escalated.StatusCode);
        }

        await host.DecideAsync(client, sessions, concernId, "approve");
        using (HttpResponseMessage close = await client.CommandAsync(sessions.InternalManager, concernId, "close"))
        {
            await close.AssertStatusAsync(HttpStatusCode.Conflict, "CONCERN_ESCALATION_OPEN");
        }

        using HttpResponseMessage escalateResolved = await client.EscalateAsync(sessions.InternalManager, concernId, Guid.NewGuid());
        await escalateResolved.AssertStatusAsync(HttpStatusCode.Conflict, "CONCERN_NOT_ESCALATABLE");
    }

    /// <summary>
    /// ADR-013's amendment to TASK-057: entities raise a blocker or an issue on their own project and see its status — intake and
    /// visibility, not escalation. local.r08 raises and reads on their entity's project and nothing on another; managing, assessing and
    /// escalating are refused to them whatever they hold, and audited; the internal Project Manager with the same R04 grants escalates.
    /// </summary>
    [Fact]
    public async Task EntitiesRaiseAndSeeTheirConcernsButNeitherManageNorEscalateThem()
    {
        using HttpClient client = host.Api.CreateClient();
        ConcernSessions sessions = await client.SignInAsync();
        Guid entityProject = await host.ProjectAsync(projectManager: 8);
        Guid concernId = await client.RaiseAsync(sessions.EntityManager, entityProject, ConcernDriver.Impacts(await host.DimensionsAsync(), 3));

        Assert.Equal("OPEN", (await client.ConcernAsync(sessions.EntityManager, concernId)).Text("status"));
        Assert.Equal(
            [concernId.ToString()],
            (await client.GetOrFailAsync(sessions.EntityManager, $"{ConcernDriver.Concerns}?projectId={entityProject}"))["items"]!.AsArray().Select(c => c!.Text("id")));

        using (HttpResponseMessage assign = await client.CommandAsync(sessions.EntityManager, concernId, "assign", new { assigneeUserId = ConcernDriver.Person(8) }))
        {
            await assign.AssertStatusAsync(HttpStatusCode.Forbidden, "PERMISSION_DENIED");
        }

        using (HttpResponseMessage assess = await client.CommandAsync(sessions.EntityManager, concernId, "assess", new { impacts = ConcernDriver.Impacts(await host.DimensionsAsync(), 1) }))
        {
            await assess.AssertStatusAsync(HttpStatusCode.Forbidden, "PERMISSION_DENIED");
        }

        using (HttpResponseMessage escalate = await client.EscalateAsync(sessions.EntityManager, concernId, Guid.NewGuid()))
        {
            await escalate.AssertStatusAsync(HttpStatusCode.Forbidden, "PERMISSION_DENIED");
        }

        Assert.Equal(["CONCERN_MANAGE|EXTERNAL_USER", "CONCERN_MANAGE|EXTERNAL_USER", "CONCERN_ESCALATE|EXTERNAL_USER"], await host.Database.QueryAsync($"""
            SELECT p.new_value || '|' || r.new_value
            FROM audit_activity.audit_event e
            JOIN audit_activity.audit_event_attribute p ON p.audit_event_id = e.id AND p.attribute_name = 'permission'
            JOIN audit_activity.audit_event_attribute r ON r.audit_event_id = e.id AND r.attribute_name = 'reason'
            WHERE e.event_type = 'ManagementConcern.AuthorityRefused' AND e.subject_id = '{concernId}' ORDER BY e.occurred_at, e.id
            """));
        Assert.Equal(["0"], await host.Database.QueryAsync($"SELECT count(*)::text FROM management_concern.concern_escalation WHERE management_concern_id = '{concernId}'"));

        // Another project, of no entity and managed by local.r05: invisible to the entity, and closed to its intake.
        Guid otherProject = await host.ProjectAsync(ofEntity: false);
        using (HttpResponseMessage elsewhere = await client.PostAsync(ConcernDriver.Concerns, sessions.EntityManager, ConcernDriver.ConcernBody(otherProject)))
        {
            await elsewhere.AssertStatusAsync(HttpStatusCode.NotFound, "NOT_FOUND");
        }

        Assert.Empty((await client.GetOrFailAsync(sessions.EntityManager, $"{ConcernDriver.Concerns}?projectId={otherProject}"))["items"]!.AsArray());

        Guid internalConcern = await client.RaiseAsync(sessions.InternalManager, otherProject);
        using HttpResponseMessage escalatedInternally = await client.EscalateAsync(sessions.InternalManager, internalConcern, Guid.NewGuid());
        Assert.Equal(HttpStatusCode.Created, escalatedInternally.StatusCode);
    }

    /// <summary>
    /// ADR-015: a concern's review cadence follows its project's governance profile — every 7 days on a Standard project, every 30 on a
    /// Light one, which carries issues though not risks — counted when it is raised and again from each review.
    /// </summary>
    [Fact]
    public async Task TheReviewCadenceFollowsTheProjectsGovernanceProfile()
    {
        using HttpClient client = host.Api.CreateClient();
        ConcernSessions sessions = await client.SignInAsync();
        Guid standard = await client.RaiseAsync(sessions.InternalManager, await host.ProjectAsync(profile: "STANDARD"));
        Guid light = await client.RaiseAsync(sessions.InternalManager, await host.ProjectAsync(profile: "LIGHT"));

        Assert.Equal(ConcernDriver.Iso(ConcernDriver.Today.AddDays(ConcernTestHost.StandardCadenceDays)), (await client.ConcernAsync(sessions.InternalManager, standard)).Text("nextReviewDate"));
        Assert.Equal(ConcernDriver.Iso(ConcernDriver.Today.AddDays(ConcernTestHost.LightCadenceDays)), (await client.ConcernAsync(sessions.InternalManager, light)).Text("nextReviewDate"));

        await host.Database.ExecuteAsync($"UPDATE management_concern.management_concern SET next_review_date = current_date WHERE id = '{light}'");
        JsonObject reviewed = await client.CommandOrFailAsync(sessions.InternalManager, light, "review");
        Assert.Equal(ConcernDriver.Iso(ConcernDriver.Today.AddDays(ConcernTestHost.LightCadenceDays)), reviewed.Text("nextReviewDate"));
        Assert.NotNull(reviewed["lastReviewedAt"]);
        Assert.Equal("OPEN", reviewed.Text("status"));
    }

    /// <summary>What a concern names is checked: a draft category, a past target date; and nothing is raised before the project is approved.</summary>
    [Fact]
    public async Task AConcernNamesPublishedReferencesOnAnApprovedProject()
    {
        using HttpClient client = host.Api.CreateClient();
        ConcernSessions sessions = await client.SignInAsync();
        Guid projectId = await host.ProjectAsync();

        using (HttpResponseMessage draft = await client.PostAsync(ConcernDriver.Concerns, sessions.InternalManager, ConcernDriver.ConcernBody(projectId, category: ConcernTestHost.DraftCategoryId)))
        {
            await draft.AssertStatusAsync(HttpStatusCode.UnprocessableEntity, "CONCERN_CATEGORY_INVALID");
        }

        using (HttpResponseMessage past = await client.PostAsync(ConcernDriver.Concerns, sessions.InternalManager, ConcernDriver.ConcernBody(projectId, target: ConcernDriver.Today.AddDays(-1))))
        {
            await past.AssertStatusAsync(HttpStatusCode.UnprocessableEntity, "CONCERN_TARGET_DATE_INVALID");
        }

        using (HttpResponseMessage noType = await client.PostAsync(ConcernDriver.Concerns, sessions.InternalManager, ConcernDriver.ConcernBody(projectId, type: "RISK")))
        {
            Assert.Equal(["concernType OUT_OF_RANGE"], await noType.ReadFieldErrorsAsync());
        }

        using HttpResponseMessage submitted = await client.PostAsync(ConcernDriver.Concerns, sessions.InternalManager, ConcernDriver.ConcernBody(await host.ProjectAsync(state: "SUBMITTED")));
        await submitted.AssertStatusAsync(HttpStatusCode.UnprocessableEntity, "CONCERN_PROJECT_NOT_ELIGIBLE");
    }
}
