using Npgsql;

namespace PMPlatform.Tests.Integration.Notifications;

/// <summary>
/// Migration TASK-039_GuardNotificationHistory: what was received and what was sent are history, fixed in the database
/// whoever writes to it — nothing is deleted, the rendered content and the source's facts never change, and states move
/// only along the runtime's transitions.
/// </summary>
[Collection(NotificationSuite.Name)]
public sealed class NotificationHistoryGuardTests(NotificationTestHost host)
{
    [Fact]
    public async Task TheDatabaseRefusesToRewriteOrDeleteNotificationHistory()
    {
        Guid escalation = Guid.NewGuid();
        await TestNotificationSource.Escalation(escalation, "ISS-GUARD-1", NotificationTestHost.EntityProjectId, NotificationTestHost.DepartmentId, NotificationTestHost.EntityId, host.Clock.GetUtcNow())
            .RunAsync(host.Api);
        await host.Api.DeliverAsync();
        string intent = $"(SELECT id FROM notifications.notification_intent WHERE source_reference = '{escalation}')";
        string inApp = $"(SELECT id FROM notifications.notification_delivery WHERE notification_intent_id = {intent} AND channel = 'IN_APP' AND recipient_user_id = '{NotificationTestHost.UserId(2)}')";
        string sms = $"(SELECT id FROM notifications.notification_delivery WHERE notification_intent_id = {intent} AND channel = 'SMS' AND recipient_user_id = '{NotificationTestHost.UserId(3)}')";

        string[] refused =
        [
            $"DELETE FROM notifications.notification_intent WHERE id = {intent}",
            $"DELETE FROM notifications.notification_intent_parameter WHERE notification_intent_id = {intent}",
            $"DELETE FROM notifications.notification_delivery WHERE id = {inApp}",
            "DELETE FROM notifications.notification_template WHERE event_type = 'ManagementConcern.ConcernEscalated'",
            $"UPDATE notifications.notification_intent SET source_reference = 'other' WHERE id = {intent}",
            $"UPDATE notifications.notification_intent SET deep_link = '/elsewhere' WHERE id = {intent}",
            $"UPDATE notifications.notification_intent SET status = 'RECEIVED' WHERE id = {intent}",
            $"UPDATE notifications.notification_intent_parameter SET parameter_value = 'other' WHERE notification_intent_id = {intent}",
            $"UPDATE notifications.notification_delivery SET rendered_body = 'rewritten' WHERE id = {inApp}",
            $"UPDATE notifications.notification_delivery SET recipient_user_id = '{NotificationTestHost.UserId(6)}' WHERE id = {inApp}",
            $"UPDATE notifications.notification_delivery SET status = 'PENDING' WHERE id = {inApp}",
            $"UPDATE notifications.notification_delivery SET status = 'PENDING', suppression_reason = NULL WHERE id = {sms}",
            $"""
            INSERT INTO notifications.notification_delivery (id, notification_intent_id, recipient_user_id, channel, notification_template_id, rendered_language,
                                                             rendered_body, status, sent_at, created_at, created_by, updated_at, updated_by)
            SELECT gen_random_uuid(), {intent}, '{NotificationTestHost.UserId(6)}', 'EMAIL', notification_template_id, 'en', 'x', 'SENT', now(), now(), created_by, now(), created_by
            FROM notifications.notification_delivery WHERE id = {inApp}
            """,
            $"""
            INSERT INTO notifications.notification_template (id, event_family_code, event_type, channel, version_no, subject_ar, subject_en, body_ar, body_en, lifecycle_state,
                                                             created_at, created_by, updated_at, updated_by)
            VALUES (gen_random_uuid(), 'X', 'Reports.GuardCase', 'IN_APP', 1, 'س', 'S', 'ن', 'B', 'PUBLISHED', now(), '{NotificationTestHost.UserId(1)}', now(), '{NotificationTestHost.UserId(1)}')
            """,
        ];

        foreach (string statement in refused)
        {
            await Assert.ThrowsAsync<PostgresException>(() => host.Database.ExecuteAsync(statement));
        }

        // Reading a notification is the one change its recipient makes, once; a read notification changes no more.
        await host.Database.ExecuteAsync($"UPDATE notifications.notification_delivery SET status = 'READ', read_at = now() WHERE id = {inApp}");
        await Assert.ThrowsAsync<PostgresException>(() => host.Database.ExecuteAsync($"UPDATE notifications.notification_delivery SET read_at = now() + interval '1 day' WHERE id = {inApp}"));
        Assert.Equal(["COMPLETED|ISS-GUARD-1|READ"], await host.Database.QueryAsync($"""
            SELECT i.status || '|' || p.parameter_value || '|' || d.status
            FROM notifications.notification_intent i
            JOIN notifications.notification_intent_parameter p ON p.notification_intent_id = i.id AND p.parameter_key = 'concernReference'
            JOIN notifications.notification_delivery d ON d.id = {inApp}
            WHERE i.id = {intent}
            """));
    }
}
