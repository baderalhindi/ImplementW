using System.Net;
using Microsoft.Extensions.DependencyInjection;
using PMPlatform.Application.Common.Events;
using PMPlatform.Application.Features.IntegrationMonitoring;
using PMPlatform.Tests.Integration.AuditActivity;
using PMPlatform.Tests.Integration.Identity;

namespace PMPlatform.Tests.Integration.IntegrationMonitoring;

/// <summary>
/// Telemetry from the modules that call integrations (event-conventions §4 rows 3–4): every provider call IdentityAccess makes, and every
/// e-mail or SMS attempt WF-15 makes, is an FG-05 invocation, recorded once, after its producer committed, with the producer's correlation
/// id. Only a technical failure says a provider is down; a provider message never reaches FG-05's tables; and losing telemetry never fails
/// the sign-in it observed.
/// </summary>
[Collection(IntegrationMonitoringSuite.Name)]
public sealed class TelemetryIntakeTests(IntegrationMonitoringTestHost host)
{
    [Fact]
    public async Task EachDirectoryCallIsAnInvocationAndAWrongPasswordIsNotAnOutage()
    {
        Guid correlationId = Guid.NewGuid();
        using HttpClient client = host.Api.CreateClient().WithCorrelationId(correlationId);

        await client.SignInOrFailAsync(2);
        await host.DispatchAsync();
        string availabilityBefore = (await host.HealthAsync(IntegrationCodes.Directory)).Split('|')[0];
        using (HttpResponseMessage refused = await client.SignInAsync("local.r02", "not-the-password"))
        {
            Assert.Equal(HttpStatusCode.Unauthorized, refused.StatusCode);
        }

        await host.DispatchAsync();

        Assert.Equal(
            ["AUTHENTICATE|SUCCEEDED||IdentityAccess", "AUTHENTICATE|FAILED|BUSINESS_VALIDATION|IdentityAccess"],
            await host.Database.QueryAsync($"""
                SELECT i.operation_code || '|' || i.status || '|' || coalesce(i.failure_category, '') || '|' || i.source_module
                FROM integration_monitoring.invocation i WHERE i.correlation_id = '{correlationId}' AND i.operation_code = 'AUTHENTICATE' ORDER BY i.started_at
                """));
        Assert.Equal(availabilityBefore, (await host.HealthAsync(IntegrationCodes.Directory)).Split('|')[0]);
    }

    [Fact]
    public async Task ARepeatedDeliveryIsRecordedOnce()
    {
        Guid delivery = Guid.NewGuid();
        Application.Features.Notifications.Contracts.Events.IntegrationInvocationCompleted envelope = Telemetry.Delivered(delivery, 1, DateTimeOffset.UtcNow);

        await RecordDirectlyAsync(envelope);
        await RecordDirectlyAsync(envelope);

        Assert.Equal(["1"], await host.Database.QueryAsync($"SELECT count(*)::text FROM integration_monitoring.invocation WHERE idempotency_key = '{delivery}'"));
    }

    /// <summary>WF-15's attempts of one delivery are one lineage: numbered, each later one ending the earlier one's pending retry.</summary>
    [Fact]
    public async Task ADeliverysAttemptsAreOneLineageAndItsDeadLetterIsAnAlert()
    {
        Guid delivery = Guid.NewGuid();
        Guid correlationId = Guid.NewGuid();
        DateTimeOffset start = DateTimeOffset.UtcNow;

        for (int attempt = 1; attempt <= 3; attempt++)
        {
            await RecordDirectlyAsync(Telemetry.EmailAttempt(delivery, attempt, start.AddMinutes(attempt), dead: attempt == 3, IntegrationCodes.Sms, correlationId));
        }

        Assert.Equal(
            ["1|FAILED", "2|FAILED", "3|DEAD_LETTER"],
            await host.Database.QueryAsync($"SELECT attempt_no || '|' || status FROM integration_monitoring.invocation WHERE idempotency_key = '{delivery}' ORDER BY attempt_no"));
        Assert.Equal(
            [correlationId.ToString()],
            await host.Database.QueryAsync($"SELECT DISTINCT correlation_id::text FROM integration_monitoring.invocation WHERE idempotency_key = '{delivery}'"));
        Assert.Contains(await host.LiveAlertsAsync(IntegrationCodes.Sms), a => a.StartsWith("DEAD_LETTER|", StringComparison.Ordinal));
    }

    /// <summary>INT-CC-08: what is not a plain code — text a provider wrote, which can echo an address — is redacted before it is stored.</summary>
    [Fact]
    public async Task AProviderMessageIsRedactedBeforeItIsStored()
    {
        Guid delivery = Guid.NewGuid();
        DateTimeOffset at = DateTimeOffset.UtcNow;
        var envelope = Telemetry.Envelope(delivery, 1, at, IntegrationCodes.ExchangeEmail, null, new IntegrationInvocationData(
            IntegrationCodes.ExchangeEmail, "SEND", delivery.ToString(), IntegrationInvocationOutcome.Failed, IntegrationFailureKind.Technical,
            IntegrationRetryDisposition.None, at, at, "550 5.1.1 <someone@ahda.gov.sa> unknown", "Mailbox someone@ahda.gov.sa rejected: Bearer abc", null,
            "mailto:someone@ahda.gov.sa"));

        await RecordDirectlyAsync(envelope);

        Assert.Equal(
            [$"{InvocationTelemetryIntake.Redacted}|{InvocationTelemetryIntake.Redacted}|"],
            await host.Database.QueryAsync($"SELECT response_code || '|' || error_summary || '|' || coalesce(source_reference, '') FROM integration_monitoring.invocation WHERE id = '{envelope.EventId}'"));
    }

    /// <summary>A producer that breaks the contract has its message kept in the outbox with the error, and nothing half-recorded.</summary>
    [Fact]
    public async Task TelemetryThatBreaksTheContractStaysInTheOutboxWithItsError()
    {
        Guid delivery = Guid.NewGuid();
        DateTimeOffset at = DateTimeOffset.UtcNow;
        var envelope = Telemetry.Envelope(delivery, 1, at, IntegrationCodes.ExchangeEmail, null, new IntegrationInvocationData(
            "NOT AN INTEGRATION", "SEND", delivery.ToString(), IntegrationInvocationOutcome.Succeeded, null, IntegrationRetryDisposition.None, at, at, null, null, null, null));
        await host.WithScopeAsync(async services =>
        {
            await services.GetRequiredService<IOutbox>().RecordAsync(envelope, CancellationToken.None);
            return true;
        });

        await host.DispatchAsync();

        Assert.Equal(["false|1|InvalidOperationException"], await host.Database.QueryAsync(
            $"SELECT (dispatched_at IS NOT NULL)::text || '|' || attempt_count || '|' || last_error FROM common.outbox_message WHERE id = '{envelope.EventId}'"));
        Assert.Empty(await host.Database.QueryAsync($"SELECT id FROM integration_monitoring.invocation WHERE idempotency_key = '{delivery}'"));
    }

    /// <summary>FG-05 §26: telemetry is decoupled from the business transaction. Its outbox refusing every telemetry message, a sign-in still succeeds.</summary>
    [Fact]
    public async Task LosingTelemetryNeverFailsTheSignInItObserved()
    {
        await host.Database.ExecuteAsync("""
            CREATE FUNCTION common.refuse_test_telemetry() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN
                RAISE EXCEPTION 'telemetry refused by the test';
            END;
            $$;
            CREATE TRIGGER refuse_test_telemetry BEFORE INSERT ON common.outbox_message
                FOR EACH ROW WHEN (NEW.message_key LIKE 'IdentityAccess.IntegrationInvocationCompleted:%') EXECUTE FUNCTION common.refuse_test_telemetry();
            """);
        try
        {
            using HttpClient client = host.Api.CreateClient();
            Session session = await client.SignInOrFailAsync(2);

            Assert.False(string.IsNullOrEmpty(session.AccessToken));
            Assert.Contains("Integration telemetry for DIRECTORY AUTHENTICATE was not recorded", host.Identity.Logs.Text, StringComparison.Ordinal);
        }
        finally
        {
            await host.Database.ExecuteAsync("DROP TRIGGER refuse_test_telemetry ON common.outbox_message; DROP FUNCTION common.refuse_test_telemetry();");
        }
    }

    private async Task RecordDirectlyAsync(EventEnvelope<IntegrationInvocationData> envelope) =>
        await host.WithScopeAsync(async services =>
        {
            await services.GetRequiredService<InvocationTelemetryIntake>().RecordAsync(envelope, CancellationToken.None);
            return true;
        });
}
