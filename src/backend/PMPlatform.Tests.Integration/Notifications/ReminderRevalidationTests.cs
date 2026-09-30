namespace PMPlatform.Tests.Integration.Notifications;

/// <summary>
/// TASK-039 acceptance criterion 3 and the workbook's second validation check: a scheduled reminder revalidates its
/// source condition when it is due, so a risk resolved in the meantime does not still trigger its overdue-review reminder.
/// </summary>
[Collection(NotificationSuite.Name)]
public sealed class ReminderRevalidationTests(NotificationTestHost host)
{
    /// <summary>The workbook's check: "resolve the underlying condition before a scheduled reminder fires and confirm the reminder is suppressed".</summary>
    [Fact]
    public async Task AReminderForARiskResolvedBeforeItIsDueIsSuppressed()
    {
        Guid risk = Guid.NewGuid();
        DateTimeOffset now = host.Clock.GetUtcNow();
        try
        {
            await TestNotificationSource.RiskWithReviewReminder(risk, "RSK-RES-1", now.AddDays(1), now).RunAsync(host.Api);
            await host.Api.DeliverAsync();
            Assert.Equal(["SCHEDULED|"], await host.Database.QueryAsync(NotificationDriver.IntentOf(NotificationTestHost.ReviewDueEvent, risk)));

            // The risk is resolved in its own module before the reminder is due.
            await host.Database.ExecuteAsync($"UPDATE test_source.risk SET status = 'RESOLVED' WHERE id = '{risk}'");
            host.Clock.Advance(TimeSpan.FromDays(1).Add(TimeSpan.FromMinutes(1)));
            await host.Api.ProcessAsync();

            Assert.Equal(["SUPPRESSED|CONDITION_RESOLVED"], await host.Database.QueryAsync(NotificationDriver.IntentOf(NotificationTestHost.ReviewDueEvent, risk)));
            Assert.Equal(["true"], await host.Database.QueryAsync(
                $"SELECT (condition_revalidated_at > scheduled_for)::text FROM notifications.notification_intent WHERE source_reference = '{risk}'"));
            Assert.Empty(await host.Database.QueryAsync(NotificationDriver.DeliveriesOf(NotificationTestHost.ReviewDueEvent, risk)));
            Assert.DoesNotContain(host.Smtp.Received, m => m.Content.Contains("RSK-RES-1", StringComparison.Ordinal));
        }
        finally
        {
            host.Clock.Reset();
        }
    }

    [Fact]
    public async Task AReminderForARiskStillOpenIsSentWhenDueAndNotBefore()
    {
        Guid risk = Guid.NewGuid();
        DateTimeOffset now = host.Clock.GetUtcNow();
        try
        {
            await TestNotificationSource.RiskWithReviewReminder(risk, "RSK-OPEN-1", now.AddHours(2), now).RunAsync(host.Api);
            await host.Api.DeliverAsync();
            host.Clock.Advance(TimeSpan.FromHours(1));
            await host.Api.ProcessAsync();
            Assert.Equal(["SCHEDULED|"], await host.Database.QueryAsync(NotificationDriver.IntentOf(NotificationTestHost.ReviewDueEvent, risk)));

            host.Clock.Advance(TimeSpan.FromHours(1).Add(TimeSpan.FromMinutes(1)));
            await host.Api.ProcessAsync();

            Assert.Equal(["COMPLETED|"], await host.Database.QueryAsync(NotificationDriver.IntentOf(NotificationTestHost.ReviewDueEvent, risk)));
            Assert.Equal(["2|EMAIL|SENT|", "2|IN_APP|SENT|"], await host.Database.QueryAsync(NotificationDriver.DeliveriesOf(NotificationTestHost.ReviewDueEvent, risk)));
            Assert.Contains(host.Smtp.To(NotificationTestHost.EmailOf(2)), m => m.Content.Contains("RSK-OPEN-1", StringComparison.Ordinal));
        }
        finally
        {
            host.Clock.Reset();
        }
    }

    [Fact]
    public async Task AReminderWhoseSourceCannotRevalidateItIsNotSentUnverified()
    {
        Guid reference = Guid.NewGuid();
        DateTimeOffset now = host.Clock.GetUtcNow();
        SourceTransaction reminder = TestNotificationSource.RiskWithReviewReminder(reference, "RSK-NOSRC-1", now, now);

        // Reports schedules a reminder, but no condition source answers for Reports.
        await (reminder with { Intent = reminder.Intent with { SourceModule = NotificationTestHost.ReportsModule } }).RunAsync(host.Api);
        await host.Api.DeliverAsync();

        Assert.Equal(["SUPPRESSED|CONDITION_UNVERIFIABLE"], await host.Database.QueryAsync(NotificationDriver.IntentOf(NotificationTestHost.ReviewDueEvent, reference)));
        Assert.Empty(await host.Database.QueryAsync(NotificationDriver.DeliveriesOf(NotificationTestHost.ReviewDueEvent, reference)));
    }
}
