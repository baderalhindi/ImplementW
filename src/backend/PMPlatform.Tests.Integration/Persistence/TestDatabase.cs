using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace PMPlatform.Tests.Integration.Persistence;

/// <summary>
/// One throwaway database with every migration applied, populated by the derived fixture and shared by the tests of
/// one class. Tests that write either roll back (<see cref="ExecuteRolledBackAsync"/>) or only add rows no other
/// test reads.
/// </summary>
public abstract class TestDatabase : IAsyncLifetime
{
    private ThrowawayDatabase? _database;

    public string ConnectionString => _database?.ConnectionString ?? throw new InvalidOperationException("The database is not initialised.");

    public async Task InitializeAsync()
    {
        _database = await ThrowawayDatabase.CreateAsync();
        await using (var context = _database.CreateContext())
        {
            await context.Database.MigrateAsync();
        }

        await PopulateAsync();
    }

    public async Task DisposeAsync()
    {
        if (_database is not null)
        {
            await _database.DisposeAsync();
        }
    }

    /// <summary>Loads the rows the fixture's tests start from, after the migrations.</summary>
    protected abstract Task PopulateAsync();

    public async Task ExecuteAsync(string sql)
    {
        await using NpgsqlConnection connection = new(ConnectionString);
        await connection.OpenAsync();
        await using NpgsqlCommand command = new(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    /// <summary>Runs <paramref name="sql"/> in a transaction that is always rolled back, so the shared data is unchanged.</summary>
    public Task ExecuteRolledBackAsync(string sql) => QueryRolledBackAsync(sql, query: null);

    /// <summary>
    /// Runs <paramref name="sql"/>, then <paramref name="query"/> (if any) in the same transaction, and always rolls
    /// back. Returns the first column of every row of the query, as text.
    /// </summary>
    public async Task<IReadOnlyList<string>> QueryRolledBackAsync(string sql, string? query)
    {
        await using NpgsqlConnection connection = new(ConnectionString);
        await connection.OpenAsync();
        await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync();
        try
        {
            await using (NpgsqlCommand command = new(sql, connection, transaction))
            {
                await command.ExecuteNonQueryAsync();
            }

            return query is null ? [] : await ReadFirstColumnAsync(new NpgsqlCommand(query, connection, transaction));
        }
        finally
        {
            await transaction.RollbackAsync();
        }
    }

    /// <summary>The first column of every row, as text.</summary>
    public async Task<IReadOnlyList<string>> QueryAsync(string sql)
    {
        await using NpgsqlConnection connection = new(ConnectionString);
        await connection.OpenAsync();
        return await ReadFirstColumnAsync(new NpgsqlCommand(sql, connection));
    }

    private static async Task<IReadOnlyList<string>> ReadFirstColumnAsync(NpgsqlCommand command)
    {
        await using (command)
        {
            await using NpgsqlDataReader reader = await command.ExecuteReaderAsync();
            List<string> rows = [];
            while (await reader.ReadAsync())
            {
                rows.Add(reader.GetString(0));
            }

            return rows;
        }
    }
}
