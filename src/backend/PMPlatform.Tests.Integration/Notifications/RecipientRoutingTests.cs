using System.Net;
using PMPlatform.Application.Common.Events;
using PMPlatform.Tests.Integration.Identity;

namespace PMPlatform.Tests.Integration.Notifications;

/// <summary>
/// Recipient resolution, the eligibility and privacy recheck, template rendering and channel routing (TASK-039), and the
/// participation amendment: entity users receive in-app and e-mail notifications on their own projects (ADR-013).
/// </summary>
[Collection(NotificationSuite.Name)]
public sealed class RecipientRoutingTests(NotificationTestHost host)
{
    /// <summary>
    /// R02, R03, R04 and R08 over the entity project: local.r02 (no anchor), local.r03 (their department's project) and
    /// local.r08 (external, R04 bound to this project and R08 on its entity). Not local.r04 (disabled), not local.r05 (R03
    /// bound to another project), not local.r07 (R08 of another, suspended entity).
    /// </summary>
    [Fact]
    public async Task TheRoleMatrixOverTheIntentsScopeChoosesTheRecipients()
    {
        Guid escalation = Guid.NewGuid();
        await TestNotificationSource.Escalation(escalation, "ISS-RCP-1", NotificationTestHost.EntityProjectId, NotificationTestHost.DepartmentId, NotificationTestHost.EntityId, host.Clock.GetUtcNow())
            .RunAsync(host.Api);
        await host.Api.DeliverAsync();

        Assert.Equal(["2", "3", "8"], await RecipientsAsync(escalation));
    }

    /// <summary>ADR-013: an entity Project Manager is notified in-app and by e-mail on their own project, and never on another entity's.</summary>
    [Fact]
    public async Task AnEntityUserIsNotifiedInAppAndByEmailOnTheirOwnProjectOnly()
    {
        Guid own = Guid.NewGuid();
        Guid other = Guid.NewGuid();
        DateTimeOffset now = host.Clock.GetUtcNow();
        await TestNotificationSource.Escalation(own, "ISS-ENT-OWN", NotificationTestHost.EntityProjectId, NotificationTestHost.DepartmentId, NotificationTestHost.EntityId, now).RunAsync(host.Api);
        await TestNotificationSource.Escalation(other, "ISS-ENT-OTHER", NotificationTestHost.OtherEntityProjectId, NotificationTestHost.DepartmentId, NotificationTestHost.SuspendedEntityId, now).RunAsync(host.Api);
        await host.Api.DeliverAsync();

        IReadOnlyList<string> ownDeliveries = await host.Database.QueryAsync(NotificationDriver.DeliveriesOf(NotificationTestHost.EscalatedEvent, own));
        Assert.Contains("8|IN_APP|SENT|", ownDeliveries);
        Assert.Contains("8|EMAIL|SENT|", ownDeliveries);
        Assert.Contains(host.Smtp.To(NotificationTestHost.EmailOf(8)), m => m.Content.Contains("ISS-ENT-OWN", StringComparison.Ordinal));

        // The same department's project of another entity: R02, the department's R03 and that project's own R03 are told; the
        // entity user is not, nor that entity's user, whose entity is suspended.
        Assert.Equal(["2", "3", "5"], await RecipientsAsync(other));
        Assert.DoesNotContain(host.Smtp.To(NotificationTestHost.EmailOf(8)), m => m.Content.Contains("ISS-ENT-OTHER", StringComparison.Ordinal));

        // Nor can they open it: another person's notification is not found.
        using HttpClient client = host.Api.CreateClient();
        string entityUser = (await client.SignInOrFailAsync(8)).AccessToken;
        string otherInApp = (await host.Database.QueryAsync($"""
            SELECT d.id::text FROM notifications.notification_delivery d JOIN notifications.notification_intent i ON i.id = d.notification_intent_id
            WHERE i.source_reference = '{other}' AND d.channel = 'IN_APP' AND d.recipient_user_id = '{NotificationTestHost.UserId(2)}'
            """)).Single();
        using HttpResponseMessage response = await client.GetAsync($"/api/v1/notifications/{otherInApp}", entityUser);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>ADR-012: each recipient reads it in their own language, rendered from the bilingual template with the source's parameters.</summary>
    [Fact]
    public async Task EachRecipientReadsItInTheirOwnLanguage()
    {
        Guid escalation = Guid.NewGuid();
        await TestNotificationSource.Escalation(escalation, "ISS-LANG-1", NotificationTestHost.EntityProjectId, NotificationTestHost.DepartmentId, NotificationTestHost.EntityId, host.Clock.GetUtcNow())
            .RunAsync(host.Api);
        await host.Api.DeliverAsync();

        Assert.Equal(
            [
                "2|EMAIL|en|Escalated: ISS-LANG-1|Concern ISS-LANG-1 was escalated. Open it: " +
                $"{NotificationTestHost.AppBaseUrl}/projects/{NotificationTestHost.EntityProjectId}/issues-challenges/{escalation}",
                "2|IN_APP|en|Escalated: ISS-LANG-1|Concern ISS-LANG-1 was escalated with severity HIGH.",
                "3|IN_APP|ar|تصعيد ISS-LANG-1|تم تصعيد ISS-LANG-1 بخطورة HIGH.",
            ],
            await host.Database.QueryAsync($"""
                SELECT right(d.recipient_user_id::text, 1) || '|' || d.channel || '|' || d.rendered_language || '|' || d.rendered_subject || '|' || d.rendered_body
                FROM notifications.notification_delivery d JOIN notifications.notification_intent i ON i.id = d.notification_intent_id
                WHERE i.source_reference = '{escalation}' AND ((d.recipient_user_id = '{NotificationTestHost.UserId(2)}' AND d.channel IN ('EMAIL', 'IN_APP'))
                                                            OR (d.recipient_user_id = '{NotificationTestHost.UserId(3)}' AND d.channel = 'IN_APP'))
                ORDER BY 1
                """));
        ReceivedEmail email = host.Smtp.To(NotificationTestHost.EmailOf(2)).Single(m => m.Content.Contains("ISS-LANG-1", StringComparison.Ordinal));
        Assert.Contains("Subject: Escalated: ISS-LANG-1", email.Content, StringComparison.Ordinal);
        Assert.Contains("Content-Language: en", email.Content, StringComparison.Ordinal);
        Assert.Contains("From: PMPlatform <pmplatform-noreply@notifications.test>", email.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnEventWithNoPublishedTemplateFailsClosedUntilItIsRedriven()
    {
        Guid job = Guid.NewGuid();
        await Report(job, NotificationTestHost.UntemplatedEvent, NotificationTestHost.UntemplatedFamily).RunAsync(host.Api);
        await host.Api.DeliverAsync();

        Assert.Equal(["FAILED|TEMPLATE_MISSING"], await host.Database.QueryAsync(NotificationDriver.IntentOf(NotificationTestHost.UntemplatedEvent, job)));
        Assert.Empty(await host.Database.QueryAsync(NotificationDriver.DeliveriesOf(NotificationTestHost.UntemplatedEvent, job)));

        using HttpClient client = host.Api.CreateClient();
        string admin = (await client.SignInOrFailAsync(1)).AccessToken;
        string intentId = (await host.Database.QueryAsync($"SELECT id::text FROM notifications.notification_intent WHERE source_reference = '{job}'")).Single();
        using HttpResponseMessage failed = await client.GetAsync("/api/v1/notification-intents?status=FAILED", admin);
        Assert.Contains(intentId, await failed.Content.ReadAsStringAsync(), StringComparison.Ordinal);

        // The template is published; the operator redrives, and it is routed on the next pass.
        await host.Database.ExecuteAsync($$$"""
            INSERT INTO notifications.notification_template (id, event_family_code, event_type, channel, version_no, subject_ar, subject_en, body_ar, body_en,
                                                             lifecycle_state, created_at, created_by, updated_at, updated_by)
            VALUES (gen_random_uuid(), '{{{NotificationTestHost.UntemplatedFamily}}}', '{{{NotificationTestHost.UntemplatedEvent}}}', 'IN_APP', 1, 'فشل التقرير', 'Report failed',
                    'فشل التقرير {{reportName}}.', 'Report {{reportName}} failed.', 'DRAFT', now(), '{{{NotificationTestHost.UserId(1)}}}', now(), '{{{NotificationTestHost.UserId(1)}}}')
            ON CONFLICT DO NOTHING;
            UPDATE notifications.notification_template SET lifecycle_state = 'VALIDATED', validated_by_user_id = '{{{NotificationTestHost.UserId(2)}}}', validated_at = now()
            WHERE event_type = '{{{NotificationTestHost.UntemplatedEvent}}}' AND lifecycle_state = 'DRAFT';
            UPDATE notifications.notification_template SET lifecycle_state = 'PUBLISHED', published_by_user_id = '{{{NotificationTestHost.UserId(3)}}}', published_at = now()
            WHERE event_type = '{{{NotificationTestHost.UntemplatedEvent}}}' AND lifecycle_state = 'VALIDATED';
            """);
        using HttpResponseMessage redriven = await client.PostAsync($"/api/v1/notification-intents/{intentId}/redrive", admin);
        Assert.Equal(HttpStatusCode.OK, redriven.StatusCode);
        await host.Api.ProcessAsync();

        Assert.Equal(["COMPLETED|"], await host.Database.QueryAsync(NotificationDriver.IntentOf(NotificationTestHost.UntemplatedEvent, job)));
        Assert.Equal(["2|IN_APP|SENT|"], await host.Database.QueryAsync(NotificationDriver.DeliveriesOf(NotificationTestHost.UntemplatedEvent, job)));
    }

    /// <summary>ADR-012: a template is mandatory on every channel the family routes to; one missing sends nothing on any.</summary>
    [Fact]
    public async Task OneRoutedChannelWithoutATemplateFailsTheIntentOnEveryChannel()
    {
        Guid risk = Guid.NewGuid();
        SourceTransaction reminder = TestNotificationSource.RiskWithReviewReminder(risk, "RSK-HALF-1", host.Clock.GetUtcNow(), host.Clock.GetUtcNow());
        NotificationIntentEnvelope overdue = reminder.Intent with
        {
            EventType = NotificationTestHost.HalfTemplatedEvent,
            MessageKey = EventMessageKey.Of(NotificationTestHost.HalfTemplatedEvent, risk.ToString()),
        };
        await (reminder with { Intent = overdue }).RunAsync(host.Api);
        await host.Api.DeliverAsync();

        Assert.Equal(["FAILED|TEMPLATE_MISSING"], await host.Database.QueryAsync(NotificationDriver.IntentOf(NotificationTestHost.HalfTemplatedEvent, risk)));
        Assert.Empty(await host.Database.QueryAsync(NotificationDriver.DeliveriesOf(NotificationTestHost.HalfTemplatedEvent, risk)));
    }

    [Fact]
    public async Task AFamilyTheRoutingConfigurationDoesNotHaveFailsClosed()
    {
        Guid job = Guid.NewGuid();
        await Report(job, NotificationTestHost.ReportEvent, "TEST_NOT_CONFIGURED").RunAsync(host.Api);
        await host.Api.DeliverAsync();

        Assert.Equal(["FAILED|CONFIGURATION_MISSING"], await host.Database.QueryAsync(NotificationDriver.IntentOf(NotificationTestHost.ReportEvent, job)));
    }

    [Fact]
    public async Task NoOneHoldingARecipientRoleOverTheScopeSuppressesTheIntent()
    {
        Guid escalation = Guid.NewGuid();

        // A scope that names only an entity no recipient role holder belongs to and no department.
        await TestNotificationSource.Escalation(escalation, "ISS-NOONE-1", NotificationTestHost.OtherEntityProjectId, Guid.NewGuid(), NotificationTestHost.SuspendedEntityId, host.Clock.GetUtcNow())
            .RunAsync(host.Api);
        await host.Api.DeliverAsync();

        // local.r02 holds R02 with no anchor and local.r05 R03 on that project alone, so both are recipients; the DEPT-anchored
        // R03 and the entity users are not.
        Assert.Equal(["2", "5"], await RecipientsAsync(escalation));

        Guid job = Guid.NewGuid();
        SourceTransaction report = Report(job, NotificationTestHost.ReportEvent, NotificationTestHost.ReportFamily);
        await host.Database.ExecuteAsync($"UPDATE identity_access.access_relationship SET status = 'ENDED', ends_at = now(), end_reason = 'MANUAL' WHERE id IN ('00000000-0111-4000-8000-000000000002', '00000000-0111-4000-8000-000000000006')");
        try
        {
            await report.RunAsync(host.Api);
            await host.Api.DeliverAsync();
            Assert.Equal(["SUPPRESSED|NO_ELIGIBLE_RECIPIENT"], await host.Database.QueryAsync(NotificationDriver.IntentOf(NotificationTestHost.ReportEvent, job)));
        }
        finally
        {
            await host.Database.ExecuteAsync("UPDATE identity_access.access_relationship SET status = 'ACTIVE', ends_at = NULL, end_reason = NULL WHERE id IN ('00000000-0111-4000-8000-000000000002', '00000000-0111-4000-8000-000000000006')");
        }
    }

    /// <summary>The recheck at the send attempt: a recipient who lost their role after routing is not sent to.</summary>
    [Fact]
    public async Task ARecipientWhoLostTheirRoleBeforeTheAttemptIsNotSentTo()
    {
        Guid escalation = Guid.NewGuid();
        await host.Smtp.StopAsync();
        try
        {
            await TestNotificationSource.Escalation(escalation, "ISS-LOST-1", NotificationTestHost.EntityProjectId, NotificationTestHost.DepartmentId, NotificationTestHost.EntityId, host.Clock.GetUtcNow())
                .RunAsync(host.Api);
            await host.Api.DeliverAsync();
            Assert.Contains("3|EMAIL|FAILED|", await host.Database.QueryAsync(NotificationDriver.DeliveriesOf(NotificationTestHost.EscalatedEvent, escalation)));

            await host.Database.ExecuteAsync("UPDATE identity_access.access_relationship SET status = 'ENDED', ends_at = now(), end_reason = 'MANUAL' WHERE id = '00000000-0111-4000-8000-000000000003'");
            await host.Smtp.RestartAsync();
            host.Clock.Advance(TimeSpan.FromMinutes(2));
            await host.Api.ProcessAsync();

            IReadOnlyList<string> deliveries = await host.Database.QueryAsync(NotificationDriver.DeliveriesOf(NotificationTestHost.EscalatedEvent, escalation));
            Assert.Contains("3|EMAIL|SUPPRESSED|RECIPIENT_INELIGIBLE", deliveries);
            Assert.Contains("2|EMAIL|SENT|", deliveries);
            Assert.DoesNotContain(host.Smtp.To(NotificationTestHost.EmailOf(3)), m => m.Content.Contains("ISS-LOST-1", StringComparison.Ordinal));
        }
        finally
        {
            await host.Database.ExecuteAsync("UPDATE identity_access.access_relationship SET status = 'ACTIVE', ends_at = NULL, end_reason = NULL WHERE id = '00000000-0111-4000-8000-000000000003'");
            await host.Smtp.RestartAsync();
            host.Clock.Reset();
        }
    }

    private SourceTransaction Report(Guid job, string eventType, string family) =>
        new(
            $"SELECT 1",
            TestNotificationSource.Intent(NotificationTestHost.ReportsModule, eventType, family, job, new EventSubject(NotificationTestHost.ReportsModule, "ReportJob", job, null),
                new EventScope(null, null, null), $"/reports/jobs/{job}", [new NotificationParameter("reportName", "Portfolio")], host.Clock.GetUtcNow()));

    private Task<IReadOnlyList<string>> RecipientsAsync(Guid reference) => host.Database.QueryAsync($"""
        SELECT DISTINCT right(d.recipient_user_id::text, 1)
        FROM notifications.notification_delivery d JOIN notifications.notification_intent i ON i.id = d.notification_intent_id
        WHERE i.source_reference = '{reference}' ORDER BY 1
        """);
}
