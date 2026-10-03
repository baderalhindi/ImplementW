using PMPlatform.Tests.Integration.Identity;

namespace PMPlatform.Tests.Integration.ProjectTask;

/// <summary>
/// The identity test database and people (IdentityDatabase) with what WF-04 needs and no environment has yet: the shipped R04
/// grants for two Project Managers, test grants for a task owner and for an editor without the reopen permission — Appendix A
/// grants neither (project-task.md F-2) — and a PUBLISHED and a DRAFT item of the PRIORITY catalogue. Projects and their
/// schedules are inserted, as fixtures may.
/// </summary>
/// <remarks>
/// People: local.r08 is ADR-013's entity Project Manager: external, R04 on the active entity. local.r05 is an internal Project
/// Manager (R04, no anchor). local.r06 is a task owner: R06 with TASK_VIEW and TASK_UPDATE at ASSIGNED. local.r02 edits every
/// task — TASK_VIEW, TASK_UPDATE and TASK_MANAGE at ALL — but holds no TASK_REOPEN.
/// </remarks>
public sealed class ProjectTaskTestHost : IAsyncLifetime
{
    public static readonly Guid DepartmentId = new(IdentityDatabase.DepartmentId);
    public static readonly Guid EntityId = new(IdentityDatabase.ActiveEntityId);
    public static readonly Guid ClassificationId = new("00000000-0131-4000-8000-000000000001");
    public static readonly Guid PriorityId = new("00000000-0480-4000-8000-000000000021");
    public static readonly Guid DraftPriorityId = new("00000000-0480-4000-8000-000000000022");

    private const string Seed = IdentityDatabase.SeedPrincipalId;

    private static string Fixture => $"""
        INSERT INTO identity_access.permission_profile_grant (id, permission_profile_version_id, permission_id, data_scope, created_at, created_by, updated_at, updated_by)
        SELECT gen_random_uuid(), g.version_id::uuid, p.id, g.scope, now(), '{Seed}', now(), '{Seed}'
        FROM (VALUES ('{IdentityDatabase.ProfileVersionId(2)}', 'TASK_VIEW', 'ALL'), ('{IdentityDatabase.ProfileVersionId(2)}', 'TASK_UPDATE', 'ALL'),
                     ('{IdentityDatabase.ProfileVersionId(2)}', 'TASK_MANAGE', 'ALL'),
                     ('{IdentityDatabase.ProfileVersionId(6)}', 'TASK_VIEW', 'ASSIGNED'), ('{IdentityDatabase.ProfileVersionId(6)}', 'TASK_UPDATE', 'ASSIGNED'))
             AS g (version_id, code, scope)
        JOIN identity_access.permission p ON p.code = g.code;

        INSERT INTO identity_access.access_relationship (id, user_id, permission_profile_version_id, external_entity_id, sponsor_user_id, starts_at, status, created_at, created_by, updated_at, updated_by)
        VALUES ('00000000-0480-4000-8000-000000000051', '{IdentityDatabase.UserId(5)}', '{IdentityDatabase.ProfileVersionId(4)}', NULL, NULL, now() - interval '1 day', 'ACTIVE', now(), '{Seed}', now(), '{Seed}'),
               ('00000000-0480-4000-8000-000000000081', '{IdentityDatabase.UserId(8)}', '{IdentityDatabase.ProfileVersionId(4)}', '{EntityId}', '{IdentityDatabase.UserId(2)}', now() - interval '1 day', 'ACTIVE', now(), '{Seed}', now(), '{Seed}');

        INSERT INTO master_data_config.master_data_item (id, catalogue_id, code, label_ar, label_en, lifecycle_state, created_at, created_by, updated_at, updated_by)
        SELECT v.id, c.id, v.code, 'أولوية', v.label, v.state, now(), '{Seed}', now(), '{Seed}'
        FROM (VALUES ('{PriorityId}'::uuid, 'TEST_HIGH', 'High', 'PUBLISHED'), ('{DraftPriorityId}'::uuid, 'TEST_DRAFT', 'Draft', 'DRAFT')) AS v (id, code, label, state)
        CROSS JOIN master_data_config.master_data_catalogue c WHERE c.code = 'PRIORITY';
        """;

    public IdentityTestHost Identity { get; } = new();

    public IdentityDatabase Database => Identity.Database;

    public IdentityApiFactory Api { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await Identity.InitializeAsync();
        await Database.ExecuteAsync(Fixture);
        Api = Identity.CreateApi(
            new Dictionary<string, string?>
            {
                ["Outbox:PollInterval"] = "01:00:00",
                ["Approval:Maintenance:PollInterval"] = "01:00:00",
                ["DocumentManagement:Scan:PollInterval"] = "01:00:00",
            });
    }

    public async Task DisposeAsync()
    {
        await Api.DisposeAsync();
        await Identity.DisposeAsync();
    }
}

[CollectionDefinition(Name)]
public sealed class ProjectTaskSuite : ICollectionFixture<ProjectTaskTestHost>
{
    public const string Name = "ProjectTask";
}
