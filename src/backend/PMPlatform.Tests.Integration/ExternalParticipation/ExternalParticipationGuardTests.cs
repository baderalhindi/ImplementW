using Npgsql;
using PMPlatform.Tests.Integration.Identity;
using static PMPlatform.Tests.Integration.ExternalParticipation.ExternalParticipationDriver;

namespace PMPlatform.Tests.Integration.ExternalParticipation;

/// <summary>
/// Migration <c>TASK-066_GuardExternalParticipationHistory</c>: what the API refuses, the database refuses too, whoever writes — so no
/// administrator, script or later code path can silently rewrite an entity's submitted answer, cross the entity boundary, or apply a revision
/// without its attempt (TASK-066 acceptance criteria 2 and 3; WF-13 BR-EXT-048).
/// </summary>
[Collection(ExternalParticipationSuite.Name)]
public sealed class ExternalParticipationGuardTests(ExternalParticipationTestHost host)
{
    [Fact]
    public async Task ASubmittedRevisionCannotBeRewrittenByAnyWriter()
    {
        using HttpClient client = host.Api.CreateClient();
        ParticipationSessions sessions = await client.SignInAsync();
        Guid projectId = await host.ProjectAsync();
        await host.GrantProjectAsync(8, projectId);
        Guid requestId = await client.IssuedRequestAsync(sessions, projectId, ExternalParticipationTestHost.EntityA, ExternalParticipationTestHost.InformationTypeId, null, responder: 8);
        Guid contributionId = AdministrationApi.IdOf(await client.AnswerAsync(sessions.EntityA, requestId, Information("As submitted.")));

        string[] rewrites =
        [
            $"UPDATE external_participation.external_contribution_field SET proposed_value = 'Rewritten.' WHERE external_contribution_id = '{contributionId}'",
            $"DELETE FROM external_participation.external_contribution_field WHERE external_contribution_id = '{contributionId}'",
            $"""
             INSERT INTO external_participation.external_contribution_field (id, external_contribution_id, field_code, proposed_value, created_at, created_by, updated_at, updated_by)
             VALUES (gen_random_uuid(), '{contributionId}', 'asOfDate', '2026-01-01', now(), '{Person(5)}', now(), '{Person(5)}')
             """,
            $"UPDATE external_participation.external_contribution SET status = 'DRAFT', submitted_at = NULL WHERE id = '{contributionId}'",
            $"UPDATE external_participation.external_contribution SET submitted_at = submitted_at - interval '1 day' WHERE id = '{contributionId}'",
            $"UPDATE external_participation.external_contribution SET status = 'APPLIED', review_started_at = now(), reviewed_at = now(), reviewed_by_user_id = '{Person(5)}' WHERE id = '{contributionId}'",
            $"DELETE FROM external_participation.external_contribution WHERE id = '{contributionId}'",
            $"UPDATE external_participation.external_update_request SET instructions = 'Asked for something else.' WHERE id = '{requestId}'",
            "TRUNCATE external_participation.external_contribution_field CASCADE",
        ];

        foreach (string rewrite in rewrites)
        {
            PostgresException refused = await Assert.ThrowsAsync<PostgresException>(() => host.Database.ExecuteRolledBackAsync(rewrite));
            Assert.True(refused.SqlState is PostgresErrorCodes.RestrictViolation or PostgresErrorCodes.CheckViolation, $"{refused.SqlState}: {rewrite}");
        }

        Assert.Equal(["SUBMITTED|As submitted."], await host.Database.QueryAsync($"""
            SELECT c.status || '|' || f.proposed_value FROM external_participation.external_contribution c
            JOIN external_participation.external_contribution_field f ON f.external_contribution_id = c.id WHERE c.id = '{contributionId}'
            """));
    }

    /// <summary>ADR-013 in the rows: a request answers to an external user of its own entity and is reviewed by an internal user; a revision likewise.</summary>
    [Fact]
    public async Task ARequestsPeopleAreItsEntitysOnOneSideAndAhdasOnTheOther()
    {
        using HttpClient client = host.Api.CreateClient();
        ParticipationSessions sessions = await client.SignInAsync();
        Guid projectId = await host.ProjectAsync();
        await host.GrantProjectAsync(8, projectId);
        Guid requestId = await client.IssuedRequestAsync(sessions, projectId, ExternalParticipationTestHost.EntityA, ExternalParticipationTestHost.InformationTypeId, null, responder: 8);
        Guid contributionId = AdministrationApi.IdOf(await client.CreatedOrFailAsync(sessions.EntityA, Contributions,
            new { externalUpdateRequestId = requestId, fields = Information("Draft.") }));

        foreach (string crossing in new[]
                 {
                     $"UPDATE external_participation.external_update_request SET responsible_user_id = '{Person(7)}' WHERE id = '{requestId}'",
                     $"UPDATE external_participation.external_update_request SET responsible_user_id = '{Person(5)}' WHERE id = '{requestId}'",
                     $"UPDATE external_participation.external_update_request SET reviewer_user_id = '{Person(8)}' WHERE id = '{requestId}'",
                     $"UPDATE external_participation.external_update_request SET external_entity_id = '{ExternalParticipationTestHost.EntityB}' WHERE id = '{requestId}'",
                     $"UPDATE external_participation.external_contribution SET contributor_user_id = '{Person(7)}' WHERE id = '{contributionId}'",
                 })
        {
            PostgresException refused = await Assert.ThrowsAsync<PostgresException>(() => host.Database.ExecuteRolledBackAsync(crossing));
            Assert.Equal(PostgresErrorCodes.RestrictViolation, refused.SqlState);
        }
    }

    /// <summary>
    /// At commit: a revision of a source record is APPLIED only with its APPLIED attempt, and an attempt is made only at an accepted revision —
    /// so no direct write can mark a revision applied without the adapter having applied it, or record an application of an unreviewed one.
    /// </summary>
    [Fact]
    public async Task ARevisionIsAppliedOnlyThroughItsAppliedAttempt()
    {
        using HttpClient client = host.Api.CreateClient();
        ParticipationSessions sessions = await client.SignInAsync();
        Guid projectId = await host.ProjectAsync();
        await host.GrantProjectAsync(8, projectId);
        Guid taskId = await client.StartedTaskAsync(sessions, projectId, "Drainage");
        Guid requestId = await client.IssuedRequestAsync(sessions, projectId, ExternalParticipationTestHost.EntityA, ExternalParticipationTestHost.TaskProgressTypeId, taskId, responder: 8);
        Guid contributionId = AdministrationApi.IdOf(await client.AnswerAsync(sessions.EntityA, requestId, Percent(25m)));

        PostgresException unreviewed = await Assert.ThrowsAsync<PostgresException>(() => host.Database.ExecuteAsync($"""
            INSERT INTO external_participation.source_application (id, external_contribution_id, attempt_no, idempotency_key, correlation_id, status, expected_target_revision_no,
                                                                  attempted_by_user_id, attempted_at, completed_at, created_at, created_by, updated_at, updated_by)
            VALUES (gen_random_uuid(), '{contributionId}', 1, 'direct:{Guid.NewGuid():N}', gen_random_uuid(), 'APPLIED', 1, '{Person(5)}', now(), now(), now(), '{Person(5)}', now(), '{Person(5)}')
            """));
        Assert.Equal(PostgresErrorCodes.RestrictViolation, unreviewed.SqlState);

        await client.DecideAsync(sessions.ProjectManager, contributionId, "accept");
        PostgresException withoutAttempt = await Assert.ThrowsAsync<PostgresException>(() => host.Database.ExecuteAsync(
            $"UPDATE external_participation.external_contribution SET status = 'APPLIED' WHERE id = '{contributionId}'"));
        Assert.Equal(PostgresErrorCodes.RestrictViolation, withoutAttempt.SqlState);
        Assert.Equal(["ACCEPTED_PENDING_APPLICATION"], await host.Database.QueryAsync($"SELECT status FROM external_participation.external_contribution WHERE id = '{contributionId}'"));
    }

    /// <summary>An attempt is history: never deleted, never changed but by the one revalidation of a CONFLICT; and a revision has one APPLIED attempt.</summary>
    [Fact]
    public async Task AnApplicationAttemptIsNeverRewritten()
    {
        using HttpClient client = host.Api.CreateClient();
        ParticipationSessions sessions = await client.SignInAsync();
        Guid projectId = await host.ProjectAsync();
        await host.GrantProjectAsync(8, projectId);
        Guid taskId = await client.StartedTaskAsync(sessions, projectId, "Paving");
        Guid requestId = await client.IssuedRequestAsync(sessions, projectId, ExternalParticipationTestHost.EntityA, ExternalParticipationTestHost.TaskProgressTypeId, taskId, responder: 8);
        Guid contributionId = AdministrationApi.IdOf(await client.AnswerAsync(sessions.EntityA, requestId, Percent(15m)));
        await client.DecideAsync(sessions.ProjectManager, contributionId, "accept");
        using (HttpResponseMessage applied = await client.ApplyAsync(sessions.ProjectManager, contributionId))
        {
            Assert.Equal(System.Net.HttpStatusCode.Created, applied.StatusCode);
        }

        foreach (string rewrite in new[]
                 {
                     $"UPDATE external_participation.source_application SET status = 'FAILED', failure_code = 'X' WHERE external_contribution_id = '{contributionId}'",
                     $"UPDATE external_participation.source_application SET expected_target_revision_no = 0 WHERE external_contribution_id = '{contributionId}'",
                     $"DELETE FROM external_participation.source_application WHERE external_contribution_id = '{contributionId}'",
                 })
        {
            PostgresException refused = await Assert.ThrowsAsync<PostgresException>(() => host.Database.ExecuteRolledBackAsync(rewrite));
            Assert.True(refused.SqlState is PostgresErrorCodes.RestrictViolation or PostgresErrorCodes.CheckViolation, $"{refused.SqlState}: {rewrite}");
        }

        PostgresException secondApplied = await Assert.ThrowsAsync<PostgresException>(() => host.Database.ExecuteAsync($"""
            INSERT INTO external_participation.source_application (id, external_contribution_id, attempt_no, idempotency_key, correlation_id, status, expected_target_revision_no,
                                                                  attempted_by_user_id, attempted_at, completed_at, created_at, created_by, updated_at, updated_by)
            VALUES (gen_random_uuid(), '{contributionId}', 2, 'direct:{Guid.NewGuid():N}', gen_random_uuid(), 'APPLIED', 1, '{Person(5)}', now(), now(), now(), '{Person(5)}', now(), '{Person(5)}')
            """));
        Assert.Equal(PostgresErrorCodes.UniqueViolation, secondApplied.SqlState);
    }
}
