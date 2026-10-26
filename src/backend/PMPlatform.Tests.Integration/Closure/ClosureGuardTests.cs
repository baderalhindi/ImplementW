using PMPlatform.Application.Features.Approval.Contracts;
using PMPlatform.Tests.Integration.Identity;

namespace PMPlatform.Tests.Integration.Closure;

/// <summary>
/// WF-10's rules held by the database, whoever writes (migrations TASK-063_AddProjectCloseoutEdges and TASK-063_GuardCloseoutHistory): a
/// project is COMPLETED only with its effected completion case and CLOSED only with its effected closure case of the path it was on; a
/// CLOSED project changes no more; a case is born DRAFT for a project its kind admits, moves only along its edges, is approved only by
/// WF-11 and is effected only with its project's transition; readiness records are never changed; obligations are never deleted, and a
/// settled one changes no more; nothing is truncated.
/// </summary>
[Collection(ClosureSuite.Name)]
public sealed class ClosureGuardTests(ClosureTestHost host)
{
    private static string Seed => IdentityDatabase.SeedPrincipalId;

    [Fact]
    public async Task AProjectIsCompletedAndClosedOnlyWithItsEffectedCase()
    {
        Guid active = await host.ProjectAsync();
        Assert.Contains("COMPLETED only by its effected completion case",
            await host.RefusedAsync($"UPDATE project.project SET lifecycle_state = 'COMPLETED' WHERE id = '{active}'"), StringComparison.Ordinal);

        Guid completed = await host.ProjectAsync(state: "COMPLETED");
        Assert.Contains("CLOSED only by its effected closure case",
            await host.RefusedAsync($"UPDATE project.project SET lifecycle_state = 'CLOSED', closed_at = now() WHERE id = '{completed}'"), StringComparison.Ordinal);

        Guid suspended = await host.ProjectAsync(state: "SUSPENDED");
        Assert.Contains("CLOSED only by its effected closure case",
            await host.RefusedAsync($"UPDATE project.project SET lifecycle_state = 'CLOSED', closed_at = now() WHERE id = '{suspended}'"), StringComparison.Ordinal);

        // A case effected without its project's transition is refused at commit.
        Guid caseId = Guid.NewGuid();
        await host.Database.ExecuteAsync($"""
            BEGIN;
            SET LOCAL session_replication_role = replica;
            INSERT INTO closure.completion_case (id, project_id, status, revision_no, requested_by_user_id, submitted_at, actual_project_completion_date,
                                                 completion_narrative, completion_narrative_lang, created_at, created_by, updated_at, updated_by)
            VALUES ('{caseId}', '{active}', 'APPROVED', 1, '{ClosureDriver.Person(8)}', now(), current_date, 'Done.', 'en', now(), '{Seed}', now(), '{Seed}');
            COMMIT;
            """);
        Assert.Contains("effected with its project's transition",
            await host.RefusedAsync($"UPDATE closure.completion_case SET status = 'EFFECTED', effected_at = now() WHERE id = '{caseId}'"), StringComparison.Ordinal);
        Assert.Equal(("ACTIVE", "COMPLETED", "SUSPENDED"), (await host.LifecycleOfAsync(active), await host.LifecycleOfAsync(completed), await host.LifecycleOfAsync(suspended)));
    }

    [Fact]
    public async Task AClosedProjectChangesNoMore()
    {
        using HttpClient client = host.Api.CreateClient();
        ClosureSessions sessions = await client.SignInAsync();
        Guid projectId = await host.ProjectAsync();
        await host.EffectedAsync(client, sessions, ClosureDriver.CompletionCases, ClosureDriver.CompletionBody(projectId));
        Guid closureId = await host.EffectedAsync(client, sessions, ClosureDriver.ClosureCases, ClosureDriver.ClosureBody(projectId));
        string before = await host.RowAsync("project.project", projectId);

        Assert.Contains("is CLOSED: terminal and read-only",
            await host.RefusedAsync($"UPDATE project.project SET title = 'Renamed' WHERE id = '{projectId}'"), StringComparison.Ordinal);
        Assert.Contains("is CLOSED: terminal and read-only",
            await host.RefusedAsync($"UPDATE project.project SET lifecycle_state = 'ACTIVE' WHERE id = '{projectId}'"), StringComparison.Ordinal);
        Assert.Contains("is EFFECTED and changes no more",
            await host.RefusedAsync($"UPDATE closure.closure_case SET closure_narrative = 'Rewritten.' WHERE id = '{closureId}'"), StringComparison.Ordinal);
        Assert.Equal(before, await host.RowAsync("project.project", projectId));
    }

    [Fact]
    public async Task ACaseIsBornDraftMovesAlongItsEdgesAndIsApprovedOnlyByWf11()
    {
        using HttpClient client = host.Api.CreateClient();
        ClosureSessions sessions = await client.SignInAsync();
        Guid projectId = await host.ProjectAsync();
        Guid completed = await host.ProjectAsync(state: "COMPLETED");

        Assert.Contains("a case is born DRAFT", await host.RefusedAsync(InsertCompletion(projectId, "SUBMITTED")), StringComparison.Ordinal);
        Assert.Contains("for an ACTIVE project", await host.RefusedAsync(InsertCompletion(completed, "DRAFT")), StringComparison.Ordinal);
        Assert.Contains("follows the effected completion case of its COMPLETED project", await host.RefusedAsync($"""
            INSERT INTO closure.closure_case (id, project_id, completion_case_id, status, revision_no, requested_by_user_id, created_at, created_by, updated_at, updated_by)
            VALUES (gen_random_uuid(), '{completed}', gen_random_uuid(), 'DRAFT', 1, '{ClosureDriver.Person(8)}', now(), '{Seed}', now(), '{Seed}')
            """), StringComparison.Ordinal);

        Guid caseId = await client.RaiseAsync(sessions.EntityManager, ClosureDriver.CompletionCases, ClosureDriver.CompletionBody(projectId));
        await client.WaiveFailedAsync(sessions, ClosureDriver.CompletionCases, caseId);
        await client.CommandOrFailAsync(sessions.EntityManager, ClosureDriver.CompletionCases, caseId, "submit");
        Assert.Contains("its fields are those it was submitted with",
            await host.RefusedAsync($"UPDATE closure.completion_case SET actual_project_completion_date = current_date - 1 WHERE id = '{caseId}'"), StringComparison.Ordinal);
        Assert.Contains("is not a step of the case's state machine",
            await host.RefusedAsync($"UPDATE closure.completion_case SET status = 'APPROVED' WHERE id = '{caseId}'"), StringComparison.Ordinal);

        await client.CommandOrFailAsync(sessions.DepartmentManager, ClosureDriver.CompletionCases, caseId, "start-review");
        Assert.Contains("approved by the approved WF-11 run",
            await host.RefusedAsync($"UPDATE closure.completion_case SET status = 'APPROVED' WHERE id = '{caseId}'"), StringComparison.Ordinal);
        Assert.Contains("only a DRAFT never submitted is deleted",
            await host.RefusedAsync($"DELETE FROM closure.completion_case WHERE id = '{caseId}'"), StringComparison.Ordinal);

        await host.DecideAndDeliverAsync("CompletionCase", caseId, ApprovalTaskDecision.Approve);
        Assert.Equal(["APPROVED"], await host.Database.QueryAsync($"SELECT status FROM closure.completion_case WHERE id = '{caseId}'"));
        Assert.Equal("ACTIVE", await host.LifecycleOfAsync(projectId));
    }

    [Fact]
    public async Task ReadinessRecordsAreNeverChangedAndObligationsNeverDeleted()
    {
        using HttpClient client = host.Api.CreateClient();
        ClosureSessions sessions = await client.SignInAsync();
        Guid projectId = await host.ProjectAsync();
        Guid caseId = await client.RaiseAsync(sessions.EntityManager, ClosureDriver.CompletionCases, ClosureDriver.CompletionBody(projectId));
        await client.CommandOrFailAsync(sessions.EntityManager, ClosureDriver.CompletionCases, caseId, "evaluate-readiness");
        Guid obligationId = await client.RaiseAsync(sessions.EntityManager, ClosureDriver.Obligations,
            new { completionCaseId = caseId, title = ClosureDriver.Narrative("Final payment"), ownerUserId = ClosureDriver.Person(8), dueDate = ClosureDriver.Iso(ClosureDriver.Today) });

        Assert.Contains("never changed or deleted (APPEND_ONLY)",
            await host.RefusedAsync($"UPDATE closure.readiness_check SET result = 'PASS', blocking_count = 0 WHERE completion_case_id = '{caseId}'"), StringComparison.Ordinal);
        Assert.Contains("never changed or deleted (APPEND_ONLY)",
            await host.RefusedAsync($"DELETE FROM closure.readiness_check WHERE completion_case_id = '{caseId}'"), StringComparison.Ordinal);
        Assert.Contains("retained and never deleted",
            await host.RefusedAsync($"DELETE FROM closure.post_project_obligation WHERE id = '{obligationId}'"), StringComparison.Ordinal);
        await client.CommandOrFailAsync(sessions.EntityManager, ClosureDriver.Obligations, obligationId, "start");
        Assert.Contains("is not a step of an obligation",
            await host.RefusedAsync($"UPDATE closure.post_project_obligation SET status = 'OPEN' WHERE id = '{obligationId}'"), StringComparison.Ordinal);
        await client.CommandOrFailAsync(sessions.EntityManager, ClosureDriver.Obligations, obligationId, "cancel");
        Assert.Contains("is CANCELLED and changes no more",
            await host.RefusedAsync($"UPDATE closure.post_project_obligation SET due_date = current_date + 7 WHERE id = '{obligationId}'"), StringComparison.Ordinal);

        // A draft that has readiness records or obligations is withdrawn, not deleted: they are kept.
        using (HttpResponseMessage deleted = await client.SendAsync(HttpMethod.Delete, $"{ClosureDriver.CompletionCases}/{caseId}", sessions.EntityManager))
        {
            Assert.Equal((System.Net.HttpStatusCode.Conflict, "CLOSURE_CASE_IN_USE"), await deleted.RefusalAsync());
        }

        Assert.Equal("WITHDRAWN", (await client.CommandOrFailAsync(sessions.EntityManager, ClosureDriver.CompletionCases, caseId, "withdraw")).Text("status"));
        foreach (string table in new[] { "completion_case", "closure_case", "readiness_check", "post_project_obligation" })
        {
            Assert.Contains("is retained and never truncated", await host.RefusedAsync($"TRUNCATE closure.{table} CASCADE"), StringComparison.Ordinal);
        }
    }

    private static string InsertCompletion(Guid projectId, string status) => $"""
        INSERT INTO closure.completion_case (id, project_id, status, revision_no, requested_by_user_id, submitted_at, created_at, created_by, updated_at, updated_by)
        VALUES (gen_random_uuid(), '{projectId}', '{status}', 1, '{ClosureDriver.Person(8)}', {(status == "DRAFT" ? "NULL" : "now()")}, now(), '{Seed}', now(), '{Seed}')
        """;
}
