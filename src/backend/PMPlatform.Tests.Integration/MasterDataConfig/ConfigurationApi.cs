using System.Net;
using System.Text.Json.Nodes;
using PMPlatform.Tests.Integration.Identity;

namespace PMPlatform.Tests.Integration.MasterDataConfig;

/// <summary>The FG-04 endpoints (TASK-034) as a client calls them, and the three people a governed row needs.</summary>
internal static class ConfigurationApi
{
    public const string Catalogues = "/api/v1/master-data-catalogues";
    public const string Items = "/api/v1/master-data-items";
    public const string KpiDefinitions = "/api/v1/kpi-definitions";
    public const string Families = "/api/v1/configuration-families";
    public const string Versions = "/api/v1/configuration-versions";
    public const string Resolutions = "/api/v1/configuration-resolutions";

    public static string Resolution(string familyCode, DateTimeOffset? asOf = null) =>
        asOf is { } at ? $"{Resolutions}/{familyCode}?asOf={Uri.EscapeDataString(at.ToString("O"))}" : $"{Resolutions}/{familyCode}";

    public static async Task<Crew> SignInCrewAsync(this HttpClient client) =>
        new(
            (await client.SignInOrFailAsync(MasterDataConfigTestHost.Author)).AccessToken,
            (await client.SignInOrFailAsync(MasterDataConfigTestHost.Reviewer)).AccessToken,
            (await client.SignInOrFailAsync(MasterDataConfigTestHost.Publisher)).AccessToken);

    public static async Task<Guid> FamilyIdAsync(this HttpClient client, string token, string familyCode)
    {
        using HttpResponseMessage response = await client.GetAsync(Families, token);
        return AdministrationApi.IdOf((await response.ReadArrayAsync()).Select(f => f!.AsObject()).Single(f => f["code"]!.GetValue<string>() == familyCode));
    }

    /// <summary>A new DRAFT of the family, by the author; its id and ETag.</summary>
    public static async Task<(Guid Id, string ETag)> CreateVersionAsync(this HttpClient client, Crew crew, string familyCode, Guid? basedOnVersionId = null)
    {
        Guid familyId = await client.FamilyIdAsync(crew.Author, familyCode);
        using HttpResponseMessage response = await client.PostAsync(Versions, crew.Author, new { familyId, basedOnVersionId, changeSummary = new { text = "Test version.", language = "en" } });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (AdministrationApi.IdOf(await response.ReadObjectAsync()), AdministrationApi.ETagOf(response));
    }

    /// <summary>The body of <c>PUT /configuration-versions/{id}</c>: the whole content, no change summary.</summary>
    public static object Update(object content) => new { content };

    /// <summary>Writes the content as the author, has the reviewer validate and the publisher publish; fails unless each step succeeds.</summary>
    public static async Task<JsonObject> WriteAndPublishAsync(this HttpClient client, Crew crew, Guid versionId, string eTag, object content, object? publication = null)
    {
        using (HttpResponseMessage put = await client.PutAsync($"{Versions}/{versionId}", crew.Author, new { content }, eTag))
        {
            Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        }

        using (HttpResponseMessage validate = await client.PostAsync($"{Versions}/{versionId}/validate", crew.Reviewer))
        {
            Assert.Equal(HttpStatusCode.OK, validate.StatusCode);
        }

        using HttpResponseMessage publish = await client.SendAsync(HttpMethod.Post, $"{Versions}/{versionId}/publish", crew.Publisher, publication);
        Assert.Equal(HttpStatusCode.OK, publish.StatusCode);
        return await publish.ReadObjectAsync();
    }

    public static async Task<string?> CodeOfAsync(this HttpResponseMessage response) => (await response.ReadObjectAsync())["code"]?.GetValue<string>();

    public static object Values(params (string Key, string Type, string Value)[] values) =>
        new { values = values.Select(v => new { key = v.Key, type = v.Type, value = v.Value }).ToArray() };
}

/// <summary>Access tokens of the author, the reviewer and the publisher.</summary>
internal sealed record Crew(string Author, string Reviewer, string Publisher);
