using System.Data.Common;
using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;
using PMPlatform.Application.Common.Auditing;
using PMPlatform.Application.Features.AuditActivity;
using PMPlatform.Domain.Common;

namespace PMPlatform.Infrastructure.Persistence;

/// <summary>
/// <c>reset-demo-data [--dry-run] [--include-unmarked]</c> (the 5 Nov 2026 review §5): removes the projects whose <c>seed_source</c> is DEMO and
/// everything attached to them, so a real import starts from an empty register. It can be run again on any environment and touches no real
/// project. <c>--include-unmarked</c> also removes the projects testers entered by hand on a test environment, and is refused in Production.
/// <para>
/// The child tables refuse a DELETE by design (they are history), so the run sets <c>session_replication_role = replica</c> for one transaction:
/// the role needs that right, or the run fails and changes nothing. The same setting turns foreign keys off, so the run follows them itself: it
/// removes rows that have lost their parent, table by table, until none is left. The trail is kept, unconstrained, and gains one entry with the
/// counts per table.
/// </para>
/// </summary>
public static partial class DemoDataReset
{
    public const string Command = "reset-demo-data";

    public const string DryRunOption = "--dry-run";

    public const string IncludeUnmarkedOption = "--include-unmarked";

    /// <summary>The event of the entry the run leaves in the trail, reading "حُذفت بيانات العرض التجريبي".</summary>
    public const string RemovedEvent = "Administration.DemoDataRemoved";

    private const string ProjectTable = "project.project";

    private const string ForeignKeysSql = """
        SELECT cn.nspname || '.' || c.relname AS child, ca.attname AS child_column,
               pn.nspname || '.' || p.relname AS parent, pa.attname AS parent_column, cardinality(k.conkey) AS columns
        FROM pg_constraint k
        JOIN pg_class c ON c.oid = k.conrelid JOIN pg_namespace cn ON cn.oid = c.relnamespace
        JOIN pg_class p ON p.oid = k.confrelid JOIN pg_namespace pn ON pn.oid = p.relnamespace
        JOIN pg_attribute ca ON ca.attrelid = k.conrelid AND ca.attnum = k.conkey[1]
        JOIN pg_attribute pa ON pa.attrelid = k.confrelid AND pa.attnum = k.confkey[1]
        WHERE k.contype = 'f'
        """;

    public static bool IsCommand(string[] args) => args is [Command, ..];

    /// <summary>Runs the command and returns the process exit code.</summary>
    public static async Task<int> RunAsync(this IServiceProvider services, string[] args, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(args);
        bool dryRun = args.Contains(DryRunOption, StringComparer.Ordinal);
        bool includeUnmarked = args.Contains(IncludeUnmarkedOption, StringComparer.Ordinal);

        await using AsyncServiceScope scope = services.CreateAsyncScope();
        ILogger logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(DemoDataReset));
        if (includeUnmarked && scope.ServiceProvider.GetRequiredService<IHostEnvironment>().IsProduction())
        {
            LogRefused(logger, IncludeUnmarkedOption);
            return 1;
        }

        PMPlatformDbContext context = scope.ServiceProvider.GetRequiredService<PMPlatformDbContext>();
        var connection = (NpgsqlConnection)context.Database.GetDbConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using DbTransaction transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var npgsqlTransaction = (NpgsqlTransaction)transaction;

        await ExecuteAsync(connection, npgsqlTransaction, "SET LOCAL session_replication_role = replica", cancellationToken).ConfigureAwait(false);
        await ExecuteAsync(connection, npgsqlTransaction, $"""
            CREATE TEMPORARY TABLE removed_project ON COMMIT DROP AS
            SELECT id FROM {ProjectTable} {(includeUnmarked ? string.Empty : "WHERE seed_source = 'DEMO'")}
            """, cancellationToken).ConfigureAwait(false);

        // A role assignment anchored on a removed project keeps the person's role (review §5 keeps users and their assignments), without the anchor.
        await ExecuteAsync(connection, npgsqlTransaction, "UPDATE identity_access.access_relationship a SET project_id = NULL FROM removed_project r WHERE a.project_id = r.id", cancellationToken).ConfigureAwait(false);

        SortedDictionary<string, long> counts = new(StringComparer.Ordinal);
        await RemoveOutsideForeignKeysAsync(connection, npgsqlTransaction, counts, cancellationToken).ConfigureAwait(false);
        long projects = await DeleteAsync(connection, npgsqlTransaction, $"DELETE FROM {ProjectTable} p USING removed_project r WHERE p.id = r.id", cancellationToken).ConfigureAwait(false);
        Add(counts, ProjectTable, projects);
        await RemoveWorkBuiltOnThemAsync(connection, npgsqlTransaction, counts, cancellationToken).ConfigureAwait(false);
        await FollowForeignKeysAsync(connection, npgsqlTransaction, counts, cancellationToken).ConfigureAwait(false);

        string report = JsonSerializer.Serialize(counts);
        if (dryRun)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            LogReport(logger, "dry run, nothing changed", report);
            return 0;
        }

        // The trail's own transaction, on its own scope, before this one commits: a run that cannot be audited removes nothing.
        await using AsyncServiceScope auditScope = services.CreateAsyncScope();
        await auditScope.ServiceProvider.GetRequiredService<IAuditTrail>().RecordAsync(new AuditEntry(AuditEventClass.PrivilegedAction, RemovedEvent, AuditOutcome.Success)
        {
            ActorUserId = AuditCapturePrincipal.Id,
            ActorType = AuditActorType.Service,
            Attributes =
            [
                AuditAttribute.Of("summary", "حُذفت بيانات العرض التجريبي"),
                AuditAttribute.Of("include_unmarked", includeUnmarked),
                .. counts.Select(c => AuditAttribute.Of(c.Key.Replace('.', '_'), c.Value)),
            ],
        }).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        LogReport(logger, "removed", report);
        return 0;
    }

    /// <summary>The records that name a project without a foreign key: they go first, while the removed projects' ids are known.</summary>
    private static async Task RemoveOutsideForeignKeysAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, SortedDictionary<string, long> counts, CancellationToken cancellationToken)
    {
        foreach ((string table, string sql) in new[]
        {
            ("approval.approval_instance", "DELETE FROM approval.approval_instance a USING removed_project r WHERE a.subject_id = r.id"),
            ("notifications.notification_intent", "DELETE FROM notifications.notification_intent n USING removed_project r WHERE n.subject_id = r.id"),
            ("common.imported_record", "DELETE FROM common.imported_record i USING removed_project r WHERE i.entity_id = r.id"),
            ("common.outbox_message", "DELETE FROM common.outbox_message m USING removed_project r WHERE m.dispatched_at IS NULL AND m.payload::text LIKE '%' || r.id::text || '%'"),
        })
        {
            Add(counts, table, await DeleteAsync(connection, transaction, sql, cancellationToken).ConfigureAwait(false));
        }
    }

    /// <summary>Removes rows whose parent is gone, along the catalogue's foreign keys, from the tables that lost rows, until no table loses any.</summary>
    private static async Task FollowForeignKeysAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, SortedDictionary<string, long> counts, CancellationToken cancellationToken)
    {
        List<(string Child, string ChildColumn, string Parent, string ParentColumn, int Columns)> keys = [];
        await using (NpgsqlCommand read = new(ForeignKeysSql, connection, transaction))
        await using (NpgsqlDataReader reader = await read.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
        {
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                keys.Add((reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetInt32(4)));
            }
        }

        HashSet<string> lostRows = [.. counts.Where(c => c.Value > 0).Select(c => c.Key)];
        while (lostRows.Count > 0)
        {
            HashSet<string> next = new(StringComparer.Ordinal);
            foreach ((string child, string childColumn, string parent, string parentColumn, int columns) in keys.Where(k => lostRows.Contains(k.Parent)))
            {
                if (columns != 1)
                {
                    throw new InvalidOperationException($"{child} refers to {parent} by {columns} columns; the reset follows single-column keys only.");
                }

                long removed = await DeleteAsync(connection, transaction, $"""
                    DELETE FROM {child} c WHERE c."{childColumn}" IS NOT NULL
                    AND NOT EXISTS (SELECT 1 FROM {parent} p WHERE p."{parentColumn}" = c."{childColumn}")
                    """, cancellationToken).ConfigureAwait(false);
                if (removed > 0)
                {
                    Add(counts, child, removed);
                    next.Add(child);
                }
            }

            lostRows = next;
        }
    }

    /// <summary>
    /// Saved views and report runs hold no project key, so which were built on the removed data cannot be told apart row by row: they go only when the
    /// register is left empty, when every one of them was.
    /// </summary>
    private static async Task RemoveWorkBuiltOnThemAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, SortedDictionary<string, long> counts, CancellationToken cancellationToken)
    {
        await using NpgsqlCommand remaining = new($"SELECT EXISTS (SELECT 1 FROM {ProjectTable})", connection, transaction);
        if ((bool)(await remaining.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false))!)
        {
            return;
        }

        foreach (string table in new[] { "reports.saved_view_share", "reports.saved_view_filter", "reports.saved_view_column", "reports.report_job", "reports.saved_view" })
        {
            Add(counts, table, await DeleteAsync(connection, transaction, $"DELETE FROM {table}", cancellationToken).ConfigureAwait(false));
        }
    }

    private static void Add(SortedDictionary<string, long> counts, string table, long removed)
    {
        if (removed > 0)
        {
            counts[table] = counts.GetValueOrDefault(table) + removed;
        }
    }

    private static async Task ExecuteAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, string sql, CancellationToken cancellationToken)
    {
        await using NpgsqlCommand command = new(sql, connection, transaction);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task<long> DeleteAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, string sql, CancellationToken cancellationToken)
    {
        await using NpgsqlCommand command = new(sql, connection, transaction);
        return Convert.ToInt64(await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false), CultureInfo.InvariantCulture);
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "reset-demo-data refused: {Option} is not allowed in Production")]
    private static partial void LogRefused(ILogger logger, string option);

    [LoggerMessage(Level = LogLevel.Information, Message = "reset-demo-data {Outcome}: {Counts}")]
    private static partial void LogReport(ILogger logger, string outcome, string counts);
}
