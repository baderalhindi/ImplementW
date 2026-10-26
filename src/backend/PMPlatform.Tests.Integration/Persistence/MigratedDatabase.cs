namespace PMPlatform.Tests.Integration.Persistence;

/// <summary>
/// One throwaway database with every migration applied and a minimal set of reference rows, shared by the tests of
/// one class. Tests that write either roll back (<see cref="TestDatabase.ExecuteRolledBackAsync"/>) or only add rows
/// no other test reads.
/// </summary>
public sealed class MigratedDatabase : TestDatabase
{
    public const string ServiceUserId = "00000000-0000-4000-8000-0000000000ff";
    public const string DepartmentId = "00000000-0020-4000-8000-000000000001";
    public const string MasterDataItemId = "00000000-0031-4000-8000-000000000001";
    public const string ConfigurationVersionId = "00000000-0041-4000-8000-000000000001";

    /// <summary>Every row is attributed to the SERVICE principal (ERD D-2); the reference data is one of each parent a test row needs.</summary>
    private const string ReferenceRows = $"""
        INSERT INTO identity_access."user" (id, user_type, username, display_name, email, preferred_language, status, created_at, created_by, updated_at, updated_by)
        VALUES ('{ServiceUserId}', 'SERVICE', 'svc.test', 'Test service principal', 'svc.test@pmplatform.test', 'en', 'ACTIVE', now(), '{ServiceUserId}', now(), '{ServiceUserId}');

        INSERT INTO identity_access.department (id, code, name_ar, name_en, created_at, created_by, updated_at, updated_by)
        VALUES ('{DepartmentId}', 'DEPT-TEST', 'إدارة الاختبار', 'Test Department', now(), '{ServiceUserId}', now(), '{ServiceUserId}');

        INSERT INTO master_data_config.master_data_catalogue (id, code, name_ar, name_en, created_at, created_by, updated_at, updated_by)
        VALUES ('00000000-0030-4000-8000-000000000001', 'TEST_CATALOGUE', 'فهرس الاختبار', 'Test catalogue', now(), '{ServiceUserId}', now(), '{ServiceUserId}');

        INSERT INTO master_data_config.master_data_item (id, catalogue_id, code, label_ar, label_en, lifecycle_state, created_at, created_by, updated_at, updated_by)
        VALUES ('{MasterDataItemId}', '00000000-0030-4000-8000-000000000001', 'TEST_ITEM', 'عنصر الاختبار', 'Test item', 'PUBLISHED', now(), '{ServiceUserId}', now(), '{ServiceUserId}');

        INSERT INTO master_data_config.configuration_family (id, code, name_ar, name_en, created_at, created_by, updated_at, updated_by)
        VALUES ('00000000-0040-4000-8000-000000000001', 'RISK_MATRIX', 'مصفوفة المخاطر', 'Risk matrix', now(), '{ServiceUserId}', now(), '{ServiceUserId}');

        INSERT INTO master_data_config.configuration_version (id, configuration_family_id, version_no, lifecycle_state, created_at, created_by, updated_at, updated_by)
        VALUES ('{ConfigurationVersionId}', '00000000-0040-4000-8000-000000000001', 1, 'DRAFT', now(), '{ServiceUserId}', now(), '{ServiceUserId}');
        """;

    protected override Task PopulateAsync() => ExecuteAsync(ReferenceRows);
}
