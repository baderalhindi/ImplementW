using System.Net;
using System.Text.Json.Nodes;
using PMPlatform.Tests.Integration.Identity;

namespace PMPlatform.Tests.Integration.DataImport;

/// <summary>
/// TASK-102 on the reference registers loaded before any project: external entities and users land in their legacy state through
/// ADM-004/013's rules; master data items and KPI definitions land DRAFT, authored by the importer, so the governed lifecycle's
/// separation of duties still holds (data-import.md D-6).
/// </summary>
[Collection(IdentitySuite.Name)]
public sealed class ReferenceDataImportTests(IdentityTestHost host)
{
    private const string MasterDataItems = "/api/v1/master-data-items";
    private const string KpiDefinitions = "/api/v1/kpi-definitions";

    [Fact]
    public async Task ExternalEntitiesLandInTheirLegacyStatusWithTheirTypeAndSponsor()
    {
        string run = Run();
        using HttpClient client = host.Api.CreateClient();
        Session administrator = await client.SignInOrFailAsync(1);
        byte[] workbook = ImportApi.Workbook(
            ["legacy_id", "code", "name_ar", "name_en", "entity_type_code", "sponsor_username", "status"],
            [$"E{run}-1", $"ENT-{run}-A", "جهة أ", "Entity A", "PRIVATE_COMPANY", "local.r02", null],
            [$"E{run}-2", $"ENT-{run}-B", "جهة ب", "Entity B", "GOVERNMENT", null, "suspended"],
            [$"E{run}-3", $"ENT-{run}-C", "جهة ج", "Entity C", "GOVERNMENT", null, "RETIRED"]);

        JsonObject report = await (await client.UploadAsync(AdministrationApi.ExternalEntities, administrator.AccessToken, workbook, commit: true)).ReadObjectAsync();

        Assert.Empty(ImportApi.IssuesOf(report));
        Assert.Equal(
            [$"ENT-{run}-A|PRIVATE_COMPANY|ACTIVE|local.r02", $"ENT-{run}-B|GOVERNMENT|SUSPENDED|-", $"ENT-{run}-C|GOVERNMENT|RETIRED|-"],
            await host.Database.QueryAsync($"""
                SELECT e.code || '|' || i.code || '|' || e.status || '|' || coalesce(u.username, '-')
                FROM identity_access.external_entity e
                JOIN master_data_config.master_data_item i ON i.id = e.entity_type_item_id
                LEFT JOIN identity_access."user" u ON u.id = e.sponsor_user_id
                WHERE e.code LIKE 'ENT-{run}-%' ORDER BY e.code
                """));
    }

    [Fact]
    public async Task AnEntityNamingAnUnknownTypeOrSponsorIsRefusedByColumn()
    {
        string run = Run();
        using HttpClient client = host.Api.CreateClient();
        Session administrator = await client.SignInOrFailAsync(1);
        byte[] workbook = ImportApi.Workbook(
            ["legacy_id", "code", "name_ar", "name_en", "entity_type_code", "sponsor_username"],
            [$"F{run}-1", $"ENT-{run}-A", "جهة أ", "Entity A", "NO_SUCH_TYPE", null],
            [$"F{run}-2", $"ENT-{run}-B", "جهة ب", "Entity B", "GOVERNMENT", "nobody"],
            [$"F{run}-3", $"ENT-{run}-C", "جهة ج", "Entity C", "GOVERNMENT", "local.r08"],
            [$"F{run}-4", "ENT-IDENTITY-ACTIVE", "جهة د", "Entity D", "GOVERNMENT", null]);

        JsonObject report = await (await client.UploadAsync(AdministrationApi.ExternalEntities, administrator.AccessToken, workbook, commit: false)).ReadObjectAsync();

        // local.r08 is external: a sponsor must be an active internal user, the service's own rule.
        Assert.Equal(
            ["2 entity_type_code REFERENCE_NOT_FOUND", "3 sponsor_username REFERENCE_NOT_FOUND", "4 sponsor_username INACTIVE", "5 code ALREADY_EXISTS"],
            ImportApi.IssuesOf(report));
    }

    [Fact]
    public async Task UsersLandWithTheirTypeEntityAndStatus()
    {
        string run = Run().ToLowerInvariant();
        using HttpClient client = host.Api.CreateClient();
        Session administrator = await client.SignInOrFailAsync(1);
        string[] header = ["legacy_id", "user_type", "username", "display_name", "email", "mobile_number", "preferred_language", "directory_subject_id", "job_title", "external_entity_code", "status"];
        byte[] workbook = ImportApi.Workbook(header,
            [$"U{run}-1", "INTERNAL", $"imp.{run}.a", "Internal A", $"a.{run}@ahda.test", "+966500000001", "en", $"SUBJ-{run}-A", null, null, null],
            [$"U{run}-2", "EXTERNAL", $"imp.{run}.b", "External B", $"b.{run}@entity.test", null, null, null, "Engineer", "ENT-IDENTITY-ACTIVE", "DISABLED"]);

        JsonObject report = await (await client.UploadAsync(AdministrationApi.Users, administrator.AccessToken, workbook, commit: true)).ReadObjectAsync();

        Assert.Empty(ImportApi.IssuesOf(report));
        Assert.Equal(
            [$"imp.{run}.a|INTERNAL|en|SUBJ-{run}-A|-|-|ACTIVE|+966500000001", $"imp.{run}.b|EXTERNAL|ar|-|Engineer|ENT-IDENTITY-ACTIVE|DISABLED|-"],
            await host.Database.QueryAsync($"""
                SELECT u.username || '|' || u.user_type || '|' || u.preferred_language || '|' || coalesce(u.directory_subject_id, '-') || '|'
                       || coalesce(u.job_title, '-') || '|' || coalesce(e.code, '-') || '|' || u.status || '|' || coalesce(u.mobile_number, '-')
                FROM identity_access."user" u LEFT JOIN identity_access.external_entity e ON e.id = u.external_entity_id
                WHERE u.username LIKE 'imp.{run}.%' ORDER BY u.username
                """));
    }

    [Fact]
    public async Task AUserRowIsHeldToTheRulesOfItsType()
    {
        string run = Run().ToLowerInvariant();
        using HttpClient client = host.Api.CreateClient();
        Session administrator = await client.SignInOrFailAsync(1);
        string[] header = ["legacy_id", "user_type", "username", "display_name", "email", "directory_subject_id", "job_title", "external_entity_code"];
        byte[] workbook = ImportApi.Workbook(header,
            [$"W{run}-1", "INTERNAL", $"imp.{run}.a", "No subject", $"a.{run}@ahda.test", null, null, null],
            [$"W{run}-2", "EXTERNAL", $"imp.{run}.b", "No entity", $"b.{run}@entity.test", null, null, null],
            [$"W{run}-3", "INTERNAL", $"imp.{run}.c", "Has a title", $"c.{run}@ahda.test", $"SUBJ-{run}-C", "Director", null],
            [$"W{run}-4", "INTERNAL", "local.r01", "Taken", $"d.{run}@ahda.test", $"SUBJ-{run}-D", null, null],
            [$"W{run}-5", "SERVICE", $"imp.{run}.e", "Service", $"e.{run}@ahda.test", null, null, null]);

        JsonObject report = await (await client.UploadAsync(AdministrationApi.Users, administrator.AccessToken, workbook, commit: false)).ReadObjectAsync();

        Assert.Equal(
            ["6 user_type ENUM_VALUE", "2 directory_subject_id REQUIRED", "3 external_entity_code REQUIRED", "4 job_title NOT_ALLOWED", "5 username ALREADY_EXISTS"],
            ImportApi.IssuesOf(report));
    }

    [Fact]
    public async Task MasterDataItemsLandDraftAuthoredByTheImporter()
    {
        string run = Run();
        using HttpClient client = host.Api.CreateClient();
        Session administrator = await client.SignInOrFailAsync(1);
        string[] header = ["legacy_id", "catalogue_code", "code", "label_ar", "label_en", "description_ar", "description_en", "parent_code", "sort_order"];
        byte[] workbook = ImportApi.Workbook(header,
            [$"M{run}-1", "PROJECT_CLASSIFICATION", $"CLS-{run}-A", "تصنيف أ", "Class A", "وصف", "Description", null, "2"],
            [$"M{run}-2", "PROJECT_CLASSIFICATION", $"CLS-{run}-B", "تصنيف ب", "Class B", null, null, null, null]);

        JsonObject report = await (await client.UploadAsync(MasterDataItems, administrator.AccessToken, workbook, commit: true)).ReadObjectAsync();

        Assert.Empty(ImportApi.IssuesOf(report));
        Assert.Equal(
            [$"CLS-{run}-A|DRAFT|2|Description|{IdentityDatabase.UserId(1)}", $"CLS-{run}-B|DRAFT|0|-|{IdentityDatabase.UserId(1)}"],
            await host.Database.QueryAsync($"""
                SELECT code || '|' || lifecycle_state || '|' || sort_order || '|' || coalesce(description_en, '-') || '|' || created_by
                FROM master_data_config.master_data_item WHERE code LIKE 'CLS-{run}-%' ORDER BY code
                """));
    }

    [Fact]
    public async Task AMasterDataRowIsHeldToItsCatalogue()
    {
        string run = Run();
        using HttpClient client = host.Api.CreateClient();
        Session administrator = await client.SignInOrFailAsync(1);
        string[] header = ["legacy_id", "catalogue_code", "code", "label_ar", "label_en", "description_ar", "parent_code"];
        byte[] workbook = ImportApi.Workbook(header,
            [$"X{run}-1", "NO_SUCH_CATALOGUE", $"CLS-{run}-A", "أ", "A", null, null],
            [$"X{run}-2", "PROJECT_CLASSIFICATION", $"CLS-{run}-B", "ب", "B", null, null],
            [$"X{run}-3", "PROJECT_CLASSIFICATION", $"CLS-{run}-C", "ج", "C", null, $"CLS-{run}-B"],
            [$"X{run}-4", "PROJECT_CLASSIFICATION", $"CLS-{run}-D", "د", "D", "وصف بلا ترجمة", null],
            [$"X{run}-5", "EXTERNAL_ENTITY_TYPE", "GOVERNMENT", "حكومية", "Government", null, null]);

        JsonObject report = await (await client.UploadAsync(MasterDataItems, administrator.AccessToken, workbook, commit: false)).ReadObjectAsync();

        // PROJECT_CLASSIFICATION has no hierarchy, which is the service's rule: the parent resolves and is then refused.
        Assert.Equal(
            ["2 catalogue_code REFERENCE_NOT_FOUND", "4 parent_code NOT_ALLOWED", "5 description_en REQUIRED", "6 code ALREADY_EXISTS"],
            ImportApi.IssuesOf(report));
    }

    [Fact]
    public async Task KpiDefinitionsLandDraftWithAPublishedUnit()
    {
        string run = Run();
        await host.Database.ExecuteAsync($"""
            INSERT INTO master_data_config.master_data_item (id, catalogue_id, code, label_ar, label_en, lifecycle_state, created_at, created_by, updated_at, updated_by)
            SELECT gen_random_uuid(), c.id, 'UNIT-{run}', 'نسبة', 'Percent', 'PUBLISHED', now(), '{IdentityDatabase.SeedPrincipalId}', now(), '{IdentityDatabase.SeedPrincipalId}'
            FROM master_data_config.master_data_catalogue c WHERE c.code = 'KPI_UNIT'
            """);
        using HttpClient client = host.Api.CreateClient();
        Session administrator = await client.SignInOrFailAsync(1);
        string[] header = ["legacy_id", "code", "name_ar", "name_en", "unit_code", "direction"];
        byte[] workbook = ImportApi.Workbook(header,
            [$"K{run}-1", $"KPI-{run}-A", "مؤشر أ", "KPI A", $"UNIT-{run}", "HIGHER_IS_BETTER"],
            [$"K{run}-2", $"KPI-{run}-B", "مؤشر ب", "KPI B", "NO-SUCH-UNIT", "TARGET_BAND"],
            [$"K{run}-3", $"KPI-{run}-C", "مؤشر ج", "KPI C", $"UNIT-{run}", "SIDEWAYS"]);

        using HttpResponseMessage refused = await client.UploadAsync(KpiDefinitions, administrator.AccessToken, workbook, commit: true);
        Assert.Equal(["4 direction ENUM_VALUE", "3 unit_code REFERENCE_NOT_FOUND"], ImportApi.IssuesOf(await refused.ReadObjectAsync()));

        byte[] clean = ImportApi.Workbook(header, [$"K{run}-1", $"KPI-{run}-A", "مؤشر أ", "KPI A", $"UNIT-{run}", "HIGHER_IS_BETTER"]);
        using HttpResponseMessage imported = await client.UploadAsync(KpiDefinitions, administrator.AccessToken, clean, commit: true);

        Assert.Equal(HttpStatusCode.OK, imported.StatusCode);
        Assert.Equal(
            [$"KPI-{run}-A|DRAFT|HIGHER_IS_BETTER|UNIT-{run}"],
            await host.Database.QueryAsync($"""
                SELECT d.code || '|' || d.lifecycle_state || '|' || d.direction || '|' || i.code
                FROM master_data_config.kpi_definition d JOIN master_data_config.master_data_item i ON i.id = d.unit_item_id
                WHERE d.code LIKE 'KPI-{run}-%'
                """));
    }

    private static string Run() => Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
}
