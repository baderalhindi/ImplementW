using Microsoft.Extensions.DependencyInjection;
using PMPlatform.Application.Features.DocumentManagement;
using PMPlatform.Tests.Integration.Identity;

namespace PMPlatform.Tests.Integration.DocumentManagement;

/// <summary>
/// The identity test database and people (IdentityDatabase) with what WF-12 needs and no environment has yet: a published
/// document type and evidence type, two ranked classifications (INTERNAL below CONFIDENTIAL, UGV-01 outstanding), document
/// clearance at INTERNAL, and test grants beside the shipped R04 ENTITY ones — Appendix A grants nothing else
/// (document-management.md F-1). A local directory is the store and <see cref="TestMalwareScanner"/> the scanner; the scan
/// worker stays idle so each test scans when it chooses.
/// </summary>
/// <remarks>
/// People: local.r02 holds every document permission at ALL (R02). local.r03 views and uploads for their department (R03,
/// DEPT anchor) and is also a member of <see cref="OtherDepartmentProjectId"/> with no document grant. local.r06 holds R06
/// only, and is a member of <see cref="IdentityDatabase.EntityProjectId"/>. local.r08 is ADR-013's entity Project Manager:
/// external, R04 bound to <see cref="IdentityDatabase.EntityProjectId"/>.
/// </remarks>
public sealed class DocumentTestHost : IAsyncLifetime
{
    public const long MaxFileSizeBytes = 256 * 1024;

    public static readonly Guid DocumentTypeId = new("00000000-0370-4000-8000-000000000001");
    public static readonly Guid DraftDocumentTypeId = new("00000000-0370-4000-8000-000000000002");
    public static readonly Guid Internal = new("00000000-0370-4000-8000-000000000011");
    public static readonly Guid Confidential = new("00000000-0370-4000-8000-000000000012");
    public static readonly Guid EvidenceTypeId = new("00000000-0370-4000-8000-000000000021");
    public static readonly Guid OtherDepartmentId = new("00000000-0370-4000-8000-000000000031");

    /// <summary>The entity Project Manager's own project.</summary>
    public static readonly Guid EntityProjectId = new(IdentityDatabase.EntityProjectId);

    /// <summary>Another project of the same entity, which local.r08 does not manage.</summary>
    public static readonly Guid SecondEntityProjectId = new("00000000-0370-4000-8000-000000000041");

    /// <summary>A project of another department, with no entity.</summary>
    public static readonly Guid OtherDepartmentProjectId = new("00000000-0370-4000-8000-000000000042");

    /// <summary>A project delivered by another entity.</summary>
    public static readonly Guid OtherEntityProjectId = new("00000000-0370-4000-8000-000000000043");

    private const string Seed = IdentityDatabase.SeedPrincipalId;

    public IdentityTestHost Identity { get; } = new();

    public IdentityDatabase Database => Identity.Database;

    public TestMalwareScanner Scanner { get; } = new();

    public IdentityApiFactory Api { get; private set; } = null!;

    /// <summary>The local directory the store writes to; removed with the host.</summary>
    public string StoreDirectory { get; } = Path.Combine(Path.GetTempPath(), "pmplatform-documents-it-" + Guid.NewGuid().ToString("N"));

    /// <summary>The settings every document API of this host shares; a test that needs another store or none overrides them.</summary>
    public Dictionary<string, string?> Settings => new()
    {
        ["DOCUMENT_STORAGE_CONNECTION_STRING"] = new Uri(StoreDirectory).AbsoluteUri,
        ["DocumentManagement:Upload:MaxFileSizeBytes"] = MaxFileSizeBytes.ToString(System.Globalization.CultureInfo.InvariantCulture),
        ["DocumentManagement:Scan:PollInterval"] = "01:00:00",
        ["Outbox:PollInterval"] = "01:00:00",
        ["Approval:Maintenance:PollInterval"] = "01:00:00",
    };

    private static string Fixture => $"""
        INSERT INTO master_data_config.master_data_item (id, catalogue_id, code, label_ar, label_en, sort_order, lifecycle_state, created_at, created_by, updated_at, updated_by)
        SELECT i.id::uuid, c.id, i.code, 'اختبار', i.label_en, i.sort_order, i.state, now(), '{Seed}', now(), '{Seed}'
        FROM (VALUES ('{DocumentTypeId}', 'DOCUMENT_TYPE', 'TEST_REPORT', 'Test report', 0, 'PUBLISHED'),
                     ('{DraftDocumentTypeId}', 'DOCUMENT_TYPE', 'TEST_DRAFT_TYPE', 'Test draft type', 0, 'DRAFT'),
                     ('{Internal}', 'DATA_CLASSIFICATION', 'TEST_INTERNAL', 'Test internal', 10, 'PUBLISHED'),
                     ('{Confidential}', 'DATA_CLASSIFICATION', 'TEST_CONFIDENTIAL', 'Test confidential', 20, 'PUBLISHED'),
                     ('{EvidenceTypeId}', 'EVIDENCE_TYPE', 'TEST_ACHIEVEMENT_PROOF', 'Test achievement proof', 0, 'PUBLISHED'))
             AS i (id, catalogue, code, label_en, sort_order, state)
        JOIN master_data_config.master_data_catalogue c ON c.code = i.catalogue;

        UPDATE identity_access.permission SET data_classification_item_id = '{Internal}'
        WHERE code IN ('DOCUMENT_VIEW', 'DOCUMENT_UPLOAD', 'DOCUMENT_MANAGE');

        INSERT INTO identity_access.permission_profile_grant (id, permission_profile_version_id, permission_id, data_scope, created_at, created_by, updated_at, updated_by)
        SELECT gen_random_uuid(), g.version_id::uuid, p.id, g.scope, now(), '{Seed}', now(), '{Seed}'
        FROM (VALUES ('{IdentityDatabase.ProfileVersionId(2)}', 'DOCUMENT_VIEW', 'ALL'), ('{IdentityDatabase.ProfileVersionId(2)}', 'DOCUMENT_UPLOAD', 'ALL'),
                     ('{IdentityDatabase.ProfileVersionId(2)}', 'DOCUMENT_MANAGE', 'ALL'),
                     ('{IdentityDatabase.ProfileVersionId(3)}', 'DOCUMENT_VIEW', 'DEPT'), ('{IdentityDatabase.ProfileVersionId(3)}', 'DOCUMENT_UPLOAD', 'DEPT'))
             AS g (version_id, code, scope)
        JOIN identity_access.permission p ON p.code = g.code;

        INSERT INTO identity_access.department (id, code, name_ar, name_en, directory_reference, created_at, created_by, updated_at, updated_by)
        VALUES ('{OtherDepartmentId}', 'DEPT-DOCUMENT-TEST', 'إدارة أخرى', 'Other test department', 'DEPT-DOCUMENT-OTHER', now(), '{Seed}', now(), '{Seed}');

        INSERT INTO project.project (id, title, title_lang, classification_item_id, department_id, external_entity_id, lifecycle_state,
                                     governance_profile_item_id, participation_mode, created_at, created_by, updated_at, updated_by)
        SELECT p.id::uuid, p.title, 'en', '00000000-0131-4000-8000-000000000001', p.department::uuid, p.entity::uuid, 'SUBMITTED', i.id, p.mode, now(), '{Seed}', now(), '{Seed}'
        FROM (VALUES ('{SecondEntityProjectId}', 'Second entity project', '{IdentityDatabase.DepartmentId}', '{IdentityDatabase.ActiveEntityId}', 'ENTITY_MANAGED'),
                     ('{OtherDepartmentProjectId}', 'Other department project', '{OtherDepartmentId}', NULL, 'AHDA_MANAGED'),
                     ('{OtherEntityProjectId}', 'Other entity project', '{IdentityDatabase.DepartmentId}', '{IdentityDatabase.SuspendedEntityId}', 'ENTITY_MANAGED'))
             AS p (id, title, department, entity, mode)
        CROSS JOIN master_data_config.master_data_item i JOIN master_data_config.master_data_catalogue c ON c.id = i.catalogue_id
        WHERE c.code = 'GOVERNANCE_PROFILE' AND i.code = 'STANDARD';

        -- Project members with no document grant: local.r06 on the entity project, local.r03 on the other department's.
        INSERT INTO identity_access.access_relationship (id, user_id, permission_profile_version_id, project_id, starts_at, status, created_at, created_by, updated_at, updated_by)
        VALUES ('00000000-0370-4000-8000-000000000051', '{IdentityDatabase.UserId(6)}', '{IdentityDatabase.ProfileVersionId(6)}', '{EntityProjectId}', now() - interval '1 day', 'ACTIVE', now(), '{Seed}', now(), '{Seed}'),
               ('00000000-0370-4000-8000-000000000052', '{IdentityDatabase.UserId(3)}', '{IdentityDatabase.ProfileVersionId(6)}', '{OtherDepartmentProjectId}', now() - interval '1 day', 'ACTIVE', now(), '{Seed}', now(), '{Seed}');
        """;

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(StoreDirectory);
        await Identity.InitializeAsync();
        await Database.ExecuteAsync(Fixture);
        Api = CreateApi();
    }

    /// <summary>An API over this database with the host's settings, <paramref name="overrides"/> on top; the test scanner unless <paramref name="withScanner"/> is false.</summary>
    public IdentityApiFactory CreateApi(IReadOnlyDictionary<string, string?>? overrides = null, bool withScanner = true)
    {
        Dictionary<string, string?> settings = Settings;
        foreach ((string key, string? value) in overrides ?? new Dictionary<string, string?>())
        {
            settings[key] = value;
        }

        return Identity.CreateApi(settings, services =>
        {
            if (withScanner)
            {
                services.AddSingleton<IMalwareScanner>(Scanner);
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
public sealed class DocumentSuite : ICollectionFixture<DocumentTestHost>
{
    public const string Name = "DocumentManagement";
}
