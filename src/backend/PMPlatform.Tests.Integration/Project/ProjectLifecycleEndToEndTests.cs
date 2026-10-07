using System.Net;
using System.Text.Json.Nodes;
using PMPlatform.Application.Common.Auditing;
using PMPlatform.Application.Features.Project;
using PMPlatform.Tests.Integration.AuditActivity;
using PMPlatform.Tests.Integration.Identity;
using PMPlatform.Tests.Integration.Schedule;

namespace PMPlatform.Tests.Integration.Project;

/// <summary>
/// TASK-043: the Project lifecycle end to end across WF-01, WF-11 and FG-06, through the API as the SPA and the approver
/// reach it. Only the outbox dispatch is driven in process, as its worker would. One run takes a draft through every
/// edge of <see cref="ProjectLifecycle"/> to ACTIVE; each attempt at a transition by someone not entitled to it is
/// refused, changes nothing, and is audited.
/// </summary>
[Collection(ProjectSuite.Name)]
public sealed class ProjectLifecycleEndToEndTests(ProjectTestHost host)
{
    private const string ApprovalInstances = "/api/v1/approval-instances";
    private const string ApprovalTasks = "/api/v1/approval-tasks";
    private const string SuspensionRequests = "/api/v1/suspension-requests";

    /// <summary>Where a refused attempt's project is registered, relative to the people of <see cref="ProjectTestHost"/>.</summary>
    public enum Placement
    {
        /// <summary>local.r08's entity, local.r03's department.</summary>
        Own,

        /// <summary>local.r08's entity, a department local.r03 does not manage.</summary>
        OtherDepartment,

        /// <summary>Another entity, local.r03's department.</summary>
        OtherEntity,
    }

    /// <summary>
    /// The happy path, with a withdrawal and a returned review on the way so that every edge is taken: DRAFT → SUBMITTED →
    /// DRAFT → SUBMITTED → UNDER_REVIEW → RETURNED → SUBMITTED (revision 2) → UNDER_REVIEW → APPROVED_PLANNED → ACTIVE, then
    /// WF-09's ACTIVE → SUSPENDED → ACTIVE, each by an approved request activated apart from its approval (TASK-062). Each step
    /// is checked in WF-01 (the project), WF-11 (its review runs) and FG-06 (its audit trail).
    /// </summary>
    [Fact]
    public async Task ADraftReachesActiveThroughEveryTransitionAndEachIsAudited()
    {
        using HttpClient client = host.Api.CreateClient();
        Sessions sessions = await client.SignInAsync();
        object manager = new { projectManagerUserId = ProjectDriver.Person(8) };

        JsonObject draft = await client.CreateOrFailAsync(sessions.Entity, host.Registration());
        Guid projectId = AdministrationApi.IdOf(draft);
        Assert.Equal("DRAFT", draft.Status());
        Assert.Equal("SUBMITTED", (await client.CommandOrFailAsync(sessions.Entity, projectId, "submit", manager)).Status());
        Assert.Equal("DRAFT", (await client.CommandOrFailAsync(sessions.Entity, projectId, "withdraw")).Status());
        Assert.Equal("SUBMITTED", (await client.CommandOrFailAsync(sessions.Entity, projectId, "submit", manager)).Status());
        Assert.Equal("UNDER_REVIEW", (await client.CommandOrFailAsync(sessions.Reviewer, projectId, "start-review")).Status());

        Guid firstRun = await DecideAsync(client, sessions.Approver, projectId, "return", new { reason = new { text = "Budget breakdown missing", language = "en" } });
        JsonObject returned = await GetAsync(client, sessions.Entity, projectId);
        Assert.Equal(("RETURNED", null), (returned.Status(), returned.FormalProjectId()));

        JsonObject resubmitted = await client.CommandOrFailAsync(sessions.Entity, projectId, "submit", manager);
        Assert.Equal(("SUBMITTED", 2), (resubmitted.Status(), resubmitted["revisionNo"]!.GetValue<int>()));
        Assert.Equal("UNDER_REVIEW", (await client.CommandOrFailAsync(sessions.Reviewer, projectId, "start-review")).Status());

        Guid secondRun = await DecideAsync(client, sessions.Approver, projectId, "approve");
        JsonObject approved = await GetAsync(client, sessions.Entity, projectId);
        Assert.Equal("APPROVED_PLANNED", approved.Status());
        Assert.Matches("^PRJ-[0-9]{6,}$", approved.FormalProjectId());

        // ADR-009: WF-03's ACTIVE baseline is the activation precondition (schedule-baseline.md D-11).
        await ScheduleFixture.ActiveBaselineAsync(host.Database, projectId);
        JsonObject active = await client.CommandOrFailAsync(sessions.Approver, projectId, "activate");
        Assert.Equal(("ACTIVE", approved.FormalProjectId()), (active.Status(), active.FormalProjectId()));

        // WF-09 (edge 7): the Department Manager raises, AHDA's approver decides through WF-11, and only the activation moves the project.
        // activated_at is compared as the database holds it (microseconds), not as the activation's response carried it.
        string activatedAt = (await GetAsync(client, sessions.Entity, projectId))["activatedAt"]!.GetValue<string>();
        await SuspendOrResumeAsync(client, sessions, projectId, "SUSPEND");
        Assert.Equal("SUSPENDED", (await GetAsync(client, sessions.Entity, projectId)).Status());
        await SuspendOrResumeAsync(client, sessions, projectId, "RESUME");
        JsonObject resumed = await GetAsync(client, sessions.Entity, projectId);
        Assert.Equal(("ACTIVE", activatedAt), (resumed.Status(), resumed["activatedAt"]!.GetValue<string>()));

        // WF-11: one run per revision, the second linked to the first, each ended by local.r02's decision.
        JsonObject first = await GetAsync(client, sessions.Approver, $"{ApprovalInstances}/{firstRun}");
        JsonObject second = await GetAsync(client, sessions.Approver, $"{ApprovalInstances}/{secondRun}");
        Assert.Equal(
            [(1, "RETURNED", null, "RETURNED"), (2, "APPROVED", firstRun.ToString(), "APPROVED")],
            new[] { first, second }.Select(run => (
                run["subject"]!["revisionNo"]!.GetValue<int>(),
                run["status"]!.GetValue<string>(),
                run["previousInstanceId"]?.GetValue<string>(),
                Assert.Single(run["tasks"]!.AsArray())!["status"]!.GetValue<string>())));

        // FG-06: every change of the project, in order, by whom, from which state to which.
        string entity = ProjectDriver.Person(8).ToString(), reviewer = ProjectDriver.Person(3).ToString(), approver = ProjectDriver.Person(2).ToString();
        Assert.Equal(
            [
                $"Project.ProjectCreated|DATA_CHANGE|SUCCESS|{entity}|>DRAFT",
                $"Project.ProjectSubmitted|LIFECYCLE_TRANSITION|SUCCESS|{entity}|DRAFT>SUBMITTED",
                $"Project.SubmissionWithdrawn|LIFECYCLE_TRANSITION|SUCCESS|{entity}|SUBMITTED>DRAFT",
                $"Project.ProjectSubmitted|LIFECYCLE_TRANSITION|SUCCESS|{entity}|DRAFT>SUBMITTED",
                $"Project.ReviewStarted|LIFECYCLE_TRANSITION|SUCCESS|{reviewer}|SUBMITTED>UNDER_REVIEW",
                $"Project.RegistrationReturned|LIFECYCLE_TRANSITION|SUCCESS|{approver}|UNDER_REVIEW>RETURNED",
                $"Project.ProjectSubmitted|LIFECYCLE_TRANSITION|SUCCESS|{entity}|RETURNED>SUBMITTED",
                $"Project.ReviewStarted|LIFECYCLE_TRANSITION|SUCCESS|{reviewer}|SUBMITTED>UNDER_REVIEW",
                $"Project.RegistrationApproved|LIFECYCLE_TRANSITION|SUCCESS|{approver}|UNDER_REVIEW>APPROVED_PLANNED",
                $"Project.ProjectActivated|LIFECYCLE_TRANSITION|SUCCESS|{approver}|APPROVED_PLANNED>ACTIVE",
                $"Project.ProjectSuspended|LIFECYCLE_TRANSITION|SUCCESS|{approver}|ACTIVE>SUSPENDED",
                $"Project.ProjectResumed|LIFECYCLE_TRANSITION|SUCCESS|{approver}|SUSPENDED>ACTIVE",
            ],
            await TrailAsync(projectId));
        Assert.Empty(await host.Database.QueryAsync(AuditStore.BrokenChainLinks));

        // The acceptance criterion, read from the machine itself: an edge added to ProjectLifecycle fails here until this run takes it.
        Assert.Equal(
            ProjectLifecycle.Transitions.Select(t => $"{AuditValue.Format(t.From)}>{AuditValue.Format(t.To)}").Order(),
            (await TrailAsync(projectId)).Where(e => e.Contains("|LIFECYCLE_TRANSITION|", StringComparison.Ordinal)).Select(e => e[(e.LastIndexOf('|') + 1)..]).Distinct().Order());
    }

    /// <summary>
    /// A transition asked for by someone the lifecycle does not let take it: refused with R-47's status (403 for a project
    /// the caller sees, 404 for one they do not), the project unchanged to the byte, no transition audited, and the refusal
    /// itself audited as an authorization denial (CTL-25).
    /// </summary>
    [Theory]
    [InlineData(6, "submit", "DRAFT", Placement.Own, HttpStatusCode.Forbidden)] // a role with no project permission
    [InlineData(3, "activate", "APPROVED_PLANNED", Placement.Own, HttpStatusCode.Forbidden)] // an AHDA reviewer, who may not activate
    [InlineData(8, "start-review", "SUBMITTED", Placement.Own, HttpStatusCode.Forbidden)] // ADR-013: an entity user, granted review
    [InlineData(8, "activate", "APPROVED_PLANNED", Placement.Own, HttpStatusCode.Forbidden)] // ADR-013: an entity user, granted activation
    [InlineData(3, "start-review", "SUBMITTED", Placement.OtherDepartment, HttpStatusCode.NotFound)] // a reviewer of another department
    [InlineData(8, "withdraw", "SUBMITTED", Placement.OtherEntity, HttpStatusCode.NotFound)] // an entity user, of another entity
    public async Task AnUnauthorizedTransitionIsRefusedAndChangesNothing(int person, string command, string state, Placement placement, HttpStatusCode refused)
    {
        using HttpClient client = host.Api.CreateClient();
        Sessions sessions = await client.SignInAsync();
        Guid projectId = await ProjectInAsync(client, sessions, state, placement);
        JsonObject before = await host.RowAsync(projectId);
        IReadOnlyList<string> trail = await TrailAsync(projectId);
        string token = (await client.SignInOrFailAsync(person)).AccessToken;

        Guid correlationId = Guid.NewGuid();
        using (HttpClient attempt = host.Api.CreateClient().WithCorrelationId(correlationId))
        using (HttpResponseMessage response = await attempt.PostAsync(
                   $"{ProjectDriver.Projects}/{projectId}/{command}", token, command == "submit" ? new { projectManagerUserId = ProjectDriver.Person(8) } : null))
        {
            Assert.Equal(refused, response.StatusCode);
        }

        Assert.Equal(before.ToJsonString(), (await host.RowAsync(projectId)).ToJsonString());
        Assert.Equal(trail.Where(Succeeded), (await TrailAsync(projectId)).Where(Succeeded));
        string denial = Assert.Single(await host.Database.EventsAsync(correlationId));
        Assert.StartsWith($"AUTHORIZATION_DENIAL|", denial, StringComparison.Ordinal);
        Assert.Contains($"|DENIED|{ProjectDriver.Person(person)}|", denial, StringComparison.Ordinal);
    }

    /// <summary>
    /// WF-11 holds the review: neither the AHDA reviewer who started it (the requester, who may not decide their own
    /// request) nor the entity user whose project it is may decide it. The project stays UNDER_REVIEW with no identifier.
    /// </summary>
    [Theory]
    [InlineData(2, 2)]
    [InlineData(3, 8)]
    public async Task OnlyTheRoutedApproverDecidesTheReview(int startedBy, int decidedBy)
    {
        using HttpClient client = host.Api.CreateClient();
        Sessions sessions = await client.SignInAsync();
        Guid projectId = await ProjectInAsync(client, sessions, "SUBMITTED", Placement.Own);
        await client.CommandOrFailAsync((await client.SignInOrFailAsync(startedBy)).AccessToken, projectId, "start-review");
        (Guid runId, Guid taskId) = await PendingTaskAsync(client, sessions.Approver, ("Project", "Project", projectId));

        Guid correlationId = Guid.NewGuid();
        using (HttpClient attempt = host.Api.CreateClient().WithCorrelationId(correlationId))
        using (HttpResponseMessage response = await attempt.PostAsync($"{ApprovalTasks}/{taskId}/approve", (await client.SignInOrFailAsync(decidedBy)).AccessToken))
        {
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        JsonObject project = await GetAsync(client, sessions.Approver, projectId);
        Assert.Equal(("UNDER_REVIEW", null), (project.Status(), project.FormalProjectId()));
        Assert.Equal("PENDING", (await GetAsync(client, sessions.Approver, $"{ApprovalInstances}/{runId}"))["status"]!.GetValue<string>());
        string denial = Assert.Single(await host.Database.EventsAsync(correlationId));
        Assert.StartsWith("AUTHORIZATION_DENIAL|", denial, StringComparison.Ordinal);
        Assert.Contains($"|DENIED|{ProjectDriver.Person(decidedBy)}|", denial, StringComparison.Ordinal);
    }

    /// <summary>A project in <paramref name="state"/>: DRAFT, SUBMITTED, or APPROVED_PLANNED (reviewed by local.r03, approved by local.r02 through the API).</summary>
    private async Task<Guid> ProjectInAsync(HttpClient client, Sessions sessions, string state, Placement placement)
    {
        (string registrant, JsonObject registration, int manager) = placement switch
        {
            Placement.Own => (sessions.Entity, host.Registration(), 8),
            Placement.OtherDepartment => (sessions.Entity, host.Registration(change: r => r["departmentId"] = ProjectTestHost.OtherDepartmentId.ToString()), 8),
            Placement.OtherEntity => (sessions.Reviewer, host.Registration(change: r => r["externalEntityId"] = ProjectTestHost.OtherEntityId.ToString()), 5),
            _ => throw new ArgumentOutOfRangeException(nameof(placement)),
        };
        Guid projectId = AdministrationApi.IdOf(await client.CreateOrFailAsync(registrant, registration));
        if (state != "DRAFT")
        {
            await client.CommandOrFailAsync(registrant, projectId, "submit", new { projectManagerUserId = ProjectDriver.Person(manager) });
        }

        if (state == "APPROVED_PLANNED")
        {
            await client.CommandOrFailAsync(sessions.Reviewer, projectId, "start-review");
            await DecideAsync(client, sessions.Approver, projectId, "approve");
        }

        Assert.Equal(state, (await host.RowAsync(projectId))["lifecycle_state"]!.GetValue<string>());
        return projectId;
    }

    /// <summary>
    /// WF-09: a request raised and put to review by the Department Manager, approved by local.r02 through WF-11 — which leaves the project
    /// as it is — and then activated by local.r02, which moves it.
    /// </summary>
    private async Task SuspendOrResumeAsync(HttpClient client, Sessions sessions, Guid projectId, string requestType)
    {
        string effective = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        using HttpResponseMessage raised = await client.PostAsync(SuspensionRequests, sessions.Reviewer, new
        {
            projectId,
            requestType,
            reason = new { text = "The board has decided.", language = "en" },
            requestedEffectiveDate = effective,
        });
        Assert.True(raised.StatusCode == HttpStatusCode.Created, $"raise: {(int)raised.StatusCode} {await raised.Content.ReadAsStringAsync()}");
        Guid requestId = AdministrationApi.IdOf(await raised.ReadObjectAsync());
        foreach (string command in new[] { "submit", "start-review" })
        {
            using HttpResponseMessage moved = await client.PostAsync($"{SuspensionRequests}/{requestId}/{command}", sessions.Reviewer);
            Assert.True(moved.StatusCode == HttpStatusCode.OK, $"{command}: {(int)moved.StatusCode} {await moved.Content.ReadAsStringAsync()}");
        }

        string before = (await GetAsync(client, sessions.Entity, projectId)).Status();
        await DecideAsync(client, sessions.Approver, ("Suspension", "SuspensionRequest", requestId), "approve");
        Assert.Equal(before, (await GetAsync(client, sessions.Entity, projectId)).Status());
        using HttpResponseMessage activated = await client.PostAsync($"{SuspensionRequests}/{requestId}/activate", sessions.Approver);
        Assert.True(activated.StatusCode == HttpStatusCode.OK, $"activate: {(int)activated.StatusCode} {await activated.Content.ReadAsStringAsync()}");
    }

    /// <summary>Decides the review's open task through the approval API and delivers the outcome, as the outbox worker would; returns the run.</summary>
    private Task<Guid> DecideAsync(HttpClient client, string token, Guid projectId, string decision, object? body = null) =>
        DecideAsync(client, token, ("Project", "Project", projectId), decision, body);

    private async Task<Guid> DecideAsync(HttpClient client, string token, (string Module, string Type, Guid Id) subject, string decision, object? body = null)
    {
        (Guid runId, Guid taskId) = await PendingTaskAsync(client, token, subject);
        using (HttpResponseMessage decided = await client.PostAsync($"{ApprovalTasks}/{taskId}/{decision}", token, body))
        {
            Assert.True(decided.StatusCode == HttpStatusCode.OK, $"{decision}: {(int)decided.StatusCode} {await decided.Content.ReadAsStringAsync()}");
        }

        Assert.True(await host.DeliverAsync(runId));
        return runId;
    }

    /// <summary>The subject's pending review run, and its open task, as SCR-115 shows them.</summary>
    private static async Task<(Guid RunId, Guid TaskId)> PendingTaskAsync(HttpClient client, string token, (string Module, string Type, Guid Id) subject)
    {
        JsonObject runs = await GetAsync(client, token, $"{ApprovalInstances}?subjectModule={subject.Module}&subjectType={subject.Type}&subjectId={subject.Id}&status=PENDING");
        Guid runId = AdministrationApi.IdOf(Assert.Single(runs["items"]!.AsArray())!.AsObject());
        JsonObject run = await GetAsync(client, token, $"{ApprovalInstances}/{runId}");
        return (runId, AdministrationApi.IdOf(Assert.Single(run["tasks"]!.AsArray(), t => t!["status"]!.GetValue<string>() == "PENDING")!.AsObject()));
    }

    /// <summary>The project's audit events, oldest first, as <c>type|class|outcome|actor|old&gt;new state</c>.</summary>
    private Task<IReadOnlyList<string>> TrailAsync(Guid projectId) => host.Database.QueryAsync($"""
        SELECT concat_ws('|', e.event_type, e.event_class, e.outcome, coalesce(e.actor_user_id::text, ''), coalesce(s.old_value, '') || '>' || coalesce(s.new_value, ''))
        FROM audit_activity.audit_event e
        LEFT JOIN audit_activity.audit_event_attribute s ON s.audit_event_id = e.id AND s.attribute_name = 'status'
        WHERE e.subject_module = 'Project' AND e.subject_id = '{projectId}'
        ORDER BY e.recorded_at, e.id
        """);

    private static bool Succeeded(string trailEntry) => trailEntry.Contains("|SUCCESS|", StringComparison.Ordinal);

    private static Task<JsonObject> GetAsync(HttpClient client, string token, Guid projectId) => GetAsync(client, token, $"{ProjectDriver.Projects}/{projectId}");

    private static async Task<JsonObject> GetAsync(HttpClient client, string token, string path)
    {
        using HttpResponseMessage response = await client.GetAsync(path, token);
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"GET {path}: {(int)response.StatusCode}");
        return await response.ReadObjectAsync();
    }
}
