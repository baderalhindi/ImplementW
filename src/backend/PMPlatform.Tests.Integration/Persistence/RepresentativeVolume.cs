using System.Globalization;
using static PMPlatform.Tests.Integration.Persistence.MigratedDatabase;

namespace PMPlatform.Tests.Integration.Persistence;

/// <summary>
/// A migrated database loaded to the representative volume of <c>indexing-strategy.md</c> §5.2: 150 named users (the
/// brief's capacity figure), 12 departments, 40 external entities, 600 master data items and 3,000 projects in the
/// real core tables, and every register table of a later module as an ERD-shaped stand-in in schema
/// <c>plan_review</c>. A stand-in has the columns, primary and unique keys of its <c>erd.dbml</c> table, the index
/// register's rows for it, and an index on every foreign-key column no other index leads with, which is what its
/// module's migration must produce.
/// </summary>
public sealed class RepresentativeVolume : IAsyncLifetime
{
    public const string DepartmentId = "00000000-0022-4000-8000-000000000003";
    public const string EntityId = "00000000-0023-4000-8000-000000000003";
    public const string UserId = "00000000-0012-4000-8000-000000000042";
    public const string ProjectId = "00000000-0050-4000-8000-000000000042";
    public const string CatalogueId = "00000000-0032-4000-8000-000000000007";
    public const string ConfigurationFamilyId = "00000000-0042-4000-8000-000000000005";
    public const string CriticalRatingId = "00000000-0070-4000-8000-000000000004";
    public const string KpiAssignmentId = "md5('kpi-42')::uuid";

    /// <summary>Two years of seconds: every timestamp and date falls between 2024-10-01 and 2026-09-30.</summary>
    private const int TwoYears = 63_072_000;

    /// <summary>
    /// Users are 0012-…-001 to 150, departments 0022-…-01 to 12, entities 0023-…-01 to 40, projects 0050-…-0001 to 3000;
    /// 30 catalogues of 20 items each, and 15 configuration families of 6 versions each.
    /// </summary>
    private const string CoreRows = $"""
        INSERT INTO identity_access.department (id, code, name_ar, name_en, created_at, created_by, updated_at, updated_by)
        SELECT ('00000000-0022-4000-8000-' || lpad(n::text, 12, '0'))::uuid, 'DEPT-' || n, 'إدارة ' || n, 'Department ' || n, now(), '{ServiceUserId}', now(), '{ServiceUserId}'
        FROM generate_series(1, 12) n;

        INSERT INTO identity_access.external_entity (id, code, name_ar, name_en, entity_type_item_id, status, created_at, created_by, updated_at, updated_by)
        SELECT ('00000000-0023-4000-8000-' || lpad(n::text, 12, '0'))::uuid, 'ENT-' || n, 'جهة ' || n, 'Entity ' || n, '{MasterDataItemId}', 'ACTIVE', now(), '{ServiceUserId}', now(), '{ServiceUserId}'
        FROM generate_series(1, 40) n;

        INSERT INTO identity_access."user" (id, user_type, username, display_name, email, department_id, status, created_at, created_by, updated_at, updated_by)
        SELECT ('00000000-0012-4000-8000-' || lpad(n::text, 12, '0'))::uuid, 'INTERNAL', 'user.' || n, 'User ' || n, 'user.' || n || '@pmplatform.test',
               ('00000000-0022-4000-8000-' || lpad((n % 12 + 1)::text, 12, '0'))::uuid, CASE WHEN n % 15 = 0 THEN 'DISABLED' ELSE 'ACTIVE' END,
               now(), '{ServiceUserId}', now(), '{ServiceUserId}'
        FROM generate_series(1, 150) n;

        INSERT INTO master_data_config.master_data_catalogue (id, code, name_ar, name_en, created_at, created_by, updated_at, updated_by)
        SELECT ('00000000-0032-4000-8000-' || lpad(n::text, 12, '0'))::uuid, 'CATALOGUE_' || n, 'فهرس ' || n, 'Catalogue ' || n, now(), '{ServiceUserId}', now(), '{ServiceUserId}'
        FROM generate_series(1, 30) n;

        INSERT INTO master_data_config.master_data_item (id, catalogue_id, code, label_ar, label_en, sort_order, lifecycle_state, created_at, created_by, updated_at, updated_by)
        SELECT gen_random_uuid(), ('00000000-0032-4000-8000-' || lpad((n % 30 + 1)::text, 12, '0'))::uuid, 'ITEM_' || n, 'عنصر ' || n, 'Item ' || n, n / 30,
               (ARRAY['PUBLISHED', 'PUBLISHED', 'PUBLISHED', 'DRAFT', 'RETIRED'])[n % 5 + 1], now(), '{ServiceUserId}', now(), '{ServiceUserId}'
        FROM generate_series(1, 600) n;

        INSERT INTO master_data_config.configuration_family (id, code, name_ar, name_en, created_at, created_by, updated_at, updated_by)
        SELECT ('00000000-0042-4000-8000-' || lpad(n::text, 12, '0'))::uuid, 'FAMILY_' || n, 'عائلة ' || n, 'Family ' || n, now(), '{ServiceUserId}', now(), '{ServiceUserId}'
        FROM generate_series(1, 15) n;

        INSERT INTO master_data_config.configuration_version (id, configuration_family_id, version_no, effective_from, lifecycle_state, created_at, created_by, updated_at, updated_by)
        SELECT gen_random_uuid(), ('00000000-0042-4000-8000-' || lpad(f::text, 12, '0'))::uuid, v, timestamptz '2024-10-01' + v * interval '90 days',
               CASE WHEN v = 6 THEN 'DRAFT' ELSE 'PUBLISHED' END, now(), '{ServiceUserId}', now(), '{ServiceUserId}'
        FROM generate_series(1, 15) f CROSS JOIN generate_series(1, 6) v;

        -- 3,000 projects: 250 per department, 20 per project manager, every second one entity-delivered.
        INSERT INTO project.project (id, formal_project_id, title, title_lang, description, description_lang, classification_item_id, department_id,
                                     external_entity_id, project_manager_user_id, lifecycle_state, governance_profile_item_id, participation_mode,
                                     created_at, created_by, updated_at, updated_by)
        SELECT ('00000000-0050-4000-8000-' || lpad(n::text, 12, '0'))::uuid,
               CASE WHEN s.state NOT IN ('DRAFT', 'SUBMITTED', 'UNDER_REVIEW', 'RETURNED') THEN 'AHDA-' || n END,
               'Project ' || n, 'en', repeat('Project scope and objectives. ', 10), 'en', '{MasterDataItemId}',
               ('00000000-0022-4000-8000-' || lpad((n % 12 + 1)::text, 12, '0'))::uuid,
               CASE WHEN n % 2 = 0 THEN ('00000000-0023-4000-8000-' || lpad((n % 40 + 1)::text, 12, '0'))::uuid END,
               ('00000000-0012-4000-8000-' || lpad((n % 150 + 1)::text, 12, '0'))::uuid,
               s.state, '{MasterDataItemId}', 'AHDA_MANAGED', t.at, '{ServiceUserId}', t.at, '{ServiceUserId}'
        FROM generate_series(1, 3000) n
        CROSS JOIN LATERAL (SELECT (ARRAY['DRAFT', 'SUBMITTED', 'UNDER_REVIEW', 'RETURNED', 'APPROVED_PLANNED', 'ACTIVE', 'ACTIVE', 'ACTIVE', 'ACTIVE',
                                          'SUSPENDED', 'COMPLETED', 'CLOSED', 'CLOSED'])[n % 13 + 1] AS state) s
        CROSS JOIN LATERAL (SELECT timestamptz '2024-10-01' + (n * 7919 % 3000) * interval '350 minutes' AS at) t;

        -- Four grants per user over time, two of them still ACTIVE.
        INSERT INTO identity_access.role (id, code, name_ar, name_en, is_system, is_external_eligible, created_at, created_by, updated_at, updated_by)
        VALUES ('00000000-0001-4000-8000-000000000003', 'R03', 'مدير الإدارة', 'Department Manager', true, false, now(), '{ServiceUserId}', now(), '{ServiceUserId}');
        INSERT INTO identity_access.permission_profile (id, code, name_ar, name_en, base_role_id, is_shipped_default, created_at, created_by, updated_at, updated_by)
        VALUES ('00000000-0002-4000-8000-000000000003', 'R03-DEFAULT', 'افتراضي', 'Default', '00000000-0001-4000-8000-000000000003', true, now(), '{ServiceUserId}', now(), '{ServiceUserId}');
        INSERT INTO identity_access.permission_profile_version (id, permission_profile_id, version_no, lifecycle_state, created_at, created_by, updated_at, updated_by)
        VALUES ('00000000-0003-4000-8000-000000000003', '00000000-0002-4000-8000-000000000003', 1, 'PUBLISHED', now(), '{ServiceUserId}', now(), '{ServiceUserId}');
        INSERT INTO identity_access.access_relationship (id, user_id, permission_profile_version_id, department_id, starts_at, status, created_at, created_by, updated_at, updated_by)
        SELECT gen_random_uuid(), ('00000000-0012-4000-8000-' || lpad((n % 150 + 1)::text, 12, '0'))::uuid, '00000000-0003-4000-8000-000000000003',
               ('00000000-0022-4000-8000-' || lpad((n % 12 + 1)::text, 12, '0'))::uuid, now(), CASE WHEN n > 300 THEN 'ENDED' ELSE 'ACTIVE' END,
               now(), '{ServiceUserId}', now(), '{ServiceUserId}'
        FROM generate_series(1, 600) n;
        """;

    /// <summary>
    /// The stand-in tables, their row counts and the columns whose values the register queries depend on. Every other
    /// NOT NULL column gets a value of its type (<see cref="DefaultValue"/>). <c>n</c> is the row number, 1 to Rows;
    /// <see cref="Pick"/> spreads a value set by a stable hash of <c>n</c>, so every run loads the same rows.
    /// </summary>
    private static readonly StandIn[] StandIns =
    [
        new("risk.risk", 45_000, new()
        {
            ["id"] = "md5('risk-' || n)::uuid",
            ["project_id"] = Project("n"),
            ["owner_user_id"] = User(Hash(1)),
            ["status"] = Pick(2, "IDENTIFIED", "ASSESSED", "TREATMENT", "MONITORING", "CLOSED", "CLOSED"),
            ["next_review_date"] = $"date '2026-10-01' + {Hash(3)} % 120",
        }),
        new("risk.risk_assessment_version", 90_000, new()
        {
            ["risk_id"] = "md5('risk-' || (n % 45000 + 1))::uuid",
            ["version_no"] = "n / 45000 + 1",
            ["risk_rating_definition_id"] = $"CASE WHEN {Hash(1)} % 20 = 0 THEN '{CriticalRatingId}'::uuid ELSE ('00000000-0070-4000-8000-' || lpad(({Hash(2)} % 3 + 1)::text, 12, '0'))::uuid END",
        }),
        new("management_concern.management_concern", 24_000, new()
        {
            ["project_id"] = Project("n"),
            ["concern_type"] = Pick(1, "ISSUE", "ISSUE", "ISSUE", "CHALLENGE"),
            ["status"] = Pick(2, "OPEN", "ASSIGNED", "IN_PROGRESS", "PENDING_VALIDATION", "RESOLVED", "CLOSED", "CLOSED"),
            ["raised_by_user_id"] = User(Hash(3)),
            ["assignee_user_id"] = User(Hash(4)),
        }),
        new("management_concern.concern_escalation", 2_400, new()
        {
            ["status"] = Pick(1, "OPEN", "RESOLVED", "RESOLVED", "RESOLVED", "WITHDRAWN"),
            ["escalated_to_role_id"] = $"('00000000-0001-4000-8000-' || lpad(({Hash(2)} % 8 + 1)::text, 12, '0'))::uuid",
        }),
        // Every hundredth task is PENDING; every second of those waits in a role queue with no user.
        new("approval.approval_task", 60_000, new()
        {
            ["assigned_role_id"] = $"('00000000-0001-4000-8000-' || lpad(({Hash(1)} % 8 + 1)::text, 12, '0'))::uuid",
            ["assigned_user_id"] = $"CASE WHEN n % 200 = 0 THEN NULL ELSE {User("n / 100")} END",
            ["status"] = $"CASE WHEN n % 100 = 0 THEN 'PENDING' ELSE {Pick(2, "APPROVED", "APPROVED", "APPROVED", "REJECTED", "RETURNED", "CANCELLED")} END",
        }),
        new("approval.approval_instance", 30_000, new()
        {
            ["scope_project_id"] = Project("n"),
            ["requested_by_user_id"] = User(Hash(1)),
            ["status"] = Pick(2, "PENDING", "APPROVED", "APPROVED", "APPROVED", "REJECTED", "RETURNED", "WITHDRAWN"),
        }),
        new("approval.approval_delegation", 300, new()
        {
            ["delegator_user_id"] = User("n"),
            ["delegate_user_id"] = User(Hash(1)),
            ["status"] = Pick(2, "ACTIVE", "EXPIRED", "EXPIRED", "REVOKED"),
        }),
        new("change_request.change_request", 9_000, new()
        {
            ["project_id"] = Project("n"),
            ["change_type"] = Pick(1, "SCOPE", "COST", "SCHEDULE", "CONTRACTUAL_OBLIGATION", "GOVERNANCE_PROFILE"),
            ["status"] = Pick(2, "DRAFT", "SUBMITTED", "UNDER_REVIEW", "APPROVED", "IMPLEMENTED", "CLOSED", "CLOSED", "REJECTED"),
            ["requested_by_user_id"] = User(Hash(3)),
        }),
        new("suspension.suspension_request", 600, new()
        {
            ["project_id"] = Project("n * 5"),
            ["request_type"] = Pick(1, "SUSPEND", "SUSPEND", "RESUME"),
            ["status"] = Pick(2, "DRAFT", "SUBMITTED", "UNDER_REVIEW", "APPROVED", "REJECTED", "EFFECTED", "EFFECTED"),
        }),
        new("document_management.document", 60_000, new()
        {
            ["project_id"] = Project("n"),
            ["owner_user_id"] = User(Hash(1)),
            ["status"] = Pick(2, "ACTIVE", "ACTIVE", "ACTIVE", "ACTIVE", "ARCHIVED"),
        }),
        new("project_task.project_task", 150_000, new()
        {
            ["project_id"] = Project("n"),
            ["assignee_user_id"] = User(Hash(1)),
            ["status"] = Pick(2, "NOT_STARTED", "IN_PROGRESS", "IN_PROGRESS", "BLOCKED", "COMPLETED", "COMPLETED", "COMPLETED", "CANCELLED"),
            ["planned_finish_date"] = $"date '2024-10-01' + {Hash(3)} % 730",
        }),
        new("schedule.project_milestone", 24_000, new()
        {
            ["project_id"] = Project("n"),
            ["status"] = Pick(1, "PLANNED", "PLANNED", "ACHIEVED", "CANCELLED"),
            ["forecast_date"] = $"date '2024-10-01' + {Hash(2)} % 730",
        }),
        new("progress.progress_submission", 72_000, new()
        {
            ["project_id"] = Project("n"),
            ["status"] = Pick(1, "PUBLISHED", "PUBLISHED", "PUBLISHED", "RETURNED", "DRAFT"),
            ["submitted_at"] = Timestamp(72_000),
        }),
        new("progress.published_progress_snapshot", 72_000, new()
        {
            ["project_id"] = Project("n"),
        }),
        new("financial_kpi.financial_progress_update", 72_000, new()
        {
            ["project_id"] = Project("n"),
            ["status"] = Pick(1, "PUBLISHED", "PUBLISHED", "PUBLISHED", "RETURNED", "DRAFT"),
            ["value_status"] = Pick(2, "MEASURED", "MEASURED", "MISSING", "STALE", "NOT_APPLICABLE"),
            ["source_type"] = Pick(3, "MANUAL", "ETIMAD", "OTHER"),
        }),
        new("financial_kpi.published_financial_snapshot", 72_000, new()
        {
            ["project_id"] = Project("n"),
            ["financial_status"] = Pick(1, "GREEN", "AMBER", "RED", "UNKNOWN"),
            ["source_type"] = Pick(2, "MANUAL", "ETIMAD", "OTHER"),
        }),
        new("financial_kpi.kpi_assignment", 15_000, new()
        {
            ["id"] = "md5('kpi-' || n)::uuid",
            ["project_id"] = Project("n"),
            ["status"] = Pick(1, "ACTIVE", "ACTIVE", "ACTIVE", "SUSPENDED", "RETIRED"),
        }),
        // Twelve monthly measurements for each KPI assignment.
        new("financial_kpi.kpi_measurement", 180_000, new()
        {
            ["kpi_assignment_id"] = "md5('kpi-' || (n % 15000 + 1))::uuid",
            ["period_start"] = "date '2025-10-01' + (n / 15000) * 30",
            ["value_status"] = Pick(1, "MEASURED", "MEASURED", "MISSING", "STALE", "NOT_APPLICABLE"),
            ["status"] = Pick(2, "PUBLISHED", "PUBLISHED", "SUBMITTED", "DRAFT"),
        }),
        new("external_participation.external_update_request", 12_000, new()
        {
            ["project_id"] = Project("n"),
            ["external_entity_id"] = $"('00000000-0023-4000-8000-' || lpad(({Hash(1)} % 40 + 1)::text, 12, '0'))::uuid",
            ["origin"] = Pick(2, "AHDA_ISSUED", "AHDA_ISSUED", "ENTITY_INITIATED"),
            ["status"] = Pick(3, "DRAFT", "ISSUED", "IN_PROGRESS", "RESPONDED", "CLOSED", "CLOSED", "CANCELLED", "EXPIRED"),
            ["issued_by_user_id"] = User(Hash(4)),
            ["due_date"] = $"date '2024-10-01' + {Hash(5)} % 730",
        }),
        new("external_participation.external_contribution", 24_000, new()
        {
            ["project_id"] = Project("n"),
            ["contributor_user_id"] = User(Hash(1)),
            ["status"] = Pick(2, "DRAFT", "SUBMITTED", "UNDER_REVIEW", "RETURNED", "REJECTED", "APPLIED", "APPLIED", "APPLIED"),
            ["submitted_at"] = Timestamp(24_000),
        }),
        new("external_participation.source_application", 12_000, new()
        {
            ["status"] = Pick(1, "APPLIED", "APPLIED", "APPLIED", "APPLIED", "CONFLICT", "FAILED", "RETRY_SCHEDULED"),
        }),
        // Notifications arrive in n order, so the newest 1,500 are the unread ones.
        new("notifications.notification_delivery", 400_000, new()
        {
            ["recipient_user_id"] = User("n"),
            ["channel"] = "CASE WHEN n % 3 = 0 THEN 'EMAIL' ELSE 'IN_APP' END",
            ["rendered_body"] = "repeat('Notification body. ', 8)",
            ["status"] = "CASE WHEN n % 3 <> 0 AND n > 398500 THEN 'DELIVERED' ELSE 'READ' END",
            ["read_at"] = $"CASE WHEN n % 3 <> 0 AND n <= 398500 THEN timestamptz '2024-10-01' + n * interval '157 seconds' + interval '1 hour' END",
            ["created_at"] = "timestamptz '2024-10-01' + n * interval '157 seconds'",
        }),
        new("audit_activity.business_activity_entry", 300_000, new()
        {
            ["project_id"] = Project("n"),
            ["actor_user_id"] = User(Hash(1)),
        }),
        new("audit_activity.audit_event", 300_000, new()
        {
            ["scope_project_id"] = Project("n"),
            ["actor_user_id"] = User(Hash(1)),
            ["event_class"] = Pick(2, "AUTHENTICATION", "AUTHORIZATION_DENIAL", "PRIVILEGED_ACTION", "PERMISSION_CHANGE", "LIFECYCLE_TRANSITION"),
            ["actor_type"] = Pick(3, "USER", "USER", "USER", "SERVICE", "INTEGRATION"),
            ["outcome"] = Pick(4, "SUCCESS", "SUCCESS", "SUCCESS", "DENIED", "FAILED"),
        }),
        new("reports.report_job", 15_000, new()
        {
            ["requested_by_user_id"] = User(Hash(1)),
            ["export_format"] = Pick(2, "PDF", "XLSX", "CSV"),
            ["status"] = Pick(3, "COMPLETED", "COMPLETED", "COMPLETED", "FAILED", "EXPIRED", "CANCELLED"),
        }),
        new("reports.saved_view", 1_500, new()
        {
            ["owner_user_id"] = User(Hash(1)),
            ["view_type"] = Pick(2, "REPORT_PARAMETERS", "EXPLORER_COMPOSITION"),
        }),
    ];

    public MigratedDatabase Database { get; } = new();

    public async Task InitializeAsync()
    {
        await Database.InitializeAsync();
        await Database.ExecuteAsync(CoreRows);
        await Database.ExecuteAsync(StandInDefinitions());
        foreach (StandIn standIn in StandIns)
        {
            await Database.ExecuteAsync(StandInRows(standIn));
        }

        await Database.ExecuteAsync("VACUUM ANALYZE");
    }

    public Task DisposeAsync() => Database.DisposeAsync();

    private static string StandInDefinitions()
    {
        List<string> statements = ["CREATE SCHEMA plan_review"];
        foreach (ErdTable table in StandIns.Select(s => ErdModel.Table(s.Table)))
        {
            List<IReadOnlyList<string>> indexes =
                [.. IndexingStrategy.Indexes.Where(i => i.Table == table.Name).Select(i => i.Columns)];
            indexes.AddRange(table.Columns
                .Where(c => c.References is not null && !indexes.Concat(table.UniqueKeys).Any(key => key[0] == c.Name))
                .Select(c => (IReadOnlyList<string>)[c.Name]));

            string name = StandInName(table.Name);
            statements.Add($"CREATE TABLE {name} ({string.Join(", ", table.Columns.Select(c => $"{c.Name} {c.Type}{(c.NotNull ? " NOT NULL" : "")}{(c.Unique ? " UNIQUE" : "")}"))}, PRIMARY KEY (id))");
            statements.AddRange(table.UniqueKeys.Select(key => $"CREATE UNIQUE INDEX ON {name} ({string.Join(", ", key)})"));
            statements.AddRange(indexes.Select(key => $"CREATE INDEX ON {name} ({string.Join(", ", key)})"));
        }

        return string.Join(";\n", statements);
    }

    private static string StandInRows(StandIn standIn)
    {
        ErdTable table = ErdModel.Table(standIn.Table);
        List<ErdColumn> columns = [.. table.Columns.Where(c => c.NotNull || standIn.Values.ContainsKey(c.Name))];

        return string.Create(CultureInfo.InvariantCulture, $"""
            INSERT INTO {StandInName(table.Name)} ({string.Join(", ", columns.Select(c => c.Name))})
            SELECT {string.Join(", ", columns.Select(c => standIn.Values.GetValueOrDefault(c.Name) ?? DefaultValue(c, standIn.Rows)))}
            FROM generate_series(1, {standIn.Rows}) n
            """);
    }

    /// <summary>A value of the column's type. Timestamps and dates are spread over two years; a unique text column gets a distinct value.</summary>
    private static string DefaultValue(ErdColumn column, int rows) => column.Type switch
    {
        "uuid" => "gen_random_uuid()",
        "text" or "bigint" or "integer" or "smallint" or "boolean" when column.Unique => throw new NotSupportedException($"No default for unique {column.Type} {column.Name}."),
        "text" => "'Text ' || n",
        "timestamp with time zone" => Timestamp(rows),
        "date" => $"date '2024-10-01' + {Hash(9)} % 730",
        "integer" or "smallint" or "bigint" => "1",
        "boolean" => "false",
        _ when column.Type.StartsWith("character varying", StringComparison.Ordinal) => column.Unique ? "'key-' || n" : "'x'",
        _ when column.Type.StartsWith("character(", StringComparison.Ordinal) => "'en'",
        _ when column.Type.StartsWith("numeric", StringComparison.Ordinal) => "0",
        _ => throw new NotSupportedException($"No default for {column.Type} {column.Name}."),
    };

    private static string StandInName(string table) => $"plan_review.{table.Split('.')[1]}";

    /// <summary>A stable pseudo-random non-negative integer for row <c>n</c>; the salt decorrelates columns of one row.</summary>
    private static string Hash(int salt) => $"(hashint4(n * 31 + {salt}) & 1048575)";

    private static string Pick(int salt, params string[] values) =>
        $"(ARRAY[{string.Join(", ", values.Select(v => $"'{v}'"))}])[{Hash(salt)} % {values.Length} + 1]";

    /// <summary>Row <c>n</c>'s place in a fixed permutation of the rows, spread evenly over two years.</summary>
    private static string Timestamp(int rows) => $"timestamptz '2024-10-01' + ((n::bigint * 7919) % {rows}) * interval '{TwoYears / rows} seconds'";

    private static string Project(string n) => $"('00000000-0050-4000-8000-' || lpad((({n}) % 3000 + 1)::text, 12, '0'))::uuid";

    private static string User(string n) => $"('00000000-0012-4000-8000-' || lpad((({n}) % 150 + 1)::text, 12, '0'))::uuid";

    private sealed record StandIn(string Table, int Rows, Dictionary<string, string> Values);
}
