using Npgsql;
using PMPlatform.Tests.Integration.AuditActivity;
using static PMPlatform.Tests.Integration.Persistence.MigratedDatabase;

namespace PMPlatform.Tests.Integration.Persistence;

/// <summary>
/// TASK-033's part of the formal audit store, against PostgreSQL with every migration applied: the three tables are the
/// ERD's, the database places every event in the hash chain, and a change to a stored event is detected.
/// </summary>
public sealed class AuditActivitySchemaTests(MigratedDatabase database) : IClassFixture<MigratedDatabase>
{
    /// <summary>ERD §5.22 without TASK-073's <c>business_activity_entry</c>.</summary>
    private static readonly string[] Tables = ["audit_activity.audit_event", "audit_activity.audit_event_attribute", "audit_activity.audit_forwarding_record"];

    /// <summary>The one ERD line not built: its target, <c>integration_monitoring.invocation</c>, does not exist yet (record F-3).</summary>
    private const string DeferredForeignKey = "fk|audit_activity.audit_forwarding_record.invocation_id|integration_monitoring.invocation";

    [Fact]
    public async Task TheAuditTablesAreTheCanonicalErd()
    {
        IReadOnlyList<string> erd = [.. ErdModel.Lines(["audit_activity"])
            .Where(line => Tables.Any(table => line.Contains($"|{table}.", StringComparison.Ordinal) || line.Contains($"|{table}|", StringComparison.Ordinal)))
            .Where(line => line != DeferredForeignKey)];
        IReadOnlyList<string> schema = [.. (await database.QueryAsync("""
            SELECT 'column|' || n.nspname || '.' || c.relname || '.' || a.attname || '|' || format_type(a.atttypid, a.atttypmod)
                   || '|' || CASE WHEN a.attnotnull THEN 'not null' ELSE 'null' END
            FROM pg_attribute a JOIN pg_class c ON c.oid = a.attrelid JOIN pg_namespace n ON n.oid = c.relnamespace
            WHERE c.relkind = 'r' AND n.nspname = 'audit_activity' AND a.attnum > 0 AND NOT a.attisdropped
            UNION ALL
            SELECT 'unique|' || n.nspname || '.' || t.relname || '|' || string_agg(a.attname, ',' ORDER BY k.ord)
            FROM pg_index i
            JOIN pg_class t ON t.oid = i.indrelid JOIN pg_namespace n ON n.oid = t.relnamespace
            CROSS JOIN LATERAL unnest(i.indkey) WITH ORDINALITY AS k(attnum, ord)
            JOIN pg_attribute a ON a.attrelid = t.oid AND a.attnum = k.attnum
            WHERE i.indisunique AND NOT i.indisprimary AND n.nspname = 'audit_activity'
            GROUP BY i.indexrelid, n.nspname, t.relname
            UNION ALL
            SELECT 'fk|' || n.nspname || '.' || t.relname || '.' || a.attname || '|' || rn.nspname || '.' || r.relname
            FROM pg_constraint x
            JOIN pg_class t ON t.oid = x.conrelid JOIN pg_namespace n ON n.oid = t.relnamespace
            JOIN pg_class r ON r.oid = x.confrelid JOIN pg_namespace rn ON rn.oid = r.relnamespace
            JOIN pg_attribute a ON a.attrelid = t.oid AND a.attnum = x.conkey[1]
            WHERE x.contype = 'f' AND n.nspname = 'audit_activity'
            """)).Order(StringComparer.Ordinal)];

        Assert.Equal(3, erd.Count(line => line.StartsWith("column|", StringComparison.Ordinal) && line.Contains(".id|", StringComparison.Ordinal)));
        Assert.Equal([.. erd.Order(StringComparer.Ordinal)], schema);
    }

    /// <summary>Whatever the writer supplies, the database sets the chain columns: a writer cannot choose its place or its hash.</summary>
    [Fact]
    public async Task TheDatabaseChainsEveryEvent()
    {
        IReadOnlyList<string> chain = await database.QueryRolledBackAsync(
            string.Join(';', Enumerable.Range(1, 3).Select(n => EventRow(n, eventHash: "'" + new string('0', 64) + "'", previousEventHash: "'" + new string('f', 64) + "'"))),
            $"""
            SELECT concat_ws('|', e.event_type,
                             coalesce(e.previous_event_hash = lag(e.event_hash) OVER w, e.previous_event_hash IS NULL)::text,
                             (e.event_hash = audit_activity.audit_event_hash(e))::text,
                             (e.recorded_at > lag(e.recorded_at) OVER w)::text)
            FROM audit_activity.audit_event e WINDOW w AS (ORDER BY e.recorded_at, e.id) ORDER BY e.recorded_at
            """);

        Assert.Equal(["Test.Event1|true|true", "Test.Event2|true|true|true", "Test.Event3|true|true|true"], chain);
    }

    /// <summary>
    /// Tamper evidence: the owner of the table can switch its protection off, so changing a stored event must at least be
    /// detected. Changing one field of the middle event breaks exactly that event's hash; deleting it breaks the next link.
    /// </summary>
    [Theory]
    [InlineData("UPDATE audit_activity.audit_event SET outcome = 'SUCCESS' WHERE event_type = 'Test.Event2'", "Test.Event2")]
    [InlineData("DELETE FROM audit_activity.audit_event WHERE event_type = 'Test.Event2'", "Test.Event3")]
    public async Task AChangedOrDeletedEventIsDetected(string tampering, string detectedAt)
    {
        string insert = string.Join(';', Enumerable.Range(1, 3).Select(n => EventRow(n)));

        IReadOnlyList<string> intact = await database.QueryRolledBackAsync(insert, $"SELECT e.event_type FROM ({AuditStore.BrokenChainLinks}) b JOIN audit_activity.audit_event e ON e.id::text = b.id");
        IReadOnlyList<string> broken = await database.QueryRolledBackAsync(
            $"{insert}; ALTER TABLE audit_activity.audit_event DISABLE TRIGGER append_only; {tampering}",
            $"SELECT e.event_type FROM ({AuditStore.BrokenChainLinks}) b JOIN audit_activity.audit_event e ON e.id::text = b.id");

        Assert.Empty(intact);
        Assert.Equal([detectedAt], broken);
    }

    [Fact]
    public async Task AStoredEventCannotBeChanged()
    {
        PostgresException refused = await Assert.ThrowsAsync<PostgresException>(() => database.ExecuteRolledBackAsync(
            $"{EventRow(1)}; UPDATE audit_activity.audit_event SET outcome = 'SUCCESS'"));

        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, refused.SqlState);
    }

    /// <summary>A forwarding record is state, not audit: the forwarder moves it on (RETAIN, not APPEND_ONLY).</summary>
    [Fact]
    public async Task AForwardingRecordCanBeUpdated()
    {
        IReadOnlyList<string> status = await database.QueryRolledBackAsync(
            $"""
            {EventRow(1)};
            INSERT INTO audit_activity.audit_forwarding_record (id, audit_event_id, status, created_at, created_by, updated_at, updated_by)
            VALUES ('00000000-0330-4000-8000-000000000001', '{EventId(1)}', 'PENDING', now(), '{ServiceUserId}', now(), '{ServiceUserId}');
            UPDATE audit_activity.audit_forwarding_record SET status = 'FORWARDED', forwarded_at = now()
            """,
            "SELECT status FROM audit_activity.audit_forwarding_record");

        Assert.Equal(["FORWARDED"], status);
    }

    private static string EventId(int n) => $"00000000-0331-4000-8000-{n:D12}";

    private static string EventRow(int n, string eventHash = "NULL", string previousEventHash = "NULL") => $"""
        INSERT INTO audit_activity.audit_event (id, occurred_at, recorded_at, event_class, event_type, actor_type, correlation_id, outcome,
                                                previous_event_hash, event_hash, created_at, created_by, updated_at, updated_by)
        VALUES ('{EventId(n)}', now(), '2000-01-01', 'AUTHENTICATION', 'Test.Event{n}', 'USER', gen_random_uuid(), 'FAILED',
                {previousEventHash}, {eventHash}, now(), '{ServiceUserId}', now(), '{ServiceUserId}')
        """;
}
