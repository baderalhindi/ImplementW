using System.Net;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using PMPlatform.Application.Common.Events;
using PMPlatform.Tests.Integration.Identity;
using static PMPlatform.Tests.Integration.IntegrationMonitoring.IntegrationMonitoringApi;

namespace PMPlatform.Tests.Integration.IntegrationMonitoring;

/// <summary>
/// An operational alert's lifecycle (FG-05 §10.2): one per condition however many calls fail; acknowledged, then resolved or dismissed with a
/// note; final once handled; and handling it changes nothing about the integration's health (BR-INT-037, BR-INT-038). Each action is
/// integration audit (ADM-053).
/// </summary>
[Collection(IntegrationMonitoringSuite.Name)]
public sealed class OperationalAlertTests(IntegrationMonitoringTestHost host)
{
    [Fact]
    public async Task AnOutageIsOneAlertWhichAnOperatorAcknowledgesAndResolvesWithoutChangingHealth()
    {
        using HttpClient administrator = await host.ClientAsync(1);
        Guid mfa = await host.DefinitionIdAsync(IntegrationCodes.MultiFactor);
        await ResolveLiveAlertsAsync(administrator, mfa);

        // A success, then three failing calls: one transition to DOWN, so one alert.
        DateTimeOffset at = await host.NextObservationTimeAsync(IntegrationCodes.MultiFactor);
        await RecordAsync(Observed(IntegrationCodes.MultiFactor, at, IntegrationInvocationOutcome.Succeeded));
        for (int n = 1; n <= 3; n++)
        {
            await RecordAsync(Observed(IntegrationCodes.MultiFactor, at.AddSeconds(n), IntegrationInvocationOutcome.Failed));
        }

        JsonObject alert = Assert.Single((await (await administrator.GetAsync($"{Alerts}?integrationId={mfa}&status=OPEN")).SuccessAsync())["items"]!.AsArray())!.AsObject();
        Assert.Equal(("AVAILABILITY", "CRITICAL"), (alert["alertType"].Text(), alert["severity"].Text()));
        string id = alert["id"].Text();
        string healthBefore = await host.HealthAsync(IntegrationCodes.MultiFactor);

        using HttpResponseMessage get = await administrator.GetAsync($"{Alerts}/{id}");
        using HttpResponseMessage stale = await administrator.SendSensitiveAsync($"{Alerts}/{id}/acknowledge", ifMatch: "\"1\"");
        JsonObject acknowledged = await (await administrator.SendSensitiveAsync($"{Alerts}/{id}/acknowledge", ifMatch: get.Headers.ETag!.Tag)).SuccessAsync();
        using HttpResponseMessage twice = await administrator.SendSensitiveAsync($"{Alerts}/{id}/acknowledge");
        using HttpResponseMessage noNote = await administrator.SendSensitiveAsync($"{Alerts}/{id}/resolve", new { });
        JsonObject resolved = await (await administrator.SendSensitiveAsync($"{Alerts}/{id}/resolve", new { note = new { text = "تمت استعادة المزود", language = "ar" } })).SuccessAsync();
        using HttpResponseMessage afterResolve = await administrator.SendSensitiveAsync($"{Alerts}/{id}/dismiss", new { note = new { text = "Late", language = "en" } });

        Assert.Equal(HttpStatusCode.PreconditionFailed, stale.StatusCode);
        Assert.Equal(("ACKNOWLEDGED", IntegrationMonitoringTestHost.Person(1).ToString()), (acknowledged["status"].Text(), acknowledged["acknowledgedByUserId"].Text()));
        Assert.Equal((HttpStatusCode.Conflict, "INVALID_TRANSITION"), (twice.StatusCode, (await twice.ReadAsync<Problem>()).Code));
        Assert.Equal(HttpStatusCode.BadRequest, noNote.StatusCode);
        Assert.Equal(("RESOLVED", "AR"), (resolved["status"].Text(), resolved["resolutionNote"]!["language"].Text()));
        Assert.Equal((HttpStatusCode.Conflict, "TERMINAL_STATE"), (afterResolve.StatusCode, (await afterResolve.ReadAsync<Problem>()).Code));
        Assert.Equal(healthBefore, await host.HealthAsync(IntegrationCodes.MultiFactor));
        Assert.Equal(
            ["INTEGRATION|IntegrationMonitoring.OperationalAlertRaised|SERVICE", "INTEGRATION|IntegrationMonitoring.AlertAcknowledged|USER", "INTEGRATION|IntegrationMonitoring.AlertResolved|USER"],
            await host.Database.QueryAsync($"SELECT event_class || '|' || event_type || '|' || actor_type FROM audit_activity.audit_event WHERE subject_id = '{id}' ORDER BY recorded_at"));
    }

    [Fact]
    public async Task ADisabledIntegrationRaisesNoAlertAndAnIntegrationThatRecoversKeepsItsAlert()
    {
        using HttpClient administrator = await host.ClientAsync(1);
        Guid nafath = await host.DefinitionIdAsync(IntegrationCodes.Nafath);
        await ResolveLiveAlertsAsync(administrator, nafath);
        DateTimeOffset at = await host.NextObservationTimeAsync(IntegrationCodes.Nafath);

        await (await administrator.SendSensitiveAsync($"{Integrations}/{nafath}/disable")).SuccessAsync();
        try
        {
            await RecordAsync(Observed(IntegrationCodes.Nafath, at, IntegrationInvocationOutcome.Succeeded));
            await RecordAsync(Observed(IntegrationCodes.Nafath, at.AddSeconds(1), IntegrationInvocationOutcome.Failed));
            Assert.Equal("DOWN", (await host.HealthAsync(IntegrationCodes.Nafath)).Split('|')[0]);
            Assert.Empty(await host.LiveAlertsAsync(IntegrationCodes.Nafath));
        }
        finally
        {
            await (await administrator.SendSensitiveAsync($"{Integrations}/{nafath}/enable")).SuccessAsync();
        }

        await RecordAsync(Observed(IntegrationCodes.Nafath, at.AddSeconds(2), IntegrationInvocationOutcome.Succeeded));
        await RecordAsync(Observed(IntegrationCodes.Nafath, at.AddSeconds(3), IntegrationInvocationOutcome.Failed));
        await RecordAsync(Observed(IntegrationCodes.Nafath, at.AddSeconds(4), IntegrationInvocationOutcome.Succeeded));

        Assert.Equal("UP", (await host.HealthAsync(IntegrationCodes.Nafath)).Split('|')[0]);
        Assert.Equal(["AVAILABILITY|OPEN"], await host.LiveAlertsAsync(IntegrationCodes.Nafath));
    }

    private static async Task ResolveLiveAlertsAsync(HttpClient administrator, Guid integrationId)
    {
        JsonArray live = (await (await administrator.GetAsync($"{Alerts}?integrationId={integrationId}&status=OPEN,ACKNOWLEDGED")).SuccessAsync())["items"]!.AsArray();
        foreach (JsonNode? alert in live)
        {
            await (await administrator.SendSensitiveAsync($"{Alerts}/{alert!["id"].Text()}/resolve", new { note = new { text = "Cleared before the test", language = "en" } })).SuccessAsync();
        }
    }

    private async Task RecordAsync(EventEnvelope<IntegrationInvocationData> envelope) =>
        await host.WithScopeAsync(async services =>
        {
            await services.GetRequiredService<Application.Features.IntegrationMonitoring.InvocationTelemetryIntake>().RecordAsync(envelope, CancellationToken.None);
            return true;
        });

    /// <summary>One call IdentityAccess made, as it publishes it: a fresh lineage, nothing retried.</summary>
    private static Application.Features.IdentityAccess.Contracts.Events.IntegrationInvocationCompleted Observed(string integrationCode, DateTimeOffset at, IntegrationInvocationOutcome outcome)
    {
        Guid id = Guid.CreateVersion7(at);
        bool failed = outcome == IntegrationInvocationOutcome.Failed;
        return new Application.Features.IdentityAccess.Contracts.Events.IntegrationInvocationCompleted
        {
            EventId = id,
            EventType = Application.Features.IdentityAccess.Contracts.Events.IntegrationInvocationCompleted.Type,
            SchemaVersion = 1,
            Kind = Domain.Common.EventKind.DomainEvent,
            MessageKey = EventMessageKey.Of(Application.Features.IdentityAccess.Contracts.Events.IntegrationInvocationCompleted.Type, id.ToString()),
            IdempotencyKey = id.ToString(),
            OccurredAt = at,
            SourceModule = "IdentityAccess",
            CorrelationId = Guid.NewGuid(),
            Actor = new EventActor(Domain.Common.AuditActorType.Service, new Guid("00000000-0000-4000-8000-0000000000f4")),
            Subject = new EventSubject("IdentityAccess", "IntegrationInvocation", id, null),
            Scope = new EventScope(null, null, null),
            Data = new IntegrationInvocationData(
                integrationCode, "START_CHALLENGE", id.ToString(), outcome, failed ? IntegrationFailureKind.Technical : null, IntegrationRetryDisposition.None,
                at, at, failed ? "PROVIDER_UNAVAILABLE" : null, null, null, null),
        };
    }
}
