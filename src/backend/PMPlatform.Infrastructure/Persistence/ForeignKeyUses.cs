using Microsoft.EntityFrameworkCore;
using PMPlatform.Application.Common.Deletion;

namespace PMPlatform.Infrastructure.Persistence;

/// <summary>
/// The rows that reference a record, through every single-column foreign key to its table, read from the database's own constraints, so a reference
/// added later is found too. Table and column names come from pg_constraint, quoted by quote_ident; values are parameters.
/// </summary>
internal static class ForeignKeyUses
{
    public static async Task<IReadOnlyList<RecordUse>> ListAsync(PMPlatformDbContext context, string referencedTable, Guid id, CancellationToken cancellationToken)
    {
        List<RecordUse> uses = [];
        foreach (ReferenceColumn c in await ColumnsAsync(context, referencedTable, cancellationToken).ConfigureAwait(false))
        {
            // A closed project takes no change, so its rows are a use of their own.
            string sql = c.Record == "project.project"
                ? $"SELECT count(*) FILTER (WHERE lifecycle_state <> 'CLOSED')::int AS open, count(*) FILTER (WHERE lifecycle_state = 'CLOSED')::int AS closed FROM {c.Quoted} WHERE {c.QuotedColumn} = @id"
                : $"SELECT count(*)::int AS open, 0 AS closed FROM {c.Quoted} WHERE {c.QuotedColumn} = @id";
#pragma warning disable EF1002 // Catalog identifiers, quoted; the id is a parameter.
            UseCount count = await context.Database.SqlQueryRaw<UseCount>(sql, new Npgsql.NpgsqlParameter("id", id)).SingleAsync(cancellationToken).ConfigureAwait(false);
#pragma warning restore EF1002
            if (count.Open > 0)
            {
                uses.Add(new RecordUse(c.Record, c.Column, c.Required, count.Open));
            }

            if (count.Closed > 0)
            {
                uses.Add(new RecordUse(c.Record + ".closed", c.Column, c.Required, count.Closed));
            }
        }

        return uses;
    }

    /// <summary>Sets the reference to null in each of <paramref name="uses"/>, stamping who and when where the table keeps audit columns; how many rows changed.</summary>
    public static async Task<int> ClearAsync(
        PMPlatformDbContext context, string referencedTable, Guid id, IReadOnlyList<RecordUse> uses, Guid actorId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        List<ReferenceColumn> columns = await ColumnsAsync(context, referencedTable, cancellationToken).ConfigureAwait(false);
        int cleared = 0;
        foreach (RecordUse use in uses)
        {
            ReferenceColumn c = columns.Single(x => x.Record == use.Record && x.Column == use.Field);
            string stamp = c.Audited ? ", updated_at = @now, updated_by = @actor" : string.Empty;
#pragma warning disable EF1002 // As above.
            cleared += await context.Database.ExecuteSqlRawAsync(
                $"UPDATE {c.Quoted} SET {c.QuotedColumn} = NULL{stamp} WHERE {c.QuotedColumn} = @id",
                [new Npgsql.NpgsqlParameter("id", id), new Npgsql.NpgsqlParameter("now", now), new Npgsql.NpgsqlParameter("actor", actorId)],
                cancellationToken).ConfigureAwait(false);
#pragma warning restore EF1002
        }

        return cleared;
    }

    private static async Task<List<ReferenceColumn>> ColumnsAsync(PMPlatformDbContext context, string referencedTable, CancellationToken cancellationToken) =>
        await context.Database.SqlQuery<ReferenceColumn>($"""
            SELECT n.nspname || '.' || t.relname AS record, a.attname AS "column",
                   quote_ident(n.nspname) || '.' || quote_ident(t.relname) AS quoted, quote_ident(a.attname) AS quoted_column,
                   a.attnotnull AS required,
                   EXISTS (SELECT 1 FROM pg_attribute u WHERE u.attrelid = t.oid AND u.attname = 'updated_by' AND NOT u.attisdropped) AS audited
            FROM pg_constraint c
            JOIN pg_class t ON t.oid = c.conrelid JOIN pg_namespace n ON n.oid = t.relnamespace
            JOIN pg_attribute a ON a.attrelid = c.conrelid AND a.attnum = c.conkey[1]
            WHERE c.contype = 'f' AND c.confrelid = to_regclass({referencedTable}) AND array_length(c.conkey, 1) = 1
            ORDER BY 1, 2
            """).ToListAsync(cancellationToken).ConfigureAwait(false);

    private sealed record ReferenceColumn(string Record, string Column, string Quoted, string QuotedColumn, bool Required, bool Audited);

    private sealed record UseCount(int Open, int Closed);
}
