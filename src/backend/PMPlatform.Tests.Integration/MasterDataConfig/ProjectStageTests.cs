using System.Net;
using System.Text.Json.Nodes;
using PMPlatform.Tests.Integration.Identity;

namespace PMPlatform.Tests.Integration.MasterDataConfig;

/// <summary>Review round 5, item 7: the stages and weights as a versioned family, so a past date resolves to the weights in force then.</summary>
[Collection(MasterDataConfigSuite.Name)]
public sealed class ProjectStageTests(MasterDataConfigTestHost host)
{
    private const string Family = "PROJECT_STAGE";

    private static object Stage(string code, string en, int order, decimal weight, bool active = true) =>
        new { code, name = new { ar = en + " ع", en }, sortOrder = order, weightPercent = weight, isActive = active };

    private static readonly string[] SeededStages = ["BUSINESS_CASE", "DESIGN", "TENDERING", "EXECUTION", "TESTING", "HANDOVER"];

    /// <summary>The six stages the seed publishes (review round 7), deactivated: a published stage is kept in every later version, never removed.</summary>
    private static object[] Kept(params object[] stages) => [.. stages, .. SeededStages.Select((code, i) => Stage(code, code, 11 + i, 0, active: false))];

    [Fact]
    public async Task WeightsMustTotal100AndAPublishedStageIsNeverRemovedAndPastDatesKeepTheirWeights()
    {
        using HttpClient client = host.Api.CreateClient();
        Crew crew = await client.SignInCrewAsync();

        (Guid badId, string badTag) = await client.CreateVersionAsync(crew, Family);
        using (HttpResponseMessage put = await client.PutAsync($"{ConfigurationApi.Versions}/{badId}", crew.Author, ConfigurationApi.Update(new { projectStages = Kept(Stage("A", "Plan", 1, 60)) }), badTag))
        {
            Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        }

        using (HttpResponseMessage validate = await client.PostAsync($"{ConfigurationApi.Versions}/{badId}/validate", crew.Reviewer))
        {
            Assert.Equal(HttpStatusCode.UnprocessableEntity, validate.StatusCode);
            Assert.Equal("MASTER_DATA_CONFIG_CONTENT_INCOMPLETE", await validate.CodeOfAsync());
        }

        using (await client.PostAsync($"{ConfigurationApi.Versions}/{badId}/retire", crew.Publisher))
        {
        }

        (Guid first, string firstTag) = await client.CreateVersionAsync(crew, Family);
        await client.WriteAndPublishAsync(crew, first, firstTag, new { projectStages = Kept(Stage("A", "Plan", 1, 40), Stage("B", "Build", 2, 60)) });
        DateTimeOffset before = host.Clock.GetUtcNow();

        (Guid removing, string removingTag) = await client.CreateVersionAsync(crew, Family);
        using (HttpResponseMessage put = await client.PutAsync($"{ConfigurationApi.Versions}/{removing}", crew.Author, ConfigurationApi.Update(new { projectStages = Kept(Stage("A", "Plan", 1, 100)) }), removingTag))
        {
            Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        }

        using (HttpResponseMessage validate = await client.PostAsync($"{ConfigurationApi.Versions}/{removing}/validate", crew.Reviewer))
        {
            Assert.Equal(HttpStatusCode.UnprocessableEntity, validate.StatusCode);
        }

        using (await client.PostAsync($"{ConfigurationApi.Versions}/{removing}/retire", crew.Publisher))
        {
        }

        host.Clock.Advance(TimeSpan.FromMinutes(1));
        (Guid second, string secondTag) = await client.CreateVersionAsync(crew, Family, first);
        await client.WriteAndPublishAsync(crew, second, secondTag, new { projectStages = Kept(Stage("A", "Planning", 1, 100), Stage("B", "Build", 2, 0, active: false)) });

        JsonArray now = await StagesAsync(client, crew, null);
        Assert.Equal((100m, "Planning"), (now[0]!["weightPercent"]!.GetValue<decimal>(), now[0]!["name"]!["en"]!.GetValue<string>()));
        Assert.False(now[1]!["isActive"]!.GetValue<bool>());

        JsonArray past = await StagesAsync(client, crew, before);
        Assert.Equal((40m, "Plan"), (past[0]!["weightPercent"]!.GetValue<decimal>(), past[0]!["name"]!["en"]!.GetValue<string>()));
    }

    private static async Task<JsonArray> StagesAsync(HttpClient client, Crew crew, DateTimeOffset? asOf)
    {
        string query = asOf is { } at ? $"?asOf={Uri.EscapeDataString(at.ToString("O", System.Globalization.CultureInfo.InvariantCulture))}" : string.Empty;
        using HttpResponseMessage response = await client.GetAsync(ConfigurationApi.Resolution(Family) + query, crew.Author);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.ReadObjectAsync())["content"]!["projectStages"]!.AsArray();
    }
}
