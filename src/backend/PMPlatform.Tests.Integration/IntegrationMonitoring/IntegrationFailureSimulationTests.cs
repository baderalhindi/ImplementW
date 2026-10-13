using System.Net;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using PMPlatform.Application.Common.Events;
using PMPlatform.Domain.Common;
using PMPlatform.Tests.Integration.Identity;
using static PMPlatform.Tests.Integration.IntegrationMonitoring.IntegrationMonitoringApi;
using NotificationTelemetry = PMPlatform.Application.Features.Notifications.Contracts.Events.IntegrationInvocationCompleted;

namespace PMPlatform.Tests.Integration.IntegrationMonitoring;

/// <summary>
/// TASK-075 acceptance criterion 1 and the workbook's first validation check: a downstream integration outage, with everything FG-05 does
/// about it — failed sign-ins and connection tests, telemetry recorded, availability gone DOWN, alerts raised, a reconciliation retried to its
/// dead letter, an e-mail relay's deliveries dead-lettered, an operator acknowledging and retrying — leaves every business table exactly as
/// it was. Only <c>integration_monitoring</c>, the audit store and the outbox change.
/// </summary>
[Collection(IntegrationMonitoringSuite.Name)]
public sealed class IntegrationFailureSimulationTests(IntegrationMonitoringTestHost host)
{
    /// <summary>Nothing listens on port 1 of the loopback interface.</summary>
    private static readonly Dictionary<string, string?> Outage = new()
    {
        ["AD_LDAP_URL"] = "ldap://127.0.0.1:1/dc=pmplatform,dc=local",
        ["SSO_OIDC_AUTHORITY"] = "http://127.0.0.1:1/",
    };

    [Fact]
    public async Task ADownstreamOutageChangesNoBusinessLifecycleFieldAndIsRecordedAsIntegrationHealthOnly()
    {
        using HttpClient administrator = await host.ClientAsync(1);
        await using IdentityApiFactory outage = host.CreateApi(Outage);
        using HttpClient anonymous = outage.CreateClient();
        using HttpClient operatorClient = outage.CreateClient();
        operatorClient.DefaultRequestHeaders.Authorization = administrator.DefaultRequestHeaders.Authorization;
        await host.DispatchAsync();

        IReadOnlyList<string> before = await host.BusinessStateAsync();
        string projectBefore = await ProjectLifecycleAsync();
        try
        {
            // The directory and the identity provider are down: sign-in fails as the platform's failure, never the person's.
            using (HttpResponseMessage signIn = await anonymous.SignInAsync("local.r02", TestDirectory.PersonPassword))
            {
                Assert.Equal(HttpStatusCode.ServiceUnavailable, signIn.StatusCode);
            }

            using (HttpResponseMessage sso = await anonymous.GetAsync(SessionApi.SsoAuthorization))
            {
                Assert.Equal(HttpStatusCode.ServiceUnavailable, sso.StatusCode);
            }

            (await operatorClient.PostAsync(SessionApi.IdentityIntegrationTest, null)).Dispose();

            // The e-mail relay is down too: WF-15 retries a delivery on a project's notification and dead-letters it.
            Guid delivery = Guid.NewGuid();
            await StageEmailAttemptsAsync(delivery, attempts: 5);

            await host.DispatchAsync(outage);

            // A reconciliation against the unreachable directory: retried under the policy until it is dead-lettered.
            Guid directory = await host.DefinitionIdAsync(IntegrationCodes.Directory);
            JsonObject started = await (await operatorClient.SendSensitiveAsync(Reconciliations, new { integrationId = directory })).SuccessAsync();
            Assert.Equal("RETRY_SCHEDULED", started["invocation"]!["status"].Text());
            host.Clock.Advance(TimeSpan.FromMinutes(1));
            await host.RunPassAsync(outage);
            host.Clock.Advance(TimeSpan.FromMinutes(2));
            await host.RunPassAsync(outage);
            await host.DispatchAsync(outage);

            // The operator acknowledges what is open and retries the dead letter: the directory is still down.
            JsonArray alerts = (await (await administrator.GetAsync($"{Alerts}?status=OPEN")).SuccessAsync())["items"]!.AsArray();
            foreach (JsonNode? alert in alerts)
            {
                (await operatorClient.SendSensitiveAsync($"{Alerts}/{alert!["id"].Text()}/acknowledge")).Dispose();
            }

            string deadLetter = Assert.Single(await host.Database.QueryAsync($"""
                SELECT id::text FROM integration_monitoring.invocation
                WHERE operation_code = 'RECONCILE' AND correlation_id = '{started["invocation"]!["correlationId"].Text()}' AND status = 'DEAD_LETTER'
                """));
            JsonObject retried = await (await operatorClient.SendSensitiveAsync($"{Invocations}/{deadLetter}/retry")).SuccessAsync();
            Assert.Equal("DEAD_LETTER", retried["invocation"]!["status"].Text());
            await host.DispatchAsync(outage);
            await host.RunPassAsync(outage);

            // FG-05 recorded the outage as integration health and alerts...
            Assert.Equal("DOWN", (await host.HealthAsync(IntegrationCodes.Directory)).Split('|')[0]);
            Assert.Equal("DOWN", (await host.HealthAsync(IntegrationCodes.SingleSignOn)).Split('|')[0]);
            Assert.Equal("DOWN", (await host.HealthAsync(IntegrationCodes.ExchangeEmail)).Split('|')[0]);
            Assert.Contains("AVAILABILITY|ACKNOWLEDGED", await host.LiveAlertsAsync(IntegrationCodes.Directory));
            Assert.Contains(await host.LiveAlertsAsync(IntegrationCodes.Directory), a => a.StartsWith("DEAD_LETTER|", StringComparison.Ordinal));
            Assert.Contains(await host.LiveAlertsAsync(IntegrationCodes.ExchangeEmail), a => a.StartsWith("DEAD_LETTER|", StringComparison.Ordinal));
            Assert.Equal(
                ["FAILED", "FAILED", "FAILED", "DEAD_LETTER"],
                await host.Database.QueryAsync($"""
                    SELECT status FROM integration_monitoring.invocation
                    WHERE operation_code = 'RECONCILE' AND correlation_id = '{started["invocation"]!["correlationId"].Text()}' ORDER BY attempt_no
                    """));

            // ...and changed no business table: the project's lifecycle, the users, the deliveries, everything.
            Assert.Equal(projectBefore, await ProjectLifecycleAsync());
            Assert.Equal(before, await host.BusinessStateAsync());
        }
        finally
        {
            host.Clock.Reset();
        }
    }

    private async Task<string> ProjectLifecycleAsync() =>
        Assert.Single(await host.Database.QueryAsync($"SELECT lifecycle_state || '|' || updated_at::text FROM project.project WHERE id = '{IdentityDatabase.EntityProjectId}'"));

    /// <summary>The telemetry WF-15 publishes for a delivery whose relay refuses every attempt, retried and finally dead-lettered.</summary>
    private async Task StageEmailAttemptsAsync(Guid delivery, int attempts) =>
        await host.WithScopeAsync(async services =>
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;
            IOutbox outbox = services.GetRequiredService<IOutbox>();
            for (int attempt = 1; attempt <= attempts; attempt++)
            {
                await outbox.RecordAsync(Telemetry.EmailAttempt(delivery, attempt, now.AddMinutes(attempt), dead: attempt == attempts), CancellationToken.None);
            }

            return true;
        });
}

/// <summary>Telemetry as WF-15 publishes it (<see cref="NotificationTelemetry"/>), for tests that play the producer.</summary>
internal static class Telemetry
{
    private static readonly Guid NotificationDispatch = new("00000000-0000-4000-8000-0000000000f9");

    /// <summary>A failed send attempt: retried after a minute, or the last (dead letter).</summary>
    public static NotificationTelemetry EmailAttempt(Guid delivery, int attempt, DateTimeOffset at, bool dead, string integrationCode = IntegrationCodes.ExchangeEmail, Guid? correlationId = null) =>
        Envelope(delivery, attempt, at, integrationCode, correlationId, new IntegrationInvocationData(
            integrationCode, "SEND", delivery.ToString(), IntegrationInvocationOutcome.Failed, IntegrationFailureKind.Technical,
            dead ? IntegrationRetryDisposition.DeadLetter : IntegrationRetryDisposition.RetryScheduled,
            at, at.AddMilliseconds(250), null, "SocketException", dead ? null : at.AddMinutes(1), $"NotificationDelivery/{delivery}"));

    /// <summary>A send the provider accepted.</summary>
    public static NotificationTelemetry Delivered(Guid delivery, int attempt, DateTimeOffset at, string integrationCode = IntegrationCodes.Sms) =>
        Envelope(delivery, attempt, at, integrationCode, null, new IntegrationInvocationData(
            integrationCode, "SEND", delivery.ToString(), IntegrationInvocationOutcome.Succeeded, null, IntegrationRetryDisposition.None,
            at, at.AddMilliseconds(400), null, null, null, $"NotificationDelivery/{delivery}"));

    /// <summary>The provider's delivery receipt for a send (SMS, TASK-103), <paramref name="after"/> its first attempt.</summary>
    public static NotificationTelemetry Receipt(Guid delivery, DateTimeOffset firstAttemptAt, TimeSpan after) =>
        Envelope(delivery, 99, firstAttemptAt + after, IntegrationCodes.Sms, null, new IntegrationInvocationData(
            IntegrationCodes.Sms, "DELIVERY_RECEIPT", delivery.ToString(), IntegrationInvocationOutcome.Succeeded, null, IntegrationRetryDisposition.None,
            firstAttemptAt + after, firstAttemptAt + after, null, null, null, $"NotificationDelivery/{delivery}"));

    public static NotificationTelemetry Envelope(Guid delivery, int attempt, DateTimeOffset at, string integrationCode, Guid? correlationId, IntegrationInvocationData data)
    {
        string key = $"{delivery:N}-{attempt}-{at.UtcTicks}-{integrationCode}-{data.OperationCode}";
        return new NotificationTelemetry
        {
            EventId = Guid.CreateVersion7(at),
            EventType = NotificationTelemetry.Type,
            SchemaVersion = NotificationTelemetry.CurrentSchemaVersion,
            Kind = EventKind.DomainEvent,
            MessageKey = EventMessageKey.Of(NotificationTelemetry.Type, key),
            IdempotencyKey = key,
            OccurredAt = data.CompletedAt,
            SourceModule = "Notifications",
            CorrelationId = correlationId ?? Guid.NewGuid(),
            Actor = new EventActor(AuditActorType.Service, NotificationDispatch),
            Subject = new EventSubject("Notifications", "NotificationDelivery", delivery, null),
            Scope = new EventScope(null, null, null),
            Data = data,
        };
    }
}
