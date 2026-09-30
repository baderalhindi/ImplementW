using System.Net;
using Microsoft.Extensions.DependencyInjection;
using PMPlatform.Application.Common.Events;
using PMPlatform.Tests.Integration.Identity;

namespace PMPlatform.Tests.Integration.Notifications;

/// <summary>
/// TASK-039 acceptance criteria 1 and 2, and the workbook's first validation check: a notification is never sent before
/// its source's transaction is durably committed, and a delivery that fails — the mail relay killed — never rolls back
/// the source, while the notification goes to retry and then to the dead letter.
/// </summary>
[Collection(NotificationSuite.Name)]
public sealed class CommitBeforeNotifyTests(NotificationTestHost host)
{
    [Fact]
    public async Task NothingIsSentWhileTheSourceTransactionIsOpenAndNothingEverForOneRolledBack()
    {
        Guid rolledBack = Guid.NewGuid();
        Guid committed = Guid.NewGuid();
        DateTimeOffset now = host.Clock.GetUtcNow();

        // Both workers run while the source's transaction is open: its intent is not visible to them, so nothing moves.
        await TestNotificationSource.Escalation(rolledBack, "ISS-RB-1", NotificationTestHost.EntityProjectId, NotificationTestHost.DepartmentId, NotificationTestHost.EntityId, now)
            .RunAsync(host.Api, commit: false, beforeEnd: async () =>
            {
                await host.Api.DeliverAsync();
                Assert.Empty(await host.Database.QueryAsync(NotificationDriver.IntentOf(NotificationTestHost.EscalatedEvent, rolledBack)));
            });
        await TestNotificationSource.Escalation(committed, "ISS-CM-1", NotificationTestHost.EntityProjectId, NotificationTestHost.DepartmentId, NotificationTestHost.EntityId, now)
            .RunAsync(host.Api, beforeEnd: async () =>
            {
                await host.Api.DeliverAsync();
                Assert.Empty(await host.Database.QueryAsync(NotificationDriver.IntentOf(NotificationTestHost.EscalatedEvent, committed)));
                Assert.DoesNotContain(host.Smtp.Received, m => m.Content.Contains("ISS-CM-1", StringComparison.Ordinal));
            });

        await host.Api.DeliverAsync();

        // The rolled-back source left no fact, no message and no notification, and never will.
        Assert.Equal(["0|0"], await host.Database.QueryAsync($"""
            SELECT (SELECT count(*) FROM test_source.concern_escalation WHERE id = '{rolledBack}')::text || '|'
                || (SELECT count(*) FROM common.outbox_message WHERE message_key = '{NotificationTestHost.EscalatedEvent}:{rolledBack}')::text
            """));
        Assert.Empty(await host.Database.QueryAsync(NotificationDriver.IntentOf(NotificationTestHost.EscalatedEvent, rolledBack)));

        // The committed one was received after its commit, and delivered.
        Assert.Equal(["COMPLETED|"], await host.Database.QueryAsync(NotificationDriver.IntentOf(NotificationTestHost.EscalatedEvent, committed)));
        Assert.Equal(["true"], await host.Database.QueryAsync($"""
            SELECT (i.received_at >= m.occurred_at AND m.dispatched_at IS NOT NULL)::text
            FROM notifications.notification_intent i JOIN common.outbox_message m ON m.message_key = i.source_event_type || ':' || i.source_reference
            WHERE i.source_reference = '{committed}'
            """));
        Assert.Contains(host.Smtp.To(NotificationTestHost.EmailOf(2)), m => m.Content.Contains("ISS-CM-1", StringComparison.Ordinal));
    }

    /// <summary>The workbook's check: "trigger a source event, kill the email provider connection, and confirm the source transaction is unaffected while the notification enters retry/dead-letter".</summary>
    [Fact]
    public async Task AKilledMailRelayNeverRollsBackTheSourceAndTheEmailGoesToRetryThenDeadLetter()
    {
        Guid escalation = Guid.NewGuid();
        await host.Smtp.StopAsync();
        try
        {
            await TestNotificationSource.Escalation(escalation, "ISS-KILL-1", NotificationTestHost.EntityProjectId, NotificationTestHost.DepartmentId, NotificationTestHost.EntityId, host.Clock.GetUtcNow())
                .RunAsync(host.Api);
            await host.Api.DeliverAsync();

            // The source's fact stands; in-app reached everyone; e-mail failed its first attempt and waits its back-off.
            Assert.Equal(["1"], await host.Database.QueryAsync($"SELECT count(*)::text FROM test_source.concern_escalation WHERE id = '{escalation}'"));
            Assert.Equal(["ROUTED|"], await host.Database.QueryAsync(NotificationDriver.IntentOf(NotificationTestHost.EscalatedEvent, escalation)));
            Assert.Equal(
                ["2|EMAIL|FAILED|", "2|IN_APP|SENT|", "2|SMS|SENT|", "3|EMAIL|FAILED|", "3|IN_APP|SENT|", "3|SMS|SUPPRESSED|MOBILE_UNVERIFIED",
                 "8|EMAIL|FAILED|", "8|IN_APP|SENT|", "8|SMS|SUPPRESSED|MOBILE_UNVERIFIED"],
                await host.Database.QueryAsync(NotificationDriver.DeliveriesOf(NotificationTestHost.EscalatedEvent, escalation)));
            Assert.Equal(["1|SocketException"], await EmailAttemptsAsync(escalation));

            // Not due yet: another pass tries nothing.
            await host.Api.ProcessAsync();
            Assert.Equal(["1|SocketException"], await EmailAttemptsAsync(escalation));

            // 1, 2, 4 and 8 minutes later, attempts 2 to 5; the fifth dead-letters it.
            foreach (int minutes in new[] { 1, 2, 4, 8 })
            {
                host.Clock.Advance(TimeSpan.FromMinutes(minutes));
                await host.Api.ProcessAsync();
            }

            Assert.Equal(["5|SocketException"], await EmailAttemptsAsync(escalation));
            Assert.Equal(["3"], await host.Database.QueryAsync($"""
                SELECT count(*)::text FROM notifications.notification_delivery d JOIN notifications.notification_intent i ON i.id = d.notification_intent_id
                WHERE i.source_reference = '{escalation}' AND d.status = 'DEAD_LETTER' AND d.dead_lettered_at IS NOT NULL AND d.next_attempt_at IS NULL
                """));
            Assert.Equal(["COMPLETED|"], await host.Database.QueryAsync(NotificationDriver.IntentOf(NotificationTestHost.EscalatedEvent, escalation)));
            Assert.Equal(["1"], await host.Database.QueryAsync($"SELECT count(*)::text FROM test_source.concern_escalation WHERE id = '{escalation}'"));

            // The relay comes back; an operator redrives one dead letter and it is sent.
            await host.Smtp.RestartAsync();
            using HttpClient client = host.Api.CreateClient();
            string admin = (await client.SignInOrFailAsync(1)).AccessToken;
            string deadLetter = (await host.Database.QueryAsync($"""
                SELECT d.id::text FROM notifications.notification_delivery d JOIN notifications.notification_intent i ON i.id = d.notification_intent_id
                WHERE i.source_reference = '{escalation}' AND d.channel = 'EMAIL' AND d.recipient_user_id = '{NotificationTestHost.UserId(2)}'
                """)).Single();
            using HttpResponseMessage listed = await client.GetAsync("/api/v1/notification-deliveries?status=DEAD_LETTER&channel=EMAIL", admin);
            Assert.Equal(HttpStatusCode.OK, listed.StatusCode);
            Assert.Contains(deadLetter, await listed.Content.ReadAsStringAsync(), StringComparison.Ordinal);

            using HttpResponseMessage redriven = await client.PostAsync($"/api/v1/notification-deliveries/{deadLetter}/redrive", admin);
            Assert.Equal(HttpStatusCode.OK, redriven.StatusCode);
            Assert.Equal(["ROUTED|"], await host.Database.QueryAsync(NotificationDriver.IntentOf(NotificationTestHost.EscalatedEvent, escalation)));
            await host.Api.ProcessAsync();

            Assert.Equal(["SENT|1"], await host.Database.QueryAsync($"SELECT status || '|' || attempt_count FROM notifications.notification_delivery WHERE id = '{deadLetter}'"));
            Assert.Contains(host.Smtp.To(NotificationTestHost.EmailOf(2)), m => m.Content.Contains("ISS-KILL-1", StringComparison.Ordinal));
            Assert.Equal(["1"], await host.Database.QueryAsync($"""
                SELECT count(*)::text FROM audit_activity.audit_event WHERE event_type = 'Notifications.DeliveryRedriven' AND subject_id = '{deadLetter}'
                """));

            using HttpResponseMessage again = await client.PostAsync($"/api/v1/notification-deliveries/{deadLetter}/redrive", admin);
            Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        }
        finally
        {
            await host.Smtp.RestartAsync();
            host.Clock.Reset();
        }
    }

    [Fact]
    public async Task AnIntentDeliveredTwiceIsReceivedOnce()
    {
        Guid escalation = Guid.NewGuid();
        await TestNotificationSource.Escalation(escalation, "ISS-DUP-1", NotificationTestHost.EntityProjectId, NotificationTestHost.DepartmentId, NotificationTestHost.EntityId, host.Clock.GetUtcNow())
            .RunAsync(host.Api);
        await host.Api.DispatchAsync();

        // The dispatcher's at-least-once delivery, repeated by hand.
        string payload = (await host.Database.QueryAsync($"SELECT payload::text FROM common.outbox_message WHERE message_key = '{NotificationTestHost.EscalatedEvent}:{escalation}'")).Single();
        await using (AsyncServiceScope scope = host.Api.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<INotificationIntentConsumer>().HandleAsync(payload, CancellationToken.None);
        }

        Assert.Equal(["1"], await host.Database.QueryAsync($"SELECT count(*)::text FROM notifications.notification_intent WHERE source_reference = '{escalation}'"));
    }

    /// <summary>EV-10 and EV-3: a deep link is an SPA route with no scheme, host or query string (which could carry a token).</summary>
    [Theory]
    [InlineData("absolute link")]
    [InlineData("protocol-relative link")]
    [InlineData("query string")]
    [InlineData("relative link")]
    [InlineData("reminder without condition")]
    [InlineData("reference not the idempotency key")]
    [InlineData("parameter named deepLink")]
    public async Task AnIntentThatBreaksTheContractStaysInTheOutboxWithItsError(string fault)
    {
        Guid escalation = Guid.NewGuid();
        SourceTransaction source = TestNotificationSource.Escalation(
            escalation, "ISS-BAD-1", NotificationTestHost.EntityProjectId, NotificationTestHost.DepartmentId, NotificationTestHost.EntityId, host.Clock.GetUtcNow());
        NotificationIntentData data = source.Intent.Data;
        NotificationIntentData broken = fault switch
        {
            "absolute link" => data with { DeepLink = "https://evil.test/reports/1" },
            "protocol-relative link" => data with { DeepLink = "//evil.test/reports/1" },
            "query string" => data with { DeepLink = "/reports/1?token=secret" },
            "relative link" => data with { DeepLink = "reports/1" },
            "reminder without condition" => data with { ScheduledFor = host.Clock.GetUtcNow().AddDays(1) },
            "reference not the idempotency key" => data with { SourceReference = Guid.NewGuid().ToString() },
            "parameter named deepLink" => data with { Parameters = [new NotificationParameter("deepLink", "https://evil.test")] },
            _ => throw new ArgumentOutOfRangeException(nameof(fault)),
        };
        await (source with { Intent = source.Intent with { Data = broken } }).RunAsync(host.Api);

        await host.Api.DeliverAsync();

        Assert.Equal(["1|JsonException|false"], await host.Database.QueryAsync($"""
            SELECT attempt_count || '|' || last_error || '|' || (dispatched_at IS NOT NULL)::text
            FROM common.outbox_message WHERE message_key = '{NotificationTestHost.EscalatedEvent}:{escalation}'
            """));
        Assert.Empty(await host.Database.QueryAsync($"SELECT id::text FROM notifications.notification_intent WHERE source_event_type = '{NotificationTestHost.EscalatedEvent}' AND source_reference IN ('{escalation}', '{broken.SourceReference}')"));
        Assert.Equal(["1"], await host.Database.QueryAsync($"SELECT count(*)::text FROM test_source.concern_escalation WHERE id = '{escalation}'"));
    }

    private Task<IReadOnlyList<string>> EmailAttemptsAsync(Guid escalation) => host.Database.QueryAsync($"""
        SELECT DISTINCT d.attempt_count || '|' || d.failure_reason
        FROM notifications.notification_delivery d JOIN notifications.notification_intent i ON i.id = d.notification_intent_id
        WHERE i.source_reference = '{escalation}' AND d.channel = 'EMAIL'
        """);
}
