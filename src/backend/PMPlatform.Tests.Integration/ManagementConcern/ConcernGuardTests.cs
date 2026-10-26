using Npgsql;
using PMPlatform.Tests.Integration.Identity;

namespace PMPlatform.Tests.Integration.ManagementConcern;

/// <summary>
/// Migration <c>TASK-057_GuardManagementConcernHistory</c> and the schema's constraints: WF-07's history holds in the database
/// whoever writes, not only through the services. Every statement runs in a transaction that is rolled back.
/// </summary>
[Collection(ConcernSuite.Name)]
public sealed class ConcernGuardTests(ConcernTestHost host)
{
    private static readonly Guid Writer = ConcernDriver.Person(2);

    /// <summary>A concern is born OPEN, moves only along its state machine, keeps its origin, is fixed from validation on and frozen once CLOSED.</summary>
    [Fact]
    public async Task AConcernMovesOnlyAlongItsStateMachine()
    {
        using HttpClient client = host.Api.CreateClient();
        ConcernSessions sessions = await client.SignInAsync();
        Guid projectId = await host.ProjectAsync();
        Guid concernId = await client.RaiseAsync(sessions.InternalManager, projectId, ConcernDriver.Impacts(await host.DimensionsAsync(), 2));

        await AssertRefusedAsync(PostgresErrorCodes.CheckViolation, "ck_management_concern_born", $"""
            INSERT INTO management_concern.management_concern (id, project_id, concern_type, title, title_lang, description, description_lang, category_item_id,
                priority_item_id, status, raised_by_user_id, raised_at, assignee_user_id, next_review_date, created_at, created_by, updated_at, updated_by)
            VALUES (gen_random_uuid(), '{projectId}', 'ISSUE', 'Inserted', 'en', 'Inserted', 'en', '{ConcernTestHost.CategoryId}', '{ConcernTestHost.HighPriorityId}',
                'ASSIGNED', '{Writer}', now(), '{Writer}', current_date, now(), '{Writer}', now(), '{Writer}')
            """);
        await AssertRefusedAsync(PostgresErrorCodes.RestrictViolation, "is not a step of the concern's state machine",
            $"UPDATE management_concern.management_concern SET status = 'RESOLVED', resolution = 'x', resolution_lang = 'en', resolved_at = now(), assignee_user_id = '{Writer}' WHERE id = '{concernId}'");
        await AssertRefusedAsync(PostgresErrorCodes.RestrictViolation, "its project, type, raiser and origin never change",
            $"UPDATE management_concern.management_concern SET concern_type = 'CHALLENGE' WHERE id = '{concernId}'");
        await AssertRefusedAsync(PostgresErrorCodes.RestrictViolation, "the revision rises by one",
            $"UPDATE management_concern.management_concern SET revision_no = 2 WHERE id = '{concernId}'");
        await AssertRefusedAsync(PostgresErrorCodes.RestrictViolation, "is retained and never deleted",
            $"DELETE FROM management_concern.management_concern WHERE id = '{concernId}'");
        await AssertRefusedAsync(PostgresErrorCodes.RestrictViolation, "never truncated", "TRUNCATE management_concern.concern_escalation");

        // From PENDING_VALIDATION its fields, severity and impacts are those it was submitted with.
        await client.StartAsync(sessions, concernId);
        await client.CommandOrFailAsync(sessions.InternalManager, concernId, "submit-resolution", ConcernDriver.Resolution("Done."));
        await AssertRefusedAsync(PostgresErrorCodes.RestrictViolation, "are those its resolution was submitted with",
            $"UPDATE management_concern.management_concern SET severity_item_id = '{ConcernTestHost.CriticalId}' WHERE id = '{concernId}'");
        await AssertRefusedAsync(PostgresErrorCodes.RestrictViolation, "are fixed once its resolution is submitted",
            $"DELETE FROM management_concern.concern_impact WHERE management_concern_id = '{concernId}'");
        await AssertRefusedAsync(PostgresErrorCodes.RestrictViolation, "is replaced by a reassessment, never rewritten",
            $"UPDATE management_concern.concern_impact SET impact_level = 5 WHERE management_concern_id = '{concernId}'");
    }

    /// <summary>
    /// ADR-013 in the database: an escalation is raised and resolved by internal users only; it is numbered next within its concern,
    /// ends once, and is never rewritten; and a concern holds one OPEN escalation.
    /// </summary>
    [Fact]
    public async Task AnEscalationIsInternalNumberedAndEndsOnce()
    {
        using HttpClient client = host.Api.CreateClient();
        ConcernSessions sessions = await client.SignInAsync();
        Guid concernId = await client.RaiseAsync(sessions.InternalManager, await host.ProjectAsync());

        await AssertRefusedAsync(PostgresErrorCodes.CheckViolation, "ck_concern_escalation_internal", EscalationInsert(concernId, 1, ConcernDriver.Person(8)));
        await AssertRefusedAsync(PostgresErrorCodes.CheckViolation, "ck_concern_escalation_order", EscalationInsert(concernId, 2, Writer));

        using HttpResponseMessage escalated = await client.EscalateAsync(sessions.InternalManager, concernId, Guid.NewGuid());
        string escalationId = (await escalated.ReadObjectAsync())["id"]!.GetValue<string>();
        await AssertRefusedAsync(PostgresErrorCodes.UniqueViolation, "ix_concern_escalation_one_open", $"""
            ALTER TABLE management_concern.concern_escalation DISABLE TRIGGER guard_concern_escalation;
            {EscalationInsert(concernId, 2, Writer)}
            """);
        await AssertRefusedAsync(PostgresErrorCodes.RestrictViolation, "an escalation is never rewritten",
            $"UPDATE management_concern.concern_escalation SET escalated_to_role_id = '00000000-0000-4000-8000-000000000002' WHERE id = '{escalationId}'");
        await AssertRefusedAsync(PostgresErrorCodes.RestrictViolation, "only its escalator withdraws",
            $"UPDATE management_concern.concern_escalation SET status = 'WITHDRAWN', resolved_at = now(), resolved_by_user_id = '{Writer}' WHERE id = '{escalationId}'");
        await AssertRefusedAsync(PostgresErrorCodes.CheckViolation, "ck_concern_escalation_internal",
            $"UPDATE management_concern.concern_escalation SET status = 'RESOLVED', resolved_at = now(), resolved_by_user_id = '{ConcernDriver.Person(8)}', resolution = 'x', resolution_lang = 'en' WHERE id = '{escalationId}'");

        await host.Database.ExecuteAsync($"""
            UPDATE management_concern.concern_escalation SET status = 'RESOLVED', resolved_at = now(), resolved_by_user_id = '{ConcernDriver.Person(3)}', resolution = 'Done', resolution_lang = 'en'
            WHERE id = '{escalationId}'
            """);
        await AssertRefusedAsync(PostgresErrorCodes.RestrictViolation, "is RESOLVED and changes no more",
            $"UPDATE management_concern.concern_escalation SET resolution = 'Rewritten' WHERE id = '{escalationId}'");
        await AssertRefusedAsync(PostgresErrorCodes.RestrictViolation, "is retained and never deleted",
            $"DELETE FROM management_concern.concern_escalation WHERE id = '{escalationId}'");
    }

    private static string EscalationInsert(Guid concernId, int number, Guid escalatedBy) => $"""
        INSERT INTO management_concern.concern_escalation (id, management_concern_id, escalation_no, escalated_by_user_id, escalated_at, escalated_to_role_id,
            reason, reason_lang, status, request_key, created_at, created_by, updated_at, updated_by)
        VALUES (gen_random_uuid(), '{concernId}', {number}, '{escalatedBy}', now(), '00000000-0000-4000-8000-000000000003', 'Inserted', 'en', 'OPEN', gen_random_uuid(),
            now(), '{escalatedBy}', now(), '{escalatedBy}')
        """;

    private async Task AssertRefusedAsync(string sqlState, string message, string sql)
    {
        PostgresException refused = await Assert.ThrowsAsync<PostgresException>(() => host.Database.ExecuteRolledBackAsync(sql));
        Assert.True(
            refused.SqlState == sqlState && (refused.MessageText.Contains(message, StringComparison.Ordinal) || refused.ConstraintName == message),
            $"{refused.SqlState} {refused.ConstraintName} {refused.MessageText}");
    }
}
