using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;

namespace PMPlatform.Infrastructure.Persistence;

/// <summary>
/// Reports the API healthy only when the database named by <c>DB_CONNECTION_STRING</c> accepts a connection
/// and answers a query (TASK-014). The connection string is read per check so a reloaded
/// <c>appsettings.{Environment}.Local.json</c> takes effect without a restart.
/// </summary>
public sealed class PostgreSqlHealthCheck(IConfiguration configuration) : IHealthCheck
{
    public const string ConnectionStringKey = "DB_CONNECTION_STRING";

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        string? connectionString = configuration[ConnectionStringKey];
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return HealthCheckResult.Unhealthy($"{ConnectionStringKey} is not configured.");
        }

        // A failure to open or query is thrown, and HealthCheckService reports it as Unhealthy with the exception.
        await using NpgsqlConnection connection = new(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using NpgsqlCommand command = new("SELECT 1", connection);
        await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);

        return HealthCheckResult.Healthy($"PostgreSQL {connection.PostgreSqlVersion} reachable.");
    }
}
