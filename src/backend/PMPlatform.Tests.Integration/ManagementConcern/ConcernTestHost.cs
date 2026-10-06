using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PMPlatform.Infrastructure.Notifications;
using PMPlatform.Infrastructure.Persistence.Messaging;
using PMPlatform.Tests.Integration.Identity;
using PMPlatform.Tests.Integration.MasterDataConfig;

namespace PMPlatform.Tests.Integration.ManagementConcern;

/// <summary>
/// The identity test database and people (IdentityDatabase) with what WF-07 needs and no environment has yet: test grants for
/// AHDA's side beside the shipped grants — Appendix A grants nothing more (management-concern.md F-2); PUBLISHED items of
/// CONCERN_CATEGORY, PRIORITY, CONCERN_SEVERITY and RISK_CATEGORY; a GOVERNANCE_PROFILE whose cadences differ by profile (ADR-015);
/// a WORKFLOW_POLICY routing concern escalations to R03; an APPROVAL_AUTHORITY routing resolutions to R02; a NOTIFICATION_ROUTING
/// family CONCERN_ESCALATION, in-app to R03, with its template; and a RISK_MATRIX version, published through FG-04's own author,
/// reviewer and publisher, whose values map overall levels 1–2 to MINOR, 3–4 to MAJOR and 5 to CRITICAL. Projects are inserted, as
/// fixtures may. The outbox and notification workers are removed, so a test dispatches and processes when it says.
/// </summary>
/// <remarks>
/// People: local.r08 is ADR-013's entity Project Manager — external, R04 on the active entity — and an R08 entity user. local.r05
/// is an internal Project Manager (R04, no anchor). local.r02 is AHDA's issue officer and validator: CONCERN_VIEW, CONCERN_MANAGE,
/// CONCERN_ESCALATE, CONCERN_ESCALATION_RESOLVE, APPROVAL_VIEW, APPROVAL_DECIDE, RISK_VIEW and RISK_ASSESS at ALL, all through R02.
/// local.r03 is the Department Manager, with the shipped R03 grants only. local.r01, r02 and r03 also author, review and publish
/// configuration (R01).
/// </remarks>
public sealed class ConcernTestHost : IAsyncLifetime
{
    public static readonly Guid DepartmentId = new(IdentityDatabase.DepartmentId);
    public static readonly Guid EntityId = new(IdentityDatabase.ActiveEntityId);
    public static readonly Guid ClassificationId = new("00000000-0131-4000-8000-000000000001");
    public static readonly Guid CategoryId = new("00000000-0570-4000-8000-000000000001");
    public static readonly Guid DraftCategoryId = new("00000000-0570-4000-8000-000000000002");
    public static readonly Guid HighPriorityId = new("00000000-0570-4000-8000-000000000003");
    public static readonly Guid LowPriorityId = new("00000000-0570-4000-8000-000000000004");
    public static readonly Guid MinorId = new("00000000-0570-4000-8000-000000000005");
    public static readonly Guid MajorId = new("00000000-0570-4000-8000-000000000006");
    public static readonly Guid CriticalId = new("00000000-0570-4000-8000-000000000007");
    public static readonly Guid RiskCategoryId = new("00000000-0570-4000-8000-000000000008");

    /// <summary>The review cadence of each profile, in days (ADR-015): different, so a test can tell which one a concern follows.</summary>
    public const int StandardCadenceDays = 7;
    public const int LightCadenceDays = 30;

    private const string Seed = IdentityDatabase.SeedPrincipalId;

    private static string RoleId(int n) => $"00000000-0000-4000-8000-{n:D12}";

    private static string Fixture => $$$"""
        INSERT INTO identity_access.permission_profile_grant (id, permission_profile_version_id, permission_id, data_scope, created_at, created_by, updated_at, updated_by)
        SELECT gen_random_uuid(), '{{{IdentityDatabase.ProfileVersionId(2)}}}', p.id, 'ALL', now(), '{{{Seed}}}', now(), '{{{Seed}}}'
        FROM identity_access.permission p
        WHERE p.code IN ('CONCERN_VIEW', 'CONCERN_MANAGE', 'CONCERN_ESCALATE', 'CONCERN_ESCALATION_RESOLVE', 'APPROVAL_VIEW', 'APPROVAL_DECIDE', 'RISK_VIEW', 'RISK_ASSESS');

        INSERT INTO identity_access.access_relationship (id, user_id, permission_profile_version_id, external_entity_id, sponsor_user_id, starts_at, status, created_at, created_by, updated_at, updated_by)
        VALUES ('00000000-0570-4000-8000-000000000051', '{{{IdentityDatabase.UserId(5)}}}', '{{{IdentityDatabase.ProfileVersionId(4)}}}', NULL, NULL, now() - interval '1 day', 'ACTIVE', now(), '{{{Seed}}}', now(), '{{{Seed}}}'),
               ('00000000-0570-4000-8000-000000000081', '{{{IdentityDatabase.UserId(8)}}}', '{{{IdentityDatabase.ProfileVersionId(4)}}}', '{{{EntityId}}}', '{{{IdentityDatabase.UserId(2)}}}', now() - interval '1 day', 'ACTIVE', now(), '{{{Seed}}}', now(), '{{{Seed}}}'),
               ('00000000-0570-4000-8000-000000000021', '{{{IdentityDatabase.UserId(2)}}}', '{{{IdentityDatabase.ProfileVersionId(1)}}}', NULL, NULL, now() - interval '1 day', 'ACTIVE', now(), '{{{Seed}}}', now(), '{{{Seed}}}'),
               ('00000000-0570-4000-8000-000000000031', '{{{IdentityDatabase.UserId(3)}}}', '{{{IdentityDatabase.ProfileVersionId(1)}}}', NULL, NULL, now() - interval '1 day', 'ACTIVE', now(), '{{{Seed}}}', now(), '{{{Seed}}}');

        INSERT INTO master_data_config.master_data_item (id, catalogue_id, code, label_ar, label_en, sort_order, lifecycle_state, created_at, created_by, updated_at, updated_by)
        SELECT i.id::uuid, c.id, i.code, 'اختبار', i.label_en, i.sort_order, i.state, now(), '{{{Seed}}}', now(), '{{{Seed}}}'
        FROM (VALUES ('{{{CategoryId}}}', 'CONCERN_CATEGORY', 'TEST_DELIVERY', 'Delivery', 0, 'PUBLISHED'),
                     ('{{{DraftCategoryId}}}', 'CONCERN_CATEGORY', 'TEST_DRAFT', 'Draft category', 0, 'DRAFT'),
                     ('{{{HighPriorityId}}}', 'PRIORITY', 'TEST_HIGH', 'High', 1, 'PUBLISHED'),
                     ('{{{LowPriorityId}}}', 'PRIORITY', 'TEST_LOW', 'Low', 2, 'PUBLISHED'),
                     ('{{{MinorId}}}', 'CONCERN_SEVERITY', 'TEST_MINOR', 'Minor', 1, 'PUBLISHED'),
                     ('{{{MajorId}}}', 'CONCERN_SEVERITY', 'TEST_MAJOR', 'Major', 2, 'PUBLISHED'),
                     ('{{{CriticalId}}}', 'CONCERN_SEVERITY', 'TEST_CRITICAL', 'Critical', 3, 'PUBLISHED'),
                     ('{{{RiskCategoryId}}}', 'RISK_CATEGORY', 'TEST_RISK', 'Delivery risk', 0, 'PUBLISHED'),
                     ('00000000-0570-4000-8000-000000000014', 'DOCUMENT_CONTROL_LEVEL', 'TEST_CONTROL', 'Test control level', 0, 'PUBLISHED'))
             AS i (id, catalogue, code, label_en, sort_order, state)
        JOIN master_data_config.master_data_catalogue c ON c.code = i.catalogue;

        INSERT INTO master_data_config.configuration_version (id, configuration_family_id, version_no, lifecycle_state, created_at, created_by, updated_at, updated_by)
        SELECT v.id, f.id, 1, 'DRAFT', now(), '{{{Seed}}}', now(), '{{{Seed}}}'
        FROM (VALUES ('00000000-0570-4000-8000-000000000101'::uuid, 'GOVERNANCE_PROFILE'),
                     ('00000000-0570-4000-8000-000000000102'::uuid, 'WORKFLOW_POLICY'),
                     ('00000000-0570-4000-8000-000000000103'::uuid, 'APPROVAL_AUTHORITY'),
                     ('00000000-0570-4000-8000-000000000104'::uuid, 'NOTIFICATION_ROUTING')) AS v (id, family)
        JOIN master_data_config.configuration_family f ON f.code = v.family;

        INSERT INTO master_data_config.governance_profile_setting (id, configuration_version_id, governance_profile_item_id, requires_baseline_approval, risk_management_required,
                                                                   change_band_count, update_cadence_days, document_control_level_item_id, included_in_reporting,
                                                                   created_at, created_by, updated_at, updated_by)
        SELECT gen_random_uuid(), '00000000-0570-4000-8000-000000000101', i.id, true, i.code <> 'LIGHT', 3,
               CASE i.code WHEN 'LIGHT' THEN {{{LightCadenceDays}}} ELSE {{{StandardCadenceDays}}} END,
               '00000000-0570-4000-8000-000000000014', true, now(), '{{{Seed}}}', now(), '{{{Seed}}}'
        FROM master_data_config.master_data_item i JOIN master_data_config.master_data_catalogue c ON c.id = i.catalogue_id
        WHERE c.code = 'GOVERNANCE_PROFILE';

        INSERT INTO master_data_config.configuration_value (id, configuration_version_id, value_key, value_text, value_type, created_at, created_by, updated_at, updated_by)
        VALUES (gen_random_uuid(), '00000000-0570-4000-8000-000000000102', 'APPROVAL_TASK_DUE_DAYS', '3', 'DURATION_DAYS', now(), '{{{Seed}}}', now(), '{{{Seed}}}'),
               (gen_random_uuid(), '00000000-0570-4000-8000-000000000102', 'APPROVAL_ESCALATION_ROLE', 'R02', 'TEXT', now(), '{{{Seed}}}', now(), '{{{Seed}}}'),
               (gen_random_uuid(), '00000000-0570-4000-8000-000000000102', 'CONCERN_ESCALATION_ROLE', 'R03', 'TEXT', now(), '{{{Seed}}}', now(), '{{{Seed}}}');

        INSERT INTO master_data_config.approval_authority_rule (id, configuration_version_id, subject_type_code, sequence_no, approver_role_id, is_mandatory, created_at, created_by, updated_at, updated_by)
        VALUES (gen_random_uuid(), '00000000-0570-4000-8000-000000000103', 'MANAGEMENT_CONCERN_RESOLUTION', 1, '{{{RoleId(2)}}}', true, now(), '{{{Seed}}}', now(), '{{{Seed}}}');

        INSERT INTO master_data_config.notification_event_family (id, configuration_version_id, code, label_ar, label_en, is_mandatory, created_at, created_by, updated_at, updated_by)
        VALUES ('00000000-0570-4000-8000-000000000111', '00000000-0570-4000-8000-000000000104', 'CONCERN_ESCALATION', 'تصعيد', 'Concern escalated', true, now(), '{{{Seed}}}', now(), '{{{Seed}}}');
        INSERT INTO master_data_config.notification_channel_rule (id, notification_event_family_id, channel, enabled_by_default, created_at, created_by, updated_at, updated_by)
        VALUES (gen_random_uuid(), '00000000-0570-4000-8000-000000000111', 'IN_APP', true, now(), '{{{Seed}}}', now(), '{{{Seed}}}');
        INSERT INTO master_data_config.notification_recipient_rule (id, notification_event_family_id, role_id, created_at, created_by, updated_at, updated_by)
        VALUES (gen_random_uuid(), '00000000-0570-4000-8000-000000000111', '{{{RoleId(3)}}}', now(), '{{{Seed}}}', now(), '{{{Seed}}}');

        UPDATE master_data_config.configuration_version
        SET lifecycle_state = 'PUBLISHED', published_at = now() - interval '1 hour', effective_from = now() - interval '1 hour'
        WHERE id IN ('00000000-0570-4000-8000-000000000101', '00000000-0570-4000-8000-000000000102', '00000000-0570-4000-8000-000000000103',
                     '00000000-0570-4000-8000-000000000104');

        INSERT INTO notifications.notification_template (id, event_family_code, event_type, channel, version_no, subject_ar, subject_en, body_ar, body_en,
                                                         lifecycle_state, created_at, created_by, updated_at, updated_by)
        VALUES (gen_random_uuid(), 'CONCERN_ESCALATION', 'ManagementConcern.ConcernEscalated', 'IN_APP', 1, 'تصعيد رقم {{escalationNo}}', 'Escalation {{escalationNo}}',
                'تم تصعيد {{concernType}} بخطورة {{severity}}.', 'A {{concernType}} was escalated to {{escalatedToRoleCode}} with severity {{severity}}.', 'DRAFT',
                now(), '{{{IdentityDatabase.UserId(1)}}}', now(), '{{{IdentityDatabase.UserId(1)}}}');
        UPDATE notifications.notification_template SET lifecycle_state = 'VALIDATED', validated_by_user_id = '{{{IdentityDatabase.UserId(2)}}}', validated_at = now();
        UPDATE notifications.notification_template SET lifecycle_state = 'PUBLISHED', published_by_user_id = '{{{IdentityDatabase.UserId(3)}}}', published_at = now();
        """;

    public IdentityTestHost Identity { get; } = new();

    public IdentityDatabase Database => Identity.Database;

    public IdentityApiFactory Api { get; private set; } = null!;

    public AdjustableTimeProvider Clock => Identity.Clock;

    /// <summary>The RISK_MATRIX version in force: the one every severity computed now pins.</summary>
    public Guid ScaleVersionId { get; private set; }

    public async Task InitializeAsync()
    {
        await Identity.InitializeAsync();
        await Database.ExecuteAsync(Fixture);
        Api = Identity.CreateApi(
            new Dictionary<string, string?>
            {
                ["Approval:Maintenance:PollInterval"] = "01:00:00",
                ["DocumentManagement:Scan:PollInterval"] = "01:00:00",
                ["Risk:Maintenance:PollInterval"] = "01:00:00",
            },
            services =>
            {
                foreach (ServiceDescriptor worker in services.Where(d => d.ServiceType == typeof(IHostedService)
                                                                        && (d.ImplementationType == typeof(NotificationWorker) || d.ImplementationType == typeof(OutboxDispatchWorker))).ToList())
                {
                    services.Remove(worker);
                }
            });

        using HttpClient client = Api.CreateClient();
        ScaleVersionId = await this.PublishScaleAsync(client, await client.SignInCrewAsync(), ConcernDriver.DefaultMapping);
    }

    public async Task DisposeAsync()
    {
        await Api.DisposeAsync();
        await Identity.DisposeAsync();
    }
}

[CollectionDefinition(Name)]
public sealed class ConcernSuite : ICollectionFixture<ConcernTestHost>
{
    public const string Name = "ManagementConcern";
}
