using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PMPlatform.Application.Features.DocumentManagement;
using PMPlatform.Infrastructure.Persistence.Messaging;
using PMPlatform.Tests.Integration.DocumentManagement;
using PMPlatform.Tests.Integration.Identity;

namespace PMPlatform.Tests.Integration.ChangeRequest;

/// <summary>
/// The identity test database and people (IdentityDatabase) with what WF-08 and its two target modules need and no environment has
/// yet: test grants for AHDA's side beside the shipped ones — Appendix A grants nothing more (change-request.md F-2); a GOVERNANCE_PROFILE
/// in which STANDARD requires baseline approval and LIGHT does not (ADR-015); a WORKFLOW_POLICY with the approval settings and the
/// Schedule Health and financial-status thresholds; an APPROVAL_AUTHORITY routing CHANGE_REQUEST to R02, and band 3 to R03 as a second
/// stage, SCHEDULE_BASELINE and COMMITMENT to R02; a MATERIALITY_BAND version with the three bands of STANDARD and LIGHT, none of FULL
/// (see <see cref="CostBand2Percent"/> and the other thresholds); and the document-management items an Approved Budget's referenced
/// document needs. Projects are inserted, as fixtures may. The outbox worker is removed, so a test delivers WF-11's outcomes when it says.
/// </summary>
/// <remarks>
/// People: local.r08 is ADR-013's entity Project Manager — external, R04 across the active entity — who raises change requests, keeps the
/// schedule and enters budgets. local.r05 is an internal Project Manager (R04, no anchor). local.r02 is AHDA's change officer: it views,
/// raises, reviews and implements change requests, decides WF-11 runs and reads schedules and budgets, all at ALL through R02. local.r03
/// is the Department Manager: the shipped R03 grants — it views and starts reviews at DEPT — and APPROVAL_DECIDE at DEPT for band 3.
/// </remarks>
public sealed class ChangeRequestTestHost : IAsyncLifetime
{
    public static readonly Guid DepartmentId = new(IdentityDatabase.DepartmentId);
    public static readonly Guid EntityId = new(IdentityDatabase.ActiveEntityId);
    public static readonly Guid ClassificationId = new("00000000-0131-4000-8000-000000000001");
    public static readonly Guid EvidenceTypeId = new("00000000-0600-4000-8000-000000000004");
    public static readonly Guid DocumentTypeId = new("00000000-0600-4000-8000-000000000005");
    public static readonly Guid Internal = new("00000000-0600-4000-8000-000000000006");
    public static readonly Guid MaterialityVersionId = new("00000000-0600-4000-8000-000000000105");

    /// <summary>Band 2: 5% of the Approved Budget, or 10 calendar days of schedule.</summary>
    public const decimal CostBand2Percent = 5m;
    public const int ScheduleBand2Days = 10;

    /// <summary>Band 3: 200,000 SAR, or half the baseline's duration; any scope change names a rule there.</summary>
    public const decimal CostBand3Sar = 200000m;
    public const decimal ScheduleBand3Percent = 50m;

    private const string Seed = IdentityDatabase.SeedPrincipalId;

    private static string RoleId(int n) => $"00000000-0000-4000-8000-{n:D12}";

    private static string Fixture => $"""
        INSERT INTO identity_access.permission_profile_grant (id, permission_profile_version_id, permission_id, data_scope, created_at, created_by, updated_at, updated_by)
        SELECT gen_random_uuid(), g.version_id::uuid, p.id, g.scope, now(), '{Seed}', now(), '{Seed}'
        FROM (VALUES ('{IdentityDatabase.ProfileVersionId(2)}', 'CHANGE_REQUEST_VIEW', 'ALL'), ('{IdentityDatabase.ProfileVersionId(2)}', 'CHANGE_REQUEST_RAISE', 'ALL'),
                     ('{IdentityDatabase.ProfileVersionId(2)}', 'CHANGE_REQUEST_REVIEW', 'ALL'), ('{IdentityDatabase.ProfileVersionId(2)}', 'CHANGE_REQUEST_IMPLEMENT', 'ALL'),
                     ('{IdentityDatabase.ProfileVersionId(2)}', 'APPROVAL_DECIDE', 'ALL'), ('{IdentityDatabase.ProfileVersionId(2)}', 'APPROVAL_VIEW', 'ALL'),
                     ('{IdentityDatabase.ProfileVersionId(2)}', 'PROJECT_VIEW', 'ALL'), ('{IdentityDatabase.ProfileVersionId(2)}', 'SCHEDULE_VIEW', 'ALL'),
                     ('{IdentityDatabase.ProfileVersionId(2)}', 'FINANCIAL_VIEW', 'ALL'), ('{IdentityDatabase.ProfileVersionId(2)}', 'DOCUMENT_VIEW', 'ALL'),
                     ('{IdentityDatabase.ProfileVersionId(3)}', 'APPROVAL_DECIDE', 'DEPT'), ('{IdentityDatabase.ProfileVersionId(3)}', 'APPROVAL_VIEW', 'DEPT'),
                     ('{IdentityDatabase.ProfileVersionId(4)}', 'FINANCIAL_SUBMIT', 'OWN'))
             AS g (version_id, code, scope)
        JOIN identity_access.permission p ON p.code = g.code;

        -- local.r08: R04 across their entity, beside the per-project one IdentityDatabase gives; local.r05: an internal R04.
        INSERT INTO identity_access.access_relationship (id, user_id, permission_profile_version_id, external_entity_id, sponsor_user_id, starts_at, status, created_at, created_by, updated_at, updated_by)
        VALUES ('00000000-0600-4000-8000-000000000081', '{IdentityDatabase.UserId(8)}', '{IdentityDatabase.ProfileVersionId(4)}', '{EntityId}', '{IdentityDatabase.UserId(2)}', now() - interval '1 day', 'ACTIVE', now(), '{Seed}', now(), '{Seed}'),
               ('00000000-0600-4000-8000-000000000051', '{IdentityDatabase.UserId(5)}', '{IdentityDatabase.ProfileVersionId(4)}', NULL, NULL, now() - interval '1 day', 'ACTIVE', now(), '{Seed}', now(), '{Seed}');

        INSERT INTO master_data_config.master_data_item (id, catalogue_id, code, label_ar, label_en, sort_order, lifecycle_state, created_at, created_by, updated_at, updated_by)
        SELECT i.id::uuid, c.id, i.code, 'اختبار', i.label_en, i.sort_order, 'PUBLISHED', now(), '{Seed}', now(), '{Seed}'
        FROM (VALUES ('{EvidenceTypeId}', 'EVIDENCE_TYPE', 'TEST_BUDGET_APPROVAL', 'Budget approval', 0),
                     ('{DocumentTypeId}', 'DOCUMENT_TYPE', 'TEST_BUDGET_LETTER', 'Budget letter', 0),
                     ('{Internal}', 'DATA_CLASSIFICATION', 'TEST_INTERNAL', 'Test internal', 10),
                     ('00000000-0600-4000-8000-000000000011', 'DOCUMENT_CONTROL_LEVEL', 'TEST_CONTROL', 'Test control level', 0))
             AS i (id, catalogue, code, label_en, sort_order)
        JOIN master_data_config.master_data_catalogue c ON c.code = i.catalogue;

        UPDATE identity_access.permission SET data_classification_item_id = '{Internal}' WHERE code IN ('DOCUMENT_VIEW', 'DOCUMENT_UPLOAD', 'DOCUMENT_MANAGE');

        INSERT INTO master_data_config.configuration_version (id, configuration_family_id, version_no, lifecycle_state, created_at, created_by, updated_at, updated_by)
        SELECT v.id, f.id, 1, 'DRAFT', now(), '{Seed}', now(), '{Seed}'
        FROM (VALUES ('00000000-0600-4000-8000-000000000101'::uuid, 'GOVERNANCE_PROFILE'),
                     ('00000000-0600-4000-8000-000000000102'::uuid, 'WORKFLOW_POLICY'),
                     ('00000000-0600-4000-8000-000000000103'::uuid, 'APPROVAL_AUTHORITY'),
                     ('{MaterialityVersionId}'::uuid, 'MATERIALITY_BAND')) AS v (id, family)
        JOIN master_data_config.configuration_family f ON f.code = v.family;

        INSERT INTO master_data_config.governance_profile_setting (id, configuration_version_id, governance_profile_item_id, requires_baseline_approval, risk_management_required,
                                                                   change_band_count, update_cadence_days, document_control_level_item_id, included_in_reporting,
                                                                   created_at, created_by, updated_at, updated_by)
        SELECT gen_random_uuid(), '00000000-0600-4000-8000-000000000101', i.id, i.code <> 'LIGHT', i.code <> 'LIGHT', 3, 7, '00000000-0600-4000-8000-000000000011', true,
               now(), '{Seed}', now(), '{Seed}'
        FROM master_data_config.master_data_item i JOIN master_data_config.master_data_catalogue c ON c.id = i.catalogue_id
        WHERE c.code = 'GOVERNANCE_PROFILE';

        INSERT INTO master_data_config.configuration_value (id, configuration_version_id, value_key, value_text, value_type, created_at, created_by, updated_at, updated_by)
        VALUES (gen_random_uuid(), '00000000-0600-4000-8000-000000000102', 'APPROVAL_TASK_DUE_DAYS', '3', 'DURATION_DAYS', now(), '{Seed}', now(), '{Seed}'),
               (gen_random_uuid(), '00000000-0600-4000-8000-000000000102', 'APPROVAL_ESCALATION_ROLE', 'R02', 'TEXT', now(), '{Seed}', now(), '{Seed}'),
               (gen_random_uuid(), '00000000-0600-4000-8000-000000000102', 'SCHEDULE_HEALTH_AMBER_FINISH_VARIANCE_DAYS', '5', 'DURATION_DAYS', now(), '{Seed}', now(), '{Seed}'),
               (gen_random_uuid(), '00000000-0600-4000-8000-000000000102', 'SCHEDULE_HEALTH_RED_FINISH_VARIANCE_DAYS', '20', 'DURATION_DAYS', now(), '{Seed}', now(), '{Seed}'),
               (gen_random_uuid(), '00000000-0600-4000-8000-000000000102', 'FINANCIAL_STATUS_AMBER_OVERRUN_PERCENT', '5', 'PERCENT', now(), '{Seed}', now(), '{Seed}'),
               (gen_random_uuid(), '00000000-0600-4000-8000-000000000102', 'FINANCIAL_STATUS_RED_OVERRUN_PERCENT', '10', 'PERCENT', now(), '{Seed}', now(), '{Seed}');

        INSERT INTO master_data_config.approval_authority_rule (id, configuration_version_id, subject_type_code, band_no, sequence_no, approver_role_id, is_mandatory,
                                                                created_at, created_by, updated_at, updated_by)
        VALUES (gen_random_uuid(), '00000000-0600-4000-8000-000000000103', 'CHANGE_REQUEST', NULL, 1, '{RoleId(2)}', true, now(), '{Seed}', now(), '{Seed}'),
               (gen_random_uuid(), '00000000-0600-4000-8000-000000000103', 'CHANGE_REQUEST', 3, 2, '{RoleId(3)}', true, now(), '{Seed}', now(), '{Seed}'),
               (gen_random_uuid(), '00000000-0600-4000-8000-000000000103', 'SCHEDULE_BASELINE', NULL, 1, '{RoleId(2)}', true, now(), '{Seed}', now(), '{Seed}'),
               (gen_random_uuid(), '00000000-0600-4000-8000-000000000103', 'COMMITMENT', NULL, 1, '{RoleId(2)}', true, now(), '{Seed}', now(), '{Seed}');

        INSERT INTO master_data_config.materiality_band (id, configuration_version_id, governance_profile_item_id, band_no, cost_threshold_pct, cost_threshold_sar,
                                                         schedule_threshold_pct, schedule_threshold_days, scope_rule_code, requires_approval,
                                                         created_at, created_by, updated_at, updated_by)
        SELECT gen_random_uuid(), '{MaterialityVersionId}', i.id, b.band_no, b.cost_pct, b.cost_sar, b.schedule_pct, b.schedule_days, b.scope_rule, b.band_no > 1,
               now(), '{Seed}', now(), '{Seed}'
        FROM (VALUES (1, NULL::numeric, NULL::numeric, NULL::numeric, NULL::integer, NULL::text),
                     (2, {CostBand2Percent}, NULL, NULL, {ScheduleBand2Days}, NULL),
                     (3, NULL, {CostBand3Sar}, {ScheduleBand3Percent}, NULL, 'ANY_SCOPE_CHANGE'))
             AS b (band_no, cost_pct, cost_sar, schedule_pct, schedule_days, scope_rule)
        CROSS JOIN master_data_config.master_data_item i JOIN master_data_config.master_data_catalogue c ON c.id = i.catalogue_id
        WHERE c.code = 'GOVERNANCE_PROFILE' AND i.code IN ('STANDARD', 'LIGHT');

        UPDATE master_data_config.configuration_version
        SET lifecycle_state = 'PUBLISHED', published_at = now() - interval '1 hour', effective_from = now() - interval '1 hour'
        WHERE id IN ('00000000-0600-4000-8000-000000000101', '00000000-0600-4000-8000-000000000102', '00000000-0600-4000-8000-000000000103', '{MaterialityVersionId}');
        """;

    public IdentityTestHost Identity { get; } = new();

    public IdentityDatabase Database => Identity.Database;

    public IdentityApiFactory Api { get; private set; } = null!;

    public TestMalwareScanner Scanner { get; } = new();

    /// <summary>The document store: a directory under the test output, removed with the host.</summary>
    public string StoreDirectory { get; } = Path.Combine(AppContext.BaseDirectory, "change-request-documents-" + Guid.NewGuid().ToString("N"));

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(StoreDirectory);
        await Identity.InitializeAsync();
        await Database.ExecuteAsync(Fixture);
        Api = Identity.CreateApi(
            new Dictionary<string, string?>
            {
                ["DOCUMENT_STORAGE_CONNECTION_STRING"] = new Uri(StoreDirectory).AbsoluteUri,
                ["Approval:Maintenance:PollInterval"] = "01:00:00",
                ["DocumentManagement:Scan:PollInterval"] = "01:00:00",
                ["Risk:Maintenance:PollInterval"] = "01:00:00",
            },
            services =>
            {
                services.AddSingleton<IMalwareScanner>(Scanner);
                foreach (ServiceDescriptor worker in services.Where(d => d.ServiceType == typeof(IHostedService) && d.ImplementationType == typeof(OutboxDispatchWorker)).ToList())
                {
                    services.Remove(worker);
                }
            });
    }

    public async Task DisposeAsync()
    {
        await Api.DisposeAsync();
        await Identity.DisposeAsync();
        if (Directory.Exists(StoreDirectory))
        {
            Directory.Delete(StoreDirectory, recursive: true);
        }
    }
}

[CollectionDefinition(Name)]
public sealed class ChangeRequestSuite : ICollectionFixture<ChangeRequestTestHost>
{
    public const string Name = "ChangeRequest";
}
