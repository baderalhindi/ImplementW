using Microsoft.Extensions.DependencyInjection;
using PMPlatform.Application.Features.DocumentManagement;
using PMPlatform.Tests.Integration.DocumentManagement;
using PMPlatform.Tests.Integration.Identity;

namespace PMPlatform.Tests.Integration.FinancialKpi;

/// <summary>
/// The identity test database and people (IdentityDatabase) with what WF-14 needs and no environment has yet: test grants for
/// entering, reviewing and source configuration beside the shipped R04/R08 views — Appendix A grants none of them
/// (financial-kpi.md F-2); a published WORKFLOW_POLICY with the approval settings and the financial-status thresholds, AMBER from
/// 5% and RED from 10% overrun; a published APPROVAL_AUTHORITY routing COMMITMENT and KPI_TARGET to R02; two KPI units, a
/// measurement frequency, three PUBLISHED KPI definitions and a DRAFT one; and the document-management items an Approved Budget's
/// referenced document needs. Projects and WF-02's reporting periods are inserted, as fixtures may.
/// </summary>
/// <remarks>
/// People: local.r08 is ADR-013's entity Project Manager — external, R08 and R04 on the active entity — who enters figures and
/// manages the project's KPIs; the test R04 profile also holds review, so the person check is what refuses them. local.r02
/// (R02, ALL) views, reviews and publishes, configures sources, and decides WF-11 runs. local.r03 (R03, DEPT) records KPI values
/// for the department's projects.
/// </remarks>
public sealed class FinancialKpiTestHost : IAsyncLifetime
{
    public static readonly Guid DepartmentId = new(IdentityDatabase.DepartmentId);
    public static readonly Guid EntityId = new(IdentityDatabase.ActiveEntityId);
    public static readonly Guid OtherEntityId = new(IdentityDatabase.SuspendedEntityId);
    public static readonly Guid ClassificationId = new("00000000-0131-4000-8000-000000000001");
    public static readonly Guid PercentUnitId = new("00000000-0520-4000-8000-000000000001");
    public static readonly Guid DaysUnitId = new("00000000-0520-4000-8000-000000000002");
    public static readonly Guid MonthlyId = new("00000000-0520-4000-8000-000000000003");
    public static readonly Guid EvidenceTypeId = new("00000000-0520-4000-8000-000000000004");
    public static readonly Guid DocumentTypeId = new("00000000-0520-4000-8000-000000000005");
    public static readonly Guid Internal = new("00000000-0520-4000-8000-000000000006");
    public static readonly Guid Restricted = new("00000000-0520-4000-8000-000000000007");

    /// <summary>HIGHER_IS_BETTER, in percent.</summary>
    public static readonly Guid SafetyKpiId = new("00000000-0520-4000-8000-000000000021");

    /// <summary>LOWER_IS_BETTER, in percent: combines with <see cref="SafetyKpiId"/>.</summary>
    public static readonly Guid ReworkKpiId = new("00000000-0520-4000-8000-000000000022");

    /// <summary>LOWER_IS_BETTER, in days: does not combine with the percentages.</summary>
    public static readonly Guid DelayKpiId = new("00000000-0520-4000-8000-000000000023");

    public static readonly Guid DraftKpiId = new("00000000-0520-4000-8000-000000000024");
    public static readonly Guid WorkflowPolicyVersionId = new("00000000-0520-4000-8000-000000000102");

    private const string Seed = IdentityDatabase.SeedPrincipalId;

    private static string RoleId(int n) => $"00000000-0000-4000-8000-{n:D12}";

    private static string Fixture => $"""
        INSERT INTO identity_access.permission_profile_grant (id, permission_profile_version_id, permission_id, data_scope, created_at, created_by, updated_at, updated_by)
        SELECT gen_random_uuid(), g.version_id::uuid, p.id, g.scope, now(), '{Seed}', now(), '{Seed}'
        FROM (VALUES ('{IdentityDatabase.ProfileVersionId(2)}', 'FINANCIAL_VIEW', 'ALL'), ('{IdentityDatabase.ProfileVersionId(2)}', 'FINANCIAL_SUBMIT', 'ALL'),
                     ('{IdentityDatabase.ProfileVersionId(2)}', 'FINANCIAL_REVIEW', 'ALL'), ('{IdentityDatabase.ProfileVersionId(2)}', 'FINANCIAL_SOURCE_MANAGE', 'ALL'),
                     ('{IdentityDatabase.ProfileVersionId(2)}', 'KPI_VIEW', 'ALL'), ('{IdentityDatabase.ProfileVersionId(2)}', 'KPI_MANAGE', 'ALL'),
                     ('{IdentityDatabase.ProfileVersionId(2)}', 'KPI_RECORD', 'ALL'), ('{IdentityDatabase.ProfileVersionId(2)}', 'KPI_REVIEW', 'ALL'),
                     ('{IdentityDatabase.ProfileVersionId(2)}', 'APPROVAL_DECIDE', 'ALL'), ('{IdentityDatabase.ProfileVersionId(2)}', 'APPROVAL_VIEW', 'ALL'),
                     ('{IdentityDatabase.ProfileVersionId(2)}', 'PROJECT_VIEW', 'ALL'), ('{IdentityDatabase.ProfileVersionId(2)}', 'DOCUMENT_VIEW', 'ALL'),
                     ('{IdentityDatabase.ProfileVersionId(3)}', 'KPI_VIEW', 'DEPT'), ('{IdentityDatabase.ProfileVersionId(3)}', 'KPI_RECORD', 'DEPT'),
                     ('{IdentityDatabase.ProfileVersionId(4)}', 'FINANCIAL_SUBMIT', 'OWN'), ('{IdentityDatabase.ProfileVersionId(4)}', 'FINANCIAL_REVIEW', 'ENTITY'),
                     ('{IdentityDatabase.ProfileVersionId(4)}', 'KPI_MANAGE', 'OWN'), ('{IdentityDatabase.ProfileVersionId(4)}', 'KPI_RECORD', 'OWN'),
                     ('{IdentityDatabase.ProfileVersionId(4)}', 'KPI_REVIEW', 'ENTITY'))
             AS g (version_id, code, scope)
        JOIN identity_access.permission p ON p.code = g.code;

        -- local.r08: R04 across their entity, beside the per-project one IdentityDatabase gives.
        INSERT INTO identity_access.access_relationship (id, user_id, permission_profile_version_id, external_entity_id, sponsor_user_id, starts_at, status, created_at, created_by, updated_at, updated_by)
        VALUES ('00000000-0520-4000-8000-000000000081', '{IdentityDatabase.UserId(8)}', '{IdentityDatabase.ProfileVersionId(4)}', '{EntityId}', '{IdentityDatabase.UserId(2)}', now() - interval '1 day', 'ACTIVE', now(), '{Seed}', now(), '{Seed}');

        INSERT INTO master_data_config.master_data_item (id, catalogue_id, code, label_ar, label_en, sort_order, lifecycle_state, created_at, created_by, updated_at, updated_by)
        SELECT i.id::uuid, c.id, i.code, 'اختبار', i.label_en, i.sort_order, 'PUBLISHED', now(), '{Seed}', now(), '{Seed}'
        FROM (VALUES ('{PercentUnitId}', 'KPI_UNIT', 'TEST_PERCENT', 'Percent', 0),
                     ('{DaysUnitId}', 'KPI_UNIT', 'TEST_DAYS', 'Days', 0),
                     ('{MonthlyId}', 'MEASUREMENT_FREQUENCY', 'TEST_MONTHLY', 'Monthly', 0),
                     ('{EvidenceTypeId}', 'EVIDENCE_TYPE', 'TEST_BUDGET_APPROVAL', 'Budget approval', 0),
                     ('{DocumentTypeId}', 'DOCUMENT_TYPE', 'TEST_BUDGET_LETTER', 'Budget letter', 0),
                     ('{Internal}', 'DATA_CLASSIFICATION', 'TEST_INTERNAL', 'Test internal', 10),
                     ('{Restricted}', 'DATA_CLASSIFICATION', 'TEST_RESTRICTED', 'Test restricted', 20))
             AS i (id, catalogue, code, label_en, sort_order)
        JOIN master_data_config.master_data_catalogue c ON c.code = i.catalogue;

        UPDATE identity_access.permission SET data_classification_item_id = '{Internal}' WHERE code IN ('DOCUMENT_VIEW', 'DOCUMENT_UPLOAD', 'DOCUMENT_MANAGE');

        INSERT INTO master_data_config.kpi_definition (id, code, name_ar, name_en, unit_item_id, direction, lifecycle_state, published_at, published_by_user_id,
                                                       created_at, created_by, updated_at, updated_by)
        VALUES ('{SafetyKpiId}', 'TEST_SAFE_HOURS', 'اختبار', 'Safe hours', '{PercentUnitId}', 'HIGHER_IS_BETTER', 'PUBLISHED', now(), '{IdentityDatabase.UserId(1)}', now(), '{Seed}', now(), '{Seed}'),
               ('{ReworkKpiId}', 'TEST_REWORK', 'اختبار', 'Rework', '{PercentUnitId}', 'LOWER_IS_BETTER', 'PUBLISHED', now(), '{IdentityDatabase.UserId(1)}', now(), '{Seed}', now(), '{Seed}'),
               ('{DelayKpiId}', 'TEST_DELAY', 'اختبار', 'Delay', '{DaysUnitId}', 'LOWER_IS_BETTER', 'PUBLISHED', now(), '{IdentityDatabase.UserId(1)}', now(), '{Seed}', now(), '{Seed}'),
               ('{DraftKpiId}', 'TEST_DRAFT_KPI', 'اختبار', 'Draft KPI', '{PercentUnitId}', 'HIGHER_IS_BETTER', 'DRAFT', NULL, NULL, now(), '{Seed}', now(), '{Seed}');

        INSERT INTO master_data_config.configuration_version (id, configuration_family_id, version_no, lifecycle_state, created_at, created_by, updated_at, updated_by)
        SELECT v.id, f.id, 1, 'DRAFT', now(), '{Seed}', now(), '{Seed}'
        FROM (VALUES ('{WorkflowPolicyVersionId}'::uuid, 'WORKFLOW_POLICY'),
                     ('00000000-0520-4000-8000-000000000103'::uuid, 'APPROVAL_AUTHORITY')) AS v (id, family)
        JOIN master_data_config.configuration_family f ON f.code = v.family;

        INSERT INTO master_data_config.configuration_value (id, configuration_version_id, value_key, value_text, value_type, created_at, created_by, updated_at, updated_by)
        VALUES (gen_random_uuid(), '{WorkflowPolicyVersionId}', 'APPROVAL_TASK_DUE_DAYS', '3', 'DURATION_DAYS', now(), '{Seed}', now(), '{Seed}'),
               (gen_random_uuid(), '{WorkflowPolicyVersionId}', 'APPROVAL_ESCALATION_ROLE', 'R02', 'TEXT', now(), '{Seed}', now(), '{Seed}'),
               (gen_random_uuid(), '{WorkflowPolicyVersionId}', 'FINANCIAL_STATUS_AMBER_OVERRUN_PERCENT', '5', 'PERCENT', now(), '{Seed}', now(), '{Seed}'),
               (gen_random_uuid(), '{WorkflowPolicyVersionId}', 'FINANCIAL_STATUS_RED_OVERRUN_PERCENT', '10', 'PERCENT', now(), '{Seed}', now(), '{Seed}');

        INSERT INTO master_data_config.approval_authority_rule (id, configuration_version_id, subject_type_code, sequence_no, approver_role_id, is_mandatory, created_at, created_by, updated_at, updated_by)
        VALUES (gen_random_uuid(), '00000000-0520-4000-8000-000000000103', 'COMMITMENT', 1, '{RoleId(2)}', true, now(), '{Seed}', now(), '{Seed}'),
               (gen_random_uuid(), '00000000-0520-4000-8000-000000000103', 'KPI_TARGET', 1, '{RoleId(2)}', true, now(), '{Seed}', now(), '{Seed}');

        UPDATE master_data_config.configuration_version
        SET lifecycle_state = 'PUBLISHED', published_at = now() - interval '1 hour', effective_from = now() - interval '1 hour'
        WHERE id IN ('{WorkflowPolicyVersionId}', '00000000-0520-4000-8000-000000000103');
        """;

    public IdentityTestHost Identity { get; } = new();

    public IdentityDatabase Database => Identity.Database;

    public IdentityApiFactory Api { get; private set; } = null!;

    public TestMalwareScanner Scanner { get; } = new();

    /// <summary>The document store: a directory under the test output, removed with the host.</summary>
    public string StoreDirectory { get; } = Path.Combine(AppContext.BaseDirectory, "financial-kpi-documents-" + Guid.NewGuid().ToString("N"));

    public Guid StandardProfileId { get; private set; }

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(StoreDirectory);
        await Identity.InitializeAsync();
        await Database.ExecuteAsync(Fixture);
        StandardProfileId = Guid.Parse(Assert.Single(await Database.QueryAsync("""
            SELECT i.id::text FROM master_data_config.master_data_item i
            JOIN master_data_config.master_data_catalogue c ON c.id = i.catalogue_id
            WHERE c.code = 'GOVERNANCE_PROFILE' AND i.code = 'STANDARD'
            """)));
        Api = Identity.CreateApi(
            new Dictionary<string, string?>
            {
                ["DOCUMENT_STORAGE_CONNECTION_STRING"] = new Uri(StoreDirectory).AbsoluteUri,
                ["Outbox:PollInterval"] = "01:00:00",
                ["Approval:Maintenance:PollInterval"] = "01:00:00",
                ["DocumentManagement:Scan:PollInterval"] = "01:00:00",
            },
            services => services.AddSingleton<IMalwareScanner>(Scanner));
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
public sealed class FinancialKpiSuite : ICollectionFixture<FinancialKpiTestHost>
{
    public const string Name = "FinancialKpi";
}
