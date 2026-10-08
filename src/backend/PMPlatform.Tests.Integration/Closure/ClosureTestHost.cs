using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PMPlatform.Infrastructure.Persistence.Messaging;
using PMPlatform.Tests.Integration.Identity;

namespace PMPlatform.Tests.Integration.Closure;

/// <summary>
/// The identity test database and people (IdentityDatabase) with what WF-10 needs and no environment has yet: test grants for AHDA's
/// side beside the shipped ones — Appendix A grants nothing more (closure.md F-2); a GOVERNANCE_PROFILE in which LIGHT needs no baseline
/// approval (ADR-015), so a test baselines a project in one step; a WORKFLOW_POLICY with the approval settings and the Schedule Health
/// thresholds; an APPROVAL_AUTHORITY routing COMPLETION, CLOSURE, SUSPENSION and RESUMPTION to R02; and a published document type and
/// classification with the document permissions cleared to it. Projects are inserted, as fixtures may. The outbox worker is removed, so
/// a test delivers WF-11's outcomes when it says, and WF-10's and WF-09's activation passes run only when a test runs them.
/// </summary>
/// <remarks>
/// People: local.r08 is ADR-013's entity Project Manager — external, R04 across the active entity — who raises cases, keeps tasks and the
/// schedule, on the shipped R04 grants, and R04 test grants for review, waiver and activation, which ADR-013's check on the person
/// refuses. local.r03 is the Department Manager: the shipped R03 grants — it views, starts reviews and waives criteria at DEPT. local.r02
/// is AHDA's officer: every permission at ALL through R02, so that what refuses a write to a closed project is never its authorization.
/// </remarks>
public sealed class ClosureTestHost : IAsyncLifetime
{
    public static readonly Guid DepartmentId = new(IdentityDatabase.DepartmentId);
    public static readonly Guid EntityId = new(IdentityDatabase.ActiveEntityId);
    public static readonly Guid ClassificationId = new("00000000-0131-4000-8000-000000000001");
    public static readonly Guid DocumentTypeId = new("00000000-0630-4000-8000-000000000021");
    public static readonly Guid InternalClassificationId = new("00000000-0630-4000-8000-000000000022");

    private const string Seed = IdentityDatabase.SeedPrincipalId;

    private static string RoleId(int n) => $"00000000-0000-4000-8000-{n:D12}";

    private static string Fixture => $"""
        -- local.r02: every permission R02's profile version lacks, at ALL.
        INSERT INTO identity_access.permission_profile_grant (id, permission_profile_version_id, permission_id, data_scope, created_at, created_by, updated_at, updated_by)
        SELECT gen_random_uuid(), '{IdentityDatabase.ProfileVersionId(2)}', p.id, 'ALL', now(), '{Seed}', now(), '{Seed}'
        FROM identity_access.permission p
        WHERE NOT EXISTS (SELECT 1 FROM identity_access.permission_profile_grant g
                          WHERE g.permission_profile_version_id = '{IdentityDatabase.ProfileVersionId(2)}' AND g.permission_id = p.id);

        -- R04 also holds review, waiver and activation here, so that what refuses them to an external Project Manager is ADR-013's check on the person.
        INSERT INTO identity_access.permission_profile_grant (id, permission_profile_version_id, permission_id, data_scope, created_at, created_by, updated_at, updated_by)
        SELECT gen_random_uuid(), '{IdentityDatabase.ProfileVersionId(4)}', p.id, 'OWN', now(), '{Seed}', now(), '{Seed}'
        FROM identity_access.permission p WHERE p.code IN ('CLOSEOUT_REVIEW', 'CLOSEOUT_WAIVE', 'CLOSEOUT_ACTIVATE');

        -- local.r08: R04 across their entity, beside the per-project one IdentityDatabase gives.
        INSERT INTO identity_access.access_relationship (id, user_id, permission_profile_version_id, external_entity_id, sponsor_user_id, starts_at, status, created_at, created_by, updated_at, updated_by)
        VALUES ('00000000-0630-4000-8000-000000000081', '{IdentityDatabase.UserId(8)}', '{IdentityDatabase.ProfileVersionId(4)}', '{EntityId}', '{IdentityDatabase.UserId(2)}', now() - interval '1 day', 'ACTIVE', now(), '{Seed}', now(), '{Seed}');

        INSERT INTO master_data_config.master_data_item (id, catalogue_id, code, label_ar, label_en, sort_order, lifecycle_state, created_at, created_by, updated_at, updated_by)
        SELECT i.id::uuid, c.id, i.code, 'اختبار', i.label_en, i.sort_order, 'PUBLISHED', now(), '{Seed}', now(), '{Seed}'
        FROM (VALUES ('00000000-0630-4000-8000-000000000011', 'DOCUMENT_CONTROL_LEVEL', 'TEST_CONTROL', 'Test control level', 0),
                     ('{DocumentTypeId}', 'DOCUMENT_TYPE', 'TEST_CLOSEOUT_REPORT', 'Test closeout report', 0),
                     ('{InternalClassificationId}', 'DATA_CLASSIFICATION', 'TEST_CLOSEOUT_INTERNAL', 'Test internal', 10)) AS i (id, catalogue, code, label_en, sort_order)
        JOIN master_data_config.master_data_catalogue c ON c.code = i.catalogue;

        UPDATE identity_access.permission SET data_classification_item_id = '{InternalClassificationId}'
        WHERE code IN ('DOCUMENT_VIEW', 'DOCUMENT_UPLOAD', 'DOCUMENT_MANAGE');

        INSERT INTO master_data_config.configuration_version (id, configuration_family_id, version_no, lifecycle_state, created_at, created_by, updated_at, updated_by)
        SELECT v.id, f.id, 1, 'DRAFT', now(), '{Seed}', now(), '{Seed}'
        FROM (VALUES ('00000000-0630-4000-8000-000000000101'::uuid, 'GOVERNANCE_PROFILE'),
                     ('00000000-0630-4000-8000-000000000102'::uuid, 'WORKFLOW_POLICY'),
                     ('00000000-0630-4000-8000-000000000103'::uuid, 'APPROVAL_AUTHORITY')) AS v (id, family)
        JOIN master_data_config.configuration_family f ON f.code = v.family;

        INSERT INTO master_data_config.governance_profile_setting (id, configuration_version_id, governance_profile_item_id, requires_baseline_approval, risk_management_required,
                                                                   change_band_count, update_cadence_days, document_control_level_item_id, included_in_reporting,
                                                                   created_at, created_by, updated_at, updated_by)
        SELECT gen_random_uuid(), '00000000-0630-4000-8000-000000000101', i.id, i.code <> 'LIGHT', i.code <> 'LIGHT', 3, 7, '00000000-0630-4000-8000-000000000011', true,
               now(), '{Seed}', now(), '{Seed}'
        FROM master_data_config.master_data_item i JOIN master_data_config.master_data_catalogue c ON c.id = i.catalogue_id
        WHERE c.code = 'GOVERNANCE_PROFILE';

        INSERT INTO master_data_config.configuration_value (id, configuration_version_id, value_key, value_text, value_type, created_at, created_by, updated_at, updated_by)
        VALUES (gen_random_uuid(), '00000000-0630-4000-8000-000000000102', 'APPROVAL_TASK_DUE_DAYS', '3', 'DURATION_DAYS', now(), '{Seed}', now(), '{Seed}'),
               (gen_random_uuid(), '00000000-0630-4000-8000-000000000102', 'APPROVAL_ESCALATION_ROLE', 'R02', 'TEXT', now(), '{Seed}', now(), '{Seed}'),
               (gen_random_uuid(), '00000000-0630-4000-8000-000000000102', 'SCHEDULE_HEALTH_AMBER_FINISH_VARIANCE_DAYS', '5', 'DURATION_DAYS', now(), '{Seed}', now(), '{Seed}'),
               (gen_random_uuid(), '00000000-0630-4000-8000-000000000102', 'SCHEDULE_HEALTH_RED_FINISH_VARIANCE_DAYS', '20', 'DURATION_DAYS', now(), '{Seed}', now(), '{Seed}');

        INSERT INTO master_data_config.approval_authority_rule (id, configuration_version_id, subject_type_code, band_no, sequence_no, approver_role_id, is_mandatory,
                                                                created_at, created_by, updated_at, updated_by)
        SELECT gen_random_uuid(), '00000000-0630-4000-8000-000000000103', k.code, NULL, 1, '{RoleId(2)}', true, now(), '{Seed}', now(), '{Seed}'
        FROM (VALUES ('COMPLETION'), ('CLOSURE'), ('SUSPENSION'), ('RESUMPTION'), ('SCHEDULE_BASELINE')) AS k (code);

        UPDATE master_data_config.configuration_version
        SET lifecycle_state = 'PUBLISHED', published_at = now() - interval '1 hour', effective_from = now() - interval '1 hour'
        WHERE id IN ('00000000-0630-4000-8000-000000000101', '00000000-0630-4000-8000-000000000102', '00000000-0630-4000-8000-000000000103');
        """;

    public IdentityTestHost Identity { get; } = new();

    public IdentityDatabase Database => Identity.Database;

    public IdentityApiFactory Api { get; private set; } = null!;

    /// <summary>
    /// A document store, under the test output, removed with the host: WF-12 refuses an upload while none is configured (503) before it
    /// reaches a project, and nothing is written to it — a closed project's upload is refused first.
    /// </summary>
    public string StoreDirectory { get; } = Path.Combine(AppContext.BaseDirectory, "closure-documents-" + Guid.NewGuid().ToString("N"));

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(StoreDirectory);
        await Identity.InitializeAsync();
        await Database.ExecuteAsync(Fixture);
        Api = Identity.CreateApi(
            new Dictionary<string, string?>
            {
                ["Approval:Maintenance:PollInterval"] = "01:00:00",
                ["Risk:Maintenance:PollInterval"] = "01:00:00",
                ["Suspension:Activation:PollInterval"] = "01:00:00",
                ["Closure:Activation:PollInterval"] = "01:00:00",
                ["DocumentManagement:Scan:PollInterval"] = "01:00:00",
                ["DOCUMENT_STORAGE_CONNECTION_STRING"] = new Uri(StoreDirectory).AbsoluteUri,
            },
            services =>
            {
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
        Directory.Delete(StoreDirectory, recursive: true);
    }
}

[CollectionDefinition(Name)]
public sealed class ClosureSuite : ICollectionFixture<ClosureTestHost>
{
    public const string Name = "Closure";
}
