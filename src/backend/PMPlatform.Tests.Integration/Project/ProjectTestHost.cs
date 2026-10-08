using PMPlatform.Tests.Integration.Identity;

namespace PMPlatform.Tests.Integration.Project;

/// <summary>
/// The identity test database and people (IdentityDatabase) with what WF-01 needs and no environment has yet: test grants
/// for AHDA's side — Appendix A grants nothing there (project-registration.md F-1) — beside the shipped R04 and R08 ENTITY
/// grants; published region, city and classification items; and published PROJECT_REGISTRATION, SUSPENSION, RESUMPTION, COMPLETION and
/// CLOSURE routes in APPROVAL_AUTHORITY with their WORKFLOW_POLICY, so that WF-09 and WF-10 can take a project along TASK-062's and
/// TASK-063's edges. The API's outbox and maintenance workers stay idle, so each test dispatches and activates when it chooses.
/// </summary>
/// <remarks>
/// People: local.r02 views, reviews and activates every project, decides the review and activates suspension and resumption
/// requests and completion and closure cases (R02, ALL). local.r03 registers, views and reviews for their department, raises suspension
/// and resumption requests and completion and closure cases there, and reviews and waives the cases' criteria (R03, DEPT anchor). local.r05 is an internal Project Manager (R04, no anchor).
/// local.r06 holds R06 only. local.r08 is ADR-013's entity user: external, R08 on the active entity, and R04 on that entity
/// too — and the test R04 profile also holds review and activation, so the person check of ADR-013 is what refuses them.
/// local.r07 is external, of a suspended entity.
/// </remarks>
public sealed class ProjectTestHost : IAsyncLifetime
{
    public const int DueDays = 3;

    public static readonly Guid DepartmentId = new(IdentityDatabase.DepartmentId);
    public static readonly Guid EntityId = new(IdentityDatabase.ActiveEntityId);
    public static readonly Guid SuspendedEntityId = new(IdentityDatabase.SuspendedEntityId);
    public static readonly Guid ClassificationId = new("00000000-0131-4000-8000-000000000001");
    public static readonly Guid DraftClassificationId = new("00000000-0410-4000-8000-000000000001");
    public static readonly Guid RegionId = new("00000000-0410-4000-8000-000000000011");
    public static readonly Guid OtherRegionId = new("00000000-0410-4000-8000-000000000012");
    public static readonly Guid CityId = new("00000000-0410-4000-8000-000000000021");
    public static readonly Guid OtherEntityId = new("00000000-0410-4000-8000-000000000031");
    public static readonly Guid OtherDepartmentId = new("00000000-0420-4000-8000-000000000001");

    private const string Seed = IdentityDatabase.SeedPrincipalId;

    private static string RoleId(int n) => $"00000000-0000-4000-8000-{n:D12}";

    private static string Fixture => $"""
        INSERT INTO master_data_config.master_data_item (id, catalogue_id, code, label_ar, label_en, parent_item_id, lifecycle_state, created_at, created_by, updated_at, updated_by)
        SELECT i.id::uuid, c.id, i.code, 'اختبار', i.label_en, i.parent::uuid, i.state, now(), '{Seed}', now(), '{Seed}'
        FROM (VALUES ('{DraftClassificationId}', 'PROJECT_CLASSIFICATION', 'TEST_DRAFT_CLASS', 'Draft classification', NULL, 'DRAFT'),
                     ('{RegionId}', 'REGION', 'TEST_REGION', 'Test region', NULL, 'PUBLISHED'),
                     ('{OtherRegionId}', 'REGION', 'TEST_OTHER_REGION', 'Other region', NULL, 'PUBLISHED'),
                     ('{CityId}', 'CITY', 'TEST_CITY', 'Test city', '{RegionId}', 'PUBLISHED'))
             AS i (id, catalogue, code, label_en, parent, state)
        JOIN master_data_config.master_data_catalogue c ON c.code = i.catalogue;

        -- A second active department: local.r03 manages the first, so is another department's manager for this one.
        INSERT INTO identity_access.department (id, code, name_ar, name_en, directory_reference, created_at, created_by, updated_at, updated_by)
        VALUES ('{OtherDepartmentId}', 'DEPT-PROJECT-OTHER', 'إدارة أخرى', 'Other department', 'DEPT-OTHER', now(), '{Seed}', now(), '{Seed}');

        INSERT INTO identity_access.external_entity (id, code, name_ar, name_en, entity_type_item_id, status, created_at, created_by, updated_at, updated_by)
        SELECT '{OtherEntityId}', 'ENT-PROJECT-OTHER', 'جهة أخرى', 'Other active entity', entity_type_item_id, 'ACTIVE', now(), '{Seed}', now(), '{Seed}'
        FROM identity_access.external_entity WHERE id = '{EntityId}';

        INSERT INTO identity_access.permission_profile_grant (id, permission_profile_version_id, permission_id, data_scope, created_at, created_by, updated_at, updated_by)
        SELECT gen_random_uuid(), g.version_id::uuid, p.id, g.scope, now(), '{Seed}', now(), '{Seed}'
        FROM (VALUES ('{IdentityDatabase.ProfileVersionId(2)}', 'PROJECT_VIEW', 'ALL'), ('{IdentityDatabase.ProfileVersionId(2)}', 'PROJECT_REVIEW', 'ALL'),
                     ('{IdentityDatabase.ProfileVersionId(2)}', 'PROJECT_ACTIVATE', 'ALL'), ('{IdentityDatabase.ProfileVersionId(2)}', 'APPROVAL_DECIDE', 'ALL'),
                     ('{IdentityDatabase.ProfileVersionId(2)}', 'APPROVAL_VIEW', 'ALL'),
                     ('{IdentityDatabase.ProfileVersionId(2)}', 'SUSPENSION_VIEW', 'ALL'), ('{IdentityDatabase.ProfileVersionId(2)}', 'SUSPENSION_ACTIVATE', 'ALL'),
                     ('{IdentityDatabase.ProfileVersionId(2)}', 'CLOSEOUT_VIEW', 'ALL'), ('{IdentityDatabase.ProfileVersionId(2)}', 'CLOSEOUT_ACTIVATE', 'ALL'),
                     ('{IdentityDatabase.ProfileVersionId(3)}', 'SUSPENSION_RAISE', 'DEPT'), ('{IdentityDatabase.ProfileVersionId(3)}', 'CLOSEOUT_RAISE', 'DEPT'),
                     ('{IdentityDatabase.ProfileVersionId(3)}', 'PROJECT_VIEW', 'DEPT'), ('{IdentityDatabase.ProfileVersionId(3)}', 'PROJECT_REGISTER', 'DEPT'),
                     ('{IdentityDatabase.ProfileVersionId(3)}', 'PROJECT_REVIEW', 'DEPT'), ('{IdentityDatabase.ProfileVersionId(3)}', 'APPROVAL_VIEW', 'DEPT'),
                     ('{IdentityDatabase.ProfileVersionId(4)}', 'PROJECT_REVIEW', 'ENTITY'), ('{IdentityDatabase.ProfileVersionId(4)}', 'PROJECT_ACTIVATE', 'ENTITY'))
             AS g (version_id, code, scope)
        JOIN identity_access.permission p ON p.code = g.code;

        -- local.r05: an internal Project Manager. local.r08: R04 across their entity, beside the per-project one IdentityDatabase gives.
        INSERT INTO identity_access.access_relationship (id, user_id, permission_profile_version_id, external_entity_id, sponsor_user_id, starts_at, status, created_at, created_by, updated_at, updated_by)
        VALUES ('00000000-0410-4000-8000-000000000051', '{IdentityDatabase.UserId(5)}', '{IdentityDatabase.ProfileVersionId(4)}', NULL, NULL, now() - interval '1 day', 'ACTIVE', now(), '{Seed}', now(), '{Seed}'),
               ('00000000-0410-4000-8000-000000000081', '{IdentityDatabase.UserId(8)}', '{IdentityDatabase.ProfileVersionId(4)}', '{EntityId}', '{IdentityDatabase.UserId(2)}', now() - interval '1 day', 'ACTIVE', now(), '{Seed}', now(), '{Seed}');

        INSERT INTO master_data_config.configuration_version (id, configuration_family_id, version_no, lifecycle_state, created_at, created_by, updated_at, updated_by)
        SELECT v.id, f.id, 1, 'DRAFT', now(), '{Seed}', now(), '{Seed}'
        FROM (VALUES ('00000000-0410-4000-8000-000000000101'::uuid, 'APPROVAL_AUTHORITY'),
                     ('00000000-0410-4000-8000-000000000102'::uuid, 'WORKFLOW_POLICY')) AS v (id, family)
        JOIN master_data_config.configuration_family f ON f.code = v.family;

        INSERT INTO master_data_config.approval_authority_rule (id, configuration_version_id, subject_type_code, sequence_no, approver_role_id, is_mandatory, created_at, created_by, updated_at, updated_by)
        VALUES (gen_random_uuid(), '00000000-0410-4000-8000-000000000101', 'PROJECT_REGISTRATION', 1, '{RoleId(2)}', true, now(), '{Seed}', now(), '{Seed}'),
               (gen_random_uuid(), '00000000-0410-4000-8000-000000000101', 'SUSPENSION', 1, '{RoleId(2)}', true, now(), '{Seed}', now(), '{Seed}'),
               (gen_random_uuid(), '00000000-0410-4000-8000-000000000101', 'RESUMPTION', 1, '{RoleId(2)}', true, now(), '{Seed}', now(), '{Seed}'),
               (gen_random_uuid(), '00000000-0410-4000-8000-000000000101', 'COMPLETION', 1, '{RoleId(2)}', true, now(), '{Seed}', now(), '{Seed}'),
               (gen_random_uuid(), '00000000-0410-4000-8000-000000000101', 'CLOSURE', 1, '{RoleId(2)}', true, now(), '{Seed}', now(), '{Seed}');

        INSERT INTO master_data_config.configuration_value (id, configuration_version_id, value_key, value_text, value_type, created_at, created_by, updated_at, updated_by)
        VALUES (gen_random_uuid(), '00000000-0410-4000-8000-000000000102', 'APPROVAL_TASK_DUE_DAYS', '{DueDays}', 'DURATION_DAYS', now(), '{Seed}', now(), '{Seed}'),
               (gen_random_uuid(), '00000000-0410-4000-8000-000000000102', 'APPROVAL_ESCALATION_ROLE', 'R02', 'TEXT', now(), '{Seed}', now(), '{Seed}');

        UPDATE master_data_config.configuration_version
        SET lifecycle_state = 'PUBLISHED', published_at = now() - interval '1 hour', effective_from = now() - interval '1 hour'
        WHERE id IN ('00000000-0410-4000-8000-000000000101', '00000000-0410-4000-8000-000000000102');
        """;

    public IdentityTestHost Identity { get; } = new();

    public IdentityDatabase Database => Identity.Database;

    public AdjustableTimeProvider Clock => Identity.Clock;

    public IdentityApiFactory Api { get; private set; } = null!;

    /// <summary>The seeded STANDARD governance profile item.</summary>
    public Guid GovernanceProfileId { get; private set; }

    public async Task InitializeAsync()
    {
        await Identity.InitializeAsync();
        await Database.ExecuteAsync(Fixture);
        GovernanceProfileId = Guid.Parse(Assert.Single(await Database.QueryAsync("""
            SELECT i.id::text FROM master_data_config.master_data_item i
            JOIN master_data_config.master_data_catalogue c ON c.id = i.catalogue_id
            WHERE c.code = 'GOVERNANCE_PROFILE' AND i.code = 'STANDARD'
            """)));
        Api = Identity.CreateApi(new Dictionary<string, string?>
        {
            ["Outbox:PollInterval"] = "01:00:00",
            ["Approval:Maintenance:PollInterval"] = "01:00:00",
            ["DocumentManagement:Scan:PollInterval"] = "01:00:00",
            ["Closure:Activation:PollInterval"] = "01:00:00",
        });
    }

    public async Task DisposeAsync()
    {
        await Api.DisposeAsync();
        await Identity.DisposeAsync();
    }
}

[CollectionDefinition(Name)]
public sealed class ProjectSuite : ICollectionFixture<ProjectTestHost>
{
    public const string Name = "Project";
}
