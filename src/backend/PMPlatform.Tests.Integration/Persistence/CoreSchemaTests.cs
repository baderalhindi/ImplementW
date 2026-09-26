using System.Text.Json;
using Npgsql;
using Xunit.Abstractions;
using static PMPlatform.Tests.Integration.Persistence.MigratedDatabase;

namespace PMPlatform.Tests.Integration.Persistence;

/// <summary>
/// TASK-025 acceptance, executed against PostgreSQL with every migration applied: the core schema is the canonical
/// ERD's, every foreign key has an index, every table has the audit columns, the Formal Project ID is unique, the
/// ERD's rules are enforced by the database, and a Project-to-owner join is served by an index.
/// </summary>
public sealed class CoreSchemaTests(MigratedDatabase database, ITestOutputHelper output) : IClassFixture<MigratedDatabase>
{
    /// <summary>The three schemas TASK-025 builds (ERD §11): 9 + 22 + 3 = 34 tables.</summary>
    private static readonly string[] CoreSchemas = ["identity_access", "master_data_config", "project"];

    private static readonly string CoreSchemaList = string.Join(", ", CoreSchemas.Select(s => $"'{s}'"));

    /// <summary>The normalisation review is documented against the ERD, so the schema must be the ERD: same tables, columns, types, nullability, unique keys and foreign keys.</summary>
    [Fact]
    public async Task TheCoreSchemaIsTheCanonicalErd()
    {
        IReadOnlyList<string> erd = ErdModel.Lines(CoreSchemas);
        IReadOnlyList<string> schema = [.. (await database.QueryAsync($"""
            SELECT 'column|' || n.nspname || '.' || c.relname || '.' || a.attname || '|' || format_type(a.atttypid, a.atttypmod)
                   || '|' || CASE WHEN a.attnotnull THEN 'not null' ELSE 'null' END
            FROM pg_attribute a JOIN pg_class c ON c.oid = a.attrelid JOIN pg_namespace n ON n.oid = c.relnamespace
            WHERE c.relkind = 'r' AND n.nspname IN ({CoreSchemaList}) AND a.attnum > 0 AND NOT a.attisdropped
            UNION ALL
            SELECT 'unique|' || n.nspname || '.' || t.relname || '|' || string_agg(a.attname, ',' ORDER BY k.ord)
            FROM pg_index i
            JOIN pg_class t ON t.oid = i.indrelid JOIN pg_namespace n ON n.oid = t.relnamespace
            CROSS JOIN LATERAL unnest(i.indkey) WITH ORDINALITY AS k(attnum, ord)
            JOIN pg_attribute a ON a.attrelid = t.oid AND a.attnum = k.attnum
            WHERE i.indisunique AND NOT i.indisprimary AND n.nspname IN ({CoreSchemaList})
            GROUP BY i.indexrelid, n.nspname, t.relname
            UNION ALL
            SELECT 'fk|' || n.nspname || '.' || t.relname || '.' || a.attname || '|' || rn.nspname || '.' || r.relname
            FROM pg_constraint x
            JOIN pg_class t ON t.oid = x.conrelid JOIN pg_namespace n ON n.oid = t.relnamespace
            JOIN pg_class r ON r.oid = x.confrelid JOIN pg_namespace rn ON rn.oid = r.relnamespace
            JOIN pg_attribute a ON a.attrelid = t.oid AND a.attnum = x.conkey[1]
            WHERE x.contype = 'f' AND n.nspname IN ({CoreSchemaList})
            """)).Order(StringComparer.Ordinal)];

        Assert.Equal(34, erd.Count(line => line.StartsWith("column|", StringComparison.Ordinal) && line.Contains(".id|", StringComparison.Ordinal)));
        Assert.Equal(erd, schema);
    }

    /// <summary>Every foreign key in the database is the leading column set of some index, so a join or a parent delete never scans the child.</summary>
    [Fact]
    public async Task EveryForeignKeyHasAnIndex()
    {
        IReadOnlyList<string> unindexed = await database.QueryAsync("""
            SELECT x.conrelid::regclass::text || ' ' || x.conname
            FROM pg_constraint x
            WHERE x.contype = 'f'
              AND NOT EXISTS (
                  SELECT 1 FROM pg_index i
                  WHERE i.indrelid = x.conrelid
                    AND (string_to_array(i.indkey::text, ' ')::int2[])[1:cardinality(x.conkey)] = x.conkey)
            """);
        int foreignKeys = int.Parse((await database.QueryAsync("SELECT count(*)::text FROM pg_constraint WHERE contype = 'f'"))[0], System.Globalization.CultureInfo.InvariantCulture);

        Assert.True(foreignKeys > 0);
        Assert.Empty(unindexed);
    }

    /// <summary>ERD D-2: created_at, created_by, updated_at, updated_by, all not null, on every table in every module schema.</summary>
    [Fact]
    public async Task EveryTableHasTheAuditColumns()
    {
        IReadOnlyList<string> missing = await database.QueryAsync("""
            SELECT n.nspname || '.' || c.relname || ' ' || required.name
            FROM pg_class c
            JOIN pg_namespace n ON n.oid = c.relnamespace
            CROSS JOIN (VALUES ('created_at', 'timestamp with time zone'), ('created_by', 'uuid'),
                               ('updated_at', 'timestamp with time zone'), ('updated_by', 'uuid')) AS required(name, type)
            WHERE c.relkind = 'r' AND n.nspname NOT IN ('common', 'pg_catalog', 'information_schema') AND n.nspname NOT LIKE 'pg\_%'
              AND NOT EXISTS (
                  SELECT 1 FROM pg_attribute a
                  WHERE a.attrelid = c.oid AND a.attname = required.name AND a.attnotnull
                    AND format_type(a.atttypid, a.atttypmod) = required.type)
            """);
        IReadOnlyList<string> tables = await database.QueryAsync(
            $"SELECT c.relname::text FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace WHERE c.relkind = 'r' AND n.nspname IN ({CoreSchemaList})");

        Assert.Equal(34, tables.Count);
        Assert.Empty(missing);
    }

    [Fact]
    public async Task TheFormalProjectIdIsUnique()
    {
        IReadOnlyList<string> index = await database.QueryAsync("""
            SELECT pg_get_indexdef(i.indexrelid) FROM pg_index i
            WHERE i.indrelid = 'project.project'::regclass AND i.indisunique
              AND i.indkey::text = (SELECT attnum::text FROM pg_attribute WHERE attrelid = 'project.project'::regclass AND attname = 'formal_project_id')
            """);

        Assert.Single(index);

        PostgresException duplicate = await Assert.ThrowsAsync<PostgresException>(() => database.ExecuteRolledBackAsync(
            ProjectRow(formalProjectId: "'AHDA-2026-0001'", lifecycleState: "APPROVED_PLANNED") + ";" +
            ProjectRow(formalProjectId: "'AHDA-2026-0001'", lifecycleState: "APPROVED_PLANNED")));
        Assert.Equal(PostgresErrorCodes.UniqueViolation, duplicate.SqlState);
    }

    /// <summary>The baseline row every rule below changes one thing of: it must be accepted, or the rejections prove nothing.</summary>
    [Fact]
    public async Task AValidDraftProjectIsAccepted() =>
        await database.ExecuteRolledBackAsync(ProjectRow());

    [Theory]
    [MemberData(nameof(RuleViolations))]
    public async Task TheDatabaseRejectsRowsThatBreakAnErdRule(string rule, string sql, string sqlState)
    {
        PostgresException rejected = await Assert.ThrowsAsync<PostgresException>(() => database.ExecuteRolledBackAsync(sql));

        Assert.True(rejected.SqlState == sqlState, $"{rule}: expected SQLSTATE {sqlState}, got {rejected.SqlState} ({rejected.MessageText})");
    }

    public static TheoryData<string, string, string> RuleViolations() => new()
    {
        { "Unknown lifecycle state", ProjectRow(lifecycleState: "CANCELLED"), PostgresErrorCodes.CheckViolation },
        { "D-7 language tag outside ar/en", ProjectRow(titleLanguage: "fr"), PostgresErrorCodes.CheckViolation },
        { "Formal Project ID missing once approved", ProjectRow(lifecycleState: "APPROVED_PLANNED"), PostgresErrorCodes.CheckViolation },
        {
            "ADR-011 impact level outside 1-5",
            $"""
            INSERT INTO master_data_config.impact_level_definition (id, configuration_version_id, impact_dimension_item_id, level, label_ar, label_en, created_at, created_by, updated_at, updated_by)
            VALUES (gen_random_uuid(), '{ConfigurationVersionId}', '{MasterDataItemId}', 6, 'حرج جداً', 'Beyond critical', now(), '{ServiceUserId}', now(), '{ServiceUserId}')
            """,
            PostgresErrorCodes.CheckViolation
        },
        {
            "ADR-011 matrix cell outside 5x5",
            $"""
            INSERT INTO master_data_config.risk_rating_definition (id, configuration_version_id, code, label_ar, label_en, sort_order, created_at, created_by, updated_at, updated_by)
            VALUES ('00000000-0042-4000-8000-000000000001', '{ConfigurationVersionId}', 'HIGH', 'مرتفع', 'High', 1, now(), '{ServiceUserId}', now(), '{ServiceUserId}');
            INSERT INTO master_data_config.risk_matrix_cell (id, configuration_version_id, probability_level, impact_level, risk_rating_definition_id, created_at, created_by, updated_at, updated_by)
            VALUES (gen_random_uuid(), '{ConfigurationVersionId}', 0, 3, '00000000-0042-4000-8000-000000000001', now(), '{ServiceUserId}', now(), '{ServiceUserId}')
            """,
            PostgresErrorCodes.CheckViolation
        },
        {
            "D-6 bilingual pair with one language missing",
            $"""
            INSERT INTO master_data_config.master_data_item (id, catalogue_id, code, label_ar, label_en, description_ar, lifecycle_state, created_at, created_by, updated_at, updated_by)
            VALUES (gen_random_uuid(), '00000000-0030-4000-8000-000000000001', 'HALF_PAIR', 'عنصر', 'Item', 'وصف بالعربية فقط', 'DRAFT', now(), '{ServiceUserId}', now(), '{ServiceUserId}')
            """,
            PostgresErrorCodes.CheckViolation
        },
        {
            "EXTERNAL user without an external entity",
            $"""
            INSERT INTO identity_access."user" (id, user_type, username, display_name, email, status, created_at, created_by, updated_at, updated_by)
            VALUES (gen_random_uuid(), 'EXTERNAL', 'ext.orphan', 'External without entity', 'ext.orphan@pmplatform.test', 'ACTIVE', now(), '{ServiceUserId}', now(), '{ServiceUserId}')
            """,
            PostgresErrorCodes.CheckViolation
        },
        {
            "D-2 APPEND_ONLY row updated",
            ProjectRow(id: "'00000000-0050-4000-8000-000000000009'") + $"""
            ;
            INSERT INTO project.project_intake (id, project_id, intake_date, declared_scope, declared_scope_lang, declared_budget_sar, declared_end_date, opening_percent_complete, opening_spend_to_date_sar, recorded_by_user_id, created_at, created_by, updated_at, updated_by)
            VALUES ('00000000-0051-4000-8000-000000000001', '00000000-0050-4000-8000-000000000009', current_date, 'نطاق معلن', 'ar', 1000000.00, current_date + 365, 40.0000, 400000.00, '{ServiceUserId}',
                    '2026-09-25T00:00:00Z', '{ServiceUserId}', '2026-09-25T00:00:00Z', '{ServiceUserId}');
            UPDATE project.project_intake SET declared_budget_sar = 2000000.00, updated_at = now() WHERE id = '00000000-0051-4000-8000-000000000001'
            """,
            PostgresErrorCodes.CheckViolation
        },
        {
            "Referenced department deleted",
            ProjectRow() + $"; DELETE FROM identity_access.department WHERE id = '{DepartmentId}'",
            PostgresErrorCodes.ForeignKeyViolation
        },
    };

    /// <summary>
    /// The workbook's validation cell: EXPLAIN ANALYZE a query joining Project to User by owner and confirm an index
    /// is used, not a sequential scan. Volume: 150 named users (the brief's capacity figure) managing 3,000 projects.
    /// The assertion is on the Project side, the side the owner foreign key serves: 150 users fit in two pages, and
    /// the planner rightly reads them with a sequential scan rather than the username index.
    /// </summary>
    [Fact]
    public async Task TheProjectToOwnerJoinUsesAnIndex()
    {
        await database.ExecuteAsync($"""
            INSERT INTO identity_access."user" (id, user_type, username, display_name, email, department_id, status, created_at, created_by, updated_at, updated_by)
            SELECT gen_random_uuid(), 'INTERNAL', 'owner.' || lpad(n::text, 3, '0'), 'Owner ' || n, 'owner.' || n || '@pmplatform.test', '{DepartmentId}', 'ACTIVE',
                   now(), '{ServiceUserId}', now(), '{ServiceUserId}'
            FROM generate_series(1, 150) AS n;

            INSERT INTO project.project (id, title, title_lang, classification_item_id, department_id, project_manager_user_id, lifecycle_state,
                                         governance_profile_item_id, participation_mode, created_at, created_by, updated_at, updated_by)
            SELECT gen_random_uuid(), 'Project ' || n, 'en', '{MasterDataItemId}', '{DepartmentId}', owner.id, 'SUBMITTED',
                   '{MasterDataItemId}', 'AHDA_MANAGED', now(), '{ServiceUserId}', now(), '{ServiceUserId}'
            FROM generate_series(1, 3000) AS n
            JOIN identity_access."user" owner ON owner.username = 'owner.' || lpad((n % 150 + 1)::text, 3, '0');

            ANALYZE identity_access."user";
            ANALYZE project.project;
            """);

        string plan = (await database.QueryAsync("""
            EXPLAIN (ANALYZE, FORMAT JSON)
            SELECT p.id, p.title, u.display_name
            FROM project.project p
            JOIN identity_access."user" u ON u.id = p.project_manager_user_id
            WHERE u.username = 'owner.042'
            """))[0];
        output.WriteLine(plan);

        List<PlanNode> nodes = QueryPlan.Nodes(JsonDocument.Parse(plan).RootElement[0].GetProperty("Plan"));

        Assert.DoesNotContain(nodes, n => n.NodeType == "Seq Scan" && n.RelationName == "project");
        Assert.Contains(nodes, n => n.IndexName == "ix_project_project_manager_user_id_updated_at_id");
    }

    private static string ProjectRow(
        string id = "gen_random_uuid()",
        string formalProjectId = "NULL",
        string lifecycleState = "DRAFT",
        string titleLanguage = "en") => $"""
        INSERT INTO project.project (id, formal_project_id, title, title_lang, classification_item_id, department_id, lifecycle_state,
                                     governance_profile_item_id, participation_mode, registration_budget_sar, created_at, created_by, updated_at, updated_by)
        VALUES ({id}, {formalProjectId}, 'Regional road upgrade', '{titleLanguage}', '{MasterDataItemId}', '{DepartmentId}', '{lifecycleState}',
                '{MasterDataItemId}', 'ENTITY_MANAGED', 25000000.00, now(), '{ServiceUserId}', now(), '{ServiceUserId}')
        """;
}
