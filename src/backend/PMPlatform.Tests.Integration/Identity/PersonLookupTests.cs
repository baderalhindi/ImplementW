using System.Net;
using System.Text.Json.Nodes;

namespace PMPlatform.Tests.Integration.Identity;

/// <summary>Review round 5, item 5: finding a person for a field, for those who may write something, without USER_VIEW and without contact details.</summary>
[Collection(IdentitySuite.Name)]
public sealed class PersonLookupTests(IdentityTestHost host)
{
    private const string Lookups = "/api/v1/person-lookups";

    [Fact]
    public async Task APersonWhoMayWriteFindsPeopleByNameAndSeesNoContactDetails()
    {
        using HttpClient client = host.Api.CreateClient();
        string token = (await client.SignInOrFailAsync(3)).AccessToken;

        using HttpResponseMessage response = await client.GetAsync($"{Lookups}?q=local.r02", token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        JsonObject page = await response.ReadObjectAsync();
        JsonObject person = Assert.Single(page["items"]!.AsArray())!.AsObject();
        Assert.Equal("local.r02", person["username"]!.GetValue<string>());
        Assert.False(person.ContainsKey("email"));
    }

    [Fact]
    public async Task AnExternalUserIsHeldToItsOwnEntity()
    {
        using HttpClient client = host.Api.CreateClient();
        string token = (await client.SignInOrFailAsync(8)).AccessToken;

        using HttpResponseMessage response = await client.GetAsync($"{Lookups}?q=local.r02&userType=INTERNAL", token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty((await response.ReadObjectAsync())["items"]!.AsArray());
    }

    [Fact]
    public async Task SomeoneWithNoWritePermissionIsRefused()
    {
        using HttpClient client = host.Api.CreateClient();
        string token = (await client.SignInOrFailAsync(5)).AccessToken;

        using HttpResponseMessage response = await client.GetAsync($"{Lookups}?q=local", token);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
