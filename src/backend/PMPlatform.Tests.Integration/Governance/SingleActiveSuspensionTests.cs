using System.Net;
using System.Text.Json.Nodes;
using PMPlatform.Application.Features.Approval.Contracts;
using PMPlatform.Tests.Integration.Identity;
using PMPlatform.Tests.Integration.Suspension;

namespace PMPlatform.Tests.Integration.Governance;

/// <summary>
/// Invariant 2 (suspension.md D-8, D-11): a project holds at most one active suspension — at every moment, through suspension and
/// resumption cycles, and however the requests that would open a second one race: several raised at once, an approved request activated
/// by several commands and WF-09's pass at once, a direct write to the database. The rule's only places are the project's state, the two
/// partial unique keys and the commit-time guard, so a race is where it would break first.
/// </summary>
[Collection(SuspensionSuite.Name)]
public sealed class SingleActiveSuspensionTests(SuspensionTestHost host)
{
    /// <summary>How many requests race each time.</summary>
    private const int Racers = 6;

    /// <summary>Of the activations that race, how many are commands; the rest are passes of WF-09's worker.</summary>
    private const int Commands = 3;

    [Fact]
    public async Task AProjectHoldsAtMostOneActiveSuspensionHoweverItsRequestsAndActivationsRace()
    {
        using HttpClient client = host.Api.CreateClient();
        SuspensionSessions sessions = await client.SignInAsync();
        Guid projectId = await host.ActiveProjectAsync();

        // Six suspension requests raised at once: one is taken, the others refused as a second one.
        Guid suspension = AssertOneRaised(await RaiseAtOnceAsync(client, sessions, projectId, "SUSPEND"), "SUSPENSION_ALREADY_EXISTS");
        Assert.Equal(["SUSPEND DRAFT"], await RequestsAsync(projectId));

        // The approved request activated by three commands and two passes at once: it takes effect once, one period opens.
        await ApproveAsync(client, sessions, suspension);
        await ActivateAtOnceAsync(client, sessions, suspension);
        Assert.Equal("SUSPENDED", await host.LifecycleOfAsync(projectId));
        Assert.Equal(["open"], await host.SuspensionPeriodsAsync(projectId));
        Assert.Empty(await host.Database.SuspensionViolationsAsync());

        // Suspended: six more suspension requests at once are all refused, and no writer opens a second period.
        Assert.All(await RaiseAtOnceAsync(client, sessions, projectId, "SUSPEND"),
            a => Assert.Equal((HttpStatusCode.Conflict, "SUSPENSION_ALREADY_EXISTS"), (a.Status, a.Code)));
        Assert.Contains("ix_active_suspension_open_project_id", await host.RefusedAsync($"""
            INSERT INTO suspension.active_suspension (id, project_id, suspension_request_id, started_at, created_at, created_by, updated_at, updated_by)
            VALUES (gen_random_uuid(), '{projectId}', '{suspension}', now(), now(), '{IdentityDatabase.SeedPrincipalId}', now(), '{IdentityDatabase.SeedPrincipalId}')
            """), StringComparison.Ordinal);

        // Six resumption requests at once: one is taken; its activation raced the same way ends the one period.
        Guid resumption = AssertOneRaised(await RaiseAtOnceAsync(client, sessions, projectId, "RESUME"), "SUSPENSION_RESUMPTION_ALREADY_EXISTS");
        await ApproveAsync(client, sessions, resumption);
        await ActivateAtOnceAsync(client, sessions, resumption);
        Assert.Equal("ACTIVE", await host.LifecycleOfAsync(projectId));
        Assert.Equal(["ended"], await host.SuspensionPeriodsAsync(projectId));
        Assert.Empty(await host.Database.SuspensionViolationsAsync());

        // A second cycle: suspended again by a new request, the history keeping both periods and only the newest open.
        Guid again = AssertOneRaised(await RaiseAtOnceAsync(client, sessions, projectId, "SUSPEND"), "SUSPENSION_ALREADY_EXISTS");
        await ApproveAsync(client, sessions, again);
        await ActivateAtOnceAsync(client, sessions, again);
        Assert.Equal("SUSPENDED", await host.LifecycleOfAsync(projectId));
        Assert.Equal(["ended", "open"], await host.SuspensionPeriodsAsync(projectId));
        Assert.Equal(["RESUME EFFECTED", "SUSPEND EFFECTED", "SUSPEND EFFECTED"], await RequestsAsync(projectId));
        JsonObject open = await client.GetOrFailAsync(sessions.EntityManager, $"{SuspensionDriver.Suspensions}?projectId={projectId}&open=true");
        Assert.Equal(again, Guid.Parse(Assert.Single(open["items"]!.AsArray())!.Text("suspensionRequestId")));
        Assert.Equal(
            ["Project.ProjectSuspended", "Project.ProjectResumed", "Project.ProjectSuspended"],
            (await host.AuditTrailAsync("Project", projectId)).Select(e => e.Split(' ')[0]));
        Assert.Empty(await host.Database.SuspensionViolationsAsync());
    }

    /// <summary>
    /// <see cref="Racers"/> requests of <paramref name="requestType"/> raised at once by the Project Manager, each under its own key: a
    /// client's retries, or several of its tabs. (AHDA's officer, who also raises, decides the request here and may not decide its own.)
    /// </summary>
    private static Task<Answer[]> RaiseAtOnceAsync(HttpClient client, SuspensionSessions sessions, Guid projectId, string requestType) =>
        GovernanceApi.AtOnceAsync(Racers, _ => GovernanceApi.AnswerAsync(client.PostAsync(
            SuspensionDriver.Requests, sessions.EntityManager, SuspensionDriver.RequestBody(projectId, requestType, SuspensionDriver.Today))));

    /// <summary>Exactly one raise was taken, every other refused 409 <paramref name="refusal"/>; returns the request taken.</summary>
    private static Guid AssertOneRaised(Answer[] raised, string refusal)
    {
        Answer[] taken = [.. raised.Where(a => a.Status == HttpStatusCode.Created)];
        Assert.True(taken.Length == 1 && raised.Except(taken).All(a => (a.Status, a.Code) == (HttpStatusCode.Conflict, refusal)),
            $"{raised.Length} requests raised at once answered: {string.Join(", ", raised.Select(a => a.ToString()))}");
        return AdministrationApi.IdOf(JsonNode.Parse(taken[0].Body)!.AsObject());
    }

    /// <summary>Submitted by the Project Manager, its review started by the Department Manager, approved through WF-11.</summary>
    private async Task ApproveAsync(HttpClient client, SuspensionSessions sessions, Guid requestId)
    {
        await client.CommandOrFailAsync(sessions.EntityManager, requestId, "submit");
        await client.CommandOrFailAsync(sessions.DepartmentManager, requestId, "start-review");
        await host.DecideAndDeliverAsync(requestId, ApprovalTaskDecision.Approve);
    }

    /// <summary>
    /// The approved request activated by <see cref="Commands"/> commands of AHDA's officer and the rest of <see cref="Racers"/> passes of
    /// WF-09's worker, all at once. It is EFFECTED once: one RequestEffected, the project moved once; no command took effect twice or failed
    /// as an error.
    /// </summary>
    private async Task ActivateAtOnceAsync(HttpClient client, SuspensionSessions sessions, Guid requestId)
    {
        int projectEvents = (await host.AuditTrailAsync("Project", Guid.Parse(await ProjectOfAsync(requestId)))).Count;
        Answer?[] answers = await GovernanceApi.AtOnceAsync(Racers, async i =>
        {
            if (i < Commands)
            {
                return await GovernanceApi.AnswerAsync(client.CommandAsync(sessions.Officer, requestId, "activate"));
            }

            await host.ActivateDueAsync();
            return null;
        });

        Answer[] commands = [.. answers.OfType<Answer>()];
        Assert.True(commands.Count(a => a.Succeeded) <= 1 && commands.All(a => a.Succeeded || a.Status is HttpStatusCode.Conflict or HttpStatusCode.PreconditionFailed),
            $"{Commands} activations sent at once answered: {string.Join(", ", commands.Select(a => a.ToString()))}");
        Assert.Equal(["EFFECTED"], await host.Database.QueryAsync($"SELECT status FROM suspension.suspension_request WHERE id = '{requestId}'"));
        Assert.Single(await host.AuditTrailAsync("Suspension", requestId), e => e.StartsWith("Suspension.RequestEffected ", StringComparison.Ordinal));
        Assert.Equal(projectEvents + 1, (await host.AuditTrailAsync("Project", Guid.Parse(await ProjectOfAsync(requestId)))).Count);
    }

    private async Task<string> ProjectOfAsync(Guid requestId) =>
        Assert.Single(await host.Database.QueryAsync($"SELECT project_id::text FROM suspension.suspension_request WHERE id = '{requestId}'"));

    /// <summary>The project's requests as "TYPE STATUS", in order.</summary>
    private Task<IReadOnlyList<string>> RequestsAsync(Guid projectId) =>
        host.Database.QueryAsync($"SELECT request_type || ' ' || status FROM suspension.suspension_request WHERE project_id = '{projectId}' ORDER BY request_type, status");
}
