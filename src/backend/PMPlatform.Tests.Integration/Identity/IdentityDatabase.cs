using PMPlatform.Infrastructure.Persistence;
using PMPlatform.Tests.Integration.Persistence;

namespace PMPlatform.Tests.Integration.Identity;

/// <summary>
/// A migrated, seeded database (roles R01–R08 and their shipped-default profile versions, TASK-027) with one platform
/// user per test-directory person, each in the state a test needs. Users are linked to the directory by subject only:
/// what they may do comes from these rows, never from the directory (ADR-007).
/// </summary>
public sealed class IdentityDatabase : TestDatabase
{
    public const string SeedPrincipalId = "00000000-0000-4000-8000-0000000000ff";
    public const string DirectorySyncPrincipalId = "00000000-0000-4000-8000-0000000000fe";
    public const string DepartmentId = "00000000-0120-4000-8000-000000000001";
    public const string ActiveEntityId = "00000000-0121-4000-8000-000000000001";
    public const string SuspendedEntityId = "00000000-0121-4000-8000-000000000002";
    public const string EntityProjectId = "00000000-0150-4000-8000-000000000001";

    /// <summary>The platform user of test-directory person local.r0<paramref name="n"/>.</summary>
    public static string UserId(int n) => $"00000000-0110-4000-8000-{n:D12}";

    /// <summary>The shipped-default profile version 1 of role R0<paramref name="n"/>, as db/seed ids it.</summary>
    public static string ProfileVersionId(int n) => $"00000000-0002-4000-8000-{n:D12}";

    private static string Users => $"""
        INSERT INTO identity_access.department (id, code, name_ar, name_en, directory_reference, created_at, created_by, updated_at, updated_by)
        VALUES ('{DepartmentId}', 'DEPT-IDENTITY-TEST', 'إدارة اختبار الهوية', 'Identity test department', 'DEPT-LOCAL', now(), '{SeedPrincipalId}', now(), '{SeedPrincipalId}');

        -- 1, 2, 3, 5, 6: internal and active. 3 starts with stale directory attributes. 4: disabled.
        INSERT INTO identity_access."user" (id, user_type, directory_subject_id, username, display_name, email, job_title, department_id, status, created_at, created_by, updated_at, updated_by)
        VALUES ('{UserId(1)}', 'INTERNAL', '{TestDirectory.Subject(1)}', 'local.r01', 'Local R01', 'r01@identity.test', 'System Administrator', '{DepartmentId}', 'ACTIVE', now(), '{SeedPrincipalId}', now(), '{SeedPrincipalId}'),
               ('{UserId(2)}', 'INTERNAL', '{TestDirectory.Subject(2)}', 'local.r02', 'Local R02', 'r02@identity.test', 'Portfolio Manager', '{DepartmentId}', 'ACTIVE', now(), '{SeedPrincipalId}', now(), '{SeedPrincipalId}'),
               ('{UserId(3)}', 'INTERNAL', '{TestDirectory.Subject(3)}', 'local.r03', 'Local R03', 'r03@identity.test', 'Stale title', NULL, 'ACTIVE', now(), '{SeedPrincipalId}', now(), '{SeedPrincipalId}'),
               ('{UserId(4)}', 'INTERNAL', '{TestDirectory.Subject(4)}', 'local.r04', 'Local R04', 'r04@identity.test', 'Project Manager', '{DepartmentId}', 'DISABLED', now(), '{SeedPrincipalId}', now(), '{SeedPrincipalId}'),
               ('{UserId(5)}', 'INTERNAL', '{TestDirectory.Subject(5)}', 'local.r05', 'Local R05', 'r05@identity.test', 'Liaison', '{DepartmentId}', 'ACTIVE', now(), '{SeedPrincipalId}', now(), '{SeedPrincipalId}'),
               ('{UserId(6)}', 'INTERNAL', '{TestDirectory.Subject(6)}', 'local.r06', 'Local R06', 'r06@identity.test', 'Viewer', '{DepartmentId}', 'ACTIVE', now(), '{SeedPrincipalId}', now(), '{SeedPrincipalId}');

        INSERT INTO identity_access.external_entity (id, code, name_ar, name_en, entity_type_item_id, status, sponsor_user_id, created_at, created_by, updated_at, updated_by)
        SELECT e.id, e.code, 'جهة', e.name_en, i.id, e.status, '{UserId(2)}', now(), '{SeedPrincipalId}', now(), '{SeedPrincipalId}'
        FROM (VALUES ('{ActiveEntityId}'::uuid, 'ENT-IDENTITY-ACTIVE', 'Active entity', 'ACTIVE'),
                     ('{SuspendedEntityId}'::uuid, 'ENT-IDENTITY-SUSPENDED', 'Suspended entity', 'SUSPENDED')) AS e (id, code, name_en, status)
        CROSS JOIN master_data_config.master_data_item i
        JOIN master_data_config.master_data_catalogue c ON c.id = i.catalogue_id AND c.code = 'EXTERNAL_ENTITY_TYPE'
        WHERE i.code = 'PRIVATE_COMPANY';

        -- 7: external, entity suspended. 8: external, entity active — the entity Project Manager (ADR-013).
        INSERT INTO identity_access."user" (id, user_type, directory_subject_id, username, display_name, email, external_entity_id, status, created_at, created_by, updated_at, updated_by)
        VALUES ('{UserId(7)}', 'EXTERNAL', '{TestDirectory.Subject(7)}', 'local.r07', 'Local R07', 'r07@identity.test', '{SuspendedEntityId}', 'ACTIVE', now(), '{SeedPrincipalId}', now(), '{SeedPrincipalId}'),
               ('{UserId(8)}', 'EXTERNAL', '{TestDirectory.Subject(8)}', 'local.r08', 'Local R08', 'r08@identity.test', '{ActiveEntityId}', 'ACTIVE', now(), '{SeedPrincipalId}', now(), '{SeedPrincipalId}');

        -- The entity-delivered project user 8 manages.
        INSERT INTO master_data_config.master_data_item (id, catalogue_id, code, label_ar, label_en, lifecycle_state, created_at, created_by, updated_at, updated_by)
        SELECT '00000000-0131-4000-8000-000000000001', c.id, 'IDENTITY_TEST', 'اختبار', 'Identity test', 'PUBLISHED', now(), '{SeedPrincipalId}', now(), '{SeedPrincipalId}'
        FROM master_data_config.master_data_catalogue c WHERE c.code = 'PROJECT_CLASSIFICATION';

        INSERT INTO project.project (id, title, title_lang, classification_item_id, department_id, external_entity_id, project_manager_user_id, lifecycle_state,
                                     governance_profile_item_id, participation_mode, created_at, created_by, updated_at, updated_by)
        SELECT '{EntityProjectId}', 'Entity-delivered project', 'en', '00000000-0131-4000-8000-000000000001', '{DepartmentId}', '{ActiveEntityId}', '{UserId(8)}', 'SUBMITTED',
               i.id, 'ENTITY_MANAGED', now(), '{SeedPrincipalId}', now(), '{SeedPrincipalId}'
        FROM master_data_config.master_data_item i JOIN master_data_config.master_data_catalogue c ON c.id = i.catalogue_id
        WHERE c.code = 'GOVERNANCE_PROFILE' AND i.code = 'STANDARD';

        -- Assignments (ADR-018: to a profile version). Suffix = the n of the row.
        INSERT INTO identity_access.access_relationship (id, user_id, permission_profile_version_id, department_id, external_entity_id, project_id, sponsor_user_id,
                                                         starts_at, ends_at, end_reason, status, created_at, created_by, updated_at, updated_by)
        VALUES
            -- 1: R01. 2: R02. 3: R03 with its DEPT anchor. 4: R04, but the user is disabled. 6: R06.
            ('00000000-0111-4000-8000-000000000001', '{UserId(1)}', '{ProfileVersionId(1)}', NULL, NULL, NULL, NULL, now() - interval '1 day', NULL, NULL, 'ACTIVE', now(), '{SeedPrincipalId}', now(), '{SeedPrincipalId}'),
            ('00000000-0111-4000-8000-000000000002', '{UserId(2)}', '{ProfileVersionId(2)}', NULL, NULL, NULL, NULL, now() - interval '1 day', NULL, NULL, 'ACTIVE', now(), '{SeedPrincipalId}', now(), '{SeedPrincipalId}'),
            ('00000000-0111-4000-8000-000000000003', '{UserId(3)}', '{ProfileVersionId(3)}', '{DepartmentId}', NULL, NULL, NULL, now() - interval '1 day', NULL, NULL, 'ACTIVE', now(), '{SeedPrincipalId}', now(), '{SeedPrincipalId}'),
            ('00000000-0111-4000-8000-000000000004', '{UserId(4)}', '{ProfileVersionId(4)}', NULL, NULL, NULL, NULL, now() - interval '1 day', NULL, NULL, 'ACTIVE', now(), '{SeedPrincipalId}', now(), '{SeedPrincipalId}'),
            ('00000000-0111-4000-8000-000000000006', '{UserId(6)}', '{ProfileVersionId(6)}', NULL, NULL, NULL, NULL, now() - interval '1 day', NULL, NULL, 'ACTIVE', now(), '{SeedPrincipalId}', now(), '{SeedPrincipalId}'),
            -- 5: none in force — one ended, one not yet started.
            ('00000000-0111-4000-8000-000000000051', '{UserId(5)}', '{ProfileVersionId(5)}', NULL, NULL, NULL, NULL, now() - interval '30 days', now() - interval '1 day', 'MANUAL', 'ENDED', now(), '{SeedPrincipalId}', now(), '{SeedPrincipalId}'),
            ('00000000-0111-4000-8000-000000000052', '{UserId(5)}', '{ProfileVersionId(6)}', NULL, NULL, NULL, NULL, now() + interval '30 days', NULL, NULL, 'ACTIVE', now(), '{SeedPrincipalId}', now(), '{SeedPrincipalId}'),
            -- 7: R08 on a suspended entity.
            ('00000000-0111-4000-8000-000000000007', '{UserId(7)}', '{ProfileVersionId(8)}', NULL, '{SuspendedEntityId}', NULL, '{UserId(2)}', now() - interval '1 day', NULL, NULL, 'ACTIVE', now(), '{SeedPrincipalId}', now(), '{SeedPrincipalId}'),
            -- 8: R08 on its entity, R04 on its entity's project (the third user type), and R01, which no external user may hold.
            ('00000000-0111-4000-8000-000000000081', '{UserId(8)}', '{ProfileVersionId(8)}', NULL, '{ActiveEntityId}', NULL, '{UserId(2)}', now() - interval '1 day', NULL, NULL, 'ACTIVE', now(), '{SeedPrincipalId}', now(), '{SeedPrincipalId}'),
            ('00000000-0111-4000-8000-000000000082', '{UserId(8)}', '{ProfileVersionId(4)}', NULL, '{ActiveEntityId}', '{EntityProjectId}', '{UserId(2)}', now() - interval '1 day', NULL, NULL, 'ACTIVE', now(), '{SeedPrincipalId}', now(), '{SeedPrincipalId}'),
            ('00000000-0111-4000-8000-000000000083', '{UserId(8)}', '{ProfileVersionId(1)}', NULL, NULL, NULL, '{UserId(2)}', now() - interval '1 day', NULL, NULL, 'ACTIVE', now(), '{SeedPrincipalId}', now(), '{SeedPrincipalId}');
        """;

    protected override async Task PopulateAsync()
    {
        await SeededDatabase.RunCommandAsync(ConnectionString, DatabaseScripts.SeedCommand);
        await ExecuteAsync(Users);
    }
}
