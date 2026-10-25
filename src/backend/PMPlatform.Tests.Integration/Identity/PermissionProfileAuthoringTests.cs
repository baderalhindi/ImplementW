using System.Net;
using System.Text.Json.Nodes;
using PMPlatform.Tests.Integration.MasterDataConfig;

namespace PMPlatform.Tests.Integration.Identity;

/// <summary>
/// ADM-008 permission profile authoring (TASK-110; the 24 Oct 2026 review, C4): a profile cloned from a shipped default, its grid
/// edited by its author with the diff audited, validated by a second person and published by a third, neither holding the profile;
/// the grid held to the catalogue and GrantPolicy; assignments moved explicitly; R01–R08's profiles never left without a version.
/// The suite has its own database: it moves assignments.
/// </summary>
[Collection(PermissionProfileSuite.Name)]
public sealed class PermissionProfileAuthoringTests(MasterDataConfigTestHost host)
{
    private const string Versions = "/api/v1/permission-profile-versions";
    private const string ProjectManagerRoleId = "00000000-0000-4000-8000-000000000004";
    private const string AdministratorRoleId = "00000000-0000-4000-8000-000000000001";
    private const string AdministratorProfileId = "00000000-0001-4000-8000-000000000001";

    [Fact]
    public async Task AProfileIsClonedEditedWithItsDiffAuditedAndPublishedByThreePeople()
    {
        using HttpClient client = host.Api.CreateClient();
        Crew crew = await client.SignInCrewAsync();
        (JsonObject draft, string eTag) = await CreateAsync(client, crew.Author, "PM_LIMITED", ProjectManagerRoleId, IdentityDatabase.ProfileVersionId(4));
        Guid versionId = AdministrationApi.IdOf(draft);
        JsonObject[] cloned = [.. draft["grants"]!.AsArray().Select(g => g!.AsObject())];
        Assert.Equal((1, "DRAFT"), (draft["versionNo"]!.GetValue<int>(), draft["lifecycleState"]!.GetValue<string>()));
        Assert.NotEmpty(cloned);

        // Drop the first grant, widen the second, keep the rest.
        string dropped = Code(cloned[0]);
        string widened = Code(cloned[1]);
        string widenedTo = Scope(cloned[1]) == "ALL" ? "OWN" : "ALL";
        object[] grid = [.. cloned.Skip(1).Select(g => new { permissionCode = Code(g), dataScope = Code(g) == widened ? widenedTo : Scope(g) })];
        object body = new { grants = grid, changeSummary = new { text = "Narrower Project Manager.", language = "en" } };

        using (HttpResponseMessage unconditional = await client.PutAsync($"{Versions}/{versionId}/grants", crew.Author, body, ifMatch: null))
        {
            Assert.Equal(HttpStatusCode.PreconditionRequired, unconditional.StatusCode);
        }

        using (HttpResponseMessage notTheAuthor = await client.PutAsync($"{Versions}/{versionId}/grants", crew.Reviewer, body, eTag))
        {
            Assert.Equal((HttpStatusCode.UnprocessableEntity, "IDENTITY_ACCESS_SEPARATION_OF_DUTIES"), (notTheAuthor.StatusCode, await notTheAuthor.CodeOfAsync()));
        }

        using HttpResponseMessage edited = await client.PutAsync($"{Versions}/{versionId}/grants", crew.Author, body, eTag);
        Assert.Equal(HttpStatusCode.OK, edited.StatusCode);
        JsonObject saved = await edited.ReadObjectAsync();
        Assert.Equal(cloned.Length - 1, saved["grants"]!.AsArray().Count);
        Assert.Equal("Narrower Project Manager.", saved["changeSummary"]!["text"]!.GetValue<string>());

        Assert.Equal(
            [$"grant_{dropped.ToLowerInvariant()} {Scope(cloned[0])}->", $"grant_{widened.ToLowerInvariant()} {Scope(cloned[1])}->{widenedTo}"],
            (await host.Database.QueryAsync($"""
                SELECT a.attribute_name || ' ' || coalesce(a.old_value, '') || '->' || coalesce(a.new_value, '')
                FROM audit_activity.audit_event e JOIN audit_activity.audit_event_attribute a ON a.audit_event_id = e.id
                WHERE e.subject_id = '{versionId}' AND e.event_type = 'IdentityAccess.PermissionProfileGrantsChanged' AND a.attribute_name LIKE 'grant\_%'
                ORDER BY a.attribute_name
                """)).Order(StringComparer.Ordinal));

        using (HttpResponseMessage selfValidated = await client.PostAsync($"{Versions}/{versionId}/validate", crew.Author))
        {
            Assert.Equal((HttpStatusCode.UnprocessableEntity, "IDENTITY_ACCESS_SEPARATION_OF_DUTIES"), (selfValidated.StatusCode, await selfValidated.CodeOfAsync()));
        }

        using (HttpResponseMessage validated = await client.PostAsync($"{Versions}/{versionId}/validate", crew.Reviewer))
        {
            Assert.Equal(HttpStatusCode.OK, validated.StatusCode);
        }

        using (HttpResponseMessage reviewerPublishes = await client.PostAsync($"{Versions}/{versionId}/publish", crew.Reviewer))
        {
            Assert.Equal((HttpStatusCode.UnprocessableEntity, "IDENTITY_ACCESS_SEPARATION_OF_DUTIES"), (reviewerPublishes.StatusCode, await reviewerPublishes.CodeOfAsync()));
        }

        using HttpResponseMessage published = await client.PostAsync($"{Versions}/{versionId}/publish", crew.Publisher);
        Assert.Equal(HttpStatusCode.OK, published.StatusCode);
        JsonObject version = await published.ReadObjectAsync();
        Assert.Equal(
            ("PUBLISHED", IdentityDatabase.UserId(MasterDataConfigTestHost.Author), IdentityDatabase.UserId(MasterDataConfigTestHost.Reviewer), IdentityDatabase.UserId(MasterDataConfigTestHost.Publisher)),
            (version["lifecycleState"]!.GetValue<string>(), version["createdBy"]!.GetValue<string>(), version["validatedByUserId"]!.GetValue<string>(), version["publishedByUserId"]!.GetValue<string>()));

        using (HttpResponseMessage frozen = await client.PutAsync($"{Versions}/{versionId}/grants", crew.Author, body, AdministrationApi.ETagOf(published)))
        {
            Assert.Equal(HttpStatusCode.Conflict, frozen.StatusCode);
        }

        Assert.Equal(
            ["PermissionProfileCreated", "PermissionProfileGrantsChanged", "PermissionProfileVersionDrafted", "PermissionProfileVersionPublished", "PermissionProfileVersionValidated"],
            (await host.Database.QueryAsync($"""
                SELECT DISTINCT replace(e.event_type, 'IdentityAccess.', '') FROM audit_activity.audit_event e
                WHERE e.subject_id IN ('{versionId}', '{draft["permissionProfileId"]!.GetValue<string>()}')
                """)).Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task AGridIsHeldToTheCatalogueAndToWhatItsBaseRoleMayHold()
    {
        using HttpClient client = host.Api.CreateClient();
        Crew crew = await client.SignInCrewAsync();
        (JsonObject draft, string eTag) = await CreateAsync(client, crew.Author, "ADMIN_ONLY", AdministratorRoleId, sourceVersionId: null);
        Assert.Empty(draft["grants"]!.AsArray());

        object[] ruled =
        [
            new { permissionCode = "USER_VIEW", dataScope = "ALL" },
            new { permissionCode = "PROJECT_VIEW", dataScope = "ALL" },
            new { permissionCode = "NOT_A_PERMISSION", dataScope = "ALL" },
            new { permissionCode = "USER_VIEW", dataScope = "OWN" },
        ];
        using (HttpResponseMessage shape = await client.PutAsync(
                   $"{Versions}/{AdministrationApi.IdOf(draft)}/grants", crew.Author, new { grants = (object[])[.. ruled, new { permissionCode = "ROLE_VIEW", dataScope = "EVERYWHERE" }] }, eTag))
        {
            Assert.Equal(HttpStatusCode.BadRequest, shape.StatusCode);
            Assert.Equal(["grants[4].dataScope ENUM_VALUE"], await shape.ReadFieldErrorsAsync());
        }

        object rules = new { grants = ruled };
        using HttpResponseMessage refused = await client.PutAsync($"{Versions}/{AdministrationApi.IdOf(draft)}/grants", crew.Author, rules, eTag);
        Assert.Equal((HttpStatusCode.UnprocessableEntity, "IDENTITY_ACCESS_PROFILE_GRANT_INVALID"), (refused.StatusCode, await refused.CodeOfAsync()));
        Assert.Equal(
            ["grants[1].permissionCode NOT_ALLOWED", "grants[2].permissionCode NOT_FOUND", "grants[3].permissionCode DUPLICATE"],
            await refused.ReadFieldErrorsAsync());
    }

    [Fact]
    public async Task NoOneApprovesAProfileTheyHold()
    {
        using HttpClient client = host.Api.CreateClient();
        Crew crew = await client.SignInCrewAsync();
        using HttpResponseMessage created = await client.PostAsync($"{AdministrationApi.PermissionProfiles}/{AdministratorProfileId}/versions", crew.Author);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Guid versionId = AdministrationApi.IdOf(await created.ReadObjectAsync());

        using (HttpResponseMessage second = await client.PostAsync($"{AdministrationApi.PermissionProfiles}/{AdministratorProfileId}/versions", crew.Author))
        {
            Assert.Equal((HttpStatusCode.Conflict, "IDENTITY_ACCESS_PROFILE_DRAFT_OPEN"), (second.StatusCode, await second.CodeOfAsync()));
        }

        // The reviewer holds R01's shipped profile: validating a new version of it would approve its own access.
        using (HttpResponseMessage validated = await client.PostAsync($"{Versions}/{versionId}/validate", crew.Reviewer))
        {
            Assert.Equal((HttpStatusCode.UnprocessableEntity, "IDENTITY_ACCESS_SELF_APPROVAL"), (validated.StatusCode, await validated.CodeOfAsync()));
        }

        using HttpResponseMessage abandoned = await client.PostAsync($"{Versions}/{versionId}/retire", crew.Author);
        Assert.Equal(HttpStatusCode.OK, abandoned.StatusCode);
    }

    [Fact]
    public async Task AssignmentsStayOnTheirVersionUntilMigratedAndAHeldOrLastShippedVersionIsNotRetired()
    {
        using HttpClient client = host.Api.CreateClient();
        Crew crew = await client.SignInCrewAsync();
        (JsonObject first, _) = await CreateAsync(client, crew.Author, "PM_MIGRATED", ProjectManagerRoleId, IdentityDatabase.ProfileVersionId(4));
        Guid firstId = AdministrationApi.IdOf(first);
        await GovernAsync(client, crew, firstId);

        using (HttpResponseMessage assigned = await client.PostAsync(
                   AdministrationApi.AccessRelationships, crew.Author, new { userId = IdentityDatabase.UserId(5), permissionProfileVersionId = firstId }))
        {
            Assert.Equal(HttpStatusCode.Created, assigned.StatusCode);
        }

        using HttpResponseMessage created = await client.PostAsync(
            $"{AdministrationApi.PermissionProfiles}/{first["permissionProfileId"]!.GetValue<string>()}/versions", crew.Author);
        Guid secondId = AdministrationApi.IdOf(await created.ReadObjectAsync());
        Assert.Equal(first["grants"]!.AsArray().Count, (await (await client.GetAsync($"{Versions}/{secondId}", crew.Author)).ReadObjectAsync())["grants"]!.AsArray().Count);

        using (HttpResponseMessage early = await client.PostAsync($"{Versions}/{secondId}/assignment-migrations", crew.Author))
        {
            Assert.Equal((HttpStatusCode.UnprocessableEntity, "IDENTITY_ACCESS_MIGRATION_TARGET_INVALID"), (early.StatusCode, await early.CodeOfAsync()));
        }

        await GovernAsync(client, crew, secondId);
        using (HttpResponseMessage held = await client.PostAsync($"{Versions}/{firstId}/retire", crew.Author))
        {
            Assert.Equal((HttpStatusCode.UnprocessableEntity, "IDENTITY_ACCESS_PROFILE_VERSION_IN_USE"), (held.StatusCode, await held.CodeOfAsync()));
        }

        using (HttpResponseMessage migrated = await client.PostAsync($"{Versions}/{secondId}/assignment-migrations", crew.Author))
        {
            Assert.Equal(HttpStatusCode.OK, migrated.StatusCode);
            Assert.Equal(1, (await migrated.ReadObjectAsync())["migratedAssignments"]!.GetValue<int>());
        }

        using (HttpResponseMessage retired = await client.PostAsync($"{Versions}/{firstId}/retire", crew.Author))
        {
            Assert.Equal(HttpStatusCode.OK, retired.StatusCode);
        }

        // R01–R08 always keep a published version.
        using HttpResponseMessage shipped = await client.PostAsync($"{Versions}/{IdentityDatabase.ProfileVersionId(7)}/retire", crew.Author);
        Assert.Equal((HttpStatusCode.UnprocessableEntity, "IDENTITY_ACCESS_PROFILE_VERSION_IN_USE"), (shipped.StatusCode, await shipped.CodeOfAsync()));
    }

    [Fact]
    public async Task OnlyAHolderOfRoleManageAuthorsAProfile()
    {
        using HttpClient client = host.Api.CreateClient();
        string viewer = (await client.SignInOrFailAsync(MasterDataConfigTestHost.Viewer)).AccessToken;
        using HttpResponseMessage refused = await client.PostAsync(
            AdministrationApi.PermissionProfiles, viewer, new { code = "NOT_MINE", name = new { ar = "ملف", en = "Profile" }, baseRoleId = ProjectManagerRoleId });
        Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
    }

    private static async Task<(JsonObject Version, string ETag)> CreateAsync(HttpClient client, string token, string code, string baseRoleId, string? sourceVersionId)
    {
        using HttpResponseMessage response = await client.PostAsync(
            AdministrationApi.PermissionProfiles, token, new { code, name = new { ar = "ملف اختبار", en = "Test profile" }, baseRoleId, sourceVersionId });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.ReadObjectAsync(), AdministrationApi.ETagOf(response));
    }

    private static async Task GovernAsync(HttpClient client, Crew crew, Guid versionId)
    {
        using (HttpResponseMessage validated = await client.PostAsync($"{Versions}/{versionId}/validate", crew.Reviewer))
        {
            Assert.Equal(HttpStatusCode.OK, validated.StatusCode);
        }

        using HttpResponseMessage published = await client.PostAsync($"{Versions}/{versionId}/publish", crew.Publisher);
        Assert.Equal(HttpStatusCode.OK, published.StatusCode);
    }

    private static string Code(JsonObject grant) => grant["permissionCode"]!.GetValue<string>();

    private static string Scope(JsonObject grant) => grant["dataScope"]!.GetValue<string>();
}

[CollectionDefinition(Name)]
public sealed class PermissionProfileSuite : ICollectionFixture<MasterDataConfigTestHost>
{
    public const string Name = "PermissionProfiles";
}
