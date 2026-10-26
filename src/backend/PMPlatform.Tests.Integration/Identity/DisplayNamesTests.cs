using System.Net;
using System.Text.Json.Nodes;

namespace PMPlatform.Tests.Integration.Identity;

/// <summary>
/// The 24 Oct 2026 review, H1: business users see names, not ids. <c>GET /display-names</c> and <c>GET /organization-units</c> tell any
/// signed-in user the names a screen shows and the units a form offers — and an external user its own entity's alone, never AHDA's
/// people or structure (ADR-013; AUD-CC-18).
/// </summary>
[Collection(IdentitySuite.Name)]
public sealed class DisplayNamesTests(IdentityTestHost host)
{
    private const string Names = "/api/v1/display-names";
    private const string Units = "/api/v1/organization-units";

    /// <summary>A Viewer, who holds no administration permission, is told AHDA's people's names and every department and entity.</summary>
    [Fact]
    public async Task AnInternalUserIsToldTheNamesAScreenShowsAndTheUnitsAFormOffers()
    {
        using HttpClient client = host.Api.CreateClient();
        Session viewer = await client.SignInOrFailAsync(6);

        JsonObject names = await OkAsync(client, viewer, $"{Names}?userId={IdentityDatabase.UserId(2)}&userId={IdentityDatabase.UserId(8)}&userId={Guid.NewGuid()}");
        JsonObject units = await OkAsync(client, viewer, Units);

        Assert.Equal(["Local R02", "Local R08"], names["users"]!.AsArray().Select(u => u!["displayName"]!.GetValue<string>()));
        Assert.Contains(IdentityDatabase.DepartmentId, Ids(units["departments"]!));
        Assert.Contains(IdentityDatabase.ActiveEntityId, Ids(units["externalEntities"]!));
        JsonObject suspended = units["externalEntities"]!.AsArray().Single(e => e!["id"]!.GetValue<string>() == IdentityDatabase.SuspendedEntityId)!.AsObject();
        Assert.False(suspended["isActive"]!.GetValue<bool>());
        Assert.NotNull(suspended["name"]!["ar"]);
    }

    /// <summary>An external user is told its own entity and its own people's names, and nothing of AHDA's people, departments or other entities.</summary>
    [Fact]
    public async Task AnExternalUserIsToldItsOwnEntitysNamesAlone()
    {
        using HttpClient client = host.Api.CreateClient();
        Session entity = await client.SignInOrFailAsync(8);

        JsonObject names = await OkAsync(client, entity, $"{Names}?userId={IdentityDatabase.UserId(2)}&userId={IdentityDatabase.UserId(8)}&userId={IdentityDatabase.UserId(7)}");
        JsonObject units = await OkAsync(client, entity, Units);

        Assert.Equal([IdentityDatabase.UserId(8)], Ids(names["users"]!));
        Assert.Empty(units["departments"]!.AsArray());
        Assert.Equal([IdentityDatabase.ActiveEntityId], Ids(units["externalEntities"]!));
    }

    [Fact]
    public async Task MoreThanAHundredIdsOrAnEmptyIdIsRefusedAndNoTokenIsUnauthorized()
    {
        using HttpClient client = host.Api.CreateClient();
        Session viewer = await client.SignInOrFailAsync(6);

        using HttpResponseMessage tooMany = await client.GetWithTokenAsync(
            $"{Names}?{string.Join('&', Enumerable.Range(0, 101).Select(_ => $"userId={Guid.NewGuid()}"))}", viewer.AccessToken);
        using HttpResponseMessage empty = await client.GetWithTokenAsync($"{Names}?userId={Guid.Empty}", viewer.AccessToken);
        using HttpResponseMessage anonymousNames = await client.GetAsync(Names);
        using HttpResponseMessage anonymousUnits = await client.GetAsync(Units);

        Assert.Equal(
            (HttpStatusCode.BadRequest, HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized, HttpStatusCode.Unauthorized),
            (tooMany.StatusCode, empty.StatusCode, anonymousNames.StatusCode, anonymousUnits.StatusCode));
    }

    private static IEnumerable<string> Ids(JsonNode list) => list.AsArray().Select(item => item!["id"]!.GetValue<string>());

    private static async Task<JsonObject> OkAsync(HttpClient client, Session session, string path)
    {
        using HttpResponseMessage response = await client.GetWithTokenAsync(path, session.AccessToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.ReadObjectAsync();
    }
}
