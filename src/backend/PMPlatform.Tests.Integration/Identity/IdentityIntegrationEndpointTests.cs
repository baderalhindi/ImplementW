using System.Net;
using System.Text.Json;

namespace PMPlatform.Tests.Integration.Identity;

/// <summary>ADM-041, SSO/Directory Integration: R01 sees what is configured and tests it; no secret value ever leaves the API.</summary>
[Collection(IdentitySuite.Name)]
public sealed class IdentityIntegrationEndpointTests(IdentityTestHost host)
{
    [Fact]
    public async Task TheStatusShowsWhatIsConfiguredAndNoSecretValue()
    {
        using HttpClient client = host.Api.CreateClient();
        Session session = await client.SignInOrFailAsync(1);

        using HttpResponseMessage response = await client.GetWithTokenAsync(SessionApi.IdentityIntegration, session.AccessToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument status = JsonDocument.Parse(body);
        JsonElement directory = status.RootElement.GetProperty("directory");
        Assert.True(directory.GetProperty("isConfigured").GetBoolean());
        Assert.True(directory.GetProperty("bindPasswordConfigured").GetBoolean());
        Assert.False(directory.GetProperty("usesTransportSecurity").GetBoolean());
        Assert.Equal("entryUUID", directory.GetProperty("subjectAttribute").GetString());
        JsonElement sso = status.RootElement.GetProperty("singleSignOn");
        Assert.True(sso.GetProperty("isConfigured").GetBoolean());
        Assert.Equal(TestIdentityProvider.ClientId, sso.GetProperty("clientId").GetString());
        Assert.True(sso.GetProperty("clientSecretConfigured").GetBoolean());

        // Secret-classified values (Environment and Secrets sheet) are reduced to "configured".
        Assert.DoesNotContain(TestDirectory.BindPassword, body, StringComparison.Ordinal);
        Assert.DoesNotContain(TestDirectory.BindDn, body, StringComparison.Ordinal);
        Assert.DoesNotContain(TestDirectory.Url, body, StringComparison.Ordinal);
        Assert.DoesNotContain(TestIdentityProvider.ClientSecret, body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheConnectionTestReachesTheDirectoryAndTheIdentityProvider()
    {
        using HttpClient client = host.Api.CreateClient();
        Session session = await client.SignInOrFailAsync(1);

        using HttpResponseMessage response = await client.PostWithTokenAsync(SessionApi.IdentityIntegrationTest, session.AccessToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument result = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("SUCCEEDED", result.RootElement.GetProperty("directory").GetProperty("outcome").GetString());
        Assert.Equal("SUCCEEDED", result.RootElement.GetProperty("singleSignOn").GetProperty("outcome").GetString());
    }

    /// <summary>The sheet's verification for AD_BIND_DN and AD_BIND_PASSWORD: the test connection from ADM-041 reports a refused bind.</summary>
    [Fact]
    public async Task TheConnectionTestReportsARefusedServiceAccount()
    {
        await using IdentityApiFactory api = host.CreateApi(new Dictionary<string, string?> { ["AD_BIND_PASSWORD"] = "not-the-service-password" });
        using HttpClient client = api.CreateClient();
        string token = await SystemAdministratorTokenAsync();

        using HttpResponseMessage response = await client.PostWithTokenAsync(SessionApi.IdentityIntegrationTest, token);

        using JsonDocument result = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        JsonElement directory = result.RootElement.GetProperty("directory");
        Assert.Equal("FAILED", directory.GetProperty("outcome").GetString());
        Assert.Equal("BIND_REJECTED", directory.GetProperty("failureCode").GetString());
    }

    /// <summary>
    /// TASK-068: Nafath's settings beside the others. The sheet classifies NAFATH_CLIENT_ID and NAFATH_CLIENT_SECRET as
    /// Secret, so both are reduced to "configured"; the callback URL and the application id are Public.
    /// </summary>
    [Fact]
    public async Task TheStatusShowsNafathsSettingsAndNeitherOfItsSecrets()
    {
        await using IdentityApiFactory api = host.CreateApi(TestNafath.Settings(host.Nafath));
        using HttpClient client = api.CreateClient();

        using HttpResponseMessage response = await client.GetWithTokenAsync(SessionApi.IdentityIntegration, await SystemAdministratorTokenAsync());

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument status = JsonDocument.Parse(body);
        JsonElement nafath = status.RootElement.GetProperty("identityVerification");
        Assert.True(nafath.GetProperty("isEnabled").GetBoolean());
        Assert.True(nafath.GetProperty("isConfigured").GetBoolean());
        Assert.True(nafath.GetProperty("clientIdConfigured").GetBoolean());
        Assert.True(nafath.GetProperty("clientSecretConfigured").GetBoolean());
        Assert.Equal(TestNafath.CallbackUrl, nafath.GetProperty("callbackUrl").GetString());
        Assert.Equal(TestNafath.ApplicationId, nafath.GetProperty("applicationId").GetString());
        Assert.Equal(["openid"], nafath.GetProperty("scopes").EnumerateArray().Select(s => s.GetString()));
        Assert.DoesNotContain(TestNafath.ClientId, body, StringComparison.Ordinal);
        Assert.DoesNotContain(TestNafath.ClientSecret, body, StringComparison.Ordinal);
    }

    /// <summary>The sheet's verification for NAFATH_*, as far as an environment without Nafath can go: discovery resolves and its issuer matches.</summary>
    [Fact]
    public async Task TheConnectionTestReachesNafathOrSaysWhyNot()
    {
        string token = await SystemAdministratorTokenAsync();
        Dictionary<string, string?> unreachable = TestNafath.Settings(host.Nafath);
        unreachable["Identity:Nafath:Authority"] = "http://127.0.0.1:1/";

        Assert.Equal(("SUCCEEDED", null), await NafathTestAsync(TestNafath.Settings(host.Nafath), token));
        Assert.Equal(("FAILED", "UNREACHABLE"), await NafathTestAsync(unreachable, token));
        Assert.Equal(("NOT_CONFIGURED", null), await NafathTestAsync([], token));
    }

    [Fact]
    public async Task OnlyASystemAdministratorMayUseIt()
    {
        using HttpClient client = host.Api.CreateClient();
        Session departmentManager = await client.SignInOrFailAsync(3);

        using HttpResponseMessage forbidden = await client.GetWithTokenAsync(SessionApi.IdentityIntegration, departmentManager.AccessToken);
        using HttpResponseMessage forbiddenTest = await client.PostWithTokenAsync(SessionApi.IdentityIntegrationTest, departmentManager.AccessToken);
        using HttpResponseMessage anonymous = await client.GetAsync(SessionApi.IdentityIntegration);

        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        Assert.Equal("PERMISSION_DENIED", (await forbidden.ReadAsync<Problem>()).Code);
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenTest.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
    }

    private async Task<(string? Outcome, string? FailureCode)> NafathTestAsync(Dictionary<string, string?> settings, string token)
    {
        await using IdentityApiFactory api = host.CreateApi(settings);
        using HttpClient client = api.CreateClient();
        using HttpResponseMessage response = await client.PostWithTokenAsync(SessionApi.IdentityIntegrationTest, token);
        using JsonDocument result = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        JsonElement nafath = result.RootElement.GetProperty("identityVerification");
        return (nafath.GetProperty("outcome").GetString(), nafath.GetProperty("failureCode").GetString());
    }

    /// <summary>Signed in on the shared API: every factory shares the signing key, so the token is valid on any of them.</summary>
    private async Task<string> SystemAdministratorTokenAsync()
    {
        using HttpClient client = host.Api.CreateClient();
        return (await client.SignInOrFailAsync(1)).AccessToken;
    }
}
