using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace PMPlatform.Tests.Integration.Identity;

/// <summary>
/// TASK-028 acceptance: session and token expiry and refresh. The lifetimes are the provisional defaults (access 15
/// minutes, idle 30 minutes, absolute 8 hours; control matrix G-2). The API's clock is moved rather than waited on.
/// </summary>
[Collection(IdentitySuite.Name)]
public sealed class SessionLifetimeTests(IdentityTestHost host) : IDisposable
{
    public void Dispose() => host.Clock.Reset();

    [Fact]
    public async Task AnAccessTokenIsRefusedOnceItExpires()
    {
        using HttpClient client = host.Api.CreateClient();
        Session session = await client.SignInOrFailAsync(1);

        host.Clock.Advance(TimeSpan.FromMinutes(14));
        using HttpResponseMessage beforeExpiry = await client.GetWithTokenAsync(SessionApi.Current, session.AccessToken);
        Assert.Equal(HttpStatusCode.OK, beforeExpiry.StatusCode);

        host.Clock.Advance(TimeSpan.FromMinutes(1) + TimeSpan.FromSeconds(1));
        using HttpResponseMessage expired = await client.GetWithTokenAsync(SessionApi.Current, session.AccessToken);

        Assert.Equal(HttpStatusCode.Unauthorized, expired.StatusCode);
        Assert.Equal("AUTHENTICATION_REQUIRED", (await expired.ReadAsync<Problem>()).Code);
        Assert.Equal("Bearer", expired.Headers.WwwAuthenticate.Single().Scheme);
    }

    [Fact]
    public async Task ARefreshIssuesAWorkingTokenPairWithoutExtendingTheSession()
    {
        using HttpClient client = host.Api.CreateClient();
        Session first = await client.SignInOrFailAsync(1);

        host.Clock.Advance(TimeSpan.FromMinutes(20));
        using HttpResponseMessage response = await client.RefreshAsync(first.RefreshToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Session second = await response.ReadAsync<Session>();
        Assert.NotEqual(first.AccessToken, second.AccessToken);
        Assert.Equal(first.SessionExpiresAt, second.SessionExpiresAt);
        Assert.Equal(["R01"], second.User.RoleAssignments.Select(a => a.RoleCode));
        using HttpResponseMessage current = await client.GetWithTokenAsync(SessionApi.Current, second.AccessToken);
        Assert.Equal(HttpStatusCode.OK, current.StatusCode);
        Assert.Equal(first.User.Id, (await current.ReadAsync<CurrentSession>()).UserId);
    }

    [Fact]
    public async Task ASessionNotRefreshedWithinTheIdleTimeoutEnds()
    {
        using HttpClient client = host.Api.CreateClient();
        Session session = await client.SignInOrFailAsync(1);

        host.Clock.Advance(TimeSpan.FromMinutes(30) + TimeSpan.FromSeconds(1));
        using HttpResponseMessage response = await client.RefreshAsync(session.RefreshToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task NoRefreshOutlivesTheAbsoluteSessionLifetime()
    {
        using HttpClient client = host.Api.CreateClient();
        Session session = await client.SignInOrFailAsync(1);
        DateTimeOffset sessionExpiresAt = session.SessionExpiresAt;

        // Refresh every 20 minutes, inside the idle timeout: 23 refreshes reach 7 h 40 min of the 8 h.
        for (int step = 0; step < 23; step++)
        {
            host.Clock.Advance(TimeSpan.FromMinutes(20));
            using HttpResponseMessage response = await client.RefreshAsync(session.RefreshToken);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            session = await response.ReadAsync<Session>();
            Assert.Equal(sessionExpiresAt, session.SessionExpiresAt);
            Assert.True(session.AccessTokenExpiresAt <= sessionExpiresAt);
            Assert.True(session.RefreshTokenExpiresAt <= sessionExpiresAt);
        }

        // The last refresh token would have lived 30 idle minutes; the session's end cuts it to 20.
        Assert.Equal(sessionExpiresAt, session.RefreshTokenExpiresAt);

        host.Clock.Advance(TimeSpan.FromMinutes(20));
        using HttpResponseMessage final = await client.RefreshAsync(session.RefreshToken);
        Assert.Equal(HttpStatusCode.Unauthorized, final.StatusCode);
    }

    /// <summary>A refresh re-reads the user: disabling them ends their session at the next refresh.</summary>
    [Fact]
    public async Task ARefreshIsRefusedOnceTheUserIsDisabled()
    {
        using HttpClient client = host.Api.CreateClient();
        Session session = await client.SignInOrFailAsync(6);
        try
        {
            await host.Database.ExecuteAsync($"UPDATE identity_access.\"user\" SET status = 'DISABLED' WHERE id = '{IdentityDatabase.UserId(6)}'");

            using HttpResponseMessage response = await client.RefreshAsync(session.RefreshToken);

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
        finally
        {
            await host.Database.ExecuteAsync($"UPDATE identity_access.\"user\" SET status = 'ACTIVE' WHERE id = '{IdentityDatabase.UserId(6)}'");
        }
    }

    [Fact]
    public async Task AnAccessTokenAndARefreshTokenCannotStandInForEachOther()
    {
        using HttpClient client = host.Api.CreateClient();
        Session session = await client.SignInOrFailAsync(1);

        using HttpResponseMessage refreshAsAccess = await client.GetWithTokenAsync(SessionApi.Current, session.RefreshToken);
        using HttpResponseMessage accessAsRefresh = await client.RefreshAsync(session.AccessToken);
        using HttpResponseMessage tampered = await client.GetWithTokenAsync(SessionApi.Current, session.AccessToken[..^4] + "AAAA");

        Assert.Equal(HttpStatusCode.Unauthorized, refreshAsAccess.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, accessAsRefresh.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, tampered.StatusCode);
    }

    /// <summary>The sheet's verification for JWT_SIGNING_KEY: a token issued and validated round-trip, and rejected once the key is rotated.</summary>
    [Fact]
    public async Task ATokenIsRejectedOnceTheSigningKeyIsRotated()
    {
        using HttpClient client = host.Api.CreateClient();
        Session session = await client.SignInOrFailAsync(1);
        IConfiguration configuration = host.Api.Services.GetRequiredService<IConfiguration>();
        try
        {
            configuration["JWT_SIGNING_KEY"] = "identity-integration-test-rotated-signing-key-32-bytes";

            using HttpResponseMessage access = await client.GetWithTokenAsync(SessionApi.Current, session.AccessToken);
            using HttpResponseMessage refresh = await client.RefreshAsync(session.RefreshToken);

            Assert.Equal(HttpStatusCode.Unauthorized, access.StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
        }
        finally
        {
            configuration["JWT_SIGNING_KEY"] = IdentityApiFactory.SigningKey;
        }
    }

    [Fact]
    public async Task AProtectedEndpointWithoutATokenIsTheSameGeneric401()
    {
        using HttpClient client = host.Api.CreateClient();

        using HttpResponseMessage response = await client.GetAsync(SessionApi.Current);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("AUTHENTICATION_REQUIRED", (await response.ReadAsync<Problem>()).Code);
    }
}
