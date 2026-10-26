using PMPlatform.Application.Features.Approval.Contracts;
using PMPlatform.Tests.Integration.Identity;

namespace PMPlatform.Tests.Integration.Suspension;

/// <summary>
/// The acceptance criteria held by the database, whoever writes (migration TASK-062_GuardSuspensionHistory): one open suspension and one
/// open request of each type per project; no suspension of a suspended project; no approval but WF-11's; no project moved without its
/// suspension record, and no request effected without the period it opens or ends; history never rewritten, deleted or truncated.
/// </summary>
[Collection(SuspensionSuite.Name)]
public sealed class SuspensionGuardTests(SuspensionTestHost host)
{
    private static string Seed => IdentityDatabase.SeedPrincipalId;

    [Fact]
    public async Task AProjectIsSuspendedAndResumedOnlyWithItsActiveSuspension()
    {
        using HttpClient client = host.Api.CreateClient();
        SuspensionSessions sessions = await client.SignInAsync();
        Guid active = await host.ActiveProjectAsync();
        Assert.Contains("exactly while it has an open active suspension",
            await host.RefusedAsync($"UPDATE project.project SET lifecycle_state = 'SUSPENDED' WHERE id = '{active}'"), StringComparison.Ordinal);

        Guid suspended = await host.ActiveProjectAsync();
        await host.EffectedAsync(client, sessions, suspended, "SUSPEND");
        Assert.Contains("exactly while it has an open active suspension",
            await host.RefusedAsync($"UPDATE project.project SET lifecycle_state = 'ACTIVE' WHERE id = '{suspended}'"), StringComparison.Ordinal);
        Assert.Contains("ix_active_suspension_open_project_id", await host.RefusedAsync($"""
            INSERT INTO suspension.active_suspension (id, project_id, suspension_request_id, started_at, created_at, created_by, updated_at, updated_by)
            SELECT gen_random_uuid(), project_id, id, now(), now(), '{Seed}', now(), '{Seed}' FROM suspension.suspension_request WHERE project_id = '{suspended}'
            """), StringComparison.Ordinal);
        Assert.Contains("ended by an effected resumption request", await host.RefusedAsync(
            $"UPDATE suspension.active_suspension SET ended_at = now(), resumption_request_id = suspension_request_id WHERE project_id = '{suspended}'"), StringComparison.Ordinal);

        Assert.Equal(("ACTIVE", "SUSPENDED"), (await host.LifecycleOfAsync(active), await host.LifecycleOfAsync(suspended)));
        Assert.Equal(["open"], await host.SuspensionPeriodsAsync(suspended));
    }

    [Fact]
    public async Task ARequestIsApprovedOnlyByWf11AndEffectedOnlyWithItsSuspensionPeriod()
    {
        using HttpClient client = host.Api.CreateClient();
        SuspensionSessions sessions = await client.SignInAsync();
        Guid projectId = await host.ActiveProjectAsync();
        Guid requestId = await client.RaiseAsync(sessions.EntityManager, SuspensionDriver.RequestBody(projectId, "SUSPEND", SuspensionDriver.Today));
        await client.CommandOrFailAsync(sessions.EntityManager, requestId, "submit");
        await client.CommandOrFailAsync(sessions.DepartmentManager, requestId, "start-review");

        Assert.Contains("approved by the approved WF-11 run",
            await host.RefusedAsync($"UPDATE suspension.suspension_request SET status = 'APPROVED' WHERE id = '{requestId}'"), StringComparison.Ordinal);
        Assert.Contains("is not a step of the request's state machine",
            await host.RefusedAsync($"UPDATE suspension.suspension_request SET status = 'EFFECTED', effected_at = now() WHERE id = '{requestId}'"), StringComparison.Ordinal);

        await host.DecideAndDeliverAsync(requestId, ApprovalTaskDecision.Approve);
        Assert.Contains("effected with the suspension period it opens or ends",
            await host.RefusedAsync($"UPDATE suspension.suspension_request SET status = 'EFFECTED', effected_at = now() WHERE id = '{requestId}'"), StringComparison.Ordinal);
        Assert.Equal(["APPROVED"], await host.Database.QueryAsync($"SELECT status FROM suspension.suspension_request WHERE id = '{requestId}'"));
        Assert.Equal("ACTIVE", await host.LifecycleOfAsync(projectId));
    }

    [Fact]
    public async Task ASuspensionRequestIsRaisedOnceForAnActiveProjectAndNeverForASuspendedOne()
    {
        using HttpClient client = host.Api.CreateClient();
        SuspensionSessions sessions = await client.SignInAsync();
        Guid active = await host.ActiveProjectAsync();
        await client.RaiseAsync(sessions.EntityManager, SuspensionDriver.RequestBody(active, "SUSPEND", null));
        Assert.Contains("ix_suspension_request_open_project_id_request_type", await host.RefusedAsync(Insert(active, "SUSPEND")), StringComparison.Ordinal);
        Assert.Contains("a resumption for a SUSPENDED one", await host.RefusedAsync(Insert(active, "RESUME")), StringComparison.Ordinal);

        Guid suspended = await host.ActiveProjectAsync();
        await host.EffectedAsync(client, sessions, suspended, "SUSPEND");
        Assert.Contains("a suspension is raised for an ACTIVE project", await host.RefusedAsync(Insert(suspended, "SUSPEND")), StringComparison.Ordinal);
    }

    [Fact]
    public async Task SuspensionHistoryIsNeverRewrittenDeletedOrTruncated()
    {
        using HttpClient client = host.Api.CreateClient();
        SuspensionSessions sessions = await client.SignInAsync();
        Guid projectId = await host.ActiveProjectAsync();
        Guid requestId = await host.EffectedAsync(client, sessions, projectId, "SUSPEND");
        string before = await host.RowAsync("suspension.suspension_request", requestId);

        Assert.Contains("changes no more", await host.RefusedAsync($"UPDATE suspension.suspension_request SET reason = 'Rewritten' WHERE id = '{requestId}'"), StringComparison.Ordinal);
        Assert.Contains("only a DRAFT never submitted is deleted", await host.RefusedAsync($"DELETE FROM suspension.suspension_request WHERE id = '{requestId}'"), StringComparison.Ordinal);
        Assert.Contains("never change", await host.RefusedAsync($"UPDATE suspension.active_suspension SET started_at = now() - interval '1 day' WHERE project_id = '{projectId}'"), StringComparison.Ordinal);
        Assert.Contains("retained and never deleted", await host.RefusedAsync($"DELETE FROM suspension.active_suspension WHERE project_id = '{projectId}'"), StringComparison.Ordinal);
        Assert.Contains("never truncated", await host.RefusedAsync("TRUNCATE suspension.active_suspension, suspension.suspension_request"), StringComparison.Ordinal);
        Assert.Equal(before, await host.RowAsync("suspension.suspension_request", requestId));
    }

    private static string Insert(Guid projectId, string requestType) => $"""
        INSERT INTO suspension.suspension_request (id, project_id, request_type, status, revision_no, reason, reason_lang, requested_by_user_id, created_at, created_by, updated_at, updated_by)
        VALUES (gen_random_uuid(), '{projectId}', '{requestType}', 'DRAFT', 1, 'Written directly', 'en', '{SuspensionDriver.Person(8)}', now(), '{Seed}', now(), '{Seed}')
        """;
}
