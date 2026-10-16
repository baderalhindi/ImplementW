using System.Net;
using System.Text.Json.Nodes;
using PMPlatform.Tests.Integration.Identity;

namespace PMPlatform.Tests.Integration.MasterDataConfig;

/// <summary>
/// ADM-020–029's lists and the edits no lifecycle test makes (TASK-084: FG-04 was the one module near the coverage minimum,
/// test-strategy.md §5.3): a catalogue renamed under If-Match and audited, items, versions and KPI definitions listed through
/// their filters, and a DRAFT KPI definition edited by its author only. The text searches contain "_": until TASK-084 no admin
/// search with a wildcard character found anything (AdministrationPersistence.LikeEscape).
/// </summary>
[Collection(MasterDataConfigSuite.Name)]
public sealed class AdministrationReadAndEditTests(MasterDataConfigTestHost host)
{
    [Fact]
    public async Task ACatalogueIsRenamedOnlyAgainstItsCurrentVersionAndTheRenameIsAudited()
    {
        using HttpClient client = host.Api.CreateClient();
        Crew crew = await client.SignInCrewAsync();
        JsonObject catalogue = await CatalogueAsync(client, crew.Author, "KPI_UNIT");
        string path = $"{ConfigurationApi.Catalogues}/{AdministrationApi.IdOf(catalogue)}";
        object original = new { name = new { ar = catalogue["name"]!["ar"]!.GetValue<string>(), en = catalogue["name"]!["en"]!.GetValue<string>() } };

        using HttpResponseMessage current = await client.GetAsync(path, crew.Author);
        using (HttpResponseMessage renamed = await client.PutAsync(path, crew.Author, new { name = new { ar = "وحدات القياس", en = "Measurement units" } }, AdministrationApi.ETagOf(current)))
        {
            JsonObject body = await renamed.ReadObjectAsync();
            Assert.Equal((HttpStatusCode.OK, "Measurement units"), (renamed.StatusCode, body["name"]!["en"]!.GetValue<string>()));

            using HttpResponseMessage stale = await client.PutAsync(path, crew.Author, original, AdministrationApi.ETagOf(current));
            Assert.Equal(HttpStatusCode.PreconditionFailed, stale.StatusCode);

            using HttpResponseMessage restored = await client.PutAsync(path, crew.Author, original, AdministrationApi.ETagOf(renamed));
            Assert.Equal(HttpStatusCode.OK, restored.StatusCode);
        }

        using (HttpResponseMessage unknown = await client.PutAsync($"{ConfigurationApi.Catalogues}/{Guid.NewGuid()}", crew.Author, original, AdministrationApi.ETagOf(current)))
        {
            Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        }

        Assert.Equal(
            ["CatalogueRenamed", "CatalogueRenamed"],
            (await host.Database.QueryAsync($"""
                SELECT e.event_type FROM audit_activity.audit_event e
                WHERE e.subject_id = '{AdministrationApi.IdOf(catalogue)}' ORDER BY e.occurred_at, e.id
                """)).Select(e => e.Replace("MasterDataConfig.", string.Empty, StringComparison.Ordinal)));
    }

    [Fact]
    public async Task ItemsAreListedByCatalogueStateAndTextAndReadOneByOne()
    {
        using HttpClient client = host.Api.CreateClient();
        Crew crew = await client.SignInCrewAsync();
        Guid region = AdministrationApi.IdOf(await CatalogueAsync(client, crew.Author, "REGION"));
        using HttpResponseMessage create = await client.PostAsync(ConfigurationApi.Items, crew.Author, new { catalogueId = region, code = "LISTED_EASTERN", label = new { ar = "الشرقية", en = "Eastern" }, sortOrder = 7 });
        Guid item = AdministrationApi.IdOf(await create.ReadObjectAsync());

        using HttpResponseMessage drafts = await client.GetAsync($"{ConfigurationApi.Items}?catalogueId={region}&lifecycleState=DRAFT&q=listed_east&page=1&pageSize=10", crew.Reviewer);
        JsonObject page = await drafts.ReadObjectAsync();
        JsonObject listed = page["items"]!.AsArray().Select(i => i!.AsObject()).Single();
        Assert.Equal((HttpStatusCode.OK, 1, 10, 1), (drafts.StatusCode, page["page"]!.GetValue<int>(), page["pageSize"]!.GetValue<int>(), page["totalCount"]!.GetValue<int>()));
        Assert.Equal((item, "LISTED_EASTERN", "DRAFT", 7), (AdministrationApi.IdOf(listed), listed["code"]!.GetValue<string>(), listed["lifecycleState"]!.GetValue<string>(), listed["sortOrder"]!.GetValue<int>()));

        using (HttpResponseMessage published = await client.GetAsync($"{ConfigurationApi.Items}?catalogueId={region}&lifecycleState=PUBLISHED&q=listed_east", crew.Reviewer))
        {
            Assert.Empty((await published.ReadObjectAsync())["items"]!.AsArray());
        }

        using (HttpResponseMessage badState = await client.GetAsync($"{ConfigurationApi.Items}?lifecycleState=SHELVED", crew.Reviewer))
        {
            Assert.Equal(HttpStatusCode.BadRequest, badState.StatusCode);
        }

        using HttpResponseMessage one = await client.GetAsync($"{ConfigurationApi.Items}/{item}", crew.Reviewer);
        Assert.Equal((HttpStatusCode.OK, "Eastern"), (one.StatusCode, (await one.ReadObjectAsync())["label"]!["en"]!.GetValue<string>()));
        using HttpResponseMessage none = await client.GetAsync($"{ConfigurationApi.Items}/{Guid.NewGuid()}", crew.Reviewer);
        Assert.Equal(HttpStatusCode.NotFound, none.StatusCode);
    }

    [Fact]
    public async Task VersionsAreListedByFamilyAndStateAndANeverPublishedOneHasNoEffectivity()
    {
        using HttpClient client = host.Api.CreateClient();
        Crew crew = await client.SignInCrewAsync();
        (Guid version, _) = await client.CreateVersionAsync(crew, "APPROVAL_AUTHORITY");
        Guid family = await client.FamilyIdAsync(crew.Author, "APPROVAL_AUTHORITY");

        using HttpResponseMessage response = await client.GetAsync($"{ConfigurationApi.Versions}?familyId={family}&lifecycleState=DRAFT&pageSize=100", crew.Reviewer);
        JsonObject page = await response.ReadObjectAsync();
        JsonObject[] rows = [.. page["items"]!.AsArray().Select(i => i!.AsObject())];

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.All(rows, r => Assert.Equal((family, "APPROVAL_AUTHORITY", "DRAFT"), (Guid.Parse(r["familyId"]!.GetValue<string>()), r["familyCode"]!.GetValue<string>(), r["lifecycleState"]!.GetValue<string>())));
        JsonObject created = rows.Single(r => AdministrationApi.IdOf(r) == version);
        Assert.Null(created["effectivity"]);
        Assert.Equal(rows.Length, page["totalCount"]!.GetValue<int>());
    }

    /// <summary>A DRAFT definition is edited by its author alone, its unit must be PUBLISHED, and the edit is listed back.</summary>
    [Fact]
    public async Task ADraftKpiDefinitionIsEditedByItsAuthorOnlyAndListedWithTheEdit()
    {
        using HttpClient client = host.Api.CreateClient();
        Crew crew = await client.SignInCrewAsync();
        Guid units = AdministrationApi.IdOf(await CatalogueAsync(client, crew.Author, "KPI_UNIT"));
        Guid hours = await PublishedItemAsync(client, crew, units, "LISTED_HOURS");
        Guid days = await PublishedItemAsync(client, crew, units, "LISTED_DAYS");
        using HttpResponseMessage draftUnit = await client.PostAsync(ConfigurationApi.Items, crew.Author, new { catalogueId = units, code = "LISTED_WEEKS", label = new { ar = "أسابيع", en = "Weeks" } });
        Guid weeks = AdministrationApi.IdOf(await draftUnit.ReadObjectAsync());

        using HttpResponseMessage create = await client.PostAsync(
            ConfigurationApi.KpiDefinitions, crew.Author, new { code = "LISTED_CYCLE_TIME", name = new { ar = "زمن الدورة", en = "Cycle time" }, unitItemId = hours, direction = "LOWER_IS_BETTER" });
        Guid kpi = AdministrationApi.IdOf(await create.ReadObjectAsync());
        string path = $"{ConfigurationApi.KpiDefinitions}/{kpi}";
        object edit = new { name = new { ar = "زمن الدورة الكلي", en = "Total cycle time" }, description = new { ar = "من البدء إلى الإغلاق", en = "From start to close" }, unitItemId = days, direction = "LOWER_IS_BETTER" };

        using (HttpResponseMessage notTheAuthor = await client.PutAsync(path, crew.Reviewer, edit, AdministrationApi.ETagOf(create)))
        {
            Assert.Equal("MASTER_DATA_CONFIG_SEPARATION_OF_DUTIES", await notTheAuthor.CodeOfAsync());
        }

        using (HttpResponseMessage unpublishedUnit = await client.PutAsync(path, crew.Author, new { name = new { ar = "زمن الدورة", en = "Cycle time" }, unitItemId = weeks, direction = "LOWER_IS_BETTER" }, AdministrationApi.ETagOf(create)))
        {
            Assert.Equal(["unitItemId NOT_PUBLISHED"], await unpublishedUnit.ReadFieldErrorsAsync());
        }

        using (HttpResponseMessage edited = await client.PutAsync(path, crew.Author, edit, AdministrationApi.ETagOf(create)))
        {
            Assert.Equal(HttpStatusCode.OK, edited.StatusCode);
        }

        using HttpResponseMessage listing = await client.GetAsync($"{ConfigurationApi.KpiDefinitions}?lifecycleState=DRAFT&q=listed_cycle", crew.Reviewer);
        JsonObject listed = (await listing.ReadObjectAsync())["items"]!.AsArray().Select(i => i!.AsObject()).Single();
        Assert.Equal((kpi, "Total cycle time", days, "DRAFT"), (AdministrationApi.IdOf(listed), listed["name"]!["en"]!.GetValue<string>(), Guid.Parse(listed["unitItemId"]!.GetValue<string>()), listed["lifecycleState"]!.GetValue<string>()));
    }

    private static async Task<JsonObject> CatalogueAsync(HttpClient client, string token, string code)
    {
        using HttpResponseMessage response = await client.GetAsync(ConfigurationApi.Catalogues, token);
        return (await response.ReadArrayAsync()).Select(c => c!.AsObject()).Single(c => c["code"]!.GetValue<string>() == code);
    }

    private static async Task<Guid> PublishedItemAsync(HttpClient client, Crew crew, Guid catalogueId, string code)
    {
        using HttpResponseMessage create = await client.PostAsync(ConfigurationApi.Items, crew.Author, new { catalogueId, code, label = new { ar = code, en = code } });
        Guid item = AdministrationApi.IdOf(await create.ReadObjectAsync());
        foreach ((string token, string step) in new[] { (crew.Reviewer, "validate"), (crew.Publisher, "publish") })
        {
            using HttpResponseMessage response = await client.PostAsync($"{ConfigurationApi.Items}/{item}/{step}", token);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        return item;
    }
}
