using Npgsql;

namespace PMPlatform.Tests.Integration.Persistence;

/// <summary>
/// The structure of every non-system schema as sorted text lines — schemas, relations, columns, constraints,
/// indexes, functions, triggers and enum labels — so two databases have the same schema exactly when their
/// snapshots are equal, and an assertion failure names the line that differs. Data is not compared.
/// </summary>
internal static class SchemaSnapshot
{
    private const string Query = """
        WITH user_schema AS (
            SELECT oid, nspname FROM pg_namespace
            WHERE nspname NOT IN ('pg_catalog', 'information_schema') AND nspname NOT LIKE 'pg\_%'
        )
        SELECT 'schema|' || nspname FROM user_schema
        UNION ALL
        SELECT 'relation|' || s.nspname || '.' || c.relname || '|' || c.relkind::text
        FROM pg_class c JOIN user_schema s ON s.oid = c.relnamespace
        UNION ALL
        SELECT 'column|' || s.nspname || '.' || c.relname || '.' || a.attname || '|' || format_type(a.atttypid, a.atttypmod)
               || '|' || CASE WHEN a.attnotnull THEN 'not null' ELSE 'null' END
               || '|' || coalesce(pg_get_expr(d.adbin, d.adrelid), '') || '|' || a.attidentity::text || a.attgenerated::text
        FROM pg_attribute a
        JOIN pg_class c ON c.oid = a.attrelid
        JOIN user_schema s ON s.oid = c.relnamespace
        LEFT JOIN pg_attrdef d ON d.adrelid = a.attrelid AND d.adnum = a.attnum
        WHERE a.attnum > 0 AND NOT a.attisdropped
        UNION ALL
        SELECT 'constraint|' || s.nspname || '.' || coalesce(c.relname, '') || '|' || x.conname || '|' || pg_get_constraintdef(x.oid)
        FROM pg_constraint x JOIN user_schema s ON s.oid = x.connamespace LEFT JOIN pg_class c ON c.oid = x.conrelid
        UNION ALL
        SELECT 'index|' || s.nspname || '|' || pg_get_indexdef(i.indexrelid)
        FROM pg_index i JOIN pg_class c ON c.oid = i.indexrelid JOIN user_schema s ON s.oid = c.relnamespace
        UNION ALL
        SELECT 'function|' || s.nspname || '.' || p.proname || '(' || pg_get_function_identity_arguments(p.oid) || ')|' || md5(p.prosrc)
        FROM pg_proc p JOIN user_schema s ON s.oid = p.pronamespace
        UNION ALL
        SELECT 'trigger|' || s.nspname || '|' || pg_get_triggerdef(t.oid)
        FROM pg_trigger t JOIN pg_class c ON c.oid = t.tgrelid JOIN user_schema s ON s.oid = c.relnamespace
        WHERE NOT t.tgisinternal
        UNION ALL
        SELECT 'enum|' || s.nspname || '.' || ty.typname || '|' || e.enumsortorder || '|' || e.enumlabel
        FROM pg_enum e JOIN pg_type ty ON ty.oid = e.enumtypid JOIN user_schema s ON s.oid = ty.typnamespace
        """;

    public static async Task<IReadOnlyList<string>> TakeAsync(string connectionString)
    {
        await using NpgsqlConnection connection = new(connectionString);
        await connection.OpenAsync();
        await using NpgsqlCommand command = new(Query, connection);
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync();

        List<string> lines = [];
        while (await reader.ReadAsync())
        {
            lines.Add(reader.GetString(0));
        }

        return [.. lines.Order(StringComparer.Ordinal)];
    }
}
