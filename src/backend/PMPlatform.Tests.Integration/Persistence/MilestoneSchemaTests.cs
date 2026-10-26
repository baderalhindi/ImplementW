namespace PMPlatform.Tests.Integration.Persistence;

/// <summary>
/// TASK-050's tables against PostgreSQL with every migration applied: <c>schedule.project_milestone</c>,
/// <c>schedule.baseline_milestone</c> and <c>milestone.milestone_achievement</c> are the ERD's — columns, types, nullability,
/// unique keys and foreign keys — and the <c>milestone</c> schema holds the achievement revisions and nothing else, so there is
/// one milestone row, WF-03's, that WF-05 names by a foreign key and never copies (ICD-04).
/// </summary>
public sealed class MilestoneSchemaTests(MigratedDatabase database) : IClassFixture<MigratedDatabase>
{
    private static readonly string[] Tables = ["schedule.project_milestone", "schedule.baseline_milestone", "milestone.milestone_achievement"];

    private static readonly string TableList = string.Join(", ", Tables.Select(t => $"'{t}'"));

    [Fact]
    public async Task TheMilestoneTablesAreTheCanonicalErd()
    {
        IReadOnlyList<string> erd = [.. ErdModel.Lines(["schedule", "milestone"])
            .Where(line => Tables.Any(table => line.Contains($"|{table}.", StringComparison.Ordinal) || line.Contains($"|{table}|", StringComparison.Ordinal)))
            .Order(StringComparer.Ordinal)];

        // The partial unique indexes are the ERD's notes, not its index blocks, so they are compared on their own below.
        IReadOnlyList<string> schema = [.. (await database.QueryAsync($"""
            SELECT 'column|' || n.nspname || '.' || c.relname || '.' || a.attname || '|' || format_type(a.atttypid, a.atttypmod)
                   || '|' || CASE WHEN a.attnotnull THEN 'not null' ELSE 'null' END
            FROM pg_attribute a JOIN pg_class c ON c.oid = a.attrelid JOIN pg_namespace n ON n.oid = c.relnamespace
            WHERE c.relkind = 'r' AND n.nspname || '.' || c.relname IN ({TableList}) AND a.attnum > 0 AND NOT a.attisdropped
            UNION ALL
            SELECT 'unique|' || n.nspname || '.' || t.relname || '|' || string_agg(a.attname, ',' ORDER BY k.ord)
            FROM pg_index i
            JOIN pg_class t ON t.oid = i.indrelid JOIN pg_namespace n ON n.oid = t.relnamespace
            CROSS JOIN LATERAL unnest(i.indkey) WITH ORDINALITY AS k(attnum, ord)
            JOIN pg_attribute a ON a.attrelid = t.oid AND a.attnum = k.attnum
            WHERE i.indisunique AND NOT i.indisprimary AND i.indpred IS NULL AND n.nspname || '.' || t.relname IN ({TableList})
            GROUP BY i.indexrelid, n.nspname, t.relname
            UNION ALL
            SELECT 'fk|' || n.nspname || '.' || t.relname || '.' || a.attname || '|' || rn.nspname || '.' || r.relname
            FROM pg_constraint x
            JOIN pg_class t ON t.oid = x.conrelid JOIN pg_namespace n ON n.oid = t.relnamespace
            JOIN pg_class r ON r.oid = x.confrelid JOIN pg_namespace rn ON rn.oid = r.relnamespace
            JOIN pg_attribute a ON a.attrelid = t.oid AND a.attnum = x.conkey[1]
            WHERE x.contype = 'f' AND n.nspname || '.' || t.relname IN ({TableList})
            """)).Order(StringComparer.Ordinal)];

        Assert.Equal(3, erd.Count(line => line.StartsWith("column|", StringComparison.Ordinal) && line.EndsWith(".id|uuid|not null", StringComparison.Ordinal)));
        Assert.Equal(erd, schema);
    }

    /// <summary>
    /// The ERD's note on <c>milestone_achievement</c>: one current ACCEPTED revision per milestone. And one open revision, DRAFT
    /// or SUBMITTED, per milestone, which the ERD does not list (milestone-achievement.md F-6).
    /// </summary>
    [Fact]
    public async Task AMilestoneHasOneAcceptedAndOneOpenRevisionAtMost()
    {
        Assert.Equal(
            [
                "ix_milestone_achievement_accepted_project_milestone_id|(project_milestone_id)|((status)::text = 'ACCEPTED'::text)",
                "ix_milestone_achievement_open_project_milestone_id|(project_milestone_id)|((status)::text = ANY ((ARRAY['DRAFT'::character varying, 'SUBMITTED'::character varying])::text[]))",
            ],
            await database.QueryAsync("""
                SELECT c.relname || '|' || substring(pg_get_indexdef(i.indexrelid) FROM '\(project_milestone_id\)') || '|' || pg_get_expr(i.indpred, i.indrelid)
                FROM pg_index i JOIN pg_class c ON c.oid = i.indexrelid JOIN pg_class t ON t.oid = i.indrelid JOIN pg_namespace n ON n.oid = t.relnamespace
                WHERE n.nspname = 'milestone' AND t.relname = 'milestone_achievement' AND i.indisunique AND i.indpred IS NOT NULL
                ORDER BY c.relname
                """));
    }

    /// <summary>Never a duplicated milestone object: WF-05's schema has one table, and its only milestone column is the key to WF-03's row.</summary>
    [Fact]
    public async Task TheMilestoneSchemaHoldsNoMilestoneOfItsOwn()
    {
        Assert.Equal(["milestone_achievement"], await database.QueryAsync(
            "SELECT c.relname FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace WHERE n.nspname = 'milestone' AND c.relkind = 'r' ORDER BY c.relname"));
        Assert.Empty(await database.QueryAsync("""
            SELECT a.attname FROM pg_attribute a JOIN pg_class c ON c.oid = a.attrelid JOIN pg_namespace n ON n.oid = c.relnamespace
            WHERE n.nspname = 'milestone' AND c.relname = 'milestone_achievement' AND a.attnum > 0
              AND a.attname IN ('title', 'title_lang', 'forecast_date', 'planned_date', 'milestone_category_item_id', 'schedule_activity_id', 'sort_order')
            """));
    }
}
