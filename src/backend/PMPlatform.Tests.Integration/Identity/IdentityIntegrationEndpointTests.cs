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

    /// <summary>Signed in on the shared API: every factory shares the signing key, so the token is valid on any of them.</summary>
    private async Task<string> SystemAdministratorTokenAsync()
    {
        using HttpClient client = host.Api.CreateClient();
        return (await client.SignInOrFailAsync(1)).AccessToken;
    }
}
