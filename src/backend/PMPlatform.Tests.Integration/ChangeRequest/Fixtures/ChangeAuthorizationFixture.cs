using PMPlatform.Tests.Integration.Identity;
using PMPlatform.Tests.Integration.Persistence;

namespace PMPlatform.Tests.Integration.ChangeRequest.Fixtures;

/// <summary>
/// For the hosts of WF-03 and WF-14, whose subject is the change and not how it was approved: a WF-08 change authorisation the target
/// module applies for real, as an approved change request being implemented would hold it — ISSUED, of the project, pinned to the
/// commitment ACTIVE now. It is inserted with the triggers off, since only WF-08's own flow may create one, and its approval run is not
/// written; the ChangeRequest tests drive that flow end to end.
/// </summary>
internal static class ChangeAuthorizationFixture
{
    /// <summary>A REBASELINE of the project's ACTIVE Approved Baseline.</summary>
    public static Task<Guid> IssueRebaselineAsync(this TestDatabase database, Guid projectId) =>
        IssueAsync(database, projectId, "SCHEDULE", "schedule_impact_days", "10", "REBASELINE", "Schedule", "ProjectBaseline", $"""
            SELECT b.id, b.version_no FROM schedule.project_baseline b
            WHERE b.project_id = '{projectId}' AND b.status = 'ACTIVE' AND b.baseline_type = 'APPROVED'
            """);

    /// <summary>A COMMITMENT_CHANGE of the project's ACTIVE Approved Budget.</summary>
    public static Task<Guid> IssueCommitmentChangeAsync(this TestDatabase database, Guid projectId) =>
        IssueAsync(database, projectId, "COST", "cost_impact_sar", "100000.00", "COMMITMENT_CHANGE", "FinancialKpi", "FinancialCommitment", $"""
            SELECT c.id, c.version_no FROM financial_kpi.financial_commitment c
            WHERE c.project_id = '{projectId}' AND c.status = 'ACTIVE' AND c.commitment_type = 'APPROVED_BUDGET'
            """);

    private static async Task<Guid> IssueAsync(
        TestDatabase database, Guid projectId, string changeType, string impactColumn, string impact, string scope, string module, string type, string target)
    {
        ArgumentNullException.ThrowIfNull(database);
        (Guid requestId, Guid authorizationId) = (Guid.NewGuid(), Guid.NewGuid());
        await database.ExecuteAsync($"""
            BEGIN;
            SET LOCAL session_replication_role = replica;
            INSERT INTO change_request.change_request (id, project_id, title, title_lang, justification, justification_lang, change_type, status, revision_no,
                                                       requested_by_user_id, submitted_at, {impactColumn}, is_contractual_obligation,
                                                       created_at, created_by, updated_at, updated_by)
            VALUES ('{requestId}', '{projectId}', 'Fixture change', 'en', 'Approved for a test', 'en', '{changeType}', 'IMPLEMENTATION', 1,
                    '{IdentityDatabase.SeedPrincipalId}', now(), {impact}, false, now(), '{IdentityDatabase.SeedPrincipalId}', now(), '{IdentityDatabase.SeedPrincipalId}');
            INSERT INTO change_request.change_authorization (id, change_request_id, approval_instance_id, authorization_scope, target_module, target_type, target_id,
                                                             target_revision_no, idempotency_key, status, issued_at, created_at, created_by, updated_at, updated_by)
            SELECT '{authorizationId}', '{requestId}', gen_random_uuid(), '{scope}', '{module}', '{type}', t.id, t.version_no, 'fixture:{authorizationId}', 'ISSUED',
                   now(), now(), '{IdentityDatabase.SeedPrincipalId}', now(), '{IdentityDatabase.SeedPrincipalId}'
            FROM ({target}) AS t (id, version_no);
            COMMIT;
            """);
        Assert.Single(await database.QueryAsync($"SELECT id::text FROM change_request.change_authorization WHERE id = '{authorizationId}'"));
        return authorizationId;
    }
}
