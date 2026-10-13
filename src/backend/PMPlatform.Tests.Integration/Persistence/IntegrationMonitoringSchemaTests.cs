using Npgsql;

namespace PMPlatform.Tests.Integration.Persistence;

/// <summary>
/// FG-05's schema against PostgreSQL with every migration applied and the seed loaded (TASK-075): its five tables are the ERD's, the registry
/// is seeded, and the database keeps the history — no row deleted, an ended attempt or run never rewritten (a retry is a new attempt), an
/// alert born OPEN and final once handled.
/// </summary>
public sealed class IntegrationMonitoringSchemaTests(SeededDatabase database) : IClassFixture<SeededDatabase>
{
    private const string Seed = "00000000-0000-4000-8000-0000000000ff";
    private const string DirectoryInstance = "(SELECT i.id FROM integration_monitoring.integration_instance i JOIN integration_monitoring.integration_definition d ON d.id = i.integration_definition_id WHERE d.code = 'DIRECTORY')";

    [Fact]
    public async Task TheIntegrationMonitoringTablesAreTheCanonicalErd()
    {
        IReadOnlyList<string> erd = ErdModel.Lines(["integration_monitoring"]);
        IReadOnlyList<string> schema = [.. (await database.QueryAsync("""
            SELECT 'column|' || n.nspname || '.' || c.relname || '.' || a.attname || '|' || format_type(a.atttypid, a.atttypmod)
                   || '|' || CASE WHEN a.attnotnull THEN 'not null' ELSE 'null' END
            FROM pg_attribute a JOIN pg_class c ON c.oid = a.attrelid JOIN pg_namespace n ON n.oid = c.relnamespace
            WHERE c.relkind = 'r' AND n.nspname = 'integration_monitoring' AND a.attnum > 0 AND NOT a.attisdropped
            UNION ALL
            SELECT 'unique|' || n.nspname || '.' || t.relname || '|' || string_agg(a.attname, ',' ORDER BY k.ord)
            FROM pg_index i
            JOIN pg_class t ON t.oid = i.indrelid JOIN pg_namespace n ON n.oid = t.relnamespace
            CROSS JOIN LATERAL unnest(i.indkey) WITH ORDINALITY AS k(attnum, ord)
            JOIN pg_attribute a ON a.attrelid = t.oid AND a.attnum = k.attnum
            WHERE i.indisunique AND NOT i.indisprimary AND i.indpred IS NULL AND n.nspname = 'integration_monitoring'
            GROUP BY i.indexrelid, n.nspname, t.relname
            UNION ALL
            SELECT 'fk|' || n.nspname || '.' || t.relname || '.' || a.attname || '|' || rn.nspname || '.' || r.relname
            FROM pg_constraint x
            JOIN pg_class t ON t.oid = x.conrelid JOIN pg_namespace n ON n.oid = t.relnamespace
            JOIN pg_class r ON r.oid = x.confrelid JOIN pg_namespace rn ON rn.oid = r.relnamespace
            JOIN pg_attribute a ON a.attrelid = t.oid AND a.attnum = x.conkey[1]
            WHERE x.contype = 'f' AND n.nspname = 'integration_monitoring'
            """)).Order(StringComparer.Ordinal)];

        Assert.Equal(5, erd.Count(line => line.StartsWith("column|", StringComparison.Ordinal) && line.EndsWith(".id|uuid|not null", StringComparison.Ordinal)));
        Assert.Equal([.. erd.Order(StringComparer.Ordinal)], schema);
    }

    /// <summary>The ERD's note: one live alert per instance and condition — a partial unique index the column lines cannot state.</summary>
    [Fact]
    public async Task OneAlertPerInstanceAndConditionIsLive()
    {
        Assert.Equal(
            ["operational_alert|(integration_instance_id, alert_type)|((status)::text = ANY ((ARRAY['OPEN'::character varying, 'ACKNOWLEDGED'::character varying])::text[]))"],
            await database.QueryAsync("""
                SELECT c.relname || '|' || substring(pg_get_indexdef(i.indexrelid) FROM '\(integration_instance_id, alert_type\)') || '|' || pg_get_expr(i.indpred, i.indrelid)
                FROM pg_index i JOIN pg_class c ON c.oid = i.indrelid JOIN pg_namespace n ON n.oid = c.relnamespace
                WHERE n.nspname = 'integration_monitoring' AND i.indisunique AND i.indpred IS NOT NULL
                """));
    }

    [Fact]
    public async Task TheRegistryIsSeededWithOneInstancePerIntegrationNamedByItsConfigurationKey()
    {
        Assert.Equal(
            [
                "DIRECTORY|INBOUND|IDENTITY|ACTIVE|3|60|PRIMARY|AD_LDAP_URL|UNKNOWN|UNKNOWN",
                "EXCHANGE_EMAIL|OUTBOUND|MESSAGING|ACTIVE|3|60|PRIMARY|EXCHANGE_SMTP_HOST|UNKNOWN|UNKNOWN",
                "MFA|OUTBOUND|IDENTITY|ACTIVE|3|60|PRIMARY|MFA_PROVIDER_ENDPOINT|UNKNOWN|UNKNOWN",
                "NAFATH|OUTBOUND|VERIFICATION|ACTIVE|3|60|PRIMARY|Identity:Nafath:Authority|UNKNOWN|UNKNOWN",
                "SMS|OUTBOUND|MESSAGING|ACTIVE|3|60|PRIMARY|SMS_PROVIDER_ENDPOINT|UNKNOWN|UNKNOWN",
                "SSO|INBOUND|IDENTITY|ACTIVE|3|60|PRIMARY|SSO_OIDC_AUTHORITY|UNKNOWN|UNKNOWN",
            ],
            await database.QueryAsync("""
                SELECT concat_ws('|', d.code, d.direction, d.integration_kind, d.status, d.max_attempts, d.retry_backoff_seconds, i.code, i.endpoint_configuration_key, i.availability, i.freshness)
                FROM integration_monitoring.integration_definition d JOIN integration_monitoring.integration_instance i ON i.integration_definition_id = d.id
                ORDER BY d.code COLLATE "C"
                """));
    }

    [Theory]
    [InlineData("DELETE FROM integration_monitoring.integration_definition WHERE code = 'SMS'")]
    [InlineData("DELETE FROM integration_monitoring.integration_instance")]
    [InlineData("DELETE FROM integration_monitoring.invocation")]
    [InlineData("DELETE FROM integration_monitoring.operational_alert")]
    [InlineData("UPDATE integration_monitoring.invocation SET status = 'SUCCEEDED', failure_category = NULL, next_attempt_at = NULL WHERE id = '00000000-0750-4000-8000-0000000000a1'")]
    [InlineData("UPDATE integration_monitoring.invocation SET error_summary = 'Rewritten' WHERE id = '00000000-0750-4000-8000-0000000000a1'")]
    [InlineData("UPDATE integration_monitoring.invocation SET correlation_id = gen_random_uuid() WHERE id = '00000000-0750-4000-8000-0000000000a2'")]
    [InlineData("UPDATE integration_monitoring.operational_alert SET detail = 'Rewritten' WHERE id = '00000000-0750-4000-8000-0000000000b1'")]
    [InlineData("UPDATE integration_monitoring.operational_alert SET status = 'OPEN' WHERE id = '00000000-0750-4000-8000-0000000000b2'")]
    public async Task TheDatabaseRefusesToRewriteOrDeleteIntegrationHistory(string statement)
    {
        PostgresException refused = await Assert.ThrowsAsync<PostgresException>(() => database.ExecuteRolledBackAsync(History + statement));
        Assert.Equal(PostgresErrorCodes.RestrictViolation, refused.SqlState);
    }

    /// <summary>The one change to an ended attempt: once its lineage's next attempt exists, a pending retry is plainly FAILED.</summary>
    [Fact]
    public async Task APendingRetryBecomesFailedAndAnInProgressAttemptEndsOnce()
    {
        Assert.Equal(
            ["a1|FAILED", "a2|SUCCEEDED"],
            await database.QueryRolledBackAsync(
                History + """
                    UPDATE integration_monitoring.invocation SET status = 'FAILED', next_attempt_at = NULL WHERE id = '00000000-0750-4000-8000-0000000000a1';
                    UPDATE integration_monitoring.invocation SET status = 'SUCCEEDED', completed_at = now() WHERE id = '00000000-0750-4000-8000-0000000000a2';
                    """,
                "SELECT right(id::text, 2) || '|' || status FROM integration_monitoring.invocation ORDER BY id"));
    }

    [Fact]
    public async Task AnAlertIsBornOpen()
    {
        PostgresException refused = await Assert.ThrowsAsync<PostgresException>(() => database.ExecuteRolledBackAsync($"""
            INSERT INTO integration_monitoring.operational_alert (id, integration_instance_id, alert_type, severity, raised_at, status, resolved_at, resolution_note, resolution_note_lang,
                                                                  created_at, created_by, updated_at, updated_by)
            VALUES (gen_random_uuid(), {DirectoryInstance}, 'AVAILABILITY', 'CRITICAL', now(), 'RESOLVED', now(), 'Born resolved', 'en', now(), '{Seed}', now(), '{Seed}')
            """));
        Assert.Equal("ck_operational_alert_born_open", refused.ConstraintName);
    }

    /// <summary>
    /// A lineage's first attempt RETRY_SCHEDULED and its second IN_PROGRESS; an alert OPEN and one RESOLVED — against the seeded directory
    /// instance, in the transaction each test rolls back.
    /// </summary>
    private static string History => $"""
        INSERT INTO integration_monitoring.invocation (id, integration_instance_id, operation_code, correlation_id, idempotency_key, attempt_no, source_module, started_at,
                                                       completed_at, status, failure_category, next_attempt_at, created_at, created_by, updated_at, updated_by)
        VALUES ('00000000-0750-4000-8000-0000000000a1', {DirectoryInstance}, 'RECONCILE', '00000000-0750-4000-8000-0000000000c1', 'key', 1, 'IntegrationMonitoring', now(),
                now(), 'RETRY_SCHEDULED', 'TECHNICAL', now() + interval '1 minute', now(), '{Seed}', now(), '{Seed}'),
               ('00000000-0750-4000-8000-0000000000a2', {DirectoryInstance}, 'RECONCILE', '00000000-0750-4000-8000-0000000000c1', 'key', 2, 'IntegrationMonitoring', now(),
                NULL, 'IN_PROGRESS', NULL, NULL, now(), '{Seed}', now(), '{Seed}');
        INSERT INTO integration_monitoring.operational_alert (id, integration_instance_id, alert_type, severity, raised_at, status, detail, created_at, created_by, updated_at, updated_by)
        VALUES ('00000000-0750-4000-8000-0000000000b1', {DirectoryInstance}, 'AVAILABILITY', 'CRITICAL', now(), 'OPEN', 'down', now(), '{Seed}', now(), '{Seed}'),
               ('00000000-0750-4000-8000-0000000000b2', {DirectoryInstance}, 'DEAD_LETTER', 'WARNING', now(), 'OPEN', 'dead', now(), '{Seed}', now(), '{Seed}');
        UPDATE integration_monitoring.operational_alert SET status = 'RESOLVED', resolved_at = now(), resolution_note = 'Handled', resolution_note_lang = 'en'
        WHERE id = '00000000-0750-4000-8000-0000000000b2';
        """;
}
