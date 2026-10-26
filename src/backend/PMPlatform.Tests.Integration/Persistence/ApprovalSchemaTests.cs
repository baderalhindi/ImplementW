namespace PMPlatform.Tests.Integration.Persistence;

/// <summary>
/// TASK-035's tables against PostgreSQL with every migration applied: the <c>approval</c> schema and
/// <c>common.outbox_message</c> are the ERD's — columns, types, nullability, unique keys and foreign keys.
/// </summary>
public sealed class ApprovalSchemaTests(MigratedDatabase database) : IClassFixture<MigratedDatabase>
{
    private static readonly string[] Tables =
        ["approval.approval_instance", "approval.approval_task", "approval.approval_delegation", "common.outbox_message"];

    [Fact]
    public async Task TheApprovalTablesAndTheOutboxAreTheCanonicalErd()
    {
        IReadOnlyList<string> erd = [.. ErdModel.Lines(["approval", "common"])
            .Where(line => Tables.Any(table => line.Contains($"|{table}.", StringComparison.Ordinal) || line.Contains($"|{table}|", StringComparison.Ordinal)))
            .Order(StringComparer.Ordinal)];
        IReadOnlyList<string> schema = [.. (await database.QueryAsync("""
            SELECT 'column|' || n.nspname || '.' || c.relname || '.' || a.attname || '|' || format_type(a.atttypid, a.atttypmod)
                   || '|' || CASE WHEN a.attnotnull THEN 'not null' ELSE 'null' END
            FROM pg_attribute a JOIN pg_class c ON c.oid = a.attrelid JOIN pg_namespace n ON n.oid = c.relnamespace
            WHERE c.relkind = 'r' AND n.nspname || '.' || c.relname IN ('approval.approval_instance', 'approval.approval_task', 'approval.approval_delegation', 'common.outbox_message')
              AND a.attnum > 0 AND NOT a.attisdropped
            UNION ALL
            SELECT 'unique|' || n.nspname || '.' || t.relname || '|' || string_agg(a.attname, ',' ORDER BY k.ord)
            FROM pg_index i
            JOIN pg_class t ON t.oid = i.indrelid JOIN pg_namespace n ON n.oid = t.relnamespace
            CROSS JOIN LATERAL unnest(i.indkey) WITH ORDINALITY AS k(attnum, ord)
            JOIN pg_attribute a ON a.attrelid = t.oid AND a.attnum = k.attnum
            WHERE i.indisunique AND NOT i.indisprimary AND n.nspname || '.' || t.relname IN ('approval.approval_instance', 'approval.approval_task', 'approval.approval_delegation', 'common.outbox_message')
            GROUP BY i.indexrelid, n.nspname, t.relname
            UNION ALL
            SELECT 'fk|' || n.nspname || '.' || t.relname || '.' || a.attname || '|' || rn.nspname || '.' || r.relname
            FROM pg_constraint x
            JOIN pg_class t ON t.oid = x.conrelid JOIN pg_namespace n ON n.oid = t.relnamespace
            JOIN pg_class r ON r.oid = x.confrelid JOIN pg_namespace rn ON rn.oid = r.relnamespace
            JOIN pg_attribute a ON a.attrelid = t.oid AND a.attnum = x.conkey[1]
            WHERE x.contype = 'f' AND n.nspname || '.' || t.relname IN ('approval.approval_instance', 'approval.approval_task', 'approval.approval_delegation', 'common.outbox_message')
            """)).Order(StringComparer.Ordinal)];

        Assert.Equal(4, erd.Count(line => line.StartsWith("column|", StringComparison.Ordinal) && line.EndsWith(".id|uuid|not null", StringComparison.Ordinal)));
        Assert.Equal(erd, schema);
    }
}
