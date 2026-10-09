using PMPlatform.Tests.Integration.Identity;

namespace PMPlatform.Tests.Integration.ExternalParticipation;

/// <summary>
/// The identity test database and people (IdentityDatabase) with what WF-13 needs and no environment has yet: a second ACTIVE entity; the
/// CONTRIBUTION_TYPE items of the platform's two typed schemas, TASK_PROGRESS and PROJECT_INFORMATION, and one, SITE_PHOTOS, for which
/// the platform has none; and a PARTICIPATION version, published as FG-04's publishers would, that enables TASK_PROGRESS and SITE_PHOTOS in
/// both participation modes and PROJECT_INFORMATION for entity-managed projects only. Only shipped grants are used: no test grant is added.
/// Projects and the per-project assignments of the entity users are inserted per test, as fixtures may.
/// </summary>
/// <remarks>
/// People: local.r05 is an internal Project Manager (R04, no anchor) and manages every test project. local.r03 is the Department Manager
/// (R03 on the test department). local.r08 is an external user of entity A and local.r07, moved here from the suspended entity, of entity
/// B; each one's entity-wide R08 assignment is ended, so an entity user reaches a project only through an explicit per-project R08
/// assignment (WF-13 EXT-CC-03). local.r01 is the System Administrator; local.r06 an internal user with no WF-13 grant.
/// </remarks>
public sealed class ExternalParticipationTestHost : IAsyncLifetime
{
    public static readonly Guid DepartmentId = new(IdentityDatabase.DepartmentId);
    public static readonly Guid EntityA = new(IdentityDatabase.ActiveEntityId);
    public static readonly Guid EntityB = new("00000000-0660-4000-8000-000000000001");
    public static readonly Guid ClassificationId = new("00000000-0131-4000-8000-000000000001");
    public static readonly Guid TaskProgressTypeId = new("00000000-0660-4000-8000-000000000011");
    public static readonly Guid InformationTypeId = new("00000000-0660-4000-8000-000000000012");
    public static readonly Guid UntypedTypeId = new("00000000-0660-4000-8000-000000000013");
    public static readonly Guid ParticipationVersionId = new("00000000-0660-4000-8000-000000000101");

    private const string Seed = IdentityDatabase.SeedPrincipalId;

    private static string Fixture => $"""
        INSERT INTO identity_access.external_entity (id, code, name_ar, name_en, entity_type_item_id, status, sponsor_user_id, created_at, created_by, updated_at, updated_by)
        SELECT '{EntityB}', 'ENT-PARTICIPATION-B', 'جهة', 'Entity B', i.id, 'ACTIVE', '{IdentityDatabase.UserId(2)}', now(), '{Seed}', now(), '{Seed}'
        FROM master_data_config.master_data_item i JOIN master_data_config.master_data_catalogue c ON c.id = i.catalogue_id AND c.code = 'EXTERNAL_ENTITY_TYPE'
        WHERE i.code = 'GOVERNMENT';

        UPDATE identity_access."user" SET external_entity_id = '{EntityB}' WHERE id = '{IdentityDatabase.UserId(7)}';
        UPDATE identity_access.access_relationship SET status = 'ENDED', ends_at = now(), end_reason = 'MANUAL'
        WHERE id IN ('00000000-0111-4000-8000-000000000007', '00000000-0111-4000-8000-000000000081');

        INSERT INTO identity_access.access_relationship (id, user_id, permission_profile_version_id, external_entity_id, sponsor_user_id, starts_at, status, created_at, created_by, updated_at, updated_by)
        VALUES ('00000000-0660-4000-8000-000000000051', '{IdentityDatabase.UserId(5)}', '{IdentityDatabase.ProfileVersionId(4)}', NULL, NULL, now() - interval '1 day', 'ACTIVE', now(), '{Seed}', now(), '{Seed}');

        INSERT INTO master_data_config.master_data_item (id, catalogue_id, code, label_ar, label_en, sort_order, lifecycle_state, created_at, created_by, updated_at, updated_by)
        SELECT i.id::uuid, c.id, i.code, 'اختبار', i.label_en, i.sort_order, 'PUBLISHED', now(), '{Seed}', now(), '{Seed}'
        FROM (VALUES ('{TaskProgressTypeId}', 'TASK_PROGRESS', 'Task progress', 1),
                     ('{InformationTypeId}', 'PROJECT_INFORMATION', 'Project information', 2),
                     ('{UntypedTypeId}', 'SITE_PHOTOS', 'Site photographs', 3)) AS i (id, code, label_en, sort_order)
        CROSS JOIN master_data_config.master_data_catalogue c WHERE c.code = 'CONTRIBUTION_TYPE';

        INSERT INTO master_data_config.configuration_version (id, configuration_family_id, version_no, lifecycle_state, created_at, created_by, updated_at, updated_by)
        SELECT '{ParticipationVersionId}', f.id, 1, 'DRAFT', now(), '{Seed}', now(), '{Seed}'
        FROM master_data_config.configuration_family f WHERE f.code = 'PARTICIPATION';

        INSERT INTO master_data_config.participation_contribution_rule (id, configuration_version_id, participation_mode, contribution_type_item_id, is_enabled, created_at, created_by, updated_at, updated_by)
        SELECT gen_random_uuid(), '{ParticipationVersionId}', r.mode, r.type_id::uuid, r.enabled, now(), '{Seed}', now(), '{Seed}'
        FROM (VALUES ('ENTITY_MANAGED', '{TaskProgressTypeId}', true), ('AHDA_MANAGED', '{TaskProgressTypeId}', true),
                     ('ENTITY_MANAGED', '{InformationTypeId}', true), ('AHDA_MANAGED', '{InformationTypeId}', false),
                     ('ENTITY_MANAGED', '{UntypedTypeId}', true), ('AHDA_MANAGED', '{UntypedTypeId}', true)) AS r (mode, type_id, enabled);

        UPDATE master_data_config.configuration_version
        SET lifecycle_state = 'PUBLISHED', published_at = now() - interval '1 hour', effective_from = now() - interval '1 hour'
        WHERE id = '{ParticipationVersionId}';
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
public sealed class ExternalParticipationSuite : ICollectionFixture<ExternalParticipationTestHost>
{
    public const string Name = "ExternalParticipation";
}
