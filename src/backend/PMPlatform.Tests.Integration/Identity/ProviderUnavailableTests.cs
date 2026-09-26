using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace PMPlatform.Tests.Integration.Identity;

/// <summary>
/// The workbook's directory-unavailable failure mode: an unreachable directory or identity provider is the platform's
/// failure, a 503, never a 401 that would blame the person, and never a session.
/// </summary>
[Collection(IdentitySuite.Name)]
public sealed class ProviderUnavailableTests(IdentityTestHost host)
{
    /// <summary>Nothing listens on port 1 of the loopback interface.</summary>
    private const string UnreachableDirectory = "ldap://127.0.0.1:1/dc=pmplatform,dc=local";

    [Fact]
    public async Task AnUnreachableDirectoryIsA503()
    {
        await using IdentityApiFactory api = host.CreateApi(new Dictionary<string, string?> { ["AD_LDAP_URL"] = UnreachableDirectory });
        using HttpClient client = api.CreateClient();

        using HttpResponseMessage response = await client.SignInAsync("local.r01", TestDirectory.PersonPassword);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("UNAVAILABLE", (await response.ReadAsync<Problem>()).Code);
    }

    [Fact]
    public async Task AnUnreachableDirectoryFailsTheConnectionTest()
    {
        await using IdentityApiFactory api = host.CreateApi(new Dictionary<string, string?> { ["AD_LDAP_URL"] = UnreachableDirectory });
        using HttpClient client = api.CreateClient();
        using HttpClient shared = host.Api.CreateClient();
        string token = (await shared.SignInOrFailAsync(1)).AccessToken;

        using HttpResponseMessage response = await client.PostWithTokenAsync(SessionApi.IdentityIntegrationTest, token);

        using JsonDocument result = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("UNREACHABLE", result.RootElement.GetProperty("directory").GetProperty("failureCode").GetString());
    }

    [Fact]
    public async Task AnUnreachableIdentityProviderIsA503()
    {
        await using IdentityApiFactory api = host.CreateApi(new Dictionary<string, string?> { ["SSO_OIDC_AUTHORITY"] = "http://127.0.0.1:1/" });
        using HttpClient client = api.CreateClient();

        using HttpResponseMessage start = await client.GetAsync(SessionApi.SsoAuthorization);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, start.StatusCode);
        Assert.Equal("UNAVAILABLE", (await start.ReadAsync<Problem>()).Code);
    }

    /// <summary>A method the environment has no configuration for is unavailable, not a failed sign-in; the API still starts without it.</summary>
    [Fact]
    public async Task AnUnconfiguredSignInMethodIsA503()
    {
        await using IdentityApiFactory api = host.CreateApi(new Dictionary<string, string?>
        {
            ["AD_LDAP_URL"] = null,
            ["SSO_OIDC_CLIENT_SECRET"] = null,
        });
        using HttpClient client = api.CreateClient();

        using HttpResponseMessage password = await client.SignInAsync("local.r01", TestDirectory.PersonPassword);
        using HttpResponseMessage sso = await client.PostAsJsonAsync(SessionApi.SsoSessions, new { code = "c", state = "s", transaction = "t" });

        Assert.Equal(HttpStatusCode.ServiceUnavailable, password.StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, sso.StatusCode);
    }

    /// <summary>ADR-007 transport: plain ldap:// carries passwords in clear text, so it is refused unless explicitly allowed.</summary>
    [Fact]
    public async Task AnUnencryptedDirectoryIsRefusedUnlessAllowed()
    {
        await using IdentityApiFactory api = host.CreateApi(new Dictionary<string, string?> { ["Identity:Directory:AllowUnencryptedConnection"] = "false" });
        using HttpClient client = api.CreateClient();

        using HttpResponseMessage response = await client.SignInAsync("local.r01", TestDirectory.PersonPassword);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }
}
