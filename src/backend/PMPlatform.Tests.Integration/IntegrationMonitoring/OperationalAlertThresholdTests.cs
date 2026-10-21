using System.Globalization;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using PMPlatform.Application.Common.Auditing;
using PMPlatform.Application.Common.Correlation;
using PMPlatform.Application.Features.IntegrationMonitoring;
using PMPlatform.Application.Features.IntegrationMonitoring.Contracts.Events;
using PMPlatform.Application.Features.Notifications;
using PMPlatform.Domain.Common;
using PMPlatform.Domain.IntegrationMonitoring;
using PMPlatform.Tests.Integration.Identity;
using static PMPlatform.Tests.Integration.IntegrationMonitoring.IntegrationMonitoringApi;
using TestSmtpServer = PMPlatform.Tests.Integration.Notifications.TestSmtpServer;

namespace PMPlatform.Tests.Integration.IntegrationMonitoring;

/// <summary>
/// TASK-092: the operational dashboard's queue state, the alert thresholds (PTBC-037; TBC-INT-08, -11, -15) and the route of a raised
/// alert to its configured recipient. Acceptance criterion 1: ADM-047's source, <c>GET /integrations</c>, gives every registered integration
/// its queue depth, retries and dead letters as they change. Criterion 2, and the workbook's validation check: a backlog forced past its
/// threshold raises one QUEUE_BACKLOG alert, which WF-15 delivers to the NOTIFICATION_ROUTING family's recipient — local.r01 (R01), in-app
/// and by e-mail — after one FG-05 pass, one outbox dispatch and one WF-15 pass, so within the sum of their poll intervals (the configured
/// window, docs/operations/alert-thresholds.json). Each test registers an integration of its own, so no other test's work is in its queue.
/// </summary>
[Collection(IntegrationMonitoringSuite.Name)]
public sealed class OperationalAlertThresholdTests(IntegrationMonitoringTestHost host)
{
    private const string Seed = IdentityDatabase.SeedPrincipalId;
    private const string R01 = "00000000-0000-4000-8000-000000000001";
    private const string AppBaseUrl = "https://pmplatform.test";
    private static readonly Guid RoutingVersionId = new("00000000-0920-4000-8000-000000000001");

    [Fact]
    public async Task EveryRegisteredIntegrationShowsItsQueueDepthRetriesAndDeadLettersAsTheyChange()
    {
        ThresholdSystem system = await RegisterAsync(maxAttempts: 3);
        await using IdentityApiFactory api = CreateApi(system);
        using HttpClient administrator = await host.ClientAsync(1, api: api);
        try
        {
            JsonArray items = (await CatalogueAsync(administrator))["items"]!.AsArray();
            Assert.All(items.SelectMany(i => i!["instances"]!.AsArray()), instance =>
            {
                JsonObject queue = instance!["queue"]!.AsObject();
                Assert.All(["depth", "inFlight", "awaitingRetry", "deadLetters", "oldestQueuedAt"], field => Assert.True(queue.ContainsKey(field), field));
            });
            Assert.Equal("0|0|0|0|", await QueueAsync(administrator, system));

            // The provider is down: two operations wait for their retry, the older one first queued at its first attempt.
            system.Operation.Outcome = _ => TestOperation.Unreachable();
            Invocation first = await StartAsync(api, system, $"queue-a-{Guid.NewGuid():N}");
            await StartAsync(api, system, $"queue-b-{Guid.NewGuid():N}");
            string firstQueuedAt = Assert.Single(await host.Database.QueryAsync(
                $"SELECT to_char(started_at AT TIME ZONE 'UTC', 'YYYY-MM-DD\"T\"HH24:MI:SS.US') FROM integration_monitoring.invocation WHERE id = '{first.Id}'"));
            Assert.Equal($"2|0|2|0|{firstQueuedAt}", await QueueAsync(administrator, system));

            // Retried once each: still queued, still dated by the first attempt of the oldest operation.
            host.Clock.Advance(TimeSpan.FromSeconds(61));
            await host.RunPassAsync(api);
            Assert.Equal($"2|0|2|0|{firstQueuedAt}", await QueueAsync(administrator, system));

            // The last attempts fail: both are dead letters now, out of the queue and waiting for an operator.
            host.Clock.Advance(TimeSpan.FromSeconds(121));
            await host.RunPassAsync(api);
            Assert.Equal("0|0|0|2|", await QueueAsync(administrator, system));
        }
        finally
        {
            host.Clock.Reset();
        }
    }

    [Fact]
    public async Task ABacklogBeyondItsThresholdRaisesOneAlertWhichWf15DeliversToTheConfiguredRecipient()
    {
        await EnsureAlertRoutingAsync();
        ThresholdSystem system = await RegisterAsync(maxAttempts: 3);
        await using TestSmtpServer smtp = await TestSmtpServer.StartAsync();
        Dictionary<string, string?> settings = new(smtp.Settings)
        {
            ["APP_BASE_URL"] = AppBaseUrl,
            [$"IntegrationMonitoring:Policy:QueueBacklogThresholds:{system.Code}:Depth"] = "1",
        };
        await using IdentityApiFactory api = CreateApi(system, settings);
        using HttpClient administrator = await host.ClientAsync(1, api: api);

        // The manually forced breach: two operations queued against a threshold of one.
        system.Operation.Outcome = _ => TestOperation.Unreachable();
        await StartAsync(api, system, $"backlog-a-{Guid.NewGuid():N}");
        await host.RunPassAsync(api);
        Assert.DoesNotContain(await host.LiveAlertsAsync(system.Code), a => a.StartsWith("QUEUE_BACKLOG|", StringComparison.Ordinal));
        await StartAsync(api, system, $"backlog-b-{Guid.NewGuid():N}");
        await host.RunPassAsync(api);
        await host.RunPassAsync(api);

        string alertId = Assert.Single(await host.Database.QueryAsync($"""
            SELECT a.id::text FROM integration_monitoring.operational_alert a
            WHERE a.integration_instance_id = '{system.InstanceId}' AND a.alert_type = 'QUEUE_BACKLOG' AND a.status = 'OPEN'
            """));
        Assert.Contains($"{system.Code} has 2 operations queued (0 in flight, 2 awaiting a retry)", Assert.Single(await host.Database.QueryAsync(
            $"SELECT detail FROM integration_monitoring.operational_alert WHERE id = '{alertId}'")), StringComparison.Ordinal);

        // Its intent is in the outbox under the alert's id, on a correlation id of its own (R-45: never the key).
        Assert.Equal("true", Assert.Single(await host.Database.QueryAsync($"""
            SELECT (payload->>'correlationId' IS NOT NULL AND payload->>'correlationId' <> payload->>'idempotencyKey')::text FROM common.outbox_message
            WHERE message_type = 'NOTIFICATION_INTENT' AND message_key = '{OperationalAlertRaised.Type}:{alertId}'
            """)));

        // One FG-05 pass raised it; one dispatch hands it to WF-15; one WF-15 pass routes and sends it.
        await host.DispatchAsync(api);
        await host.WithScopeAsync(services => services.GetRequiredService<INotificationProcessing>().RunAsync(100, CancellationToken.None), api);

        string deepLink = $"{OperationalAlerting.DeepLinkPrefix}{system.DefinitionId}";
        Assert.Equal(
            [$"{OperationalAlertRaised.Type}|{alertId}|{OperationalAlertRaised.EventFamilyCode}|{deepLink}"],
            (await host.AlertIntentsAsync()).Where(i => i.Contains(alertId, StringComparison.Ordinal)));
        Assert.Equal(
            [$"EMAIL|{IntegrationMonitoringTestHost.Person(1)}|SENT", $"IN_APP|{IntegrationMonitoringTestHost.Person(1)}|SENT"],
            await host.Database.QueryAsync($"""
                SELECT d.channel || '|' || d.recipient_user_id || '|' || d.status FROM notifications.notification_delivery d
                JOIN notifications.notification_intent i ON i.id = d.notification_intent_id
                WHERE i.source_reference = '{alertId}' ORDER BY d.channel
                """));

        JsonObject inbox = await (await administrator.GetAsync($"/api/v1/notifications?eventFamilyCode={OperationalAlertRaised.EventFamilyCode}")).SuccessAsync();
        // The provider being down is an AVAILABILITY alert of its own, on the same integration; the backlog's is this one.
        JsonNode notification = Assert.Single(inbox["items"]!.AsArray(), n => n!["subject"].Text() == $"WARNING QUEUE_BACKLOG: {system.Code}")!;
        Assert.Equal(deepLink, notification["deepLink"].Text());

        // The e-mail reached the relay, linking to ADM-046 on the environment's address; codes only, never the alert's detail.
        string email = Assert.Single(smtp.To("r01@identity.test"), m => m.Content.Contains($"Subject: WARNING QUEUE_BACKLOG: {system.Code}", StringComparison.Ordinal)).Content;
        Assert.DoesNotContain("awaiting a retry", email, StringComparison.Ordinal);
        Assert.Contains($"{AppBaseUrl}{deepLink}", Assert.Single(await host.Database.QueryAsync($"""
            SELECT d.rendered_body FROM notifications.notification_delivery d JOIN notifications.notification_intent i ON i.id = d.notification_intent_id
            WHERE i.source_reference = '{alertId}' AND d.channel = 'EMAIL'
            """)), StringComparison.Ordinal);
    }

    [Fact]
    public async Task WithoutABacklogThresholdNoBacklogAlertIsInvented()
    {
        ThresholdSystem system = await RegisterAsync(maxAttempts: 3);
        await using IdentityApiFactory api = CreateApi(system);
        system.Operation.Outcome = _ => TestOperation.Unreachable();
        for (int n = 0; n < 5; n++)
        {
            await StartAsync(api, system, $"unpoliced-{n}-{Guid.NewGuid():N}");
        }

        await host.RunPassAsync(api);

        Assert.Equal("5", Assert.Single(await host.Database.QueryAsync(
            $"SELECT count(*)::text FROM integration_monitoring.invocation WHERE integration_instance_id = '{system.InstanceId}' AND status = 'RETRY_SCHEDULED'")));
        Assert.DoesNotContain(await host.LiveAlertsAsync(system.Code), a => a.StartsWith("QUEUE_BACKLOG|", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AnAvailabilityAlertWaitsForItsDelayAndIsRaisedOncePerOutage()
    {
        ThresholdSystem system = await RegisterAsync(maxAttempts: 1);
        await using IdentityApiFactory api = CreateApi(system, new Dictionary<string, string?>
        {
            [$"IntegrationMonitoring:Policy:AvailabilityAlertDelays:{system.Code}"] = "00:05:00",
        });
        using HttpClient administrator = await host.ClientAsync(1, api: api);
        try
        {
            await StartAsync(api, system, $"up-{Guid.NewGuid():N}");
            system.Operation.Outcome = _ => TestOperation.Unreachable();
            await StartAsync(api, system, $"down-{Guid.NewGuid():N}");
            Assert.Equal("DOWN", (await host.HealthAsync(system.Code)).Split('|')[0]);

            // Down, but not for five minutes yet: no AVAILABILITY alert, at the failure or on the next pass.
            await host.RunPassAsync(api);
            Assert.Empty(await AvailabilityAlertsAsync(system));

            host.Clock.Advance(TimeSpan.FromMinutes(5));
            await host.RunPassAsync(api);
            string alert = Assert.Single(await AvailabilityAlertsAsync(system));
            Assert.StartsWith("OPEN|", alert, StringComparison.Ordinal);
            Assert.Contains("beyond its availability alert delay of 00:05:00", alert, StringComparison.Ordinal);

            // The operator closes it while the outage goes on: the outage has had its alert, so no second one.
            (await administrator.SendSensitiveAsync($"{Alerts}/{alert.Split('|')[1]}/resolve", new { note = new { text = "Provider ticket opened", language = "en" } })).Dispose();
            host.Clock.Advance(TimeSpan.FromMinutes(5));
            await host.RunPassAsync(api);
            Assert.Single(await AvailabilityAlertsAsync(system));

            // It recovers and goes down again: a new outage, alerted once its delay has passed.
            system.Operation.Outcome = _ => IntegrationOperationResult.Success();
            await StartAsync(api, system, $"recovered-{Guid.NewGuid():N}");
            system.Operation.Outcome = _ => TestOperation.Unreachable();
            await StartAsync(api, system, $"down-again-{Guid.NewGuid():N}");
            await host.RunPassAsync(api);
            Assert.Single(await AvailabilityAlertsAsync(system));
            host.Clock.Advance(TimeSpan.FromMinutes(6));
            await host.RunPassAsync(api);
            Assert.Equal(2, (await AvailabilityAlertsAsync(system)).Count);
        }
        finally
        {
            host.Clock.Reset();
        }
    }

    [Fact]
    public async Task AReconciliationMismatchWithinItsToleranceRaisesNoAlertAndOneBeyondItDoes()
    {
        ThresholdSystem system = await RegisterAsync(maxAttempts: 3, runType: SyncRunType.Reconciliation);
        await using IdentityApiFactory api = CreateApi(system, new Dictionary<string, string?>
        {
            [$"IntegrationMonitoring:Policy:ReconciliationMismatchTolerances:{system.Code}"] = "2",
        });

        system.Operation.Outcome = _ => IntegrationOperationResult.Success(new IntegrationRunOutcome(SyncRunStatus.Succeeded, 10, 0, 0, 2, "2 people differ"));
        await StartAsync(api, system, $"within-{Guid.NewGuid():N}");
        Assert.DoesNotContain(await host.LiveAlertsAsync(system.Code), a => a.StartsWith("RECONCILIATION_MISMATCH|", StringComparison.Ordinal));

        system.Operation.Outcome = _ => IntegrationOperationResult.Success(new IntegrationRunOutcome(SyncRunStatus.Succeeded, 10, 0, 0, 3, "3 people differ"));
        await StartAsync(api, system, $"beyond-{Guid.NewGuid():N}");
        Assert.Contains("RECONCILIATION_MISMATCH|OPEN", await host.LiveAlertsAsync(system.Code));

        // Both runs recorded every difference they found: the tolerance decides the alert, never the record.
        Assert.Equal(["2", "3"], await host.Database.QueryAsync(
            $"SELECT mismatch_count::text FROM integration_monitoring.sync_run WHERE integration_instance_id = '{system.InstanceId}' ORDER BY started_at"));
    }

    /// <summary>
    /// An integration registered as AHDA would register one, with an operation the test drives. Its code is the test's own, so its queue,
    /// health and alerts are the test's alone.
    /// </summary>
    private async Task<ThresholdSystem> RegisterAsync(short maxAttempts, SyncRunType? runType = null)
    {
        string code = $"THRESHOLD_{Guid.NewGuid():N}"[..20].ToUpperInvariant();
        Guid definitionId = Guid.NewGuid();
        Guid instanceId = Guid.NewGuid();
        await host.Database.ExecuteAsync($"""
            INSERT INTO integration_monitoring.integration_definition (id, code, name_ar, name_en, direction, integration_kind, max_attempts, retry_backoff_seconds, status,
                                                                       created_at, created_by, updated_at, updated_by)
            VALUES ('{definitionId}', '{code}', 'نظام لاختبار العتبات', 'Threshold test system', 'OUTBOUND', 'DATA', {maxAttempts}, 60, 'ACTIVE', now(), '{Seed}', now(), '{Seed}');
            INSERT INTO integration_monitoring.integration_instance (id, integration_definition_id, code, endpoint_configuration_key, is_enabled, availability, freshness,
                                                                     created_at, created_by, updated_at, updated_by)
            VALUES ('{instanceId}', '{definitionId}', 'PRIMARY', 'THRESHOLD_TEST_ENDPOINT', true, 'UNKNOWN', 'UNKNOWN', now(), '{Seed}', now(), '{Seed}');
            """);
        return new ThresholdSystem(code, definitionId, instanceId, new ThresholdOperation(code, runType));
    }

    /// <summary>An API with the suite's workers parked, the test operations registered and <paramref name="overrides"/> on top.</summary>
    private IdentityApiFactory CreateApi(ThresholdSystem system, IReadOnlyDictionary<string, string?>? overrides = null)
    {
        Dictionary<string, string?> settings = new(IntegrationMonitoringTestHost.ParkedWorkers);
        foreach ((string key, string? value) in overrides ?? new Dictionary<string, string?>())
        {
            settings[key] = value;
        }

        return host.Identity.CreateApi(settings, services =>
        {
            services.AddScoped<IIntegrationOperation>(_ => host.Operation);
            services.AddScoped<IIntegrationOperation>(_ => system.Operation);
        });
    }

    private Task<Invocation> StartAsync(IdentityApiFactory api, ThresholdSystem system, string key) =>
        host.WithScopeAsync(async services =>
        {
            using IDisposable scope = CorrelationScope.Begin(Guid.NewGuid());
            IntegrationDefinition definition = (await services.GetRequiredService<IIntegrationMonitoringRepository>().FindDefinitionByCodeAsync(system.Code, CancellationToken.None))!;
            IntegrationInvocationEngine engine = services.GetRequiredService<IntegrationInvocationEngine>();
            return await engine.StartAsync(
                definition, engine.OperationFor(system.Code, ThresholdOperation.Operation)!, key,
                attempt => new AuditEntry(AuditEventClass.Integration, "IntegrationMonitoring.ReconciliationRequested", AuditOutcome.Success)
                {
                    ActorUserId = IntegrationMonitoringServicePrincipal.Id,
                    ActorType = AuditActorType.Service,
                    Subject = new AuditSubject("IntegrationMonitoring", nameof(Invocation), attempt.Id),
                },
                CancellationToken.None);
        }, api);

    private static async Task<JsonObject> CatalogueAsync(HttpClient client) => await (await client.GetAsync(Integrations)).SuccessAsync();

    /// <summary>The system's queue on ADM-047's source as <c>depth|inFlight|awaitingRetry|deadLetters|oldestQueuedAt</c>.</summary>
    private static async Task<string> QueueAsync(HttpClient client, ThresholdSystem system)
    {
        JsonNode item = Assert.Single((await CatalogueAsync(client))["items"]!.AsArray(), i => i!["code"].Text() == system.Code)!;
        JsonObject queue = Assert.Single(item["instances"]!.AsArray())!["queue"]!.AsObject();
        string oldest = queue["oldestQueuedAt"] is { } at ? Iso(DateTimeOffset.Parse(at.GetValue<string>(), CultureInfo.InvariantCulture)) : string.Empty;
        return string.Join('|', queue["depth"]!.GetValue<int>(), queue["inFlight"]!.GetValue<int>(), queue["awaitingRetry"]!.GetValue<int>(), queue["deadLetters"]!.GetValue<int>(), oldest);
    }

    /// <summary>Every AVAILABILITY alert of the system, open or closed, as <c>status|id|detail</c>, oldest first.</summary>
    private Task<IReadOnlyList<string>> AvailabilityAlertsAsync(ThresholdSystem system) => host.Database.QueryAsync($"""
        SELECT status || '|' || id || '|' || detail FROM integration_monitoring.operational_alert
        WHERE integration_instance_id = '{system.InstanceId}' AND alert_type = 'AVAILABILITY' ORDER BY raised_at
        """);

    private static string Iso(DateTimeOffset value) => value.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.ffffff", CultureInfo.InvariantCulture);

    /// <summary>
    /// The configuration an environment adds for TASK-092: the INTEGRATION_OPERATIONAL_ALERT family, in-app and e-mail to R01, and the two
    /// templates of <see cref="OperationalAlertRaised"/>, born DRAFT, validated by one person and published by another (ADR-012, D-8).
    /// </summary>
    private async Task EnsureAlertRoutingAsync()
    {
        if ((await host.Database.QueryAsync($"SELECT id::text FROM master_data_config.configuration_version WHERE id = '{RoutingVersionId}'")).Count > 0)
        {
            return;
        }

        string family = OperationalAlertRaised.EventFamilyCode;
        string type = OperationalAlertRaised.Type;
        string r01 = IntegrationMonitoringTestHost.Person(1).ToString();
        string r02 = IntegrationMonitoringTestHost.Person(2).ToString();
        string r03 = IntegrationMonitoringTestHost.Person(3).ToString();
        await host.Database.ExecuteAsync($$$"""
            INSERT INTO master_data_config.configuration_version (id, configuration_family_id, version_no, lifecycle_state, created_at, created_by, updated_at, updated_by)
            SELECT '{{{RoutingVersionId}}}', f.id, 1 + (SELECT count(*) FROM master_data_config.configuration_version o WHERE o.configuration_family_id = f.id),
                   'DRAFT', now(), '{{{Seed}}}', now(), '{{{Seed}}}'
            FROM master_data_config.configuration_family f WHERE f.code = 'NOTIFICATION_ROUTING';
            INSERT INTO master_data_config.notification_event_family (id, configuration_version_id, code, label_ar, label_en, is_mandatory, created_at, created_by, updated_at, updated_by)
            VALUES ('00000000-0920-4000-8000-000000000011', '{{{RoutingVersionId}}}', '{{{family}}}', 'تنبيه تشغيلي للتكامل', 'Integration operational alert', true,
                    now(), '{{{Seed}}}', now(), '{{{Seed}}}');
            INSERT INTO master_data_config.notification_channel_rule (id, notification_event_family_id, channel, enabled_by_default, created_at, created_by, updated_at, updated_by)
            VALUES (gen_random_uuid(), '00000000-0920-4000-8000-000000000011', 'IN_APP', true, now(), '{{{Seed}}}', now(), '{{{Seed}}}'),
                   (gen_random_uuid(), '00000000-0920-4000-8000-000000000011', 'EMAIL', true, now(), '{{{Seed}}}', now(), '{{{Seed}}}');
            INSERT INTO master_data_config.notification_recipient_rule (id, notification_event_family_id, role_id, created_at, created_by, updated_at, updated_by)
            VALUES (gen_random_uuid(), '00000000-0920-4000-8000-000000000011', '{{{R01}}}', now(), '{{{Seed}}}', now(), '{{{Seed}}}');
            UPDATE master_data_config.configuration_version
            SET lifecycle_state = 'PUBLISHED', published_at = now() - interval '1 hour', effective_from = now() - interval '1 hour'
            WHERE id = '{{{RoutingVersionId}}}';

            INSERT INTO notifications.notification_template (id, event_family_code, event_type, channel, version_no, subject_ar, subject_en, body_ar, body_en,
                                                             lifecycle_state, created_at, created_by, updated_at, updated_by)
            VALUES ('00000000-0920-4000-8000-000000000021', '{{{family}}}', '{{{type}}}', 'IN_APP', 1,
                    '{{severity}} {{alertType}}: {{integrationCode}}', '{{severity}} {{alertType}}: {{integrationCode}}',
                    'رُفع تنبيه تشغيلي ({{alertType}}) على التكامل {{integrationCode}}.', 'An operational alert ({{alertType}}) was raised on integration {{integrationCode}}.',
                    'DRAFT', now(), '{{{r01}}}', now(), '{{{r01}}}'),
                   ('00000000-0920-4000-8000-000000000022', '{{{family}}}', '{{{type}}}', 'EMAIL', 1,
                    '{{severity}} {{alertType}}: {{integrationCode}}', '{{severity}} {{alertType}}: {{integrationCode}}',
                    'رُفع تنبيه تشغيلي ({{alertType}}) على التكامل {{integrationCode}}: {{deepLink}}', 'An operational alert ({{alertType}}) was raised on integration {{integrationCode}}: {{deepLink}}',
                    'DRAFT', now(), '{{{r01}}}', now(), '{{{r01}}}');
            UPDATE notifications.notification_template SET lifecycle_state = 'VALIDATED', validated_by_user_id = '{{{r02}}}', validated_at = now()
            WHERE id IN ('00000000-0920-4000-8000-000000000021', '00000000-0920-4000-8000-000000000022');
            UPDATE notifications.notification_template SET lifecycle_state = 'PUBLISHED', published_by_user_id = '{{{r03}}}', published_at = now()
            WHERE id IN ('00000000-0920-4000-8000-000000000021', '00000000-0920-4000-8000-000000000022');
            """);
    }

    private sealed record ThresholdSystem(string Code, Guid DefinitionId, Guid InstanceId, ThresholdOperation Operation);

    /// <summary>One operation of a test-registered integration, whose outcome the test sets; a run when <c>runType</c> is given.</summary>
    private sealed class ThresholdOperation(string code, SyncRunType? runType) : IIntegrationOperation
    {
        public const string Operation = "THRESHOLD_PROBE";

        public string IntegrationCode => code;

        public string OperationCode => Operation;

        public SyncRunType? RunType => runType;

        public bool IsAvailable => true;

        public Func<IntegrationOperationContext, IntegrationOperationResult> Outcome { get; set; } = _ => IntegrationOperationResult.Success();

        public Task<IntegrationOperationResult> ExecuteAsync(IntegrationOperationContext context, CancellationToken cancellationToken) =>
            Task.FromResult(Outcome(context));
    }
}
