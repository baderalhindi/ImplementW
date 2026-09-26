using static PMPlatform.Tests.Integration.Persistence.MigratedDatabase;

namespace PMPlatform.Tests.Integration.Persistence;

/// <summary>
/// A migrated database loaded to the representative volume of <c>indexing-strategy.md</c> §5: 150 named users (the
/// brief's capacity figure), 12 departments, 40 external entities and 3,000 projects in the real core tables, and the
/// four busiest registers of later modules as ERD-shaped stand-ins in schema <c>plan_review</c>. A stand-in has the
/// columns, primary and unique keys of its <c>erd.dbml</c> table, the index register's rows for it, and an index on
/// every foreign-key column no other index leads with, which is what its module's migration must produce.
/// </summary>
public sealed class RepresentativeVolume : IAsyncLifetime
{
    public const string DepartmentId = "00000000-0022-4000-8000-000000000003";
    public const string UserId = "00000000-0012-4000-8000-000000000042";

    /// <summary>The registers whose tables TASK-025 does not build: SCR-080, SCR-083, SCR-100 and SCR-150.</summary>
    private static readonly string[] StandInTables =
        ["risk.risk", "management_concern.management_concern", "approval.approval_task", "notifications.notification_delivery"];

    /// <summary>
    /// Timestamps spread over two years by a fixed permutation, so every run loads the same rows. Users are
    /// 0012-…-001 to 150, departments 0022-…-01 to 12, external entities 0023-…-01 to 40.
    /// </summary>
    private const string Rows = $"""
        INSERT INTO identity_access.department (id, code, name_ar, name_en, created_at, created_by, updated_at, updated_by)
        SELECT ('00000000-0022-4000-8000-' || lpad(n::text, 12, '0'))::uuid, 'DEPT-' || n, 'إدارة ' || n, 'Department ' || n, now(), '{ServiceUserId}', now(), '{ServiceUserId}'
        FROM generate_series(1, 12) n;

        INSERT INTO identity_access.external_entity (id, code, name_ar, name_en, entity_type_item_id, status, created_at, created_by, updated_at, updated_by)
        SELECT ('00000000-0023-4000-8000-' || lpad(n::text, 12, '0'))::uuid, 'ENT-' || n, 'جهة ' || n, 'Entity ' || n, '{MasterDataItemId}', 'ACTIVE', now(), '{ServiceUserId}', now(), '{ServiceUserId}'
        FROM generate_series(1, 40) n;

        INSERT INTO identity_access."user" (id, user_type, username, display_name, email, department_id, status, created_at, created_by, updated_at, updated_by)
        SELECT ('00000000-0012-4000-8000-' || lpad(n::text, 12, '0'))::uuid, 'INTERNAL', 'user.' || n, 'User ' || n, 'user.' || n || '@pmplatform.test',
               ('00000000-0022-4000-8000-' || lpad((n % 12 + 1)::text, 12, '0'))::uuid, 'ACTIVE', now(), '{ServiceUserId}', now(), '{ServiceUserId}'
        FROM generate_series(1, 150) n;

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

        CREATE TEMPORARY TABLE numbered_project AS
        SELECT (row_number() OVER (ORDER BY id))::int AS n, id FROM project.project;

        -- 15 risks per project: 45,000.
        INSERT INTO plan_review.risk (id, project_id, title, title_lang, description, description_lang, risk_category_item_id, owner_user_id, status,
                                      identified_date, next_review_date, reopened_count, created_at, created_by, updated_at, updated_by)
        SELECT gen_random_uuid(), p.id, 'Risk ' || g, 'en', repeat('Risk cause, event and effect. ', 10), 'en', '{MasterDataItemId}',
               ('00000000-0012-4000-8000-' || lpad(((p.n * 15 + g) % 150 + 1)::text, 12, '0'))::uuid,
               (ARRAY['IDENTIFIED', 'ASSESSED', 'TREATMENT', 'MONITORING', 'CLOSED', 'CLOSED'])[(p.n + g) % 6 + 1],
               date '2024-10-01' + (p.n * 15 + g) * 7919 % 730, date '2026-10-01' + (p.n + g) % 120, 0, t.at, '{ServiceUserId}', t.at, '{ServiceUserId}'
        FROM numbered_project p CROSS JOIN generate_series(1, 15) g
        CROSS JOIN LATERAL (SELECT timestamptz '2024-10-01' + ((p.n * 15 + g) * 7919 % 45000) * interval '23 minutes' AS at) t;

        -- 8 management concerns per project, 6 issues and 2 challenges: 24,000.
        INSERT INTO plan_review.management_concern (id, project_id, concern_type, title, title_lang, description, description_lang, category_item_id,
                                                    priority_item_id, status, revision_no, raised_by_user_id, raised_at, assignee_user_id,
                                                    created_at, created_by, updated_at, updated_by)
        SELECT gen_random_uuid(), p.id, CASE WHEN g <= 6 THEN 'ISSUE' ELSE 'CHALLENGE' END, 'Concern ' || g, 'en', repeat('Concern description. ', 10), 'en',
               '{MasterDataItemId}', '{MasterDataItemId}',
               (ARRAY['OPEN', 'ASSIGNED', 'IN_PROGRESS', 'PENDING_VALIDATION', 'RESOLVED', 'CLOSED', 'CLOSED'])[(p.n + g) % 7 + 1], 1,
               ('00000000-0012-4000-8000-' || lpad(((p.n + g) % 150 + 1)::text, 12, '0'))::uuid, t.at,
               ('00000000-0012-4000-8000-' || lpad(((p.n * 8 + g) % 150 + 1)::text, 12, '0'))::uuid, t.at, '{ServiceUserId}', t.at, '{ServiceUserId}'
        FROM numbered_project p CROSS JOIN generate_series(1, 8) g
        CROSS JOIN LATERAL (SELECT timestamptz '2024-10-01' + ((p.n * 8 + g) * 7919 % 24000) * interval '43 minutes' AS at) t;

        -- 10 approval instances per project with 2 stages each: 60,000 tasks. The latest instance of every tenth
        -- project is open: 300 personal PENDING tasks (20 for user 42) and 300 in role queues.
        INSERT INTO plan_review.approval_task (id, approval_instance_id, sequence_no, assigned_role_id, assigned_user_id, status, due_at,
                                               created_at, created_by, updated_at, updated_by)
        SELECT gen_random_uuid(), md5(p.id::text || i)::uuid, s, ('00000000-0001-4000-8000-' || lpad((s + 1)::text, 12, '0'))::uuid,
               CASE WHEN o.open AND s = 2 THEN NULL ELSE ('00000000-0012-4000-8000-' || lpad(((p.n + s) % 150 + 1)::text, 12, '0'))::uuid END,
               CASE WHEN o.open THEN 'PENDING' ELSE (ARRAY['APPROVED', 'APPROVED', 'APPROVED', 'REJECTED', 'RETURNED', 'CANCELLED'])[(p.n + i + s) % 6 + 1] END,
               t.at + interval '5 days', t.at, '{ServiceUserId}', t.at, '{ServiceUserId}'
        FROM numbered_project p CROSS JOIN generate_series(1, 10) i CROSS JOIN generate_series(1, 2) s
        CROSS JOIN LATERAL (SELECT i = 10 AND p.n % 10 = 0 AS open) o
        CROSS JOIN LATERAL (SELECT timestamptz '2024-10-01' + ((p.n * 20 + i * 2 + s) * 7919 % 60000) * interval '17 minutes' AS at) t;

        -- 2,000 in-app notifications per user over two years, the latest 10 unread, and an e-mail copy of every
        -- third: 400,000 deliveries.
        INSERT INTO plan_review.notification_delivery (id, notification_intent_id, recipient_user_id, channel, notification_template_id, rendered_language,
                                                       rendered_subject, rendered_body, status, attempt_count, read_at, created_at, created_by, updated_at, updated_by)
        SELECT gen_random_uuid(), md5(u || '/' || k)::uuid, ('00000000-0012-4000-8000-' || lpad(u::text, 12, '0'))::uuid, c.channel,
               '00000000-0060-4000-8000-000000000001', 'ar', 'Subject ' || k, repeat('Notification body. ', 8),
               CASE WHEN c.channel = 'IN_APP' AND k > 1990 THEN 'DELIVERED' ELSE 'READ' END, 1,
               CASE WHEN c.channel = 'IN_APP' AND k <= 1990 THEN t.at + interval '1 hour' END, t.at, '{ServiceUserId}', t.at, '{ServiceUserId}'
        FROM generate_series(1, 150) u CROSS JOIN generate_series(1, 2000) k
        CROSS JOIN LATERAL (SELECT channel FROM (VALUES ('IN_APP'), ('EMAIL')) v(channel) WHERE channel = 'IN_APP' OR k % 3 = 0) c
        CROSS JOIN LATERAL (SELECT timestamptz '2024-10-01' + k * interval '526 minutes' AS at) t;
        """;

    public MigratedDatabase Database { get; } = new();

    public async Task InitializeAsync()
    {
        await Database.InitializeAsync();
        await Database.ExecuteAsync(StandInDefinitions());
        await Database.ExecuteAsync(Rows);
        await Database.ExecuteAsync("VACUUM ANALYZE");
    }

    public Task DisposeAsync() => Database.DisposeAsync();

    private static string StandInDefinitions()
    {
        List<string> statements = ["CREATE SCHEMA plan_review"];
        foreach (ErdTable table in StandInTables.Select(ErdModel.Table))
        {
            string name = $"plan_review.{table.Name.Split('.')[1]}";
            List<IReadOnlyList<string>> indexes =
                [.. IndexingStrategy.Indexes.Where(i => i.Table == table.Name).Select(i => i.Columns)];
            indexes.AddRange(table.Columns
                .Where(c => c.References is not null && !indexes.Concat(table.UniqueKeys).Any(key => key[0] == c.Name))
                .Select(c => (IReadOnlyList<string>)[c.Name]));

            statements.Add($"CREATE TABLE {name} ({string.Join(", ", table.Columns.Select(c => $"{c.Name} {c.Type}{(c.NotNull ? " NOT NULL" : "")}{(c.Unique ? " UNIQUE" : "")}"))}, PRIMARY KEY (id))");
            statements.AddRange(table.UniqueKeys.Select(key => $"CREATE UNIQUE INDEX ON {name} ({string.Join(", ", key)})"));
            statements.AddRange(indexes.Select(key => $"CREATE INDEX ON {name} ({string.Join(", ", key)})"));
        }

        return string.Join(";\n", statements);
    }
}
