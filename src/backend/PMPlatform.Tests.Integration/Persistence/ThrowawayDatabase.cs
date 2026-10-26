using Microsoft.EntityFrameworkCore;
using Npgsql;
using PMPlatform.Infrastructure.Persistence;
using PMPlatform.Infrastructure.Secrets;

namespace PMPlatform.Tests.Integration.Persistence;

/// <summary>
/// An empty database created for one test and dropped after it, on the PostgreSQL server named by
/// <c>DB_CONNECTION_STRING</c>. Only the server and credentials are taken from that value; the database it names is
/// never touched. The role needs CREATEDB.
/// </summary>
internal sealed class ThrowawayDatabase : IAsyncDisposable
{
    private readonly string _serverConnectionString;
    private readonly string _name;

    private ThrowawayDatabase(string serverConnectionString, string name, string connectionString)
    {
        _serverConnectionString = serverConnectionString;
        _name = name;
        ConnectionString = connectionString;
    }

    public string ConnectionString { get; }

    public static async Task<ThrowawayDatabase> CreateAsync()
    {
        string server = Environment.GetEnvironmentVariable(ApplicationSecrets.DatabaseConnectionString) is { Length: > 0 } value
            ? value
            : throw new InvalidOperationException(
                $"{ApplicationSecrets.DatabaseConnectionString} is unset. The migration tests need a PostgreSQL 17 server they can " +
                "create a database on, e.g. Host=localhost;Port=5432;Database=postgres;Username=postgres;Password=…");

        string name = $"pmplatform_migration_test_{Guid.NewGuid():N}";
        await ExecuteAsync(server, $"CREATE DATABASE \"{name}\"");

        NpgsqlConnectionStringBuilder database = new(server) { Database = name };
        return new ThrowawayDatabase(server, name, database.ConnectionString);
    }

    public PMPlatformDbContext CreateContext()
    {
        DbContextOptionsBuilder<PMPlatformDbContext> options = new();
        options.UsePlatformDatabase(ConnectionString);
        return new PMPlatformDbContext(options.Options);
    }

    public async ValueTask DisposeAsync()
    {
        NpgsqlConnection.ClearAllPools();
        await ExecuteAsync(_serverConnectionString, $"DROP DATABASE IF EXISTS \"{_name}\" WITH (FORCE)");
    }

    private static async Task ExecuteAsync(string connectionString, string sql)
    {
        await using NpgsqlConnection connection = new(connectionString);
        await connection.OpenAsync();
        await using NpgsqlCommand command = new(sql, connection);
        await command.ExecuteNonQueryAsync();
    }
}
