using System.Net;
using PMPlatform.Tests.Integration.AuditActivity;

namespace PMPlatform.Tests.Integration.Identity;

/// <summary>
/// SR-10, AUTH-T13: refresh tokens rotate, a replaced one presented again ends the session for every holder, sign-out ends
/// the session on the server, and disabling a user ends their sessions for good.
/// </summary>
[Collection(IdentitySuite.Name)]
public sealed class SessionRevocationTests(IdentityTestHost host)
{
    [Fact]
    public async Task ARefreshAnswersANewRefreshTokenAndTheUsedOneStopsWorking()
    {
        using HttpClient client = host.Api.CreateClient();
        Session signIn = await client.SignInOrFailAsync(2);

        using HttpResponseMessage refreshed = await client.RefreshAsync(signIn.RefreshToken);
        Session next = await refreshed.ReadAsync<Session>();
        using HttpResponseMessage again = await client.RefreshAsync(next.RefreshToken);

        Assert.Equal(HttpStatusCode.OK, refreshed.StatusCode);
        Assert.NotEqual(signIn.RefreshToken, next.RefreshToken);
        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
        Assert.Equal(SessionStatus.Active, await SessionStatusAsync(next.AccessToken));
    }

    /// <summary>The thief's copy and the person's are the same token: whichever comes second ends the session, and the newer token with it.</summary>
    [Fact]
    public async Task AReplacedRefreshTokenPresentedAgainIsTheGeneric401AndRevokesTheSession()
    {
        Guid correlationId = Guid.NewGuid();
        using HttpClient client = host.Api.CreateClient();
        Session signIn = await client.SignInOrFailAsync(2);
        using HttpResponseMessage refreshed = await client.RefreshAsync(signIn.RefreshToken);
        Session next = await refreshed.ReadAsync<Session>();
        using HttpResponseMessage forged = await client.RefreshAsync("not.a.token");

        using HttpClient reuser = host.Api.CreateClient().WithCorrelationId(correlationId);
        using HttpResponseMessage reused = await reuser.RefreshAsync(signIn.RefreshToken);
        using HttpResponseMessage newer = await client.RefreshAsync(next.RefreshToken);

        Assert.Equal(HttpStatusCode.Unauthorized, reused.StatusCode);
        Assert.Equal(await forged.ReadProblemShapeAsync(), await reused.ReadProblemShapeAsync());
        Assert.Equal(HttpStatusCode.Unauthorized, newer.StatusCode);
        Assert.Equal(SessionStatus.RevokedForReuse, await SessionStatusAsync(next.AccessToken));
        string failed = Assert.Single(await host.Database.EventsAsync(correlationId), e => e.Contains("|FAILED|", StringComparison.Ordinal));
        Assert.StartsWith("AUTHENTICATION|IdentityAccess.SessionRefreshFailed|FAILED|", failed, StringComparison.Ordinal);
        Assert.Contains("failure_reason=>REFRESH_TOKEN_REUSED", failed, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AfterSignOutTheSessionsRefreshTokenIsRefused()
    {
        Guid correlationId = Guid.NewGuid();
        using HttpClient client = host.Api.CreateClient().WithCorrelationId(correlationId);
        Session signIn = await client.SignInOrFailAsync(2);

        using HttpResponseMessage signOut = await client.PostWithTokenAsync(SessionApi.SignOut, signIn.AccessToken);
        using HttpResponseMessage refresh = await client.RefreshAsync(signIn.RefreshToken);
        using HttpResponseMessage signOutAgain = await client.PostWithTokenAsync(SessionApi.SignOut, signIn.AccessToken);

        Assert.Equal(HttpStatusCode.NoContent, signOut.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, signOutAgain.StatusCode);
        Assert.Equal(SessionStatus.SignedOut, await SessionStatusAsync(signIn.AccessToken));
        IReadOnlyList<string> events = await host.Database.EventsAsync(correlationId);
        Assert.Contains(events, e => e.StartsWith($"AUTHENTICATION|IdentityAccess.SignedOut|SUCCESS|{IdentityDatabase.UserId(2)}|", StringComparison.Ordinal));
        Assert.Contains(events, e => e.Contains("IdentityAccess.SessionRefreshFailed|FAILED|", StringComparison.Ordinal) && e.Contains("failure_reason=>SESSION_ENDED", StringComparison.Ordinal));
    }

    /// <summary>DEV-C-01: signing out a session that has already ended still answers 204 but records nothing, since nothing ended.</summary>
    [Fact]
    public async Task SigningOutAnEndedSessionAgainIsA204WithNoSecondSignedOutEvent()
    {
        Guid correlationId = Guid.NewGuid();
        Guid revokedCorrelationId = Guid.NewGuid();
        using HttpClient client = host.Api.CreateClient().WithCorrelationId(correlationId);
        Session signIn = await client.SignInOrFailAsync(2);
        Session revoked = await client.SignInOrFailAsync(2);
        (await client.RefreshAsync(revoked.RefreshToken)).Dispose();
        (await client.RefreshAsync(revoked.RefreshToken)).Dispose();
        using HttpClient revokedClient = host.Api.CreateClient().WithCorrelationId(revokedCorrelationId);

        using HttpResponseMessage signOut = await client.PostWithTokenAsync(SessionApi.SignOut, signIn.AccessToken);
        using HttpResponseMessage signOutAgain = await client.PostWithTokenAsync(SessionApi.SignOut, signIn.AccessToken);
        using HttpResponseMessage signOutRevoked = await revokedClient.PostWithTokenAsync(SessionApi.SignOut, revoked.AccessToken);

        Assert.Equal(HttpStatusCode.NoContent, signOut.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, signOutAgain.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, signOutRevoked.StatusCode);
        Assert.Single(await host.Database.EventsAsync(correlationId), e => e.Contains("|IdentityAccess.SignedOut|", StringComparison.Ordinal));
        Assert.DoesNotContain(await host.Database.EventsAsync(revokedCorrelationId), e => e.Contains("|IdentityAccess.SignedOut|", StringComparison.Ordinal));
        Assert.Equal(SessionStatus.RevokedForReuse, await SessionStatusAsync(revoked.AccessToken));
    }

    /// <summary>Signing out ends this session only: the person's session in another browser goes on.</summary>
    [Fact]
    public async Task SigningOutLeavesThePersonsOtherSessions()
    {
        using HttpClient client = host.Api.CreateClient();
        Session here = await client.SignInOrFailAsync(2);
        Session elsewhere = await client.SignInOrFailAsync(2);

        (await client.PostWithTokenAsync(SessionApi.SignOut, here.AccessToken)).Dispose();
        using HttpResponseMessage refresh = await client.RefreshAsync(elsewhere.RefreshToken);

        Assert.Equal(HttpStatusCode.OK, refresh.StatusCode);
    }

    [Fact]
    public async Task SigningOutNeedsAnAccessToken()
    {
        using HttpClient client = host.Api.CreateClient();
        Session signIn = await client.SignInOrFailAsync(2);

        using HttpResponseMessage withRefreshToken = await client.PostWithTokenAsync(SessionApi.SignOut, signIn.RefreshToken);

        Assert.Equal(HttpStatusCode.Unauthorized, withRefreshToken.StatusCode);
        Assert.Equal(SessionStatus.Active, await SessionStatusAsync(signIn.AccessToken));
    }

    /// <summary>Disabling ends the session itself, so it does not come back when the user is activated again.</summary>
    [Fact]
    public async Task ASessionEndedByDisablingDoesNotResumeWhenTheUserIsActivatedAgain()
    {
        string viewer = IdentityDatabase.UserId(6);
        using HttpClient client = host.Api.CreateClient();
        Session administrator = await client.SignInOrFailAsync(1);
        Session session = await client.SignInOrFailAsync(6);
        try
        {
            using HttpResponseMessage disable = await client.PostAsync($"{AdministrationApi.Users}/{viewer}/disable", administrator.AccessToken);
            Assert.Equal(HttpStatusCode.OK, disable.StatusCode);
            await host.Database.ExecuteAsync($"UPDATE identity_access.\"user\" SET status = 'ACTIVE', disabled_at = NULL WHERE id = '{viewer}'");

            using HttpResponseMessage refresh = await client.RefreshAsync(session.RefreshToken);

            Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
            Assert.Equal(SessionStatus.RevokedForDisable, await SessionStatusAsync(session.AccessToken));
        }
        finally
        {
            await host.Database.ExecuteAsync($"UPDATE identity_access.\"user\" SET status = 'ACTIVE', disabled_at = NULL WHERE id = '{viewer}'");
        }
    }

    private static class SessionStatus
    {
        public const string Active = "ACTIVE|";
        public const string SignedOut = "SIGNED_OUT|SIGNED_OUT";
        public const string RevokedForReuse = "REVOKED|REFRESH_TOKEN_REUSED";
        public const string RevokedForDisable = "REVOKED|USER_DISABLED";
    }

    /// <summary>The status and end reason of the session an access token belongs to (its <c>sid</c>).</summary>
    private async Task<string> SessionStatusAsync(string accessToken)
    {
        using HttpClient client = host.Api.CreateClient();
        using HttpResponseMessage current = await client.GetWithTokenAsync(SessionApi.Current, accessToken);
        Guid sessionId = (await current.ReadAsync<CurrentSession>()).SessionId;
        return Assert.Single(await host.Database.QueryAsync(
            $"SELECT status || '|' || coalesce(end_reason, '') FROM identity_access.user_session WHERE id = '{sessionId}'"));
    }
}
