using Microsoft.Extensions.DependencyInjection;
using PMPlatform.Application.Features.Schedule;
using PMPlatform.Tests.Integration.Identity;

namespace PMPlatform.Tests.Integration.Schedule;

/// <summary>
/// The identity test database and people (IdentityDatabase) with what WF-03 needs and no environment has yet: test grants for
/// AHDA's side beside the shipped R04 grants — Appendix A grants nothing there (schedule-baseline.md F-2); a published
/// GOVERNANCE_PROFILE version in which STANDARD requires baseline approval and LIGHT does not (ADR-015); a published
/// SCHEDULE_BASELINE route in APPROVAL_AUTHORITY, decided by R02; a WORKFLOW_POLICY with the approval settings and the
/// Schedule Health thresholds, AMBER from 5 and RED from 20 working days late; and <see cref="Rebaselines"/> standing in for
/// WF-08's change authorisations, which TASK-060 has not built (F-1). Projects are inserted, as fixtures may.
/// </summary>
/// <remarks>
/// People: local.r02 views and edits every schedule, decides baseline runs and activates projects (R02, ALL). local.r05 is an
/// internal Project Manager (R04, no anchor). local.r08 is ADR-013's entity Project Manager: external, R04 on the active
/// entity. local.r06 holds R06 only.
/// </remarks>
public sealed class ScheduleTestHost : IAsyncLifetime
{
    public const int AmberDays = 5;
    public const int RedDays = 20;

    public static readonly Guid DepartmentId = new(IdentityDatabase.DepartmentId);
    public static readonly Guid EntityId = new(IdentityDatabase.ActiveEntityId);
    public static readonly Guid ClassificationId = new("00000000-0131-4000-8000-000000000001");

    private const string Seed = IdentityDatabase.SeedPrincipalId;

    private static string RoleId(int n) => $"00000000-0000-4000-8000-{n:D12}";

    private static string Fixture => $"""
        INSERT INTO identity_access.permission_profile_grant (id, permission_profile_version_id, permission_id, data_scope, created_at, created_by, updated_at, updated_by)
        SELECT gen_random_uuid(), g.version_id::uuid, p.id, g.scope, now(), '{Seed}', now(), '{Seed}'
        FROM (VALUES ('{IdentityDatabase.ProfileVersionId(2)}', 'SCHEDULE_VIEW', 'ALL'), ('{IdentityDatabase.ProfileVersionId(2)}', 'SCHEDULE_EDIT', 'ALL'),
                     ('{IdentityDatabase.ProfileVersionId(2)}', 'APPROVAL_DECIDE', 'ALL'), ('{IdentityDatabase.ProfileVersionId(2)}', 'APPROVAL_VIEW', 'ALL'),
                     ('{IdentityDatabase.ProfileVersionId(2)}', 'PROJECT_VIEW', 'ALL'), ('{IdentityDatabase.ProfileVersionId(2)}', 'PROJECT_ACTIVATE', 'ALL'))
             AS g (version_id, code, scope)
        JOIN identity_access.permission p ON p.code = g.code;

        INSERT INTO identity_access.access_relationship (id, user_id, permission_profile_version_id, external_entity_id, sponsor_user_id, starts_at, status, created_at, created_by, updated_at, updated_by)
        VALUES ('00000000-0460-4000-8000-000000000051', '{IdentityDatabase.UserId(5)}', '{IdentityDatabase.ProfileVersionId(4)}', NULL, NULL, now() - interval '1 day', 'ACTIVE', now(), '{Seed}', now(), '{Seed}'),
               ('00000000-0460-4000-8000-000000000081', '{IdentityDatabase.UserId(8)}', '{IdentityDatabase.ProfileVersionId(4)}', '{EntityId}', '{IdentityDatabase.UserId(2)}', now() - interval '1 day', 'ACTIVE', now(), '{Seed}', now(), '{Seed}');

        INSERT INTO master_data_config.master_data_item (id, catalogue_id, code, label_ar, label_en, lifecycle_state, created_at, created_by, updated_at, updated_by)
        SELECT '00000000-0460-4000-8000-000000000011', c.id, 'TEST_CONTROL', 'اختبار', 'Test control level', 'PUBLISHED', now(), '{Seed}', now(), '{Seed}'
        FROM master_data_config.master_data_catalogue c WHERE c.code = 'DOCUMENT_CONTROL_LEVEL';

        INSERT INTO master_data_config.configuration_version (id, configuration_family_id, version_no, lifecycle_state, created_at, created_by, updated_at, updated_by)
        SELECT v.id, f.id, 1, 'DRAFT', now(), '{Seed}', now(), '{Seed}'
        FROM (VALUES ('00000000-0460-4000-8000-000000000101'::uuid, 'GOVERNANCE_PROFILE'),
                     ('00000000-0460-4000-8000-000000000102'::uuid, 'WORKFLOW_POLICY'),
                     ('00000000-0460-4000-8000-000000000103'::uuid, 'APPROVAL_AUTHORITY')) AS v (id, family)
        JOIN master_data_config.configuration_family f ON f.code = v.family;

        INSERT INTO master_data_config.governance_profile_setting (id, configuration_version_id, governance_profile_item_id, requires_baseline_approval, risk_management_required,
                                                                   change_band_count, update_cadence_days, document_control_level_item_id, included_in_reporting,
                                                                   created_at, created_by, updated_at, updated_by)
        SELECT gen_random_uuid(), '00000000-0460-4000-8000-000000000101', i.id, i.code <> 'LIGHT', i.code <> 'LIGHT', 3, 7, '00000000-0460-4000-8000-000000000011', true,
               now(), '{Seed}', now(), '{Seed}'
        FROM master_data_config.master_data_item i JOIN master_data_config.master_data_catalogue c ON c.id = i.catalogue_id
        WHERE c.code = 'GOVERNANCE_PROFILE' AND i.code IN ('LIGHT', 'STANDARD');

        INSERT INTO master_data_config.configuration_value (id, configuration_version_id, value_key, value_text, value_type, created_at, created_by, updated_at, updated_by)
        VALUES (gen_random_uuid(), '00000000-0460-4000-8000-000000000102', 'APPROVAL_TASK_DUE_DAYS', '3', 'DURATION_DAYS', now(), '{Seed}', now(), '{Seed}'),
               (gen_random_uuid(), '00000000-0460-4000-8000-000000000102', 'APPROVAL_ESCALATION_ROLE', 'R02', 'TEXT', now(), '{Seed}', now(), '{Seed}'),
               (gen_random_uuid(), '00000000-0460-4000-8000-000000000102', 'SCHEDULE_HEALTH_AMBER_FINISH_VARIANCE_DAYS', '{AmberDays}', 'DURATION_DAYS', now(), '{Seed}', now(), '{Seed}'),
               (gen_random_uuid(), '00000000-0460-4000-8000-000000000102', 'SCHEDULE_HEALTH_RED_FINISH_VARIANCE_DAYS', '{RedDays}', 'DURATION_DAYS', now(), '{Seed}', now(), '{Seed}');

        INSERT INTO master_data_config.approval_authority_rule (id, configuration_version_id, subject_type_code, sequence_no, approver_role_id, is_mandatory, created_at, created_by, updated_at, updated_by)
        VALUES (gen_random_uuid(), '00000000-0460-4000-8000-000000000103', 'SCHEDULE_BASELINE', 1, '{RoleId(2)}', true, now(), '{Seed}', now(), '{Seed}');

        UPDATE master_data_config.configuration_version
        SET lifecycle_state = 'PUBLISHED', published_at = now() - interval '1 hour', effective_from = now() - interval '1 hour'
        WHERE id IN ('00000000-0460-4000-8000-000000000101', '00000000-0460-4000-8000-000000000102', '00000000-0460-4000-8000-000000000103');
        """;

    public IdentityTestHost Identity { get; } = new();

    public IdentityDatabase Database => Identity.Database;

    public IdentityApiFactory Api { get; private set; } = null!;

    /// <summary>The WF-08 change authorisations a test declares applicable; none until it says.</summary>
    internal FakeRebaselineAuthorization Rebaselines { get; } = new();

    public Guid StandardProfileId { get; private set; }

    public Guid LightProfileId { get; private set; }

    public async Task InitializeAsync()
    {
        await Identity.InitializeAsync();
        await Database.ExecuteAsync(Fixture);
        StandardProfileId = await ProfileAsync("STANDARD");
        LightProfileId = await ProfileAsync("LIGHT");
        Api = Identity.CreateApi(
            new Dictionary<string, string?>
            {
                ["Outbox:PollInterval"] = "01:00:00",
                ["Approval:Maintenance:PollInterval"] = "01:00:00",
                ["DocumentManagement:Scan:PollInterval"] = "01:00:00",
            },
            services => services.AddSingleton<IRebaselineAuthorization>(Rebaselines));
    }

    public async Task DisposeAsync()
    {
        await Api.DisposeAsync();
        await Identity.DisposeAsync();
    }

    private async Task<Guid> ProfileAsync(string code) => Guid.Parse(Assert.Single(await Database.QueryAsync($"""
        SELECT i.id::text FROM master_data_config.master_data_item i
        JOIN master_data_config.master_data_catalogue c ON c.id = i.catalogue_id
        WHERE c.code = 'GOVERNANCE_PROFILE' AND i.code = '{code}'
        """)));
}

[CollectionDefinition(Name)]
public sealed class ScheduleSuite : ICollectionFixture<ScheduleTestHost>
{
    public const string Name = "Schedule";
}
