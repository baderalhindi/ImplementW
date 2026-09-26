using System.Net.Http.Json;

namespace PMPlatform.Tests.Integration.Identity;

/// <summary>
/// The workbook's validation cell: "confirm no credential appears in application logs" (CTL-06, CTL-27). Every log line
/// the API wrote while signing people in, rejecting them, refreshing and failing over is searched for every credential
/// that passed through it.
/// </summary>
[Collection(IdentitySuite.Name)]
public sealed class CredentialLoggingTests(IdentityTestHost host)
{
    [Fact]
    public async Task NoCredentialOrTokenAppearsInTheLogs()
    {
        using HttpClient client = host.Api.CreateClient();
        List<string> credentials = [TestDirectory.PersonPassword, TestDirectory.BindPassword, TestIdentityProvider.ClientSecret, IdentityApiFactory.SigningKey];

        Session session = await client.SignInOrFailAsync(1);
        credentials.Add(session.AccessToken);
        credentials.Add(session.RefreshToken);
        using (HttpResponseMessage refreshed = await client.RefreshAsync(session.RefreshToken))
        {
            Session next = await refreshed.ReadAsync<Session>();
            credentials.Add(next.AccessToken);
            credentials.Add(next.RefreshToken);
        }

        const string mistypedPassword = "a-mistyped-password-7731";
        credentials.Add(mistypedPassword);
        (await client.SignInAsync("local.r01", mistypedPassword)).Dispose();
        (await client.SignInAsync("local.unregistered", TestDirectory.PersonPassword)).Dispose();
        (await client.PostAsJsonAsync(SessionApi.SsoSessions, new { code = "forged-code", state = "forged-state", transaction = "forged" })).Dispose();

        await using (IdentityApiFactory unreachable = host.CreateApi(new Dictionary<string, string?> { ["AD_LDAP_URL"] = "ldap://127.0.0.1:1/dc=pmplatform,dc=local" }))
        {
            using HttpClient failing = unreachable.CreateClient();
            (await failing.SignInAsync("local.r01", TestDirectory.PersonPassword)).Dispose();
        }

        string logs = host.Logs.Text;
        Assert.NotEmpty(logs);
        Assert.Contains("Directory unavailable during sign-in", logs, StringComparison.Ordinal);
        foreach (string credential in credentials)
        {
            Assert.DoesNotContain(credential, logs, StringComparison.Ordinal);
        }
    }
}
