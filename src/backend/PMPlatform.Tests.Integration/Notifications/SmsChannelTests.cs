using PMPlatform.Application.Common.Events;
using PMPlatform.Tests.Integration.Identity;

namespace PMPlatform.Tests.Integration.Notifications;

/// <summary>
/// ADR-004 as changed: SMS alongside in-app and e-mail, carrying the event and its deep link only, to a verified number
/// only, within the Arabic segment budget; on by default with recipient opt-out, mandatory families not.
/// </summary>
[Collection(NotificationSuite.Name)]
public sealed class SmsChannelTests(NotificationTestHost host)
{
    [Fact]
    public async Task AnSmsCarriesTheEventAndItsLinkOnlyAndGoesOnlyToAVerifiedNumber()
    {
        Guid escalation = Guid.NewGuid();
        await TestNotificationSource.Escalation(escalation, "ISS-SMS-1", NotificationTestHost.EntityProjectId, NotificationTestHost.DepartmentId, NotificationTestHost.EntityId, host.Clock.GetUtcNow())
            .RunAsync(host.Api);
        await host.Api.DeliverAsync();

        string link = $"{NotificationTestHost.AppBaseUrl}/projects/{NotificationTestHost.EntityProjectId}/issues-challenges/{escalation}";
        Assert.Contains((NotificationTestHost.VerifiedMobileNumber, $"A concern on your project was escalated: {link}"), host.Sms.Sent);
        Assert.DoesNotContain(host.Sms.Sent, s => s.Text.Contains("ISS-SMS-1", StringComparison.Ordinal) || s.Text.Contains("HIGH", StringComparison.Ordinal));

        // English with its link costs two GSM-7 segments (166 characters); the recipients with no verified number get none.
        Assert.Equal(["2|SENT|2|true", "3|SUPPRESSED|MOBILE_UNVERIFIED|false", "8|SUPPRESSED|MOBILE_UNVERIFIED|false"], await host.Database.QueryAsync($"""
            SELECT right(d.recipient_user_id::text, 1) || '|' || d.status || '|' || coalesce(d.suppression_reason, d.segment_count::text) || '|' || (d.provider_message_id IS NOT NULL)::text
            FROM notifications.notification_delivery d JOIN notifications.notification_intent i ON i.id = d.notification_intent_id
            WHERE i.source_reference = '{escalation}' AND d.channel = 'SMS'
            ORDER BY 1
            """));
    }

    [Fact]
    public async Task WithoutAProviderSmsIsSuppressedWhileInAppAndEmailStillDeliver()
    {
        Guid escalation = Guid.NewGuid();
        await using IdentityApiFactory api = host.CreateApi(withSms: false);
        await TestNotificationSource.Escalation(escalation, "ISS-NOSMS-1", NotificationTestHost.EntityProjectId, NotificationTestHost.DepartmentId, NotificationTestHost.EntityId, host.Clock.GetUtcNow())
            .RunAsync(api);
        await api.DeliverAsync();

        IReadOnlyList<string> deliveries = await host.Database.QueryAsync(NotificationDriver.DeliveriesOf(NotificationTestHost.EscalatedEvent, escalation));
        Assert.Contains("2|SMS|SUPPRESSED|CHANNEL_NOT_CONFIGURED", deliveries);
        Assert.Contains("2|IN_APP|SENT|", deliveries);
        Assert.Contains("2|EMAIL|SENT|", deliveries);
    }

    [Fact]
    public async Task AnSmsOverItsSegmentBudgetIsSuppressedNeverCut()
    {
        Guid escalation = Guid.NewGuid();
        SourceTransaction source = TestNotificationSource.Escalation(
            escalation, "ISS-LONG-1", NotificationTestHost.EntityProjectId, NotificationTestHost.DepartmentId, NotificationTestHost.EntityId, host.Clock.GetUtcNow());
        string longLink = $"/projects/{NotificationTestHost.EntityProjectId}/issues-challenges/{escalation}/{new string('a', 300)}";
        await (source with { Intent = source.Intent with { Data = source.Intent.Data with { DeepLink = longLink } } }).RunAsync(host.Api);
        await host.Api.DeliverAsync();

        Assert.Contains("2|SMS|SUPPRESSED|SMS_SEGMENT_BUDGET_EXCEEDED", await host.Database.QueryAsync(NotificationDriver.DeliveriesOf(NotificationTestHost.EscalatedEvent, escalation)));
        Assert.DoesNotContain(host.Sms.Sent, s => s.Text.Contains(escalation.ToString(), StringComparison.Ordinal));
    }

    /// <summary>
    /// On by default with recipient opt-out; the security and critical escalation families are mandatory. The opt-out on the
    /// escalation family stands for one chosen while that family was still configurable: a later configuration made it
    /// mandatory, and the old choice no longer counts.
    /// </summary>
    [Fact]
    public async Task ARecipientWhoTurnedSmsOffGetsNoneForThatFamilyButStillGetsMandatoryOnes()
    {
        Guid job = Guid.NewGuid();
        Guid escalation = Guid.NewGuid();
        await host.Database.ExecuteAsync($"""
            INSERT INTO notifications.notification_preference (id, user_id, event_family_code, channel, is_enabled, created_at, created_by, updated_at, updated_by)
            VALUES (gen_random_uuid(), '{NotificationTestHost.UserId(2)}', '{NotificationTestHost.ReportFamily}', 'SMS', false, now(), '{NotificationTestHost.UserId(2)}', now(), '{NotificationTestHost.UserId(2)}'),
                   (gen_random_uuid(), '{NotificationTestHost.UserId(2)}', '{NotificationTestHost.EscalationFamily}', 'SMS', false, now(), '{NotificationTestHost.UserId(2)}', now(), '{NotificationTestHost.UserId(2)}')
            ON CONFLICT (user_id, event_family_code, channel) DO UPDATE SET is_enabled = false
            """);
        try
        {
            await new SourceTransaction(
                    $"SELECT 1",
                    TestNotificationSource.Intent(NotificationTestHost.ReportsModule, NotificationTestHost.ReportEvent, NotificationTestHost.ReportFamily, job,
                        new EventSubject(NotificationTestHost.ReportsModule, "ReportJob", job, null), new EventScope(null, null, null), $"/reports/jobs/{job}",
                        [new NotificationParameter("reportName", "Portfolio")], host.Clock.GetUtcNow()))
                .RunAsync(host.Api);
            await TestNotificationSource.Escalation(escalation, "ISS-MAND-1", NotificationTestHost.EntityProjectId, NotificationTestHost.DepartmentId, NotificationTestHost.EntityId, host.Clock.GetUtcNow())
                .RunAsync(host.Api);
            await host.Api.DeliverAsync();

            IReadOnlyList<string> report = await host.Database.QueryAsync(NotificationDriver.DeliveriesOf(NotificationTestHost.ReportEvent, job));
            Assert.Contains("2|SMS|SUPPRESSED|RECIPIENT_OPTED_OUT", report);
            Assert.Contains("2|IN_APP|SENT|", report);
            Assert.Contains("2|EMAIL|SENT|", report);
            Assert.Contains("2|SMS|SENT|", await host.Database.QueryAsync(NotificationDriver.DeliveriesOf(NotificationTestHost.EscalatedEvent, escalation)));
        }
        finally
        {
            await host.Database.ExecuteAsync(
                $"DELETE FROM notifications.notification_preference WHERE user_id = '{NotificationTestHost.UserId(2)}' AND event_family_code IN ('{NotificationTestHost.ReportFamily}', '{NotificationTestHost.EscalationFamily}')");
        }
    }
}
