using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;

namespace PMPlatform.Infrastructure.Persistence;

/// <summary>
/// Reports the API healthy only when the database named by <c>DB_CONNECTION_STRING</c> accepts a connection
/// and answers a query (TASK-014). The connection string is read per probe so a reloaded
/// <c>appsettings.{Environment}.Local.json</c> or a rotated secret takes effect without a restart.
/// </summary>
/// <remarks>
/// TASK-091: the readiness endpoints are anonymous, so one instance runs at most one probe at a time and starts at most one every
/// <see cref="ReuseFor"/>; the checks in between share its result. A flood of requests costs the database one query every two seconds,
/// not one per request. The probe gives the driver its own <see cref="TimeoutSeconds"/> for the connection and the query — Npgsql does not
/// honour a cancellation token while it waits for the server's handshake — so a database that accepts connections and never answers is
/// reported within seconds, not after the default 15. It takes no caller's cancellation, so a caller that disconnects cannot fail the
/// result every other caller is waiting for. Its connections, opened with their own timeouts, are pooled apart from the application's.
/// Registered as a singleton.
/// </remarks>
public sealed class PostgreSqlHealthCheck(IConfiguration configuration) : IHealthCheck
{
    public const string ConnectionStringKey = "DB_CONNECTION_STRING";

    /// <summary>For the connection and for the query. Below the start-up probe's own timeout and the uptime check's (both in <c>infra/terraform</c>).</summary>
    public const int TimeoutSeconds = 3;

    public static readonly TimeSpan ReuseFor = TimeSpan.FromSeconds(2);

    private readonly Lock _gate = new();
    private Task<HealthCheckResult>? _probe;
    private long _probeStartedAt;

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        Task<HealthCheckResult> current;
        lock (_gate)
        {
            if (_probe is null || (_probe.IsCompleted && Stopwatch.GetElapsedTime(_probeStartedAt) >= ReuseFor))
            {
                _probeStartedAt = Stopwatch.GetTimestamp();
                _probe = ProbeAsync();
            }

            current = _probe;
        }

        return current.WaitAsync(cancellationToken);
    }

    private async Task<HealthCheckResult> ProbeAsync()
    {
        string? connectionString = configuration[ConnectionStringKey];
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return HealthCheckResult.Unhealthy($"{ConnectionStringKey} is not configured.");
        }

        NpgsqlConnectionStringBuilder probe = new(connectionString) { Timeout = TimeoutSeconds, CommandTimeout = TimeoutSeconds };
#pragma warning disable CA1031 // Every way the database can fail to answer is the same result: unhealthy, with the reason for the log.
        try
        {
            await using NpgsqlConnection connection = new(probe.ConnectionString);
            await connection.OpenAsync().ConfigureAwait(false);
            await using NpgsqlCommand command = new("SELECT 1", connection);
            await command.ExecuteScalarAsync().ConfigureAwait(false);
            return HealthCheckResult.Healthy($"PostgreSQL {connection.PostgreSqlVersion} reachable.");
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy("PostgreSQL unreachable.", exception);
        }
#pragma warning restore CA1031
    }
}
