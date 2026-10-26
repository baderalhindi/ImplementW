namespace PMPlatform.Tests.Integration.Persistence;

/// <summary>
/// TASK-026: the index register in <c>indexing-strategy.md</c> §4 names real ERD columns, and every index it lists
/// exists as soon as its table does. Tables of later modules are held to their rows the day their migration lands.
/// </summary>
public sealed class IndexingStrategyTests(MigratedDatabase database) : IClassFixture<MigratedDatabase>
{
    /// <summary>A typo in the register would otherwise pass unnoticed until the module that owns the table is built.</summary>
    [Fact]
    public void EverySpecifiedIndexNamesErdColumns()
    {
        IReadOnlyList<SpecifiedIndex> indexes = IndexingStrategy.Indexes;

        Assert.NotEmpty(indexes);
        Assert.Equal(indexes.Count, indexes.Select(i => i.Id).Distinct().Count());
        Assert.All(indexes, index =>
        {
            HashSet<string> columns = [.. ErdModel.Table(index.Table).Columns.Select(c => c.Name)];
            Assert.All(index.Columns, column => Assert.True(columns.Contains(column), $"{index.Id}: {index.Table} has no column {column}"));
        });
    }

    [Fact]
    public async Task EverySpecifiedIndexIsBuiltWithItsTable()
    {
        HashSet<string> tables = [.. await database.QueryAsync(
            "SELECT n.nspname || '.' || c.relname FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace WHERE c.relkind = 'r'")];
        HashSet<string> keys = [.. await database.QueryAsync("""
            SELECT n.nspname || '.' || t.relname || '|' || string_agg(a.attname, ', ' ORDER BY k.ord)
            FROM pg_index i
            JOIN pg_class t ON t.oid = i.indrelid JOIN pg_namespace n ON n.oid = t.relnamespace
            CROSS JOIN LATERAL unnest(i.indkey) WITH ORDINALITY AS k(attnum, ord)
            JOIN pg_attribute a ON a.attrelid = t.oid AND a.attnum = k.attnum
            WHERE i.indpred IS NULL AND i.indexprs IS NULL
            GROUP BY i.indexrelid, n.nspname, t.relname
            """)];

        List<SpecifiedIndex> due = [.. IndexingStrategy.Indexes.Where(i => tables.Contains(i.Table))];
        List<string> missing = [.. due
            .Where(i => !keys.Contains($"{i.Table}|{string.Join(", ", i.Columns)}"))
            .Select(i => $"{i.Id} {i.Table} ({string.Join(", ", i.Columns)})")];

        Assert.Equal(10, due.Count(i => i.Table.Split('.')[0] is "identity_access" or "master_data_config" or "project"));
        Assert.Empty(missing);
    }
}
