namespace PMPlatform.Tests.Integration.ChangeRequest;

/// <summary>
/// Migration <c>TASK-060_GuardChangeRequestHistory</c>: the database holds WF-08's history and its acceptance criteria whoever writes —
/// above all, that an authorisation is applied once, only while its change is being implemented, never by the approval itself.
/// </summary>
[Collection(ChangeRequestSuite.Name)]
public sealed class ChangeRequestGuardTests(ChangeRequestTestHost host)
{
    [Fact]
    public async Task TheDatabaseHoldsTheChangeRequestHistory()
    {
        using HttpClient client = host.Api.CreateClient();
        ChangeSessions sessions = await client.SignInAsync();
        Guid projectId = await host.ProjectAsync("LIGHT");
        await host.BaselinedAsync(client, sessions, projectId);
        Guid draft = await client.RaiseAsync(sessions.EntityManager, ChangeRequestDriver.RequestBody(projectId, "SCHEDULE", scheduleImpactDays: 3));
        Guid submitted = await client.RaiseAsync(sessions.EntityManager, ChangeRequestDriver.RequestBody(projectId, "SCHEDULE", scheduleImpactDays: 3));
        await client.CommandOrFailAsync(sessions.EntityManager, submitted, "submit");
        Guid approved = await client.RaiseAsync(sessions.EntityManager, ChangeRequestDriver.RequestBody(projectId, "SCHEDULE", scheduleImpactDays: 3));
        await host.ApprovedAsync(client, sessions, approved);
        Guid issued = Guid.Parse(Assert.Single(await host.Database.QueryAsync($"SELECT id::text FROM change_request.change_authorization WHERE change_request_id = '{approved}'")));
        Guid applied = await client.RaiseAsync(sessions.EntityManager, ChangeRequestDriver.RequestBody(projectId, "SCHEDULE", scheduleImpactDays: 3));
        Guid used = ChangeRequestDriver.AuthorizationOf(await host.ImplementingAsync(client, sessions, applied), "REBASELINE");
        await client.SubmittedBaselineAsync(sessions.EntityManager, projectId, used);
        Guid pending = await client.RaiseAsync(sessions.EntityManager, ChangeRequestDriver.RequestBody(projectId, "SCHEDULE", scheduleImpactDays: 3));
        await host.ImplementingAsync(client, sessions, pending);

        // A change request.
        Assert.Contains("born DRAFT", await host.RefusedAsync($"""
            INSERT INTO change_request.change_request (id, project_id, title, title_lang, justification, justification_lang, change_type, status, requested_by_user_id,
                                                       submitted_at, created_at, created_by, updated_at, updated_by)
            VALUES (gen_random_uuid(), '{projectId}', 'x', 'en', 'x', 'en', 'SCOPE', 'APPROVED', '{ChangeRequestDriver.Person(8)}', now(), now(), '{ChangeRequestDriver.Person(8)}', now(), '{ChangeRequestDriver.Person(8)}')
            """), StringComparison.Ordinal);
        Assert.Contains("is not a step", await host.RefusedAsync($"UPDATE change_request.change_request SET status = 'APPROVED' WHERE id = '{submitted}'"), StringComparison.Ordinal);
        Assert.Contains("those it was submitted with", await host.RefusedAsync($"UPDATE change_request.change_request SET schedule_impact_days = 30 WHERE id = '{submitted}'"), StringComparison.Ordinal);
        Assert.Contains("project, type and requester", await host.RefusedAsync($"UPDATE change_request.change_request SET change_type = 'COST' WHERE id = '{draft}'"), StringComparison.Ordinal);
        Assert.Contains("only a DRAFT", await host.RefusedAsync($"DELETE FROM change_request.change_request WHERE id = '{submitted}'"), StringComparison.Ordinal);
        Assert.Contains("evaluation of revision", await host.RefusedAsync($"UPDATE change_request.change_request SET status = 'UNDER_REVIEW' WHERE id = '{submitted}'"), StringComparison.Ordinal);
        Assert.Contains("each of its authorisations is applied", await host.RefusedAsync(
            $"UPDATE change_request.change_request SET status = 'IMPLEMENTED', implemented_at = now() WHERE id = '{pending}'"), StringComparison.Ordinal);
        Assert.Contains("never truncated", await host.RefusedAsync("TRUNCATE change_request.change_request CASCADE"), StringComparison.Ordinal);
        Assert.Null(await host.RefusedAsync($"DELETE FROM change_request.change_request WHERE id = '{draft}'"));

        // An evaluation.
        Assert.Contains("never changed or deleted", await host.RefusedAsync($"UPDATE change_request.materiality_evaluation SET resulting_band_no = 3 WHERE change_request_id = '{approved}'"), StringComparison.Ordinal);
        Assert.Contains("while it is SUBMITTED", await host.RefusedAsync($"""
            INSERT INTO change_request.materiality_evaluation (id, change_request_id, revision_no, evaluated_at, materiality_configuration_version_id, cumulative_cost_impact_sar,
                                                               cumulative_schedule_impact_days, resulting_band_no, created_at, created_by, updated_at, updated_by)
            VALUES (gen_random_uuid(), '{draft}', 1, now(), '{ChangeRequestTestHost.MaterialityVersionId}', 0, 0, 1, now(), '{ChangeRequestDriver.Person(2)}', now(), '{ChangeRequestDriver.Person(2)}')
            """), StringComparison.Ordinal);

        // An authorisation: issued only by an approval; applied once, only while its change is being implemented; never rewritten or deleted.
        Assert.Contains("issued by the approval", await host.RefusedAsync($"""
            INSERT INTO change_request.change_authorization (id, change_request_id, approval_instance_id, authorization_scope, target_module, target_type, target_id,
                                                             target_revision_no, idempotency_key, status, issued_at, created_at, created_by, updated_at, updated_by)
            SELECT gen_random_uuid(), '{submitted}', a.approval_instance_id, a.authorization_scope, a.target_module, a.target_type, a.target_id, a.target_revision_no,
                   'forged', 'ISSUED', now(), now(), a.created_by, now(), a.created_by
            FROM change_request.change_authorization a WHERE a.id = '{issued}'
            """), StringComparison.Ordinal);
        Assert.Contains("never by the approval itself", await host.RefusedAsync($"""
            UPDATE change_request.change_authorization SET status = 'APPLIED', applied_at = now(), applied_by_user_id = '{ChangeRequestDriver.Person(8)}', applied_reference = 'x'
            WHERE id = '{issued}'
            """), StringComparison.Ordinal);
        Assert.Contains("applied once", await host.RefusedAsync($"UPDATE change_request.change_authorization SET applied_reference = 'Schedule.ProjectBaseline:other' WHERE id = '{used}'"), StringComparison.Ordinal);
        Assert.Contains("pinned target never change", await host.RefusedAsync($"UPDATE change_request.change_authorization SET target_revision_no = 9 WHERE id = '{issued}'"), StringComparison.Ordinal);
        Assert.Contains("never deleted", await host.RefusedAsync($"DELETE FROM change_request.change_authorization WHERE id = '{issued}'"), StringComparison.Ordinal);
        Assert.Contains("ix_change_authorization_idempotency_key", await host.RefusedAsync($"""
            INSERT INTO change_request.change_authorization (id, change_request_id, approval_instance_id, authorization_scope, target_module, target_type, target_id,
                                                             target_revision_no, idempotency_key, status, issued_at, created_at, created_by, updated_at, updated_by)
            SELECT gen_random_uuid(), a.change_request_id, a.approval_instance_id, a.authorization_scope, a.target_module, a.target_type, a.target_id, a.target_revision_no,
                   a.idempotency_key, 'ISSUED', now(), now(), a.created_by, now(), a.created_by
            FROM change_request.change_authorization a WHERE a.id = '{issued}'
            """), StringComparison.Ordinal);
    }
}
