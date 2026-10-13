using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using PMPlatform.Application.Common.Events;
using PMPlatform.Application.Features.IntegrationMonitoring;
using PMPlatform.Tests.Integration.AuditActivity;
using PMPlatform.Tests.Integration.Identity;

namespace PMPlatform.Tests.Integration.IntegrationMonitoring;

/// <summary>
/// The identity test database and providers with what FG-05 (TASK-075) needs beside the seeded registry: an internal system,
/// <see cref="TestOperation.Code"/>, whose one operation the test drives, registered as AHDA would register a future integration. The API's
/// outbox dispatcher and FG-05's pass are parked (an hour), so a test dispatches telemetry and runs the pass itself, in order.
/// </summary>
public sealed class IntegrationMonitoringTestHost : IAsyncLifetime
{
    public static readonly Guid TestSystemId = new("00000000-0750-4000-8000-000000000001");
    public static readonly Guid TestSystemInstanceId = new("00000000-0750-4000-8000-000000000002");

    /// <summary>Settings that park every background pass the tests run themselves.</summary>
    public static readonly IReadOnlyDictionary<string, string?> ParkedWorkers = new Dictionary<string, string?>
    {
        ["Outbox:PollInterval"] = "01:00:00",
        ["IntegrationMonitoring:Worker:PollInterval"] = "01:00:00",
        ["Notifications:Worker:PollInterval"] = "01:00:00",
        ["Approval:Maintenance:PollInterval"] = "01:00:00",
        ["Audit:Ingestion:PollInterval"] = "01:00:00",
        ["Audit:Activity:PollInterval"] = "01:00:00",
    };

    private const string Seed = IdentityDatabase.SeedPrincipalId;

    private static string Fixture => $"""
        INSERT INTO integration_monitoring.integration_definition (id, code, name_ar, name_en, direction, integration_kind, max_attempts, retry_backoff_seconds, status,
                                                                   created_at, created_by, updated_at, updated_by)
        VALUES ('{TestSystemId}', '{TestOperation.Code}', 'نظام داخلي للاختبار', 'Internal test system', 'OUTBOUND', 'DATA', 3, 60, 'ACTIVE', now(), '{Seed}', now(), '{Seed}');
        INSERT INTO integration_monitoring.integration_instance (id, integration_definition_id, code, endpoint_configuration_key, is_enabled, availability, freshness,
                                                                 created_at, created_by, updated_at, updated_by)
        VALUES ('{TestSystemInstanceId}', '{TestSystemId}', 'PRIMARY', 'TEST_SYSTEM_ENDPOINT', true, 'UNKNOWN', 'UNKNOWN', now(), '{Seed}', now(), '{Seed}');
        """;

    public IdentityTestHost Identity { get; } = new() { SharedApiSettings = ParkedWorkers };

    public IdentityDatabase Database => Identity.Database;

    public AdjustableTimeProvider Clock => Identity.Clock;

    internal TestOperation Operation { get; } = new();

    public IdentityApiFactory Api { get; private set; } = null!;

    public static Guid Person(int n) => Guid.Parse(IdentityDatabase.UserId(n));

    public async Task InitializeAsync()
    {
        await Identity.InitializeAsync();
        await Database.ExecuteAsync(Fixture);
        Api = CreateApi();
    }

    /// <summary>An API on the suite's database with the test operation registered, the workers parked and <paramref name="overrides"/> on top.</summary>
    public IdentityApiFactory CreateApi(IReadOnlyDictionary<string, string?>? overrides = null)
    {
        Dictionary<string, string?> settings = new(ParkedWorkers);
        foreach ((string key, string? value) in overrides ?? new Dictionary<string, string?>())
        {
            settings[key] = value;
        }

        return Identity.CreateApi(settings, services => services.AddScoped<IIntegrationOperation>(_ => Operation));
    }

    /// <summary>A client of <paramref name="api"/> (the suite's by default) signed in as local.r0<paramref name="person"/>.</summary>
    public async Task<HttpClient> ClientAsync(int person, Guid? correlationId = null, IdentityApiFactory? api = null)
    {
        HttpClient client = (api ?? Api).CreateClient();
        Session session = await client.SignInOrFailAsync(person);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
        if (correlationId is { } id)
        {
            client.WithCorrelationId(id);
        }

        return client;
    }

    /// <summary>Delivers every outbox message that is due — the integration telemetry among them — until none is left; bounded.</summary>
    public async Task DispatchAsync(IdentityApiFactory? api = null)
    {
        IOutboxDispatcher dispatcher = (api ?? Api).Services.GetRequiredService<IOutboxDispatcher>();
        for (int pass = 0; pass < 20 && await dispatcher.DispatchDueAsync(500, CancellationToken.None) > 0; pass++)
        {
        }
    }

    /// <summary>One FG-05 pass on <paramref name="api"/>: due retries, abandoned attempts, health.</summary>
    public async Task RunPassAsync(IdentityApiFactory? api = null)
    {
        await using AsyncServiceScope scope = (api ?? Api).Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IIntegrationMonitoringMaintenance>().RunAsync(100, CancellationToken.None);
    }

    public async Task<T> WithScopeAsync<T>(Func<IServiceProvider, Task<T>> action, IdentityApiFactory? api = null)
    {
        ArgumentNullException.ThrowIfNull(action);
        await using AsyncServiceScope scope = (api ?? Api).Services.CreateAsyncScope();
        return await action(scope.ServiceProvider);
    }

    /// <summary>The id of the definition with <paramref name="code"/>.</summary>
    public async Task<Guid> DefinitionIdAsync(string code) =>
        Guid.Parse(Assert.Single(await Database.QueryAsync($"SELECT id::text FROM integration_monitoring.integration_definition WHERE code = '{code}'")));

    /// <summary>The instance of <paramref name="code"/> as <c>availability|freshness</c>.</summary>
    public async Task<string> HealthAsync(string code) => Assert.Single(await Database.QueryAsync($"""
        SELECT i.availability || '|' || i.freshness FROM integration_monitoring.integration_instance i
        JOIN integration_monitoring.integration_definition d ON d.id = i.integration_definition_id WHERE d.code = '{code}'
        """));

    /// <summary>
    /// A time after every attempt recorded against <paramref name="code"/>'s instance, so an observation a test records is the latest
    /// whatever another test recorded with the clock advanced: availability follows the latest technical outcome.
    /// </summary>
    public async Task<DateTimeOffset> NextObservationTimeAsync(string code) =>
        DateTimeOffset.Parse(Assert.Single(await Database.QueryAsync($"""
            SELECT to_char((greatest(now(), i.last_success_at, i.last_failure_at) + interval '1 second') AT TIME ZONE 'UTC', 'YYYY-MM-DD"T"HH24:MI:SS.US"Z"')
            FROM integration_monitoring.integration_instance i
            JOIN integration_monitoring.integration_definition d ON d.id = i.integration_definition_id WHERE d.code = '{code}'
            """)), System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>The live (OPEN or ACKNOWLEDGED) alerts of <paramref name="code"/>, as <c>alert_type|status</c>, by type.</summary>
    public Task<IReadOnlyList<string>> LiveAlertsAsync(string code) => Database.QueryAsync($"""
        SELECT a.alert_type || '|' || a.status FROM integration_monitoring.operational_alert a
        JOIN integration_monitoring.integration_instance i ON i.id = a.integration_instance_id
        JOIN integration_monitoring.integration_definition d ON d.id = i.integration_definition_id
        WHERE d.code = '{code}' AND a.status IN ('OPEN', 'ACKNOWLEDGED') ORDER BY a.alert_type
        """);

    /// <summary>
    /// Every table outside <c>integration_monitoring</c>, the audit store and the outbox, each with a digest of all its rows: the business
    /// state FG-05 must never change (TASK-075 acceptance criterion 1).
    /// </summary>
    public async Task<IReadOnlyList<string>> BusinessStateAsync()
    {
        IReadOnlyList<string> tables = await Database.QueryAsync("""
            SELECT format('%I.%I', n.nspname, c.relname) FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace
            WHERE c.relkind = 'r' AND n.nspname NOT IN ('pg_catalog', 'information_schema', 'integration_monitoring', 'audit_activity')
              AND NOT (n.nspname = 'common' AND c.relname = 'outbox_message')
            ORDER BY 1
            """);
        return await Database.QueryAsync(string.Join(" UNION ALL ", tables.Select(t =>
            $"SELECT '{t}|' || count(*) || '|' || coalesce(md5(string_agg(r::text, '|' ORDER BY r::text)), '') FROM {t} r")) + " ORDER BY 1");
    }

    public async Task DisposeAsync()
    {
        await Api.DisposeAsync();
        await Identity.DisposeAsync();
    }
}

[CollectionDefinition(Name)]
public sealed class IntegrationMonitoringSuite : ICollectionFixture<IntegrationMonitoringTestHost>
{
    public const string Name = "IntegrationMonitoring";
}

internal static class IntegrationMonitoringApi
{
    public const string Integrations = "/api/v1/integrations";
    public const string Invocations = "/api/v1/integration-invocations";
    public const string SyncRuns = "/api/v1/sync-runs";
    public const string Reconciliations = "/api/v1/integration-reconciliations";
    public const string Alerts = "/api/v1/operational-alerts";

    /// <summary>A sensitive write with <paramref name="idempotencyKey"/> (a fresh one by default), a JSON body when given, and <c>If-Match</c> when given.</summary>
    public static async Task<HttpResponseMessage> SendSensitiveAsync(
        this HttpClient client, string path, object? body = null, Guid? idempotencyKey = null, string? ifMatch = null)
    {
        using HttpRequestMessage request = new(HttpMethod.Post, path);
        request.Headers.Add("Idempotency-Key", (idempotencyKey ?? Guid.NewGuid()).ToString());
        if (ifMatch is not null)
        {
            request.Headers.TryAddWithoutValidation("If-Match", ifMatch);
        }

        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return await client.SendAsync(request);
    }

    /// <summary>The body of a 2xx, as JSON.</summary>
    public static async Task<JsonObject> SuccessAsync(this HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return await response.ReadObjectAsync();
    }

    public static string Text(this JsonNode? node) => node!.GetValue<string>();
}
