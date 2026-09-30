namespace PMPlatform.Tests.Integration.Persistence;

/// <summary>
/// TASK-037's tables against PostgreSQL with every migration applied: the <c>document_management</c> schema is the ERD's —
/// columns, types, nullability, unique keys and foreign keys.
/// </summary>
public sealed class DocumentSchemaTests(MigratedDatabase database) : IClassFixture<MigratedDatabase>
{
    [Fact]
    public async Task TheDocumentTablesAreTheCanonicalErd()
    {
        IReadOnlyList<string> erd = [.. ErdModel.Lines(["document_management"]).Order(StringComparer.Ordinal)];
        IReadOnlyList<string> schema = [.. (await database.QueryAsync("""
            SELECT 'column|' || n.nspname || '.' || c.relname || '.' || a.attname || '|' || format_type(a.atttypid, a.atttypmod)
                   || '|' || CASE WHEN a.attnotnull THEN 'not null' ELSE 'null' END
            FROM pg_attribute a JOIN pg_class c ON c.oid = a.attrelid JOIN pg_namespace n ON n.oid = c.relnamespace
            WHERE c.relkind = 'r' AND n.nspname = 'document_management' AND a.attnum > 0 AND NOT a.attisdropped
            UNION ALL
            SELECT 'unique|' || n.nspname || '.' || t.relname || '|' || string_agg(a.attname, ',' ORDER BY k.ord)
            FROM pg_index i
            JOIN pg_class t ON t.oid = i.indrelid JOIN pg_namespace n ON n.oid = t.relnamespace
            CROSS JOIN LATERAL unnest(i.indkey) WITH ORDINALITY AS k(attnum, ord)
            JOIN pg_attribute a ON a.attrelid = t.oid AND a.attnum = k.attnum
            WHERE i.indisunique AND NOT i.indisprimary AND n.nspname = 'document_management'
            GROUP BY i.indexrelid, n.nspname, t.relname
            UNION ALL
            SELECT 'fk|' || n.nspname || '.' || t.relname || '.' || a.attname || '|' || rn.nspname || '.' || r.relname
            FROM pg_constraint x
            JOIN pg_class t ON t.oid = x.conrelid JOIN pg_namespace n ON n.oid = t.relnamespace
            JOIN pg_class r ON r.oid = x.confrelid JOIN pg_namespace rn ON rn.oid = r.relnamespace
            JOIN pg_attribute a ON a.attrelid = t.oid AND a.attnum = x.conkey[1]
            WHERE x.contype = 'f' AND n.nspname = 'document_management'
            """)).Order(StringComparer.Ordinal)];

        Assert.Equal(4, erd.Count(line => line.StartsWith("column|", StringComparison.Ordinal) && line.EndsWith(".id|uuid|not null", StringComparison.Ordinal)));
        Assert.Equal(erd, schema);
    }
}
