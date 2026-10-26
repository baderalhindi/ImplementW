using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using PMPlatform.Application.Common.Events;
using PMPlatform.Application.Features.Approval.Contracts;
using PMPlatform.Application.Features.Approval.Contracts.Events;
using PMPlatform.Application.Features.Schedule.Contracts;
using PMPlatform.Domain.Common;
using PMPlatform.Infrastructure.Persistence;
using PMPlatform.Tests.Integration.ChangeRequest.Fixtures;
using PMPlatform.Tests.Integration.Identity;

namespace PMPlatform.Tests.Integration.Schedule;

/// <summary>
/// The acceptance criterion's first half: approving a new baseline supersedes the prior ACTIVE one atomically, and a project
/// never has two ACTIVE baselines at once — verified under concurrency, in the application and in the database alone.
/// </summary>
[Collection(ScheduleSuite.Name)]
public sealed class BaselineConcurrencyTests(ScheduleTestHost host)
{
    private const string Seed = IdentityDatabase.SeedPrincipalId;

    /// <summary>
    /// The workbook's validation check: two candidates of one project, both approved, their outcomes applied at the same moment
    /// in two dispatch transactions. Exactly one becomes ACTIVE. The other meets an APPROVED baseline it was not submitted to
    /// replace, carries no change authorisation, and is returned (BR-SCH-034); nothing was superseded.
    /// </summary>
    [Fact]
    public async Task TwoBaselinesApprovedConcurrentlyLeaveExactlyOneActive()
    {
        using HttpClient client = host.Api.CreateClient();
        ScheduleSessions sessions = await client.SignInAsync();
        Guid projectId = await host.ScheduledProjectAsync(client, sessions.ProjectManager);
        await client.ActivityAsync(sessions.ProjectManager, projectId, "1", 0, 10);
        Guid first = AdministrationApi.IdOf(await client.SubmittedBaselineAsync(sessions.ProjectManager, projectId));

        // A second candidate in flight, as a race between two submissions would leave it (the service allows one at a time).
        Guid second = Guid.NewGuid();
        await host.Database.ExecuteAsync($"""
            INSERT INTO schedule.project_baseline (id, project_id, baseline_type, version_no, revision_no, status, baseline_finish_date, created_at, created_by, updated_at, updated_by)
            VALUES ('{second}', '{projectId}', 'APPROVED', 2, 1, 'DRAFT', '{ScheduleDriver.Iso(ScheduleDriver.Day1.AddDays(9))}', now(), '{Seed}', now(), '{Seed}');
            UPDATE schedule.project_baseline SET status = 'SUBMITTED' WHERE id = '{second}';
            """);

        Exception?[] failures = await Task.WhenAll(ApplyAsync(Approved(first)), ApplyAsync(Approved(second)));

        Assert.All(failures, Assert.Null);
        IReadOnlyList<string> states = await host.Database.QueryAsync($"SELECT status FROM schedule.project_baseline WHERE project_id = '{projectId}' ORDER BY status");
        Assert.Equal(["ACTIVE", "RETURNED"], states);
        Assert.Empty(await host.Database.QueryAsync($"SELECT id::text FROM schedule.project_baseline WHERE project_id = '{projectId}' AND superseded_at IS NOT NULL"));
        Assert.Contains("REBASELINE_NOT_AUTHORIZED", await host.Database.QueryAsync($"""
            SELECT a.new_value FROM audit_activity.audit_event e JOIN audit_activity.audit_event_attribute a ON a.audit_event_id = e.id
            WHERE e.event_type = 'Schedule.BaselineReturned' AND a.attribute_name = 'reason' AND e.subject_id IN ('{first}', '{second}')
            """));
    }

    /// <summary>
    /// Without the service: two transactions each make a different baseline of one project ACTIVE. The partial unique index
    /// refuses the second the moment the first commits, so no writer and no interleaving leaves two ACTIVE baselines.
    /// </summary>
    [Fact]
    public async Task TheDatabaseRefusesASecondActiveBaselineWhateverTheWriter()
    {
        Guid projectId = await host.ProjectAsync();
        Guid schedule = Guid.NewGuid();
        Guid activity = Guid.NewGuid();
        Guid[] baselines = [Guid.NewGuid(), Guid.NewGuid()];
        await host.Database.ExecuteAsync($"""
            INSERT INTO schedule.project_schedule (id, project_id, created_at, created_by, updated_at, updated_by) VALUES ('{schedule}', '{projectId}', now(), '{Seed}', now(), '{Seed}');
            INSERT INTO schedule.schedule_activity (id, project_schedule_id, wbs_code, name, name_lang, activity_kind, requested_start_date, planned_start_date, planned_finish_date,
                                                    planned_duration_days, forecast_start_date, forecast_finish_date, status, created_at, created_by, updated_at, updated_by)
            VALUES ('{activity}', '{schedule}', '1', 'Works', 'en', 'ACTIVITY', current_date, current_date, current_date, 1, current_date, current_date, 'PLANNED', now(), '{Seed}', now(), '{Seed}');
            {string.Concat(baselines.Select((b, i) => $"""
                INSERT INTO schedule.project_baseline (id, project_id, baseline_type, version_no, revision_no, status, baseline_finish_date, created_at, created_by, updated_at, updated_by)
                VALUES ('{b}', '{projectId}', 'APPROVED', {i + 1}, 1, 'DRAFT', current_date, now(), '{Seed}', now(), '{Seed}');
                INSERT INTO schedule.baseline_activity (id, project_baseline_id, schedule_activity_id, activity_kind, planned_start_date, planned_finish_date, planned_duration_days,
                                                        created_at, created_by, updated_at, updated_by)
                VALUES (gen_random_uuid(), '{b}', '{activity}', 'ACTIVITY', current_date, current_date, 1, now(), '{Seed}', now(), '{Seed}');
                """))}
            """);

        await using NpgsqlConnection one = new(host.Database.ConnectionString);
        await using NpgsqlConnection two = new(host.Database.ConnectionString);
        await one.OpenAsync();
        await two.OpenAsync();
        await using NpgsqlTransaction firstWriter = await one.BeginTransactionAsync();
        await using NpgsqlTransaction secondWriter = await two.BeginTransactionAsync();
        await ActivateAsync(one, firstWriter, baselines[0]);

        // The second writer waits on the first's uncommitted index entry, then fails when it commits.
        Task blocked = ActivateAsync(two, secondWriter, baselines[1]);
        await Task.Delay(TimeSpan.FromMilliseconds(300));
        Assert.False(blocked.IsCompleted);
        await firstWriter.CommitAsync();

        PostgresException refused = await Assert.ThrowsAsync<PostgresException>(() => blocked);
        Assert.Equal((PostgresErrorCodes.UniqueViolation, "ix_project_baseline_active_project_id"), (refused.SqlState, refused.ConstraintName));
        Assert.Equal([baselines[0].ToString()], await host.Database.QueryAsync($"SELECT id::text FROM schedule.project_baseline WHERE project_id = '{projectId}' AND status = 'ACTIVE'"));
    }

    /// <summary>
    /// The acceptance criterion: a rebaseline implementing an approved WF-08 authorisation (BR-SCH-034) is approved, and in the
    /// same transaction it becomes ACTIVE and the prior baseline SUPERSEDED, pointing at it, at the same instant.
    /// </summary>
    [Fact]
    public async Task ApprovingARebaselineSupersedesThePriorActiveBaselineAtomically()
    {
        using HttpClient client = host.Api.CreateClient();
        ScheduleSessions sessions = await client.SignInAsync();
        Guid projectId = await host.ScheduledProjectAsync(client, sessions.ProjectManager);
        (_, Guid prior) = await host.ApprovedBaselineAsync(client, sessions.ProjectManager, projectId);

        using (HttpResponseMessage unauthorized = await client.PostAsync(ScheduleDriver.Baselines, sessions.ProjectManager, new { projectId }))
        {
            Guid candidate = AdministrationApi.IdOf(await unauthorized.ReadObjectAsync());
            using HttpResponseMessage refused = await client.PostAsync($"{ScheduleDriver.Baselines}/{candidate}/submit", sessions.ProjectManager);
            Assert.Equal((HttpStatusCode.UnprocessableEntity, "SCHEDULE_CHANGE_AUTHORIZATION_REQUIRED"), await refused.RefusalAsync());
            using HttpResponseMessage notApplicable = await client.PostAsync($"{ScheduleDriver.Baselines}/{candidate}/submit", sessions.ProjectManager, new { changeAuthorizationId = Guid.NewGuid() });
            Assert.Equal((HttpStatusCode.UnprocessableEntity, "SCHEDULE_CHANGE_AUTHORIZATION_REQUIRED"), await notApplicable.RefusalAsync());
            await client.SendAsync(HttpMethod.Delete, $"{ScheduleDriver.Baselines}/{candidate}", sessions.ProjectManager);
        }

        Guid authorization = await host.Database.IssueRebaselineAsync(projectId);
        Guid rebaseline = AdministrationApi.IdOf(await client.SubmittedBaselineAsync(sessions.ProjectManager, projectId, authorization));
        // The deleted DRAFT left no history (HARD_DRAFT), so the rebaseline takes version 2.
        Assert.Equal(["1 ACTIVE", "2 SUBMITTED"], await host.BaselineStatesAsync(projectId));

        await host.DecideAndDeliverAsync(rebaseline, ApprovalTaskDecision.Approve);

        Assert.Equal(["1 SUPERSEDED", "2 ACTIVE"], await host.BaselineStatesAsync(projectId));
        Assert.Equal(
            [$"{rebaseline}|true|{authorization}"],
            await host.Database.QueryAsync($"""
                SELECT p.superseded_by_baseline_id || '|' || (p.superseded_at = n.activated_at) || '|' || n.change_authorization_id
                FROM schedule.project_baseline p JOIN schedule.project_baseline n ON n.id = p.superseded_by_baseline_id
                WHERE p.id = '{prior}'
                """));
        Assert.Equal(["Schedule.BaselineCreated", "Schedule.BaselineSubmitted", "Schedule.BaselineActivated", "Schedule.BaselineSuperseded"], await host.AuditEventsAsync(prior));
    }

    /// <summary>
    /// SCH-CC-21, BR-SCH-030: an activation that fails after the prior baseline was superseded leaves nothing behind — the
    /// prior stays ACTIVE, the candidate SUBMITTED with no copy — and the outcome, delivered again, then applies whole.
    /// </summary>
    [Fact]
    public async Task AFailedActivationLeavesThePriorBaselineActiveAndChangesNothing()
    {
        using HttpClient client = host.Api.CreateClient();
        ScheduleSessions sessions = await client.SignInAsync();
        Guid projectId = await host.ScheduledProjectAsync(client, sessions.ProjectManager);
        (_, Guid prior) = await host.ApprovedBaselineAsync(client, sessions.ProjectManager, projectId);
        Guid rebaseline = AdministrationApi.IdOf(await client.SubmittedBaselineAsync(sessions.ProjectManager, projectId, await host.Database.IssueRebaselineAsync(projectId)));
        ApprovalInstanceDetail run = Assert.Single(await host.RunsAsync(rebaseline));

        string failActivation = $"fail_activation_{rebaseline:N}";
        await host.Database.ExecuteAsync($"""
            CREATE FUNCTION schedule.{failActivation}() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN
                IF NEW.id = '{rebaseline}' AND NEW.status = 'ACTIVE' THEN RAISE EXCEPTION 'activation fails'; END IF;
                RETURN NEW;
            END;
            $$;
            CREATE TRIGGER {failActivation} BEFORE UPDATE ON schedule.project_baseline FOR EACH ROW EXECUTE FUNCTION schedule.{failActivation}();
            """);
        try
        {
            await host.WithScopeAsync(services => services.GetRequiredService<IApprovalWorkflowService>().DecideAsync(
                ScheduleDriver.Person(2), run.Tasks.Single().Id, ApprovalTaskDecision.Approve, null, CancellationToken.None));
            Assert.False(await host.DeliverAsync(run.Id));

            Assert.Equal(["1 ACTIVE", "2 SUBMITTED"], await host.BaselineStatesAsync(projectId));
            Assert.Equal(["|"], await host.Database.QueryAsync($"SELECT coalesce(superseded_at::text, '') || '|' || coalesce(superseded_by_baseline_id::text, '') FROM schedule.project_baseline WHERE id = '{prior}'"));
            Assert.Equal(["0"], await host.Database.QueryAsync($"SELECT count(*)::text FROM schedule.baseline_activity WHERE project_baseline_id = '{rebaseline}'"));
        }
        finally
        {
            await host.Database.ExecuteAsync($"DROP TRIGGER {failActivation} ON schedule.project_baseline; DROP FUNCTION schedule.{failActivation}();");
        }

        Assert.True(await host.DeliverAsync(run.Id));
        Assert.Equal(["1 SUPERSEDED", "2 ACTIVE"], await host.BaselineStatesAsync(projectId));
    }

    /// <summary>EV-4, EV-5: the same outcome handed to the handler again finds the candidate no longer SUBMITTED and changes nothing.</summary>
    [Fact]
    public async Task AnOutcomeHandedToTheHandlerTwiceIsAppliedOnce()
    {
        using HttpClient client = host.Api.CreateClient();
        ScheduleSessions sessions = await client.SignInAsync();
        Guid projectId = await host.ScheduledProjectAsync(client, sessions.ProjectManager);
        (_, Guid baseline) = await host.ApprovedBaselineAsync(client, sessions.ProjectManager, projectId);
        string before = Assert.Single(await host.Database.QueryAsync($"SELECT to_jsonb(b)::text FROM schedule.project_baseline b WHERE b.id = '{baseline}'"));

        Assert.Null(await ApplyAsync(Approved(baseline)));

        Assert.Equal(before, Assert.Single(await host.Database.QueryAsync($"SELECT to_jsonb(b)::text FROM schedule.project_baseline b WHERE b.id = '{baseline}'")));
        Assert.Contains("Schedule.ApprovalOutcomeIgnored", await host.AuditEventsAsync(baseline));
    }

    /// <summary>One outcome applied as the outbox dispatcher applies it: in a transaction of its own scope, committed if the handler returns.</summary>
    private async Task<Exception?> ApplyAsync(ApprovalOutcomeRecorded outcome)
    {
        await Task.Yield();
        await using AsyncServiceScope scope = host.Api.Services.CreateAsyncScope();
        PMPlatformDbContext context = scope.ServiceProvider.GetRequiredService<PMPlatformDbContext>();
        await using IDbContextTransaction transaction = await context.Database.BeginTransactionAsync();
        try
        {
            await scope.ServiceProvider.GetServices<IApprovalOutcomeHandler>().Single(h => h.SubjectModule == ScheduleApprovalRouting.SubjectModule)
                .HandleAsync(outcome, CancellationToken.None);
            await transaction.CommitAsync();
            return null;
        }
        catch (Exception exception) when (exception is InvalidOperationException or DbUpdateException or PostgresException)
        {
            return exception;
        }
    }

    private static ApprovalOutcomeRecorded Approved(Guid baselineId)
    {
        Guid runId = Guid.NewGuid();
        string key = $"apr-{runId:D}-outcome";
        return new ApprovalOutcomeRecorded
        {
            EventId = Guid.NewGuid(),
            EventType = ApprovalOutcomeRecorded.Type,
            SchemaVersion = ApprovalOutcomeRecorded.CurrentSchemaVersion,
            Kind = EventKind.DomainEvent,
            MessageKey = EventMessageKey.Of(ApprovalOutcomeRecorded.Type, key),
            IdempotencyKey = key,
            OccurredAt = DateTimeOffset.UtcNow,
            SourceModule = "Approval",
            CorrelationId = Guid.NewGuid(),
            Actor = new EventActor(AuditActorType.User, ScheduleDriver.Person(2)),
            Subject = new EventSubject(ScheduleApprovalRouting.SubjectModule, ScheduleApprovalRouting.SubjectType, baselineId, 1),
            Scope = new EventScope(null, null, null),
            Data = new ApprovalOutcomeData(runId, ScheduleApprovalRouting.BaselineRoutingKey, ApprovalOutcomeDecision.Approved, DateTimeOffset.UtcNow, ScheduleDriver.Person(2), Guid.NewGuid()),
        };
    }

    private static async Task ActivateAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid baselineId)
    {
        await using NpgsqlCommand command = new($"UPDATE schedule.project_baseline SET status = 'ACTIVE', activated_at = now() WHERE id = '{baselineId}'", connection, transaction);
        await command.ExecuteNonQueryAsync();
    }
}
