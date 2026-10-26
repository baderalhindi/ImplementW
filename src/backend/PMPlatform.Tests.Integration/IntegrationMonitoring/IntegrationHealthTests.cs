using System.Globalization;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using PMPlatform.Application.Common.Correlation;
using PMPlatform.Application.Common.Events;
using PMPlatform.Application.Features.IntegrationMonitoring;
using PMPlatform.Domain.IntegrationMonitoring;
using PMPlatform.Tests.Integration.Identity;
using static PMPlatform.Tests.Integration.IntegrationMonitoring.IntegrationMonitoringApi;

namespace PMPlatform.Tests.Integration.IntegrationMonitoring;

/// <summary>
/// ADR-004's participation amendment — the SMS provider monitored like Exchange and Nafath, by delivery rate, failure rate and receipt
/// latency — and the time-driven half of health: freshness lapses without any new attempt, and a delivery rate below the configured minimum
/// raises an alert. A measure with no sample is null, never zero; no threshold applies unless configured.
/// </summary>
[Collection(IntegrationMonitoringSuite.Name)]
public sealed class IntegrationHealthTests(IntegrationMonitoringTestHost host)
{
    /// <summary>A day no other test records anything on, so the window holds only this test's attempts.</summary>
    private static readonly DateTimeOffset Day = new(2031, 3, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task TheSmsProvidersDeliveryRateFailureRateLatencyAndReceiptLatencyAreMeasured()
    {
        using HttpClient administrator = await host.ClientAsync(1);
        Guid a = Guid.NewGuid(), b = Guid.NewGuid(), c = Guid.NewGuid();
        await RecordAsync(Telemetry.Delivered(a, 1, Day));
        await RecordAsync(Telemetry.EmailAttempt(b, 1, Day.AddMinutes(1), dead: false, IntegrationCodes.Sms));
        await RecordAsync(Telemetry.Delivered(b, 2, Day.AddMinutes(2)));
        await RecordAsync(Telemetry.EmailAttempt(c, 1, Day.AddMinutes(3), dead: true, IntegrationCodes.Sms));
        await RecordAsync(Telemetry.Receipt(a, Day, TimeSpan.FromSeconds(30)));
        Guid sms = await host.DefinitionIdAsync(IntegrationCodes.Sms);

        JsonObject metrics = Assert.Single((await (await administrator.GetAsync($"{Integrations}/{sms}?from={Iso(Day)}&to={Iso(Day.AddDays(1))}")).SuccessAsync())["metrics"]!.AsArray())!.AsObject();
        JsonObject empty = Assert.Single((await (await administrator.GetAsync($"{Integrations}/{sms}?from={Iso(Day.AddYears(1))}&to={Iso(Day.AddYears(1).AddDays(1))}")).SuccessAsync())["metrics"]!.AsArray())!.AsObject();

        Assert.Equal((4, 2, 0.5), (metrics["attempts"]!.GetValue<int>(), metrics["failedAttempts"]!.GetValue<int>(), metrics["failureRate"]!.GetValue<double>()));
        Assert.Equal((2, 1), (metrics["deliveredLineages"]!.GetValue<int>(), metrics["undeliveredLineages"]!.GetValue<int>()));
        Assert.Equal(2.0 / 3, metrics["deliveryRate"]!.GetValue<double>(), 6);
        Assert.Equal(325, metrics["medianLatencyMilliseconds"]!.GetValue<double>(), 0);
        Assert.Equal((1, 30_000d), (metrics["receipts"]!.GetValue<int>(), metrics["medianReceiptLatencyMilliseconds"]!.GetValue<double>()));
        Assert.Equal(0, empty["attempts"]!.GetValue<int>());
        Assert.All(["failureRate", "deliveryRate", "medianLatencyMilliseconds", "p95LatencyMilliseconds", "medianReceiptLatencyMilliseconds"], m => Assert.Null(empty[m]));
    }

    [Fact]
    public async Task FreshnessLapsesWithoutAnyNewAttemptAndRaisesAnAlert()
    {
        await using IdentityApiFactory policed = host.CreateApi(new Dictionary<string, string?> { [$"IntegrationMonitoring:Policy:FreshnessWindows:{TestOperation.Code}"] = "00:10:00" });
        await ResolveLiveAlertsAsync(TestOperation.Code);
        try
        {
            await host.WithScopeAsync(StartTestOperationAsync, policed);
            Assert.Equal("FRESH", (await host.HealthAsync(TestOperation.Code)).Split('|')[1]);

            DateTimeOffset lastSuccess = DateTimeOffset.Parse(Assert.Single(await host.Database.QueryAsync(
                $"SELECT to_char(last_success_at AT TIME ZONE 'UTC', 'YYYY-MM-DD\"T\"HH24:MI:SS.US\"Z\"') FROM integration_monitoring.integration_instance WHERE id = '{IntegrationMonitoringTestHost.TestSystemInstanceId}'")),
                CultureInfo.InvariantCulture);
            host.Clock.Advance(lastSuccess - host.Clock.GetUtcNow() + TimeSpan.FromMinutes(11));
            await host.RunPassAsync(policed);

            Assert.Equal("STALE", (await host.HealthAsync(TestOperation.Code)).Split('|')[1]);
            Assert.Contains("FRESHNESS|OPEN", await host.LiveAlertsAsync(TestOperation.Code));
        }
        finally
        {
            host.Clock.Reset();
        }
    }

    [Fact]
    public async Task ADeliveryRateBelowTheConfiguredMinimumRaisesAnAlertAndWithoutOneNothingIsInvented()
    {
        await ResolveLiveAlertsAsync(IntegrationCodes.Sms);
        await RecordAsync(Telemetry.EmailAttempt(Guid.NewGuid(), 1, DateTimeOffset.UtcNow.AddMinutes(-1), dead: true, IntegrationCodes.Sms));
        await ResolveLiveAlertsAsync(IntegrationCodes.Sms);

        await host.RunPassAsync();
        Assert.DoesNotContain(await host.LiveAlertsAsync(IntegrationCodes.Sms), a => a.StartsWith("DELIVERY_RATE|", StringComparison.Ordinal));

        await using IdentityApiFactory policed = host.CreateApi(new Dictionary<string, string?>
        {
            [$"IntegrationMonitoring:Policy:MinimumDeliveryRates:{IntegrationCodes.Sms}"] = "0.9",
            ["IntegrationMonitoring:Policy:MetricsWindow"] = "1.00:00:00",
        });
        await host.RunPassAsync(policed);

        Assert.Contains("DELIVERY_RATE|OPEN", await host.LiveAlertsAsync(IntegrationCodes.Sms));
    }

    private async Task ResolveLiveAlertsAsync(string code) => await host.Database.ExecuteAsync($"""
        UPDATE integration_monitoring.operational_alert a SET status = 'RESOLVED', resolved_at = now(), resolution_note = 'Cleared before the test', resolution_note_lang = 'en'
        FROM integration_monitoring.integration_instance i JOIN integration_monitoring.integration_definition d ON d.id = i.integration_definition_id
        WHERE a.integration_instance_id = i.id AND d.code = '{code}' AND a.status IN ('OPEN', 'ACKNOWLEDGED')
        """);

    private async Task<bool> StartTestOperationAsync(IServiceProvider services)
    {
        using IDisposable scope = CorrelationScope.Begin(Guid.NewGuid());
        IntegrationDefinition definition = (await services.GetRequiredService<IIntegrationMonitoringRepository>().FindDefinitionByCodeAsync(TestOperation.Code, CancellationToken.None))!;
        IntegrationInvocationEngine engine = services.GetRequiredService<IntegrationInvocationEngine>();
        await engine.StartAsync(
            definition, engine.OperationFor(TestOperation.Code, TestOperation.Operation)!, $"fresh-{Guid.NewGuid():N}",
            attempt => new Application.Common.Auditing.AuditEntry(Domain.Common.AuditEventClass.Integration, "IntegrationMonitoring.ReconciliationRequested", Domain.Common.AuditOutcome.Success)
            {
                ActorUserId = IntegrationMonitoringServicePrincipal.Id,
                ActorType = Domain.Common.AuditActorType.Service,
            },
            CancellationToken.None);
        return true;
    }

    private async Task RecordAsync(EventEnvelope<IntegrationInvocationData> envelope) =>
        await host.WithScopeAsync(async services =>
        {
            await services.GetRequiredService<InvocationTelemetryIntake>().RecordAsync(envelope, CancellationToken.None);
            return true;
        });

    private static string Iso(DateTimeOffset value) => Uri.EscapeDataString(value.ToString("O", CultureInfo.InvariantCulture));
}
