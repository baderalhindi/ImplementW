using Microsoft.Extensions.DependencyInjection;
using PMPlatform.Application.Features.ManagementConcern.Contracts;
using PMPlatform.Tests.Integration.Identity;
using PMPlatform.Tests.Integration.MasterDataConfig;

namespace PMPlatform.Tests.Integration.Risk;

/// <summary>
/// The identity test database and people (IdentityDatabase) with what WF-06 needs and no environment has yet: test grants for
/// AHDA's side beside the shipped R04 grants — Appendix A grants nothing there (risk-management.md F-2); a published
/// GOVERNANCE_PROFILE in which STANDARD carries risk management and LIGHT does not (ADR-015); a PUBLISHED and a DRAFT item of
/// RISK_CATEGORY; and a RISK_MATRIX version published through FG-04's own author, reviewer and publisher. WF-07 is not built, so
/// <see cref="TestIssueRegister"/> plays it (risk-management.md F-1). Projects are inserted, as fixtures may.
/// </summary>
/// <remarks>
/// People: local.r08 is ADR-013's entity Project Manager — external, R04 on the active entity. local.r05 is an internal Project
/// Manager (R04, no anchor). R04 holds RISK_ASSESS and RISK_ACCEPT at OWN by test grant, so the two differ only in being internal.
/// local.r02 is AHDA's risk officer: RISK_VIEW, RISK_MANAGE, RISK_ASSESS and RISK_ACCEPT at ALL, and no RISK_REOPEN. local.r03 holds
/// RISK_VIEW and RISK_REOPEN for its department. local.r01, r02 and r03 also author, review and publish configuration (R01).
/// </remarks>
public sealed class RiskTestHost : IAsyncLifetime
{
    public static readonly Guid DepartmentId = new(IdentityDatabase.DepartmentId);
    public static readonly Guid EntityId = new(IdentityDatabase.ActiveEntityId);
    public static readonly Guid ClassificationId = new("00000000-0131-4000-8000-000000000001");
    public static readonly Guid CategoryId = new("00000000-0550-4000-8000-000000000001");
    public static readonly Guid DraftCategoryId = new("00000000-0550-4000-8000-000000000002");
    public static readonly Guid ConcernCategoryId = new("00000000-0550-4000-8000-000000000003");
    public static readonly Guid PriorityId = new("00000000-0550-4000-8000-000000000004");

    /// <summary>A concern category the test issue register refuses, as WF-07 would refuse one of its own rules.</summary>
    public static readonly Guid RefusedConcernCategoryId = new("00000000-0550-4000-8000-000000000005");

    private const string Seed = IdentityDatabase.SeedPrincipalId;

    private static string Fixture => $"""
        INSERT INTO identity_access.permission_profile_grant (id, permission_profile_version_id, permission_id, data_scope, created_at, created_by, updated_at, updated_by)
        SELECT gen_random_uuid(), g.version_id::uuid, p.id, g.scope, now(), '{Seed}', now(), '{Seed}'
        FROM (VALUES ('{IdentityDatabase.ProfileVersionId(2)}', 'RISK_VIEW', 'ALL'), ('{IdentityDatabase.ProfileVersionId(2)}', 'RISK_MANAGE', 'ALL'),
                     ('{IdentityDatabase.ProfileVersionId(2)}', 'RISK_ASSESS', 'ALL'), ('{IdentityDatabase.ProfileVersionId(2)}', 'RISK_ACCEPT', 'ALL'),
                     ('{IdentityDatabase.ProfileVersionId(3)}', 'RISK_VIEW', 'DEPT'), ('{IdentityDatabase.ProfileVersionId(3)}', 'RISK_REOPEN', 'DEPT'),
                     ('{IdentityDatabase.ProfileVersionId(4)}', 'RISK_ASSESS', 'OWN'), ('{IdentityDatabase.ProfileVersionId(4)}', 'RISK_ACCEPT', 'OWN'))
             AS g (version_id, code, scope)
        JOIN identity_access.permission p ON p.code = g.code;

        INSERT INTO identity_access.access_relationship (id, user_id, permission_profile_version_id, external_entity_id, sponsor_user_id, starts_at, status, created_at, created_by, updated_at, updated_by)
        VALUES ('00000000-0550-4000-8000-000000000051', '{IdentityDatabase.UserId(5)}', '{IdentityDatabase.ProfileVersionId(4)}', NULL, NULL, now() - interval '1 day', 'ACTIVE', now(), '{Seed}', now(), '{Seed}'),
               ('00000000-0550-4000-8000-000000000081', '{IdentityDatabase.UserId(8)}', '{IdentityDatabase.ProfileVersionId(4)}', '{EntityId}', '{IdentityDatabase.UserId(2)}', now() - interval '1 day', 'ACTIVE', now(), '{Seed}', now(), '{Seed}'),
               ('00000000-0550-4000-8000-000000000021', '{IdentityDatabase.UserId(2)}', '{IdentityDatabase.ProfileVersionId(1)}', NULL, NULL, now() - interval '1 day', 'ACTIVE', now(), '{Seed}', now(), '{Seed}'),
               ('00000000-0550-4000-8000-000000000031', '{IdentityDatabase.UserId(3)}', '{IdentityDatabase.ProfileVersionId(1)}', NULL, NULL, now() - interval '1 day', 'ACTIVE', now(), '{Seed}', now(), '{Seed}');

        INSERT INTO master_data_config.master_data_item (id, catalogue_id, code, label_ar, label_en, sort_order, lifecycle_state, created_at, created_by, updated_at, updated_by)
        SELECT i.id::uuid, c.id, i.code, 'اختبار', i.label_en, 0, i.state, now(), '{Seed}', now(), '{Seed}'
        FROM (VALUES ('{CategoryId}', 'RISK_CATEGORY', 'TEST_DELIVERY', 'Delivery', 'PUBLISHED'),
                     ('{DraftCategoryId}', 'RISK_CATEGORY', 'TEST_DRAFT', 'Draft category', 'DRAFT'),
                     ('{ConcernCategoryId}', 'CONCERN_CATEGORY', 'TEST_CONCERN', 'Concern', 'PUBLISHED'),
                     ('{RefusedConcernCategoryId}', 'CONCERN_CATEGORY', 'TEST_REFUSED', 'Refused concern', 'PUBLISHED'),
                     ('{PriorityId}', 'PRIORITY', 'TEST_URGENT', 'Urgent', 'PUBLISHED'),
                     ('00000000-0550-4000-8000-000000000014', 'DOCUMENT_CONTROL_LEVEL', 'TEST_CONTROL', 'Test control level', 'PUBLISHED'))
             AS i (id, catalogue, code, label_en, state)
        JOIN master_data_config.master_data_catalogue c ON c.code = i.catalogue;

        INSERT INTO master_data_config.configuration_version (id, configuration_family_id, version_no, lifecycle_state, created_at, created_by, updated_at, updated_by)
        SELECT '00000000-0550-4000-8000-000000000101', f.id, 1, 'DRAFT', now(), '{Seed}', now(), '{Seed}'
        FROM master_data_config.configuration_family f WHERE f.code = 'GOVERNANCE_PROFILE';

        INSERT INTO master_data_config.governance_profile_setting (id, configuration_version_id, governance_profile_item_id, requires_baseline_approval, risk_management_required,
                                                                   change_band_count, update_cadence_days, document_control_level_item_id, included_in_reporting,
                                                                   created_at, created_by, updated_at, updated_by)
        SELECT gen_random_uuid(), '00000000-0550-4000-8000-000000000101', i.id, true, i.code <> 'LIGHT', 3, 7, '00000000-0550-4000-8000-000000000014', true, now(), '{Seed}', now(), '{Seed}'
        FROM master_data_config.master_data_item i JOIN master_data_config.master_data_catalogue c ON c.id = i.catalogue_id
        WHERE c.code = 'GOVERNANCE_PROFILE';

        UPDATE master_data_config.configuration_version
        SET lifecycle_state = 'PUBLISHED', published_at = now() - interval '1 hour', effective_from = now() - interval '1 hour'
        WHERE id = '00000000-0550-4000-8000-000000000101';

        -- WF-07's table as the test issue register keeps it: the one foreign key both sides query (ERD §5.10).
        CREATE SCHEMA test_issue;
        CREATE TABLE test_issue.issue (
            id uuid PRIMARY KEY,
            project_id uuid NOT NULL,
            originating_risk_id uuid NOT NULL REFERENCES risk.risk (id),
            status text NOT NULL,
            raised_by_user_id uuid NOT NULL,
            raised_at timestamptz NOT NULL);
        """;

    public IdentityTestHost Identity { get; } = new();

    public IdentityDatabase Database => Identity.Database;

    public IdentityApiFactory Api { get; private set; } = null!;

    public AdjustableTimeProvider Clock => Identity.Clock;

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
                ["Risk:Maintenance:PollInterval"] = "01:00:00",
            },
            services => services.AddScoped<IRiskIssueMaterialisation, TestIssueRegister>());

        using HttpClient client = Api.CreateClient();
        await this.PublishMatrixAsync(client, await client.SignInCrewAsync(), highFrom: 12);
    }

    public async Task DisposeAsync()
    {
        await Api.DisposeAsync();
        await Identity.DisposeAsync();
    }
}

[CollectionDefinition(Name)]
public sealed class RiskSuite : ICollectionFixture<RiskTestHost>
{
    public const string Name = "Risk";
}
