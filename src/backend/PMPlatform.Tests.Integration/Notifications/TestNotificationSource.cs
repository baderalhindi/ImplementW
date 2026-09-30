using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using PMPlatform.Application.Common.Events;
using PMPlatform.Domain.Common;
using PMPlatform.Infrastructure.Persistence;
using PMPlatform.Tests.Integration.Identity;

namespace PMPlatform.Tests.Integration.Notifications;

/// <summary>
/// The source modules the tests play: ManagementConcern escalates (<c>test_source.concern_escalation</c>) and Risk schedules
/// review reminders (<c>test_source.risk</c>). A source's transaction writes its business row and stages its intent through
/// the platform's <see cref="IOutbox"/>, as a real producer does (event-conventions EV-6), and commits both or neither.
/// </summary>
internal static class TestNotificationSource
{
    public const string Schema = """
        CREATE SCHEMA test_source;
        CREATE TABLE test_source.concern_escalation (id uuid PRIMARY KEY, concern_reference text NOT NULL);
        CREATE TABLE test_source.risk (id uuid PRIMARY KEY, risk_reference text NOT NULL, status text NOT NULL);
        """;

    /// <summary>ManagementConcern's escalation: the escalation row is the fact, its id the intent's idempotency key (TASK-057).</summary>
    public static SourceTransaction Escalation(Guid escalationId, string concernReference, Guid projectId, Guid departmentId, Guid? entityId, DateTimeOffset now) =>
        new(
            $"INSERT INTO test_source.concern_escalation (id, concern_reference) VALUES ({escalationId}, {concernReference})",
            Intent(NotificationTestHost.ConcernModule, NotificationTestHost.EscalatedEvent, NotificationTestHost.EscalationFamily, escalationId,
                new EventSubject(NotificationTestHost.ConcernModule, "ManagementConcern", Guid.NewGuid(), 1), new EventScope(projectId, departmentId, entityId),
                $"/projects/{projectId}/issues-challenges/{escalationId}",
                [new NotificationParameter("concernReference", concernReference), new NotificationParameter("severity", "HIGH")],
                now));

    /// <summary>Risk's review reminder: the risk is OPEN, and the reminder, due at <paramref name="dueAt"/>, holds only while it still is.</summary>
    public static SourceTransaction RiskWithReviewReminder(Guid riskId, string riskReference, DateTimeOffset dueAt, DateTimeOffset now) =>
        new(
            $"INSERT INTO test_source.risk (id, risk_reference, status) VALUES ({riskId}, {riskReference}, 'OPEN')",
            Intent(NotificationTestHost.RiskModule, NotificationTestHost.ReviewDueEvent, NotificationTestHost.ReminderFamily, riskId,
                new EventSubject(NotificationTestHost.RiskModule, "Risk", riskId, 1),
                new EventScope(NotificationTestHost.EntityProjectId, NotificationTestHost.DepartmentId, NotificationTestHost.EntityId),
                $"/projects/{NotificationTestHost.EntityProjectId}/risks/{riskId}",
                [new NotificationParameter("riskReference", riskReference)],
                now,
                dueAt,
                new NotificationCondition("Risk", riskId, ["OPEN", "IN_TREATMENT"])));

    public static NotificationIntentEnvelope Intent(
        string sourceModule, string eventType, string familyCode, Guid reference, EventSubject subject, EventScope scope, string deepLink,
        IReadOnlyList<NotificationParameter> parameters, DateTimeOffset now, DateTimeOffset? scheduledFor = null, NotificationCondition? condition = null) =>
        new()
        {
            EventId = Guid.NewGuid(),
            EventType = eventType,
            SchemaVersion = 1,
            Kind = EventKind.NotificationIntent,
            MessageKey = EventMessageKey.Of(eventType, reference.ToString()),
            IdempotencyKey = reference.ToString(),
            OccurredAt = now,
            SourceModule = sourceModule,
            CorrelationId = Guid.NewGuid(),
            Actor = new EventActor(AuditActorType.User, NotificationTestHost.UserId(2)),
            Subject = subject,
            Scope = scope,
            Data = new NotificationIntentData(familyCode, reference.ToString(), scheduledFor, deepLink, parameters, condition),
        };
}

/// <summary>A source's business write and the intent it publishes with it.</summary>
internal sealed record SourceTransaction(FormattableString BusinessWrite, NotificationIntentEnvelope Intent)
{
    /// <summary>
    /// Runs the source's transaction: the business write and the staged intent, saved, then <paramref name="beforeEnd"/> while
    /// the transaction is still open, then commit — or roll back when <paramref name="commit"/> is false.
    /// </summary>
    public async Task RunAsync(IdentityApiFactory api, bool commit = true, Func<Task>? beforeEnd = null)
    {
        await using AsyncServiceScope scope = api.Services.CreateAsyncScope();
        PMPlatformDbContext context = scope.ServiceProvider.GetRequiredService<PMPlatformDbContext>();
        await using IDbContextTransaction transaction = await context.Database.BeginTransactionAsync();
        await context.Database.ExecuteSqlAsync(BusinessWrite);
        scope.ServiceProvider.GetRequiredService<IOutbox>().Stage(Intent);
        await context.SaveChangesAsync();
        if (beforeEnd is not null)
        {
            await beforeEnd();
        }

        if (commit)
        {
            await transaction.CommitAsync();
        }
        else
        {
            await transaction.RollbackAsync();
        }
    }
}
