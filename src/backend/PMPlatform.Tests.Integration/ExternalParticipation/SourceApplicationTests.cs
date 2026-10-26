using System.Net;
using System.Text.Json.Nodes;
using PMPlatform.Tests.Integration.Identity;
using static PMPlatform.Tests.Integration.ExternalParticipation.ExternalParticipationDriver;

namespace PMPlatform.Tests.Integration.ExternalParticipation;

/// <summary>
/// TASK-066 acceptance criterion 3: source application uses an allowlisted typed adapter with concurrency and idempotency protection. An
/// entity reports a WF-04 task's percentage; AHDA accepts it; the TASK_PROGRESS adapter applies it through WF-04's own command, and nothing
/// else of the task (WF-13 EXT-P-16, EXT-P-17, BR-EXT-020 to BR-EXT-025; Appendix C rows 5 and 6).
/// </summary>
[Collection(ExternalParticipationSuite.Name)]
public sealed class SourceApplicationTests(ExternalParticipationTestHost host)
{
    /// <summary>
    /// The accepted value reaches the task once, as WF-04's own change with the entity's lineage. A retry with the same key — a client after a
    /// timeout — answers with the same attempt and changes nothing again (Appendix C row 6); a new key finds the revision applied (409).
    /// </summary>
    [Fact]
    public async Task AnAcceptedReportIsAppliedOnceThroughWf04WithItsLineage()
    {
        using HttpClient client = host.Api.CreateClient();
        ParticipationSessions sessions = await client.SignInAsync();
        (Guid requestId, Guid contributionId, Guid taskId) = await AcceptedReportAsync(client, sessions, percent: 40m);
        Guid key = Guid.NewGuid();

        JsonObject applied = await CreatedAsync(client.ApplyAsync(sessions.ProjectManager, contributionId, key));
        Assert.Equal(("APPLIED", 1, "UPDATE_ALLOWED_SOURCE_FIELDS"), (applied.Text("status"), applied["attemptNo"]!.GetValue<int>(), applied.Text("applicationMode")));
        Assert.Equal(40m, (await client.GetOrFailAsync(sessions.ProjectManager, $"{Tasks}/{taskId}"))["actualPercentComplete"]!.GetValue<decimal>());

        using (HttpResponseMessage retried = await client.ApplyAsync(sessions.ProjectManager, contributionId, key))
        {
            Assert.Equal(HttpStatusCode.Created, retried.StatusCode);
            Assert.Equal("true", retried.Headers.GetValues("Idempotent-Replayed").Single());
            Assert.Equal(applied.Text("id"), (await retried.ReadObjectAsync()).Text("id"));
        }

        using (HttpResponseMessage again = await client.ApplyAsync(sessions.ProjectManager, contributionId))
        {
            Assert.Equal((HttpStatusCode.Conflict, "SOURCE_APPLICATION_ALREADY_COMPLETED"), await again.RefusalAsync());
        }

        Assert.Equal("APPLIED", (await client.GetOrFailAsync(sessions.EntityA, $"{Contributions}/{contributionId}")).Text("status"));
        Assert.Equal("CLOSED", (await client.GetOrFailAsync(sessions.EntityA, $"{Requests}/{requestId}")).Text("status"));
        Assert.Equal(["1"], await host.Database.QueryAsync($"SELECT count(*)::text FROM external_participation.source_application WHERE external_contribution_id = '{contributionId}'"));
        Assert.Equal(
            [$"ProjectTask.TaskProgressReported EXTERNAL_ENTITY {ExternalParticipationTestHost.EntityA} {contributionId} {applied.Text("id")}"],
            await host.Database.QueryAsync($"""
                SELECT e.event_type || ' ' || max(a.new_value) FILTER (WHERE a.attribute_name = 'source') || ' '
                       || max(a.new_value) FILTER (WHERE a.attribute_name = 'external_entity_id') || ' '
                       || max(a.new_value) FILTER (WHERE a.attribute_name = 'external_contribution_id') || ' '
                       || max(a.new_value) FILTER (WHERE a.attribute_name = 'source_application_id')
                FROM audit_activity.audit_event e JOIN audit_activity.audit_event_attribute a ON a.audit_event_id = e.id
                WHERE e.subject_module = 'ProjectTask' AND e.subject_id = '{taskId}' AND e.event_type = 'ProjectTask.TaskProgressReported'
                GROUP BY e.id, e.event_type
                """));
    }

    /// <summary>
    /// Appendix C row 5, BR-EXT-022: the task changed after the entity answered — the Project Manager entered another figure — so the attempt is
    /// a CONFLICT and the newer figure stays. Nothing applies until an AHDA user revalidates; then the next attempt applies it, consciously.
    /// </summary>
    [Fact]
    public async Task AChangedSourceIsAConflictNotAnOverwrite()
    {
        using HttpClient client = host.Api.CreateClient();
        ParticipationSessions sessions = await client.SignInAsync();
        (_, Guid contributionId, Guid taskId) = await AcceptedReportAsync(client, sessions, percent: 40m,
            beforeAcceptance: (c, t) => c.OkOrFailAsync(sessions.ProjectManager, $"{Tasks}/{t}/report-progress", new { actualPercentComplete = 55 }));

        JsonObject conflict = await CreatedAsync(client.ApplyAsync(sessions.ProjectManager, contributionId));
        Assert.Equal("CONFLICT", conflict.Text("status"));
        Assert.NotEqual(conflict["expectedTargetRevisionNo"]!.GetValue<long>(), conflict["actualTargetRevisionNo"]!.GetValue<long>());
        Assert.Equal(55m, (await client.GetOrFailAsync(sessions.ProjectManager, $"{Tasks}/{taskId}"))["actualPercentComplete"]!.GetValue<decimal>());
        Assert.Equal("ACCEPTED_PENDING_APPLICATION", (await client.GetOrFailAsync(sessions.ProjectManager, $"{Contributions}/{contributionId}")).Text("status"));

        using (HttpResponseMessage blind = await client.ApplyAsync(sessions.ProjectManager, contributionId))
        {
            Assert.Equal((HttpStatusCode.Conflict, "SOURCE_APPLICATION_CONFLICT"), await blind.RefusalAsync());
        }

        JsonObject revalidated = await client.OkOrFailAsync(sessions.ProjectManager, $"{Applications}/{conflict.Text("id")}/revalidate");
        Assert.Equal(revalidated["actualTargetRevisionNo"]!.GetValue<long>(), revalidated["revalidatedTargetRevisionNo"]!.GetValue<long>());
        JsonObject applied = await CreatedAsync(client.ApplyAsync(sessions.ProjectManager, contributionId));
        Assert.Equal(("APPLIED", 2), (applied.Text("status"), applied["attemptNo"]!.GetValue<int>()));
        Assert.Equal(40m, (await client.GetOrFailAsync(sessions.ProjectManager, $"{Tasks}/{taskId}"))["actualPercentComplete"]!.GetValue<decimal>());
    }

    /// <summary>
    /// EXT-CC-18 under contention: five attempts at one accepted revision, each with its own key, at once — one applies, the others find it
    /// applied or moving, none fails with a server error, and the task changes once.
    /// </summary>
    [Fact]
    public async Task RacingAttemptsApplyARevisionOnce()
    {
        using HttpClient client = host.Api.CreateClient();
        ParticipationSessions sessions = await client.SignInAsync();
        (_, Guid contributionId, Guid taskId) = await AcceptedReportAsync(client, sessions, percent: 70m);

        HttpResponseMessage[] answers = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => client.ApplyAsync(sessions.ProjectManager, contributionId)));
        try
        {
            Assert.All(answers, a => Assert.True((int)a.StatusCode < 500, $"{(int)a.StatusCode}"));
            Assert.Single(answers, a => a.StatusCode == HttpStatusCode.Created);
        }
        finally
        {
            foreach (HttpResponseMessage answer in answers)
            {
                answer.Dispose();
            }
        }

        Assert.Equal(["1"], await host.Database.QueryAsync($"SELECT count(*)::text FROM external_participation.source_application WHERE external_contribution_id = '{contributionId}' AND status = 'APPLIED'"));
        Assert.Equal(["1"], await host.Database.QueryAsync(
            $"SELECT count(*)::text FROM audit_activity.audit_event WHERE subject_id = '{taskId}' AND event_type = 'ProjectTask.TaskProgressReported'"));
    }

    /// <summary>
    /// WF-13 §7.3 SOURCE_TERMINAL: a task cancelled after acceptance can never take the figure. The attempt FAILED, nothing applied, the
    /// revision APPLICATION_FAILED and the request closed; the entity's accepted answer stays as it was.
    /// </summary>
    [Fact]
    public async Task ASourceThatCanNoLongerTakeTheChangeFailsTheApplicationSafely()
    {
        using HttpClient client = host.Api.CreateClient();
        ParticipationSessions sessions = await client.SignInAsync();
        (Guid requestId, Guid contributionId, Guid taskId) = await AcceptedReportAsync(client, sessions, percent: 20m);
        await client.OkOrFailAsync(sessions.ProjectManager, $"{Tasks}/{taskId}/cancel");

        JsonObject failed = await CreatedAsync(client.ApplyAsync(sessions.ProjectManager, contributionId));
        Assert.Equal(("FAILED", "SOURCE_RECORD_TERMINAL"), (failed.Text("status"), failed.Text("failureCode")));
        JsonObject contribution = await client.GetOrFailAsync(sessions.ProjectManager, $"{Contributions}/{contributionId}");
        Assert.Equal(("APPLICATION_FAILED", "20"), (contribution.Text("status"), contribution["fields"]![0]!.Text("value")));
        Assert.Equal("CLOSED", (await client.GetOrFailAsync(sessions.ProjectManager, $"{Requests}/{requestId}")).Text("status"));
        Assert.Null((await client.GetOrFailAsync(sessions.ProjectManager, $"{Tasks}/{taskId}"))["actualPercentComplete"]);
    }

    /// <summary>Application is AHDA's (EXT-CC-10): the entity cannot apply its own answer, and reads no attempt.</summary>
    [Fact]
    public async Task OnlyAhdaAppliesAndOnlyAhdaSeesTheAttempts()
    {
        using HttpClient client = host.Api.CreateClient();
        ParticipationSessions sessions = await client.SignInAsync();
        (_, Guid contributionId, _) = await AcceptedReportAsync(client, sessions, percent: 10m);

        using (HttpResponseMessage byEntity = await client.ApplyAsync(sessions.EntityA, contributionId))
        {
            Assert.Equal(HttpStatusCode.Forbidden, byEntity.StatusCode);
        }

        JsonObject applied = await CreatedAsync(client.ApplyAsync(sessions.ProjectManager, contributionId));
        Assert.Empty(await client.IdsAsync(sessions.EntityA, $"{Applications}?externalContributionId={contributionId}"));
        using HttpResponseMessage read = await client.GetAsync($"{Applications}/{applied.Text("id")}", sessions.EntityA);
        Assert.Equal(HttpStatusCode.NotFound, read.StatusCode);
    }

    /// <summary>
    /// A project with a task, an issued TASK_PROGRESS request for it to local.r08, the answer submitted, and — after <paramref name="beforeAcceptance"/>
    /// — accepted by local.r05, awaiting application.
    /// </summary>
    private async Task<(Guid RequestId, Guid ContributionId, Guid TaskId)> AcceptedReportAsync(
        HttpClient client, ParticipationSessions sessions, decimal percent, Func<HttpClient, Guid, Task>? beforeAcceptance = null)
    {
        Guid projectId = await host.ProjectAsync();
        await host.GrantProjectAsync(8, projectId);
        Guid taskId = await client.StartedTaskAsync(sessions, projectId, "Excavation");
        Guid requestId = await client.IssuedRequestAsync(sessions, projectId, ExternalParticipationTestHost.EntityA, ExternalParticipationTestHost.TaskProgressTypeId, taskId, responder: 8);
        JsonObject request = await client.GetOrFailAsync(sessions.EntityA, $"{Requests}/{requestId}");
        Assert.Equal(("Excavation", "ProjectTask"), (request["targetLabel"]!.Text("text"), request.Text("targetType")));

        Guid contributionId = AdministrationApi.IdOf(await client.AnswerAsync(sessions.EntityA, requestId, Percent(percent, "Two of three excavators on site.")));
        if (beforeAcceptance is not null)
        {
            await beforeAcceptance(client, taskId);
        }

        JsonObject accepted = await client.DecideAsync(sessions.ProjectManager, contributionId, "accept", note: "Matches the site visit.");
        Assert.Equal("ACCEPTED_PENDING_APPLICATION", accepted.Text("status"));
        if (beforeAcceptance is null)
        {
            Assert.Null((await client.GetOrFailAsync(sessions.ProjectManager, $"{Tasks}/{taskId}"))["actualPercentComplete"]);
        }

        return (requestId, contributionId, taskId);
    }

    private static async Task<JsonObject> CreatedAsync(Task<HttpResponseMessage> call)
    {
        using HttpResponseMessage response = await call;
        Assert.True(response.StatusCode == HttpStatusCode.Created, $"{(int)response.StatusCode} {await response.Content.ReadAsStringAsync()}");
        return await response.ReadObjectAsync();
    }
}
