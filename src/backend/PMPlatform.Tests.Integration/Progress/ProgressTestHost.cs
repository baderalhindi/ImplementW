using Microsoft.Extensions.DependencyInjection;
using PMPlatform.Application.Features.Progress;
using PMPlatform.Tests.Integration.Identity;
using PMPlatform.Tests.Integration.Schedule;

namespace PMPlatform.Tests.Integration.Progress;

/// <summary>
/// The identity test database and people (IdentityDatabase) with what WF-02 needs and no environment has yet: test grants
/// for AHDA's side — Appendix A grants no review (progress-update.md F-2) — beside the shipped R04 and R08 grants; a
/// published GOVERNANCE_PROFILE version giving STANDARD a 7-day cadence; a published WORKFLOW_POLICY version holding the
/// health thresholds, AMBER from 5 and RED from 15 points of slippage; and <see cref="Inputs"/> standing in for WF-03, WF-04
/// and WF-14, which are not connected (F-1), with the baseline they report written ACTIVE so that progress_submission.baseline_id's
/// key holds. Projects are inserted ACTIVE, as fixtures may: an INSERT is no lifecycle transition.
/// </summary>
/// <remarks>
/// People: local.r02 views, submits and reviews everything (R02, ALL). local.r03 views and reviews for their department (R03,
/// DEPT). local.r05 is an internal Project Manager (R04, no anchor). local.r08 is ADR-013's entity Project Manager: external,
/// R08 and R04 on the active entity — and the test R04 profile also holds review, so the person check is what refuses them.
/// local.r06 holds R06 only.
/// </remarks>
public sealed class ProgressTestHost : IAsyncLifetime
{
    public const int CadenceDays = 7;
    public const decimal AmberSlippage = 5;
    public const decimal RedSlippage = 15;

    public static readonly Guid DepartmentId = new(IdentityDatabase.DepartmentId);
    public static readonly Guid EntityId = new(IdentityDatabase.ActiveEntityId);
    public static readonly Guid ClassificationId = new("00000000-0131-4000-8000-000000000001");
    public static readonly Guid WorkflowPolicyVersionId = new("00000000-0440-4000-8000-000000000102");

    private const string Seed = IdentityDatabase.SeedPrincipalId;

    private static string Fixture => $"""
        INSERT INTO identity_access.permission_profile_grant (id, permission_profile_version_id, permission_id, data_scope, created_at, created_by, updated_at, updated_by)
        SELECT gen_random_uuid(), g.version_id::uuid, p.id, g.scope, now(), '{Seed}', now(), '{Seed}'
        FROM (VALUES ('{IdentityDatabase.ProfileVersionId(2)}', 'PROGRESS_VIEW', 'ALL'), ('{IdentityDatabase.ProfileVersionId(2)}', 'PROGRESS_SUBMIT', 'ALL'),
                     ('{IdentityDatabase.ProfileVersionId(2)}', 'PROGRESS_REVIEW', 'ALL'),
                     ('{IdentityDatabase.ProfileVersionId(3)}', 'PROGRESS_VIEW', 'DEPT'), ('{IdentityDatabase.ProfileVersionId(3)}', 'PROGRESS_REVIEW', 'DEPT'),
                     ('{IdentityDatabase.ProfileVersionId(4)}', 'PROGRESS_REVIEW', 'ENTITY'))
             AS g (version_id, code, scope)
        JOIN identity_access.permission p ON p.code = g.code;

        -- local.r05: an internal Project Manager. local.r08: R04 across their entity, beside the per-project one IdentityDatabase gives.
        INSERT INTO identity_access.access_relationship (id, user_id, permission_profile_version_id, external_entity_id, sponsor_user_id, starts_at, status, created_at, created_by, updated_at, updated_by)
        VALUES ('00000000-0440-4000-8000-000000000051', '{IdentityDatabase.UserId(5)}', '{IdentityDatabase.ProfileVersionId(4)}', NULL, NULL, now() - interval '1 day', 'ACTIVE', now(), '{Seed}', now(), '{Seed}'),
               ('00000000-0440-4000-8000-000000000081', '{IdentityDatabase.UserId(8)}', '{IdentityDatabase.ProfileVersionId(4)}', '{EntityId}', '{IdentityDatabase.UserId(2)}', now() - interval '1 day', 'ACTIVE', now(), '{Seed}', now(), '{Seed}');

        INSERT INTO master_data_config.master_data_item (id, catalogue_id, code, label_ar, label_en, lifecycle_state, created_at, created_by, updated_at, updated_by)
        SELECT '00000000-0440-4000-8000-000000000011', c.id, 'TEST_CONTROL', 'اختبار', 'Test control level', 'PUBLISHED', now(), '{Seed}', now(), '{Seed}'
        FROM master_data_config.master_data_catalogue c WHERE c.code = 'DOCUMENT_CONTROL_LEVEL';

        INSERT INTO master_data_config.configuration_version (id, configuration_family_id, version_no, lifecycle_state, created_at, created_by, updated_at, updated_by)
        SELECT v.id, f.id, 1, 'DRAFT', now(), '{Seed}', now(), '{Seed}'
        FROM (VALUES ('00000000-0440-4000-8000-000000000101'::uuid, 'GOVERNANCE_PROFILE'),
                     ('{WorkflowPolicyVersionId}'::uuid, 'WORKFLOW_POLICY')) AS v (id, family)
        JOIN master_data_config.configuration_family f ON f.code = v.family;

        INSERT INTO master_data_config.governance_profile_setting (id, configuration_version_id, governance_profile_item_id, requires_baseline_approval, risk_management_required,
                                                                   change_band_count, update_cadence_days, document_control_level_item_id, included_in_reporting,
                                                                   created_at, created_by, updated_at, updated_by)
        SELECT gen_random_uuid(), '00000000-0440-4000-8000-000000000101', i.id, true, true, 3, {CadenceDays}, '00000000-0440-4000-8000-000000000011', true, now(), '{Seed}', now(), '{Seed}'
        FROM master_data_config.master_data_item i JOIN master_data_config.master_data_catalogue c ON c.id = i.catalogue_id
        WHERE c.code = 'GOVERNANCE_PROFILE' AND i.code = 'STANDARD';

        INSERT INTO master_data_config.configuration_value (id, configuration_version_id, value_key, value_text, value_type, created_at, created_by, updated_at, updated_by)
        VALUES (gen_random_uuid(), '{WorkflowPolicyVersionId}', 'PROGRESS_HEALTH_AMBER_SLIPPAGE_PERCENT', '{AmberSlippage}', 'PERCENT', now(), '{Seed}', now(), '{Seed}'),
               (gen_random_uuid(), '{WorkflowPolicyVersionId}', 'PROGRESS_HEALTH_RED_SLIPPAGE_PERCENT', '{RedSlippage}', 'PERCENT', now(), '{Seed}', now(), '{Seed}');

        UPDATE master_data_config.configuration_version
        SET lifecycle_state = 'PUBLISHED', published_at = now() - interval '1 hour', effective_from = now() - interval '1 hour'
        WHERE id IN ('00000000-0440-4000-8000-000000000101', '{WorkflowPolicyVersionId}');
        """;

    public IdentityTestHost Identity { get; } = new();

    public IdentityDatabase Database => Identity.Database;

    public IdentityApiFactory Api { get; private set; } = null!;

    /// <summary>What WF-03, WF-04 and WF-14 would give, per project; nothing until a test says.</summary>
    internal FakeProgressInputs Inputs { get; } = new();

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

        // progress_submission.baseline_id references schedule.project_baseline (TASK-046), so the baseline the fake inputs
        // report exists, ACTIVE on a project of its own.
        await ScheduleFixture.ActiveBaselineAsync(Database, await this.ProjectAsync(), FakeProgressInputs.BaselineId);
        Api = Identity.CreateApi(
            new Dictionary<string, string?>
            {
                ["Outbox:PollInterval"] = "01:00:00",
                ["Approval:Maintenance:PollInterval"] = "01:00:00",
                ["DocumentManagement:Scan:PollInterval"] = "01:00:00",
            },
            services => services.AddSingleton<IProgressInputs>(Inputs));
    }

    public async Task DisposeAsync()
    {
        await Api.DisposeAsync();
        await Identity.DisposeAsync();
    }
}

[CollectionDefinition(Name)]
public sealed class ProgressSuite : ICollectionFixture<ProgressTestHost>
{
    public const string Name = "Progress";
}
