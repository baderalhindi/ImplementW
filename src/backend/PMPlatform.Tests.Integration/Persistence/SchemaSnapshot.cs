using Npgsql;

namespace PMPlatform.Tests.Integration.Persistence;

/// <summary>
/// The structure of every non-system schema as sorted text lines — schemas, relations, columns, constraints,
/// indexes, functions, triggers and enum labels — so two databases have the same schema exactly when their
/// snapshots are equal, and an assertion failure names the line that differs. Data is not compared. The query is
/// <c>db/migrations/validation/schema-snapshot.sql</c>, which the TASK-089 volume rehearsal checksums too.
/// </summary>
internal static class SchemaSnapshot
{
    private static readonly Lazy<string> Query = new(() => File.ReadAllText(RepositoryFile.Path("db/migrations/validation/schema-snapshot.sql")));

    public static async Task<IReadOnlyList<string>> TakeAsync(string connectionString)
    {
        await using NpgsqlConnection connection = new(connectionString);
        await connection.OpenAsync();
        await using NpgsqlCommand command = new(Query.Value, connection);
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync();

        List<string> lines = [];
        while (await reader.ReadAsync())
        {
            lines.Add(reader.GetString(0));
        }

        return [.. lines.Order(StringComparer.Ordinal)];
    }
}
