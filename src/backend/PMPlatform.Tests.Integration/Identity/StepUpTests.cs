using System.Net;
using System.Net.Http.Json;

namespace PMPlatform.Tests.Integration.Identity;

/// <summary>
/// TASK-029 acceptance criterion 2 and validation check 1: a privileged action requires a fresh authentication context
/// no older than a configurable threshold (default 5 minutes). The shipped trigger is the ADM-041 connection test
/// (appsettings.json); the trigger list itself is configuration (ADR-010). The API's clock is moved, not waited on.
/// </summary>
[Collection(IdentitySuite.Name)]
public sealed class StepUpTests(IdentityTestHost host) : IDisposable
{
    public void Dispose() => host.Clock.Reset();

    [Fact]
    public async Task APrivilegedActionWithAStaleSessionRequiresReauthentication()
    {
        using HttpClient client = host.Api.CreateClient();
        Session session = await client.SignInOrFailAsync(1);
        using (HttpResponseMessage fresh = await client.PostWithTokenAsync(SessionApi.IdentityIntegrationTest, session.AccessToken))
        {
            Assert.Equal(HttpStatusCode.OK, fresh.StatusCode);
        }

        host.Clock.Advance(TimeSpan.FromMinutes(5) + TimeSpan.FromSeconds(1));
        using HttpResponseMessage stale = await client.PostWithTokenAsync(SessionApi.IdentityIntegrationTest, session.AccessToken);

        Assert.Equal(HttpStatusCode.Forbidden, stale.StatusCode);
        Assert.Equal("STEP_UP_REQUIRED", (await stale.ReadAsync<Problem>()).Code);
        string challenge = stale.Headers.WwwAuthenticate.ToString();
        Assert.Contains("error=\"insufficient_user_authentication\"", challenge, StringComparison.Ordinal);
        Assert.Contains("max_age=300", challenge, StringComparison.Ordinal);

        // The same access token still works where no step-up is required: the session is stale, not ended.
        using (HttpResponseMessage status = await client.GetWithTokenAsync(SessionApi.IdentityIntegration, session.AccessToken))
        {
            Assert.Equal(HttpStatusCode.OK, status.StatusCode);
        }

        Session steppedUp = await client.StepUpOrFailAsync(session.RefreshToken);
        using HttpResponseMessage again = await client.PostWithTokenAsync(SessionApi.IdentityIntegrationTest, steppedUp.AccessToken);

        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
        Assert.Equal(session.SessionExpiresAt, steppedUp.SessionExpiresAt);
        Assert.True(steppedUp.User.AuthenticatedAt > session.User.AuthenticatedAt);
        using HttpResponseMessage before = await client.GetWithTokenAsync(SessionApi.Current, session.AccessToken);
        using HttpResponseMessage after = await client.GetWithTokenAsync(SessionApi.Current, steppedUp.AccessToken);
        Assert.Equal((await before.ReadAsync<CurrentSession>()).SessionId, (await after.ReadAsync<CurrentSession>()).SessionId);
    }

    /// <summary>A refresh carries the authentication time forward: it never makes a stale session fresh.</summary>
    [Fact]
    public async Task ARefreshDoesNotMakeAStaleSessionFresh()
    {
        using HttpClient client = host.Api.CreateClient();
        Session session = await client.SignInOrFailAsync(1);

        host.Clock.Advance(TimeSpan.FromMinutes(6));
        using HttpResponseMessage refreshed = await client.RefreshAsync(session.RefreshToken);
        Session next = await refreshed.ReadAsync<Session>();
        using HttpResponseMessage response = await client.PostWithTokenAsync(SessionApi.IdentityIntegrationTest, next.AccessToken);

        Assert.Equal(session.User.AuthenticatedAt, next.User.AuthenticatedAt);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("STEP_UP_REQUIRED", (await response.ReadAsync<Problem>()).Code);
    }

    [Fact]
    public async Task TheThresholdIsConfiguration()
    {
        await using IdentityApiFactory api = host.CreateApi(new Dictionary<string, string?> { ["Identity:StepUp:MaxAge"] = "00:01:00" });
        using HttpClient client = api.CreateClient();
        Session session = await client.SignInOrFailAsync(1);

        host.Clock.Advance(TimeSpan.FromSeconds(61));
        using HttpResponseMessage response = await client.PostWithTokenAsync(SessionApi.IdentityIntegrationTest, session.AccessToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Contains("max_age=60", response.Headers.WwwAuthenticate.ToString(), StringComparison.Ordinal);
    }

    /// <summary>ADR-010: which actions step up follows the classification taxonomy, so the list is configuration, not code.</summary>
    [Fact]
    public async Task TheTriggerListIsConfiguration()
    {
        await using IdentityApiFactory api = host.CreateApi(new Dictionary<string, string?> { ["Identity:StepUp:Operations:0"] = "IdentityAccess_GetIdentityIntegration" });
        using HttpClient client = api.CreateClient();
        Session session = await client.SignInOrFailAsync(1);

        host.Clock.Advance(TimeSpan.FromMinutes(6));
        using HttpResponseMessage nowTriggered = await client.GetWithTokenAsync(SessionApi.IdentityIntegration, session.AccessToken);
        using HttpResponseMessage noLongerTriggered = await client.PostWithTokenAsync(SessionApi.IdentityIntegrationTest, session.AccessToken);

        Assert.Equal(HttpStatusCode.Forbidden, nowTriggered.StatusCode);
        Assert.Equal(HttpStatusCode.OK, noLongerTriggered.StatusCode);
    }

    /// <summary>A trigger naming no endpoint stops start-up: a typing error must not switch step-up off without anyone noticing.</summary>
    [Fact]
    public async Task AnUnknownTriggerStopsStartUp()
    {
        await using IdentityApiFactory api = host.CreateApi(new Dictionary<string, string?> { ["Identity:StepUp:Operations:1"] = "IdentityAccess_AssignRol" });

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(api.CreateClient);
        Assert.Contains("IdentityAccess_AssignRol", error.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// A step-up operation needs a second factor even from a user whose roles do not require MFA at sign-in; they
    /// enrol one at the step-up (SCR-002 for a standard user).
    /// </summary>
    [Fact]
    public async Task AStepUpOperationNeedsASecondFactorFromEveryUser()
    {
        await using IdentityApiFactory api = host.CreateApi(new Dictionary<string, string?> { ["Identity:StepUp:Operations:0"] = "IdentityAccess_GetCurrentSession" });
        using HttpClient client = api.CreateClient();
        try
        {
            Session session = await client.SignInOrFailAsync(2);
            using HttpResponseMessage withoutMfa = await client.GetWithTokenAsync(SessionApi.Current, session.AccessToken);
            Assert.Equal(HttpStatusCode.Forbidden, withoutMfa.StatusCode);
            Assert.Equal("STEP_UP_REQUIRED", (await withoutMfa.ReadAsync<Problem>()).Code);

            MfaChallenge enrolment = await client.StartChallengeOrFailAsync(SessionApi.StepUpChallenge, new { session.RefreshToken });
            Assert.NotNull(enrolment.ProvisioningUri);
            Session steppedUp = await client.StepUpOrFailAsync(session.RefreshToken);
            using HttpResponseMessage withMfa = await client.GetWithTokenAsync(SessionApi.Current, steppedUp.AccessToken);

            Assert.Equal(HttpStatusCode.OK, withMfa.StatusCode);
            Assert.True((await withMfa.ReadAsync<CurrentSession>()).MultiFactorAuthenticated);
        }
        finally
        {
            await host.Database.ExecuteAsync($"UPDATE identity_access.\"user\" SET mfa_enrolled_at = NULL WHERE id = '{IdentityDatabase.UserId(2)}'");
        }
    }

    [Fact]
    public async Task AStepUpWithAWrongCodeIsTheGeneric401AndChangesNothing()
    {
        using HttpClient client = host.Api.CreateClient();
        Session session = await client.SignInOrFailAsync(1);
        host.Clock.Advance(TimeSpan.FromMinutes(6));
        MfaChallenge challenge = await client.StartChallengeOrFailAsync(SessionApi.StepUpChallenge, new { session.RefreshToken });

        using HttpResponseMessage wrong = await client.PostAsJsonAsync(
            SessionApi.StepUp, new { session.RefreshToken, challengeId = challenge.ChallengeId, code = TestMultiFactorProvider.WrongCodeFor(challenge.ChallengeId) });
        using HttpResponseMessage stillStale = await client.PostWithTokenAsync(SessionApi.IdentityIntegrationTest, session.AccessToken);

        Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
        Assert.Equal("AUTHENTICATION_REQUIRED", (await wrong.ReadAsync<Problem>()).Code);
        Assert.Equal(HttpStatusCode.Forbidden, stillStale.StatusCode);
    }
}
