using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace PMPlatform.Tests.Integration.Identity;

/// <summary>
/// TASK-029 acceptance criterion 1: MFA is enforced before a session is granted for at least R01, on every sign-in path,
/// against the test MFA provider. The sheet's verification for MFA_PROVIDER_*: a challenge/verify round-trip succeeds.
/// </summary>
[Collection(IdentitySuite.Name)]
public sealed class MultiFactorSignInTests(IdentityTestHost host) : IDisposable
{
    public void Dispose()
    {
        host.Clock.Reset();
        host.IdentityProvider.NextSubject = TestDirectory.Subject(1);
        host.IdentityProvider.AuthenticationMethods = null;
        host.IdentityProvider.AuthenticatedAt = null;
    }

    [Fact]
    public async Task ASystemAdministratorGetsNoSessionUntilTheSecondFactorIsVerified()
    {
        using HttpClient client = host.Api.CreateClient();

        using HttpResponseMessage signIn = await client.SignInAsync("local.r01", TestDirectory.PersonPassword);

        Assert.Equal(HttpStatusCode.OK, signIn.StatusCode);
        Assert.True(signIn.Headers.CacheControl?.NoStore);
        using (JsonDocument body = JsonDocument.Parse(await signIn.Content.ReadAsStringAsync()))
        {
            Assert.False(body.RootElement.TryGetProperty("accessToken", out _));
            Assert.False(body.RootElement.TryGetProperty("refreshToken", out _));
        }

        MfaPending pending = await signIn.ReadAsync<MfaPending>();
        Session session = await client.PassSecondFactorOrFailAsync(pending.MfaToken);

        Assert.True(session.User.MultiFactorAuthenticated);
        Assert.Equal(["R01"], session.User.RoleAssignments.Select(a => a.RoleCode));
        using HttpResponseMessage current = await client.GetWithTokenAsync(SessionApi.Current, session.AccessToken);
        CurrentSession claims = await current.ReadAsync<CurrentSession>();
        Assert.True(claims.MultiFactorAuthenticated);
        Assert.Equal("DIRECTORY", claims.AuthenticationMethod);
        Assert.Equal(session.User.AuthenticatedAt, claims.AuthenticatedAt);
    }

    [Fact]
    public async Task AnSsoSignInOfASystemAdministratorAlsoWaitsForTheSecondFactor()
    {
        using HttpClient client = host.Api.CreateClient();
        Callback callback = await SsoBrowser.AuthorizeAsync(client, host.IdentityProvider, TestDirectory.Subject(1));

        using HttpResponseMessage response = await client.PostAsJsonAsync(SessionApi.SsoSessions, callback);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Session session = await client.PassSecondFactorOrFailAsync((await response.ReadAsync<MfaPending>()).MfaToken);
        Assert.Equal("SINGLE_SIGN_ON", session.User.AuthenticationMethod);
        Assert.True(session.User.MultiFactorAuthenticated);
    }

    [Fact]
    public async Task AUserWhoseRolesDoNotRequireMfaGetsASessionAtOnce()
    {
        using HttpClient client = host.Api.CreateClient();

        using HttpResponseMessage response = await client.SignInAsync("local.r02", TestDirectory.PersonPassword);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.False((await response.ReadAsync<Session>()).User.MultiFactorAuthenticated);
    }

    /// <summary>The first sign-in enrols the factor (recorded as the user's own change); every later one verifies it.</summary>
    [Fact]
    public async Task TheFirstSecondFactorEnrolsAndEveryLaterOneVerifies()
    {
        await SetUnenrolledAsync(1);
        using HttpClient client = host.Api.CreateClient();

        MfaPending first = await PendingAsync(client, "local.r01");
        Assert.True(first.EnrolmentRequired);
        MfaChallenge enrolment = await client.StartChallengeOrFailAsync(SessionApi.MfaChallenge, new { first.MfaToken });
        Assert.StartsWith("otpauth://", enrolment.ProvisioningUri, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.Created, (await CompleteAsync(client, first.MfaToken, enrolment, TestMultiFactorProvider.CodeFor(enrolment.ChallengeId))).StatusCode);

        IReadOnlyList<string> row = await host.Database.QueryAsync(
            $"SELECT concat_ws('|', mfa_enrolled_at IS NOT NULL, updated_by) FROM identity_access.\"user\" WHERE id = '{IdentityDatabase.UserId(1)}'");
        Assert.Equal($"t|{IdentityDatabase.UserId(1)}", Assert.Single(row));

        MfaPending second = await PendingAsync(client, "local.r01");
        Assert.False(second.EnrolmentRequired);
        MfaChallenge verification = await client.StartChallengeOrFailAsync(SessionApi.MfaChallenge, new { second.MfaToken });
        Assert.Null(verification.ProvisioningUri);
        Assert.Equal(HttpStatusCode.Created, (await CompleteAsync(client, second.MfaToken, verification, TestMultiFactorProvider.CodeFor(verification.ChallengeId))).StatusCode);
    }

    /// <summary>
    /// SCR-002 for standard users is configuration: listing their role requires MFA of them too. R01 stays on the list
    /// whatever is configured, so CTL-07's minimum scope cannot be configured away.
    /// </summary>
    [Fact]
    public async Task RequiredRolesAreConfigurationAndR01CannotBeRemoved()
    {
        await using IdentityApiFactory api = host.CreateApi(new Dictionary<string, string?> { ["Identity:Mfa:RequiredRoles:0"] = "R02" });
        using HttpClient client = api.CreateClient();

        using HttpResponseMessage administrator = await client.SignInAsync("local.r01", TestDirectory.PersonPassword);
        using HttpResponseMessage portfolioManager = await client.SignInAsync("local.r02", TestDirectory.PersonPassword);
        using HttpResponseMessage viewer = await client.SignInAsync("local.r06", TestDirectory.PersonPassword);

        Assert.Equal(HttpStatusCode.OK, administrator.StatusCode);
        Assert.Equal(HttpStatusCode.OK, portfolioManager.StatusCode);
        Assert.Equal(HttpStatusCode.Created, viewer.StatusCode);
    }

    /// <summary>Once enrolled, always challenged: an enrolment cannot be skipped later because the user's roles do not demand it.</summary>
    [Fact]
    public async Task AnEnrolledUserIsChallengedWhateverTheirRoles()
    {
        using HttpClient client = host.Api.CreateClient();
        try
        {
            Session withoutMfa = await client.SignInOrFailAsync(2);
            Assert.False(withoutMfa.User.MultiFactorAuthenticated);
            await client.StepUpOrFailAsync(withoutMfa.RefreshToken);

            using HttpResponseMessage next = await client.SignInAsync("local.r02", TestDirectory.PersonPassword);

            Assert.Equal(HttpStatusCode.OK, next.StatusCode);
            Assert.False((await next.ReadAsync<MfaPending>()).EnrolmentRequired);
        }
        finally
        {
            await SetUnenrolledAsync(2);
        }
    }

    [Fact]
    public async Task WithoutEnrolmentAtSignInAnUnenrolledAdministratorIsRefused()
    {
        await SetUnenrolledAsync(1);
        await using IdentityApiFactory api = host.CreateApi(new Dictionary<string, string?> { ["Identity:Mfa:AllowEnrolmentAtSignIn"] = "false" });
        using HttpClient client = api.CreateClient();

        using HttpResponseMessage response = await client.SignInAsync("local.r01", TestDirectory.PersonPassword);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("AUTHENTICATION_REQUIRED", (await response.ReadAsync<Problem>()).Code);
    }

    /// <summary>Fail closed: an environment without an MFA provider issues no session to anyone who requires MFA.</summary>
    [Fact]
    public async Task WithoutAnMfaProviderNoSessionIsIssuedToAUserWhoRequiresMfa()
    {
        await using IdentityApiFactory api = host.CreateApi(new Dictionary<string, string?> { ["MFA_PROVIDER_ENDPOINT"] = string.Empty });
        using HttpClient client = api.CreateClient();

        using HttpResponseMessage administrator = await client.SignInAsync("local.r01", TestDirectory.PersonPassword);
        using HttpResponseMessage portfolioManager = await client.SignInAsync("local.r02", TestDirectory.PersonPassword);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, administrator.StatusCode);
        Assert.Equal("UNAVAILABLE", (await administrator.ReadAsync<Problem>()).Code);
        Assert.Equal(HttpStatusCode.Created, portfolioManager.StatusCode);
    }

    [Fact]
    public async Task AnMfaProviderThatRefusesThePlatformIsA503()
    {
        await using IdentityApiFactory api = host.CreateApi(new Dictionary<string, string?> { ["MFA_PROVIDER_API_KEY"] = "not-the-provider-api-key" });
        using HttpClient client = api.CreateClient();
        MfaPending pending = await PendingAsync(client, "local.r01");

        using HttpResponseMessage response = await client.PostAsJsonAsync(SessionApi.MfaChallenge, new { pending.MfaToken });

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    /// <summary>The identity provider's own second factor counts only when the policy names its amr value, and only with an auth_time.</summary>
    [Fact]
    public async Task AnIdentityProviderSecondFactorCountsOnlyWhenTheConfigurationTrustsIt()
    {
        DateTimeOffset authenticatedAt = DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.AddMinutes(-2).ToUnixTimeSeconds());
        host.IdentityProvider.AuthenticationMethods = ["pwd", "mfa"];
        host.IdentityProvider.AuthenticatedAt = authenticatedAt;
        await using IdentityApiFactory trusting = host.CreateApi(new Dictionary<string, string?> { ["Identity:Mfa:IdentityProviderMethods:0"] = "mfa" });

        using HttpClient defaultClient = host.Api.CreateClient();
        using HttpResponseMessage untrusted = await defaultClient.PostAsJsonAsync(
            SessionApi.SsoSessions, await SsoBrowser.AuthorizeAsync(defaultClient, host.IdentityProvider, TestDirectory.Subject(1)));
        Assert.Equal(HttpStatusCode.OK, untrusted.StatusCode);

        using HttpClient trustingClient = trusting.CreateClient();
        using HttpResponseMessage trusted = await trustingClient.PostAsJsonAsync(
            SessionApi.SsoSessions, await SsoBrowser.AuthorizeAsync(trustingClient, host.IdentityProvider, TestDirectory.Subject(1)));
        Assert.Equal(HttpStatusCode.Created, trusted.StatusCode);
        Session session = await trusted.ReadAsync<Session>();
        Assert.True(session.User.MultiFactorAuthenticated);
        Assert.Equal(authenticatedAt, session.User.AuthenticatedAt);

        host.IdentityProvider.AuthenticatedAt = null;
        using HttpResponseMessage withoutAuthTime = await trustingClient.PostAsJsonAsync(
            SessionApi.SsoSessions, await SsoBrowser.AuthorizeAsync(trustingClient, host.IdentityProvider, TestDirectory.Subject(1)));
        Assert.Equal(HttpStatusCode.OK, withoutAuthTime.StatusCode);
    }

    /// <summary>The provider is told the platform user id and the code, nothing else: no name, username or email (data minimisation).</summary>
    [Fact]
    public async Task TheMfaProviderIsSentOnlyTheUserIdAndTheCode()
    {
        using HttpClient client = host.Api.CreateClient();
        await client.SignInOrFailAsync(1);

        Assert.NotEmpty(host.MultiFactorProvider.RequestBodies);
        foreach (string body in host.MultiFactorProvider.RequestBodies)
        {
            using JsonDocument document = JsonDocument.Parse(body);
            Assert.All(document.RootElement.EnumerateObject(), property => Assert.Contains(property.Name, (string[])["subject", "code"]));
            Assert.True(Guid.TryParse(document.RootElement.GetProperty("subject").GetString(), out _));
        }
    }

    private static async Task<MfaPending> PendingAsync(HttpClient client, string username)
    {
        using HttpResponseMessage response = await client.SignInAsync(username, TestDirectory.PersonPassword);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.ReadAsync<MfaPending>();
    }

    private static Task<HttpResponseMessage> CompleteAsync(HttpClient client, string mfaToken, MfaChallenge challenge, string code) =>
        client.PostAsJsonAsync(SessionApi.MfaSessions, new { mfaToken, challengeId = challenge.ChallengeId, code });

    private Task SetUnenrolledAsync(int person) =>
        host.Database.ExecuteAsync($"UPDATE identity_access.\"user\" SET mfa_enrolled_at = NULL WHERE id = '{IdentityDatabase.UserId(person)}'");
}
