using System.Net;
using System.Text.Json.Nodes;
using PMPlatform.Tests.Integration.Identity;

namespace PMPlatform.Tests.Integration.GlobalSearch;

internal static class GlobalSearchDriver
{
    public const string Search = "/api/v1/search";

    /// <summary>Text no other test's record holds, so a match is this test's alone and never pushed out of the five by others.</summary>
    public static string UniqueText() => $"gs{Guid.NewGuid():N}";

    public static async Task<IReadOnlyList<(string Type, Guid Id)>> HitsAsync(this HttpClient client, string token, string text)
    {
        using HttpResponseMessage response = await client.GetWithTokenAsync($"{Search}?q={Uri.EscapeDataString(text)}", token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        JsonObject body = await response.ReadObjectAsync();
        return [.. body["items"]!.AsArray().Select(hit => (hit!["type"]!.GetValue<string>(), Guid.Parse(hit["id"]!.GetValue<string>())))];
    }

    /// <summary>
    /// The guarantee of the search: a person finds a record exactly when its own page would open for them. <paramref name="findable"/> states which
    /// way this person should go, so neither direction can pass by accident.
    /// </summary>
    public static async Task AssertFindableExactlyWhenReadableAsync(this HttpClient client, string token, string text, string type, Guid id, string detailPath, bool findable)
    {
        IReadOnlyList<(string Type, Guid Id)> hits = await client.HitsAsync(token, text);
        using HttpResponseMessage detail = await client.GetWithTokenAsync(detailPath, token);

        Assert.Equal(findable, hits.Contains((type, id)));
        Assert.Equal(findable, detail.StatusCode == HttpStatusCode.OK);
    }

    /// <summary>The same guarantee with no expectation stated: for roles R02 to R08 in turn, the search and the record's page give one answer.</summary>
    public static async Task AssertSearchAgreesWithThePageForEveryRoleAsync(
        this HttpClient client, string text, string type, Guid id, string detailPath, Func<int, Task>? beforeRole = null)
    {
        List<int> compared = [];
        foreach (int role in Enumerable.Range(2, 7))
        {
            if (beforeRole is not null)
            {
                await beforeRole(role);
            }

            using HttpResponseMessage signIn = await client.SignInAsync($"local.r0{role}", TestDirectory.PersonPassword);
            if (signIn.StatusCode == HttpStatusCode.Unauthorized)
            {
                continue; // Not everyone is active in every suite's fixture; the count below keeps this from passing on nobody.
            }

            string token = (await client.SessionOrSecondFactorAsync(signIn)).AccessToken;
            IReadOnlyList<(string Type, Guid Id)> hits = await client.HitsAsync(token, text);
            using HttpResponseMessage detail = await client.GetWithTokenAsync(detailPath, token);
            Assert.True(
                hits.Contains((type, id)) == (detail.StatusCode == HttpStatusCode.OK),
                $"R0{role}: found {hits.Contains((type, id))}, its page answered {(int)detail.StatusCode}");
            compared.Add(role);
        }

        Assert.True(compared.Count >= 4, $"Only compared: {string.Join(", ", compared)}");
    }
}
