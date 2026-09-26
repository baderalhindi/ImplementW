using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.WebUtilities;

namespace PMPlatform.Tests.Integration.Identity;

/// <summary>
/// TASK-028 single sign-on: the authorization code flow with PKCE against the test identity provider, redirect and all.
/// The identity provider says who the person is; the platform's assignments say what they hold (ADR-007).
/// </summary>
[Collection(IdentitySuite.Name)]
public sealed class SingleSignOnTests(IdentityTestHost host) : IDisposable
{
    public void Dispose()
    {
        host.Clock.Reset();
        host.IdentityProvider.NextSubject = TestDirectory.Subject(1);
        host.IdentityProvider.NonceOverride = null;
    }

    [Fact]
    public async Task AnSsoSignInIssuesASessionWithThePlatformRolesOfTheUsersAssignments()
    {
        using HttpClient client = host.Api.CreateClient();
        Callback callback = await AuthorizeAsync(client, TestDirectory.Subject(1));

        using HttpResponseMessage response = await client.PostAsJsonAsync(SessionApi.SsoSessions, callback);

        // R01 requires MFA (TASK-029) on the SSO path as on the directory path.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Session session = await client.SessionOrSecondFactorAsync(response);
        Assert.Equal(Guid.Parse(IdentityDatabase.UserId(1)), session.User.Id);
        Assert.Equal("SINGLE_SIGN_ON", session.User.AuthenticationMethod);
        Assert.Equal(["R01"], session.User.RoleAssignments.Select(a => a.RoleCode));
        using HttpResponseMessage current = await client.GetWithTokenAsync(SessionApi.Current, session.AccessToken);
        Assert.Equal("SINGLE_SIGN_ON", (await current.ReadAsync<CurrentSession>()).AuthenticationMethod);
    }

    /// <summary>After SSO the directory is read by subject, so its attributes are applied exactly as on a directory sign-in.</summary>
    [Fact]
    public async Task AnSsoSignInCopiesTheDirectoryAuthoritativeAttributes()
    {
        await host.Database.ExecuteAsync($"UPDATE identity_access.\"user\" SET job_title = 'Stale title', manager_user_id = NULL WHERE id = '{IdentityDatabase.UserId(3)}'");
        using HttpClient client = host.Api.CreateClient();
        Callback callback = await AuthorizeAsync(client, TestDirectory.Subject(3));

        using HttpResponseMessage response = await client.PostAsJsonAsync(SessionApi.SsoSessions, callback);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        IReadOnlyList<string> row = await host.Database.QueryAsync(
            $"SELECT concat_ws('|', job_title, manager_user_id) FROM identity_access.\"user\" WHERE id = '{IdentityDatabase.UserId(3)}'");
        Assert.Equal($"Department Manager|{IdentityDatabase.UserId(2)}", Assert.Single(row));
    }

    [Fact]
    public async Task TheAuthorizationRequestUsesPkceAndAsksForNoRoleOrGroupClaims()
    {
        using HttpClient client = host.Api.CreateClient();

        using HttpResponseMessage response = await client.GetAsync(SessionApi.SsoAuthorization);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
        SsoAuthorization authorization = await response.ReadAsync<SsoAuthorization>();
        Dictionary<string, Microsoft.Extensions.Primitives.StringValues> query = QueryHelpers.ParseQuery(authorization.AuthorizationUrl.Query);
        Assert.Equal("S256", query["code_challenge_method"]);
        Assert.Equal(43, query["code_challenge"].ToString().Length);
        Assert.Equal("openid", query["scope"]);
        Assert.Equal(TestIdentityProvider.CallbackUrl, query["redirect_uri"]);
        Assert.False(query.ContainsKey("code_verifier"));
        Assert.DoesNotContain(authorization.Transaction, authorization.AuthorizationUrl.AbsoluteUri, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ATamperedTransactionIsRejected()
    {
        using HttpClient client = host.Api.CreateClient();
        Callback callback = await AuthorizeAsync(client, TestDirectory.Subject(1));
        char last = callback.Transaction[^1];

        await AssertRejectedAsync(client, callback with { Transaction = callback.Transaction[..^1] + (last == 'A' ? 'B' : 'A') });
    }

    [Fact]
    public async Task AStateThatIsNotTheTransactionsIsRejected()
    {
        using HttpClient client = host.Api.CreateClient();
        Callback callback = await AuthorizeAsync(client, TestDirectory.Subject(1));

        await AssertRejectedAsync(client, callback with { State = "not-the-state-this-browser-started" });
    }

    [Fact]
    public async Task ACodeCanBeRedeemedOnlyOnce()
    {
        using HttpClient client = host.Api.CreateClient();
        Callback callback = await AuthorizeAsync(client, TestDirectory.Subject(2));
        using HttpResponseMessage first = await client.PostAsJsonAsync(SessionApi.SsoSessions, callback);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        await AssertRejectedAsync(client, callback);
    }

    [Fact]
    public async Task AnIdTokenForAnotherSignInIsRejected()
    {
        using HttpClient client = host.Api.CreateClient();
        Callback callback = await AuthorizeAsync(client, TestDirectory.Subject(1));
        host.IdentityProvider.NonceOverride = "a-nonce-from-another-sign-in";

        await AssertRejectedAsync(client, callback);
    }

    [Fact]
    public async Task AnExpiredTransactionIsRejected()
    {
        using HttpClient client = host.Api.CreateClient();
        Callback callback = await AuthorizeAsync(client, TestDirectory.Subject(1));
        host.Clock.Advance(TimeSpan.FromMinutes(10) + TimeSpan.FromSeconds(1));

        await AssertRejectedAsync(client, callback);
    }

    /// <summary>The identity provider vouches for the person; the platform still refuses one it has no user for, or one it does not let in.</summary>
    [Theory]
    [InlineData(99)]
    [InlineData(4)]
    [InlineData(7)]
    public async Task APersonThePlatformDoesNotAdmitIsRejected(int person)
    {
        using HttpClient client = host.Api.CreateClient();
        Callback callback = await AuthorizeAsync(client, TestDirectory.Subject(person));

        await AssertRejectedAsync(client, callback);
    }

    private static async Task AssertRejectedAsync(HttpClient client, Callback callback)
    {
        using HttpResponseMessage response = await client.PostAsJsonAsync(SessionApi.SsoSessions, callback);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("AUTHENTICATION_REQUIRED", (await response.ReadAsync<Problem>()).Code);
    }

    private Task<Callback> AuthorizeAsync(HttpClient client, string subject) => SsoBrowser.AuthorizeAsync(client, host.IdentityProvider, subject);
}
