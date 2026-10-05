using Npgsql;

namespace PMPlatform.Tests.Integration.Risk;

/// <summary>
/// Migration <c>TASK-055_GuardRiskHistory</c> and the schema's constraints: WF-06's history holds in the database whoever writes, not
/// only through the services. Every statement runs in a transaction that is rolled back.
/// </summary>
[Collection(RiskSuite.Name)]
public sealed class RiskGuardTests(RiskTestHost host)
{
    private static readonly Guid Writer = RiskDriver.Person(2);

    /// <summary>The acceptance criterion in the database: a recorded assessment — its matrix version, rating and impacts — is never rewritten.</summary>
    [Fact]
    public async Task ARecordedAssessmentIsNeverRewritten()
    {
        using HttpClient client = host.Api.CreateClient();
        RiskSessions sessions = await client.SignInAsync();
        Guid[] dimensions = await host.DimensionsAsync();
        Guid riskId = await client.RiskAsync(sessions.EntityManager, await host.ProjectAsync());
        await client.CommandOrFailAsync(sessions.Officer, riskId, "assess", RiskDriver.Assessment(dimensions, 3, 3));
        string assessment = Assert.Single(await host.Database.QueryAsync($"SELECT id::text FROM risk.risk_assessment_version WHERE risk_id = '{riskId}'"));

        await AssertRefusedAsync(PostgresErrorCodes.RestrictViolation, "is a recorded assessment and is never changed",
            $"UPDATE risk.risk_assessment_version SET risk_rating_definition_id = risk_rating_definition_id WHERE id = '{assessment}'");
        await AssertRefusedAsync(PostgresErrorCodes.RestrictViolation, "is a recorded assessment and is never changed",
            $"UPDATE risk.risk_assessment_version SET matrix_configuration_version_id = matrix_configuration_version_id WHERE id = '{assessment}'");
        await AssertRefusedAsync(PostgresErrorCodes.RestrictViolation, "is part of a recorded assessment",
            $"UPDATE risk.risk_assessment_impact SET impact_level = 1 WHERE risk_assessment_version_id = '{assessment}'");
        await AssertRefusedAsync(PostgresErrorCodes.RestrictViolation, "is retained and never deleted",
            $"DELETE FROM risk.risk_assessment_version WHERE id = '{assessment}'");
        await AssertRefusedAsync(PostgresErrorCodes.RestrictViolation, "is retained and never deleted",
            $"DELETE FROM risk.risk_assessment_impact WHERE risk_assessment_version_id = '{assessment}'");
        await AssertRefusedAsync(PostgresErrorCodes.RestrictViolation, "never truncated", "TRUNCATE risk.risk_assessment_version CASCADE");

        // No impact is added to a recorded assessment, and no version jumps or repeats its number.
        await AssertRefusedAsync(PostgresErrorCodes.CheckViolation, "ck_risk_assessment_impact_with_assessment", $"""
            INSERT INTO risk.risk_assessment_impact (id, risk_assessment_version_id, impact_dimension_item_id, impact_level, created_at, created_by, updated_at, updated_by)
            SELECT gen_random_uuid(), v.id, '{RiskTestHost.CategoryId}', 1, now(), '{Writer}', now(), '{Writer}' FROM risk.risk_assessment_version v WHERE v.id = '{assessment}'
            """);
        await AssertRefusedAsync(PostgresErrorCodes.CheckViolation, "ck_risk_assessment_version_order", AssessmentInsert(riskId, 3));
        await AssertRefusedAsync(PostgresErrorCodes.UniqueViolation, "ix_risk_assessment_version_risk_id_version_no", $"""
            ALTER TABLE risk.risk_assessment_version DISABLE TRIGGER guard_risk_assessment_version;
            {AssessmentInsert(riskId, 1)}
            """);
    }

    /// <summary>A risk is born IDENTIFIED, moves only along the state machine, raises its reopen count only on a reopen, and is frozen while CLOSED.</summary>
    [Fact]
    public async Task ARiskMovesOnlyAlongItsStateMachine()
    {
        using HttpClient client = host.Api.CreateClient();
        RiskSessions sessions = await client.SignInAsync();
        Guid projectId = await host.ProjectAsync();
        Guid riskId = await client.RiskAsync(sessions.EntityManager, projectId);

        await AssertRefusedAsync(PostgresErrorCodes.CheckViolation, "ck_risk_born", $"""
            INSERT INTO risk.risk (id, project_id, title, title_lang, description, description_lang, risk_category_item_id, status, identified_date, created_at, created_by, updated_at, updated_by)
            VALUES (gen_random_uuid(), '{projectId}', 'Inserted', 'en', 'Inserted', 'en', '{RiskTestHost.CategoryId}', 'ASSESSED', current_date, now(), '{Writer}', now(), '{Writer}')
            """);
        await AssertRefusedAsync(PostgresErrorCodes.RestrictViolation, "IDENTIFIED to TREATMENT is not a step",
            $"UPDATE risk.risk SET status = 'TREATMENT' WHERE id = '{riskId}'");
        await AssertRefusedAsync(PostgresErrorCodes.RestrictViolation, "the reopen count rises by one with each reopen, and only then",
            $"UPDATE risk.risk SET reopened_count = 5 WHERE id = '{riskId}'");
        await AssertRefusedAsync(PostgresErrorCodes.RestrictViolation, "its project never changes",
            $"UPDATE risk.risk SET project_id = '{await host.ProjectAsync()}' WHERE id = '{riskId}'");
        await AssertRefusedAsync(PostgresErrorCodes.CheckViolation, "ck_risk_closed",
            $"UPDATE risk.risk SET status = 'CLOSED' WHERE id = '{riskId}'");
        await AssertRefusedAsync(PostgresErrorCodes.RestrictViolation, "is retained and never deleted", $"DELETE FROM risk.risk WHERE id = '{riskId}'");

        await client.CommandOrFailAsync(sessions.EntityManager, riskId, "close", RiskDriver.Rationale("Raised in error."));
        await AssertRefusedAsync(PostgresErrorCodes.RestrictViolation, "is CLOSED and changes no more until it is reopened",
            $"UPDATE risk.risk SET closure_rationale = 'Rewritten' WHERE id = '{riskId}'");
        await AssertRefusedAsync(PostgresErrorCodes.RestrictViolation, "the reopen count rises by one with each reopen, and only then",
            $"UPDATE risk.risk SET status = 'IDENTIFIED', closed_at = NULL, closed_by_user_id = NULL, closure_rationale = NULL, closure_rationale_lang = NULL WHERE id = '{riskId}'");
        await AssertRefusedAsync(PostgresErrorCodes.RestrictViolation, "is not assessed", AssessmentInsert(riskId, 1));

        // Written whole, a reopen is a step.
        await host.Database.ExecuteRolledBackAsync(
            $"UPDATE risk.risk SET status = 'IDENTIFIED', reopened_count = 1, closed_at = NULL, closed_by_user_id = NULL, closure_rationale = NULL, closure_rationale_lang = NULL WHERE id = '{riskId}'");
    }

    /// <summary>An acceptance only expires or is revoked, never extended, and a risk has one ACTIVE at most; a materialisation never moves.</summary>
    [Fact]
    public async Task AnAcceptanceIsNeverExtendedAndAMaterialisationNeverMoves()
    {
        using HttpClient client = host.Api.CreateClient();
        RiskSessions sessions = await client.SignInAsync();
        Guid riskId = await client.RiskAsync(sessions.EntityManager, await host.ProjectAsync());
        await client.CommandOrFailAsync(sessions.Officer, riskId, "assess", RiskDriver.Assessment(await host.DimensionsAsync(), 1, 1));
        await client.CommandOrFailAsync(sessions.Officer, riskId, "accept",
            new { expiresOn = RiskDriver.Iso(RiskDriver.Today.AddDays(10)), rationale = new { text = "Tolerable.", language = "en" } });
        await client.CommandOrFailAsync(sessions.EntityManager, riskId, "materialise", new { categoryItemId = RiskTestHost.ConcernCategoryId, priorityItemId = RiskTestHost.PriorityId });

        await AssertRefusedAsync(PostgresErrorCodes.RestrictViolation, "its expiry never extended",
            $"UPDATE risk.risk_acceptance SET expires_on = expires_on + 365 WHERE risk_id = '{riskId}'");
        await AssertRefusedAsync(PostgresErrorCodes.CheckViolation, "ck_risk_acceptance_expires_on", $"""
            ALTER TABLE risk.risk_acceptance DISABLE TRIGGER guard_risk_acceptance;
            UPDATE risk.risk_acceptance SET expires_on = (accepted_at AT TIME ZONE 'UTC')::date WHERE risk_id = '{riskId}'
            """);
        await AssertRefusedAsync(PostgresErrorCodes.UniqueViolation, "ix_risk_acceptance_active_risk_id", $"""
            INSERT INTO risk.risk_acceptance (id, risk_id, accepted_by_user_id, accepted_at, expires_on, rationale, rationale_lang, status, created_at, created_by, updated_at, updated_by)
            VALUES (gen_random_uuid(), '{riskId}', '{Writer}', now(), current_date + 30, 'Again', 'en', 'ACTIVE', now(), '{Writer}', now(), '{Writer}')
            """);
        await AssertRefusedAsync(PostgresErrorCodes.RestrictViolation, "REVOKED and changes no more", $"""
            UPDATE risk.risk_acceptance SET status = 'REVOKED', revoked_at = now() WHERE risk_id = '{riskId}';
            UPDATE risk.risk_acceptance SET status = 'ACTIVE', revoked_at = NULL WHERE risk_id = '{riskId}'
            """);
        await AssertRefusedAsync(PostgresErrorCodes.RestrictViolation, "its materialisation is recorded once and never moves",
            $"UPDATE risk.risk SET materialised_at = NULL WHERE id = '{riskId}'");
    }

    private static string AssessmentInsert(Guid riskId, int versionNo) => $"""
        INSERT INTO risk.risk_assessment_version (id, risk_id, version_no, assessed_at, assessed_by_user_id, matrix_configuration_version_id, probability_level,
                                                  overall_impact_level, risk_rating_definition_id, created_at, created_by, updated_at, updated_by)
        SELECT gen_random_uuid(), '{riskId}', {versionNo}, now(), '{Writer}', d.configuration_version_id, 1, 1, d.id, now(), '{Writer}', now(), '{Writer}'
        FROM master_data_config.risk_rating_definition d LIMIT 1
        """;

    private async Task AssertRefusedAsync(string sqlState, string message, string sql)
    {
        PostgresException refused = await Assert.ThrowsAsync<PostgresException>(() => host.Database.ExecuteRolledBackAsync(sql));
        Assert.True(
            refused.SqlState == sqlState && (refused.MessageText.Contains(message, StringComparison.Ordinal) || refused.ConstraintName == message),
            $"{refused.SqlState} {refused.ConstraintName} {refused.MessageText}");
    }
}
