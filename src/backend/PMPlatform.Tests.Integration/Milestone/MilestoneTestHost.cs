using Microsoft.Extensions.DependencyInjection;
using PMPlatform.Application.Features.DocumentManagement;
using PMPlatform.Tests.Integration.DocumentManagement;
using PMPlatform.Tests.Integration.Identity;

namespace PMPlatform.Tests.Integration.Milestone;

/// <summary>
/// The identity test database and people (IdentityDatabase) with what WF-03's milestones and WF-05's achievements need and no
/// environment has yet: test grants for AHDA's side beside the shipped R04 grants — Appendix A grants nothing there
/// (milestone-achievement.md F-2); a published MILESTONE_ACHIEVEMENT route in APPROVAL_AUTHORITY, and SCHEDULE_BASELINE, both
/// decided by R02; a GOVERNANCE_PROFILE in which STANDARD requires baseline approval; a WORKFLOW_POLICY with the approval
/// settings; two published MILESTONE_CATEGORY items and a draft one; and a published EVIDENCE_POLICY that makes one evidence
/// type mandatory for <see cref="HandoverCategoryId"/> and nothing for <see cref="GeneralCategoryId"/>. Documents are stored in
/// a directory under the test output and scanned by <see cref="TestMalwareScanner"/> when a test asks. Projects are inserted, as
/// fixtures may.
/// </summary>
/// <remarks>
/// People: local.r08 is ADR-013's entity Project Manager — external, R04 on the active entity — who claims achievements.
/// local.r05 is an internal Project Manager (R04, no anchor). local.r02 views and edits every schedule, views every achievement,
/// decides approval runs and activates projects (R02, ALL).
/// </remarks>
public sealed class MilestoneTestHost : IAsyncLifetime
{
    public static readonly Guid DepartmentId = new(IdentityDatabase.DepartmentId);
    public static readonly Guid EntityId = new(IdentityDatabase.ActiveEntityId);
    public static readonly Guid ClassificationId = new("00000000-0131-4000-8000-000000000001");
    public static readonly Guid GeneralCategoryId = new("00000000-0500-4000-8000-000000000001");
    public static readonly Guid HandoverCategoryId = new("00000000-0500-4000-8000-000000000002");
    public static readonly Guid DraftCategoryId = new("00000000-0500-4000-8000-000000000003");
    public static readonly Guid EvidenceTypeId = new("00000000-0500-4000-8000-000000000011");
    public static readonly Guid DocumentTypeId = new("00000000-0500-4000-8000-000000000012");
    public static readonly Guid Internal = new("00000000-0500-4000-8000-000000000013");

    private const string Seed = IdentityDatabase.SeedPrincipalId;

    private static string RoleId(int n) => $"00000000-0000-4000-8000-{n:D12}";

    private static string Fixture => $"""
        INSERT INTO identity_access.permission_profile_grant (id, permission_profile_version_id, permission_id, data_scope, created_at, created_by, updated_at, updated_by)
        SELECT gen_random_uuid(), '{IdentityDatabase.ProfileVersionId(2)}', p.id, 'ALL', now(), '{Seed}', now(), '{Seed}'
        FROM identity_access.permission p
        WHERE p.code IN ('SCHEDULE_VIEW', 'SCHEDULE_EDIT', 'APPROVAL_DECIDE', 'APPROVAL_VIEW', 'PROJECT_VIEW', 'PROJECT_ACTIVATE', 'MILESTONE_VIEW',
                         'DOCUMENT_VIEW', 'DOCUMENT_UPLOAD');

        INSERT INTO identity_access.access_relationship (id, user_id, permission_profile_version_id, external_entity_id, sponsor_user_id, starts_at, status, created_at, created_by, updated_at, updated_by)
        VALUES ('00000000-0500-4000-8000-000000000051', '{IdentityDatabase.UserId(5)}', '{IdentityDatabase.ProfileVersionId(4)}', NULL, NULL, now() - interval '1 day', 'ACTIVE', now(), '{Seed}', now(), '{Seed}'),
               ('00000000-0500-4000-8000-000000000081', '{IdentityDatabase.UserId(8)}', '{IdentityDatabase.ProfileVersionId(4)}', '{EntityId}', '{IdentityDatabase.UserId(2)}', now() - interval '1 day', 'ACTIVE', now(), '{Seed}', now(), '{Seed}');

        INSERT INTO master_data_config.master_data_item (id, catalogue_id, code, label_ar, label_en, sort_order, lifecycle_state, created_at, created_by, updated_at, updated_by)
        SELECT i.id::uuid, c.id, i.code, 'اختبار', i.label_en, i.sort_order, i.state, now(), '{Seed}', now(), '{Seed}'
        FROM (VALUES ('{GeneralCategoryId}', 'MILESTONE_CATEGORY', 'TEST_GENERAL', 'General', 0, 'PUBLISHED'),
                     ('{HandoverCategoryId}', 'MILESTONE_CATEGORY', 'TEST_HANDOVER', 'Handover', 0, 'PUBLISHED'),
                     ('{DraftCategoryId}', 'MILESTONE_CATEGORY', 'TEST_DRAFT_CATEGORY', 'Draft category', 0, 'DRAFT'),
                     ('{EvidenceTypeId}', 'EVIDENCE_TYPE', 'TEST_HANDOVER_CERTIFICATE', 'Handover certificate', 0, 'PUBLISHED'),
                     ('{DocumentTypeId}', 'DOCUMENT_TYPE', 'TEST_CERTIFICATE', 'Certificate', 0, 'PUBLISHED'),
                     ('{Internal}', 'DATA_CLASSIFICATION', 'TEST_INTERNAL', 'Test internal', 10, 'PUBLISHED'),
                     ('00000000-0500-4000-8000-000000000014', 'DOCUMENT_CONTROL_LEVEL', 'TEST_CONTROL', 'Test control level', 0, 'PUBLISHED'))
             AS i (id, catalogue, code, label_en, sort_order, state)
        JOIN master_data_config.master_data_catalogue c ON c.code = i.catalogue;

        UPDATE identity_access.permission SET data_classification_item_id = '{Internal}' WHERE code IN ('DOCUMENT_VIEW', 'DOCUMENT_UPLOAD', 'DOCUMENT_MANAGE');

        INSERT INTO master_data_config.configuration_version (id, configuration_family_id, version_no, lifecycle_state, created_at, created_by, updated_at, updated_by)
        SELECT v.id, f.id, 1, 'DRAFT', now(), '{Seed}', now(), '{Seed}'
        FROM (VALUES ('00000000-0500-4000-8000-000000000101'::uuid, 'GOVERNANCE_PROFILE'),
                     ('00000000-0500-4000-8000-000000000102'::uuid, 'WORKFLOW_POLICY'),
                     ('00000000-0500-4000-8000-000000000103'::uuid, 'APPROVAL_AUTHORITY'),
                     ('00000000-0500-4000-8000-000000000104'::uuid, 'EVIDENCE_POLICY')) AS v (id, family)
        JOIN master_data_config.configuration_family f ON f.code = v.family;

        INSERT INTO master_data_config.governance_profile_setting (id, configuration_version_id, governance_profile_item_id, requires_baseline_approval, risk_management_required,
                                                                   change_band_count, update_cadence_days, document_control_level_item_id, included_in_reporting,
                                                                   created_at, created_by, updated_at, updated_by)
        SELECT gen_random_uuid(), '00000000-0500-4000-8000-000000000101', i.id, true, true, 3, 7, '00000000-0500-4000-8000-000000000014', true, now(), '{Seed}', now(), '{Seed}'
        FROM master_data_config.master_data_item i JOIN master_data_config.master_data_catalogue c ON c.id = i.catalogue_id
        WHERE c.code = 'GOVERNANCE_PROFILE' AND i.code = 'STANDARD';

        INSERT INTO master_data_config.configuration_value (id, configuration_version_id, value_key, value_text, value_type, created_at, created_by, updated_at, updated_by)
        VALUES (gen_random_uuid(), '00000000-0500-4000-8000-000000000102', 'APPROVAL_TASK_DUE_DAYS', '3', 'DURATION_DAYS', now(), '{Seed}', now(), '{Seed}'),
               (gen_random_uuid(), '00000000-0500-4000-8000-000000000102', 'APPROVAL_ESCALATION_ROLE', 'R02', 'TEXT', now(), '{Seed}', now(), '{Seed}');

        INSERT INTO master_data_config.approval_authority_rule (id, configuration_version_id, subject_type_code, sequence_no, approver_role_id, is_mandatory, created_at, created_by, updated_at, updated_by)
        VALUES (gen_random_uuid(), '00000000-0500-4000-8000-000000000103', 'MILESTONE_ACHIEVEMENT', 1, '{RoleId(2)}', true, now(), '{Seed}', now(), '{Seed}'),
               (gen_random_uuid(), '00000000-0500-4000-8000-000000000103', 'SCHEDULE_BASELINE', 1, '{RoleId(2)}', true, now(), '{Seed}', now(), '{Seed}');

        INSERT INTO master_data_config.evidence_requirement_rule (id, configuration_version_id, milestone_category_item_id, evidence_type_item_id, is_mandatory, created_at, created_by, updated_at, updated_by)
        VALUES (gen_random_uuid(), '00000000-0500-4000-8000-000000000104', '{HandoverCategoryId}', '{EvidenceTypeId}', true, now(), '{Seed}', now(), '{Seed}');

        UPDATE master_data_config.configuration_version
        SET lifecycle_state = 'PUBLISHED', published_at = now() - interval '1 hour', effective_from = now() - interval '1 hour'
        WHERE id IN ('00000000-0500-4000-8000-000000000101', '00000000-0500-4000-8000-000000000102', '00000000-0500-4000-8000-000000000103',
                     '00000000-0500-4000-8000-000000000104');
        """;

    public IdentityTestHost Identity { get; } = new();

    public IdentityDatabase Database => Identity.Database;

    public IdentityApiFactory Api { get; private set; } = null!;

    public TestMalwareScanner Scanner { get; } = new();

    /// <summary>The document store: a directory under the test output, removed with the host.</summary>
    public string StoreDirectory { get; } = Path.Combine(AppContext.BaseDirectory, "milestone-documents-" + Guid.NewGuid().ToString("N"));

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
public sealed class MilestoneSuite : ICollectionFixture<MilestoneTestHost>
{
    public const string Name = "Milestone";
}
