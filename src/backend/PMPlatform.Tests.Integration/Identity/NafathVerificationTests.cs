using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using PMPlatform.Tests.Integration.AuditActivity;

namespace PMPlatform.Tests.Integration.Identity;

/// <summary>
/// TASK-068 against the in-process Nafath (<see cref="TestNafath"/>). Acceptance criterion 1: Nafath is called only for the
/// use case PTBC-031's resolution confirms (ADR-007) — an external user, at onboarding, once — and never as a sign-in.
/// Criterion 3 and the workbook's validation check: a Nafath outage neither grants nor denies access but is an explicit,
/// retryable state. Criterion 2 is <see cref="NafathDataMinimisationTests"/>.
/// </summary>
[Collection(IdentitySuite.Name)]
public sealed class NafathVerificationTests(IdentityTestHost host) : IAsyncLifetime
{
    /// <summary>local.r08: EXTERNAL, entity active, R04 on its entity's project and R08 on the entity — the person Nafath is for.</summary>
    private static readonly string ExternalUser = IdentityDatabase.UserId(8);

    private static readonly string[] DirectoryMethod = ["pwd"];

    public Task InitializeAsync() => ResetAsync();

    public Task DisposeAsync() => ResetAsync();

    [Fact]
    public async Task AnExternalUserGetsNoSessionUntilNafathVerifiesThemAndSignsInWithoutNafathAfterwards()
    {
        await using IdentityApiFactory api = host.CreateApi(TestNafath.Settings(host.Nafath));
        Guid correlationId = Guid.NewGuid();
        using HttpClient client = api.CreateClient().WithCorrelationId(correlationId);

        using HttpResponseMessage signIn = await client.SignInAsync("local.r08", TestDirectory.PersonPassword);

        Assert.Equal(HttpStatusCode.OK, signIn.StatusCode);
        Assert.True(signIn.Headers.CacheControl?.NoStore);
        string body = await signIn.Content.ReadAsStringAsync();
        Assert.DoesNotContain("accessToken", body, StringComparison.Ordinal);
        Assert.DoesNotContain("refreshToken", body, StringComparison.Ordinal);
        string verificationToken = (await signIn.ReadAsync<IdentityVerificationPending>()).IdentityVerificationToken;

        // The verification token is not a session: no endpoint serves it as a bearer.
        using (HttpResponseMessage current = await client.GetWithTokenAsync(SessionApi.Current, verificationToken))
        {
            Assert.Equal(HttpStatusCode.Unauthorized, current.StatusCode);
        }

        NafathCallback callback = await TestNafath.VerifyInBrowserAsync(client, verificationToken);
        Dictionary<string, Microsoft.Extensions.Primitives.StringValues> request = QueryHelpers.ParseQuery(callback.AuthorizationUrl.Query);
        Assert.StartsWith(host.Nafath.Authority.AbsoluteUri, callback.AuthorizationUrl.AbsoluteUri, StringComparison.Ordinal);
        Assert.Equal(TestNafath.ClientId, request["client_id"]);
        Assert.Equal(TestNafath.CallbackUrl, request["redirect_uri"]);
        Assert.Equal("openid", request["scope"]);
        Assert.Equal("S256", request["code_challenge_method"]);

        using HttpResponseMessage verified = await TestNafath.CompleteAsync(client, verificationToken, callback);

        Assert.Equal(HttpStatusCode.Created, verified.StatusCode);
        Session session = await verified.ReadAsync<Session>();
        Assert.Equal("EXTERNAL", session.User.UserType);
        Assert.Equal("DIRECTORY", session.User.AuthenticationMethod);
        Assert.Equal(["R04", "R08"], session.User.RoleAssignments.Select(a => a.RoleCode).Order(StringComparer.Ordinal));
        string reference = Assert.Single(await host.Database.QueryAsync(
            $"SELECT nafath_verification_reference FROM identity_access.\"user\" WHERE id = '{ExternalUser}' AND nafath_verified_at IS NOT NULL AND updated_by = '{ExternalUser}'"));
        Assert.True(Guid.TryParse(reference, out _));
        IReadOnlyList<string> events = await host.Database.EventsAsync(correlationId);
        Assert.Contains(events, e => e.StartsWith($"AUTHENTICATION|IdentityAccess.IdentityVerificationRequired|SUCCESS|{ExternalUser}|{ExternalUser}", StringComparison.Ordinal));
        Assert.Contains(events, e => e.StartsWith($"AUTHENTICATION|IdentityAccess.IdentityVerified|SUCCESS|{ExternalUser}|{ExternalUser}", StringComparison.Ordinal)
                                     && e.Contains($"verification_reference=>{reference}", StringComparison.Ordinal));
        Assert.Contains(events, e => e.StartsWith($"AUTHENTICATION|IdentityAccess.SignInSucceeded|SUCCESS|{ExternalUser}", StringComparison.Ordinal));

        // ADR-013: verification at onboarding, then a persistent sign-in. The next sign-in is a session, and Nafath hears nothing of it.
        int requestsBefore = host.Nafath.RequestCount;
        using HttpResponseMessage again = await client.SignInAsync("local.r08", TestDirectory.PersonPassword);
        Assert.Equal(HttpStatusCode.Created, again.StatusCode);
        using HttpResponseMessage refreshed = await client.RefreshAsync((await again.ReadAsync<Session>()).RefreshToken);
        Assert.Equal(HttpStatusCode.OK, refreshed.StatusCode);
        Assert.Equal(requestsBefore, host.Nafath.RequestCount);
    }

    /// <summary>ADR-007: never internal sign-in. Internal users sign in with Nafath on, by every path, and Nafath is sent nothing.</summary>
    [Fact]
    public async Task NafathIsNeverCalledForAnInternalUser()
    {
        await using IdentityApiFactory api = host.CreateApi(TestNafath.Settings(host.Nafath));
        using HttpClient client = api.CreateClient();
        int requestsBefore = host.Nafath.RequestCount;

        foreach (int person in new[] { 1, 2, 3, 6 })
        {
            Session session = await client.SignInOrFailAsync(person);
            Assert.Equal("INTERNAL", session.User.UserType);
        }

        Callback sso = await SsoBrowser.AuthorizeAsync(client, host.IdentityProvider, TestDirectory.Subject(2));
        using HttpResponseMessage ssoSession = await client.PostAsJsonAsync(SessionApi.SsoSessions, sso);
        Assert.Equal(HttpStatusCode.Created, ssoSession.StatusCode);

        // A verification token for an internal user cannot come from a sign-in, so one is forged with the platform's key.
        // The user is re-read and refused before Nafath is involved.
        string forged = ForgeVerificationToken(IdentityDatabase.UserId(2));
        using HttpResponseMessage start = await client.PostAsJsonAsync(SessionApi.IdentityVerificationAuthorization, new { identityVerificationToken = forged });
        using HttpResponseMessage complete = await client.PostAsJsonAsync(
            SessionApi.IdentityVerificationSessions, new { identityVerificationToken = forged, code = "c", state = "s", transaction = "t" });
        Assert.Equal(HttpStatusCode.Unauthorized, start.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, complete.StatusCode);

        Assert.Equal(requestsBefore, host.Nafath.RequestCount);
    }

    /// <summary>OQ-007: built behind a feature flag. Off — the default, and the shared API's state — an external user signs in as before and Nafath is never called.</summary>
    [Fact]
    public async Task WithTheFeatureOffNafathIsNeverCalled()
    {
        Dictionary<string, string?> settings = TestNafath.Settings(host.Nafath);
        settings["Identity:Nafath:Enabled"] = "false";
        await using IdentityApiFactory api = host.CreateApi(settings);
        using HttpClient client = api.CreateClient();
        using HttpClient shared = host.Api.CreateClient();
        int requestsBefore = host.Nafath.RequestCount;

        using HttpResponseMessage signIn = await client.SignInAsync("local.r08", TestDirectory.PersonPassword);
        using HttpResponseMessage sharedSignIn = await shared.SignInAsync("local.r08", TestDirectory.PersonPassword);
        using HttpResponseMessage start = await client.PostAsJsonAsync(
            SessionApi.IdentityVerificationAuthorization, new { identityVerificationToken = ForgeVerificationToken(ExternalUser) });

        Assert.Equal(HttpStatusCode.Created, signIn.StatusCode);
        Assert.Equal(HttpStatusCode.Created, sharedSignIn.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, start.StatusCode);
        Assert.Equal(requestsBefore, host.Nafath.RequestCount);
    }

    /// <summary>
    /// Nafath verifies; it never signs anyone in. A genuine Nafath callback is refused without the verification token of the
    /// sign-in it belongs to — with an MFA, refresh or access token in its place, or none — and as an SSO sign-in.
    /// </summary>
    [Fact]
    public async Task ANafathVerificationIsNeverASignIn()
    {
        await using IdentityApiFactory api = host.CreateApi(TestNafath.Settings(host.Nafath));
        using HttpClient client = api.CreateClient();
        string verificationToken = await VerificationTokenAsync(client);
        NafathCallback callback = await TestNafath.VerifyInBrowserAsync(client, verificationToken);
        Session internalSession = await client.SignInOrFailAsync(2);
        string mfaToken = await client.MfaTokenOrFailAsync("local.r01");

        foreach (string stranger in new[] { mfaToken, internalSession.RefreshToken, internalSession.AccessToken, "not-a-token" })
        {
            using HttpResponseMessage response = await TestNafath.CompleteAsync(client, stranger, callback);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.DoesNotContain("accessToken", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        }

        using HttpResponseMessage asSso = await client.PostAsJsonAsync(SessionApi.SsoSessions, new { callback.Code, callback.State, callback.Transaction });
        Assert.Equal(HttpStatusCode.Unauthorized, asSso.StatusCode);
        Assert.Empty(await host.Database.QueryAsync($"SELECT id::text FROM identity_access.\"user\" WHERE nafath_verified_at IS NOT NULL"));

        // The callback was never redeemed by any of them; with its own token it still verifies.
        using HttpResponseMessage verified = await TestNafath.CompleteAsync(client, verificationToken, callback);
        Assert.Equal(HttpStatusCode.Created, verified.StatusCode);
    }

    /// <summary>
    /// The workbook's validation check: a Nafath outage in a non-PROD environment. Unreachable when the verification starts,
    /// down when the code is redeemed, or not configured: each answers 503 IDENTITY_VERIFICATION_UNAVAILABLE with Retry-After.
    /// No session is issued (not a silent grant) and nothing is held against the person — account active, still unverified,
    /// the same verification token still good — so the retry succeeds once Nafath is back (not a silent denial).
    /// </summary>
    [Fact]
    public async Task ANafathOutageIsAnExplicitRetryStateThatNeitherGrantsNorDeniesAccess()
    {
        await using IdentityApiFactory working = host.CreateApi(TestNafath.Settings(host.Nafath));
        using HttpClient client = working.CreateClient();

        // Unreachable: nothing listens on port 1. Sign-in itself never calls Nafath, so the person reaches the verification.
        Dictionary<string, string?> unreachableSettings = TestNafath.Settings(host.Nafath);
        unreachableSettings["Identity:Nafath:Authority"] = "http://127.0.0.1:1/";
        await using (IdentityApiFactory unreachable = host.CreateApi(unreachableSettings))
        {
            using HttpClient outage = unreachable.CreateClient();
            string token = await VerificationTokenAsync(outage);

            using HttpResponseMessage start = await outage.PostAsJsonAsync(SessionApi.IdentityVerificationAuthorization, new { identityVerificationToken = token });

            await AssertRetryStateAsync(start);
            await AssertNothingGrantedOrHeldAsync();

            // The same token, once Nafath answers, verifies and signs the person in.
            NafathCallback callback = await TestNafath.VerifyInBrowserAsync(client, token);
            using HttpResponseMessage recovered = await TestNafath.CompleteAsync(client, token, callback);
            Assert.Equal(HttpStatusCode.Created, recovered.StatusCode);
        }

        await ResetAsync();

        // Down at the code's redemption, after the person approved at Nafath.
        string verificationToken = await VerificationTokenAsync(client);
        NafathCallback approved = await TestNafath.VerifyInBrowserAsync(client, verificationToken);
        host.Nafath.Unavailable = true;
        using (HttpResponseMessage down = await TestNafath.CompleteAsync(client, verificationToken, approved))
        {
            await AssertRetryStateAsync(down);
        }

        await AssertNothingGrantedOrHeldAsync();
        host.Nafath.Unavailable = false;
        using (HttpResponseMessage retried = await TestNafath.CompleteAsync(client, verificationToken, approved))
        {
            Assert.Equal(HttpStatusCode.Created, retried.StatusCode);
        }

        await ResetAsync();

        // Turned on without a client: the person is told verification is unavailable, never let through without it.
        Dictionary<string, string?> unconfigured = TestNafath.Settings(host.Nafath);
        unconfigured["NAFATH_CLIENT_SECRET"] = null;
        await using IdentityApiFactory notConfigured = host.CreateApi(unconfigured);
        using HttpClient noClient = notConfigured.CreateClient();
        string unconfiguredToken = await VerificationTokenAsync(noClient);
        using HttpResponseMessage unavailable = await noClient.PostAsJsonAsync(
            SessionApi.IdentityVerificationAuthorization, new { identityVerificationToken = unconfiguredToken });
        await AssertRetryStateAsync(unavailable);
        await AssertNothingGrantedOrHeldAsync();
    }

    /// <summary>
    /// A verification Nafath answered and did not grant — an ID token for another sign-in, a code already spent, a state or
    /// transaction not this verification's, a transaction bound to another person — is 422 IDENTITY_VERIFICATION_FAILED,
    /// not the generic sign-in 401: the person is signed in and may start again with the same token, which then succeeds.
    /// </summary>
    [Fact]
    public async Task AVerificationNafathDidNotGrantIsAnExplicitFailureThatCanBeStartedAgain()
    {
        await using IdentityApiFactory api = host.CreateApi(TestNafath.Settings(host.Nafath));
        using HttpClient client = api.CreateClient();
        string verificationToken = await VerificationTokenAsync(client);

        // An ID token minted for another authorization (its nonce is not this one's).
        host.Nafath.NonceOverride = "another-authorizations-nonce";
        NafathCallback replayed = await TestNafath.VerifyInBrowserAsync(client, verificationToken);
        await AssertNotVerifiedAsync(await TestNafath.CompleteAsync(client, verificationToken, replayed));
        host.Nafath.NonceOverride = null;

        // The same code again: Nafath redeems a code once.
        await AssertNotVerifiedAsync(await TestNafath.CompleteAsync(client, verificationToken, replayed));

        NafathCallback callback = await TestNafath.VerifyInBrowserAsync(client, verificationToken);
        await AssertNotVerifiedAsync(await TestNafath.CompleteAsync(client, verificationToken, callback with { State = "another-state" }));
        await AssertNotVerifiedAsync(await TestNafath.CompleteAsync(client, verificationToken, callback with { Transaction = callback.Transaction[..^4] + "AAAA" }));

        // A transaction started for someone else cannot complete this person's verification.
        await WithSecondExternalUserAsync(async otherUser =>
        {
            NafathCallback theirs = await TestNafath.VerifyInBrowserAsync(client, ForgeVerificationToken(otherUser));
            await AssertNotVerifiedAsync(await TestNafath.CompleteAsync(client, verificationToken, theirs));
        });

        await AssertNothingGrantedOrHeldAsync();
        NafathCallback fresh = await TestNafath.VerifyInBrowserAsync(client, verificationToken);
        using HttpResponseMessage verified = await TestNafath.CompleteAsync(client, verificationToken, fresh);
        Assert.Equal(HttpStatusCode.Created, verified.StatusCode);
    }

    /// <summary>The verification comes after every factor: MFA first, then Nafath, then a session that remembers both.</summary>
    [Fact]
    public async Task ThePersonPassesMfaFirstAndTheSessionAfterTheVerificationKeepsIt()
    {
        Dictionary<string, string?> settings = TestNafath.Settings(host.Nafath);
        settings["Identity:Mfa:RequiredRoles:0"] = "R08";
        await using IdentityApiFactory api = host.CreateApi(settings);
        using HttpClient client = api.CreateClient();
        string mfaToken = await client.MfaTokenOrFailAsync("local.r08");
        int requestsBefore = host.Nafath.RequestCount;

        using HttpResponseMessage secondFactor = await client.CompleteSecondFactorAsync(mfaToken);

        Assert.Equal(HttpStatusCode.OK, secondFactor.StatusCode);
        Assert.Equal(requestsBefore, host.Nafath.RequestCount);
        string verificationToken = (await secondFactor.ReadAsync<IdentityVerificationPending>()).IdentityVerificationToken;
        using HttpResponseMessage verified = await TestNafath.CompleteAsync(client, verificationToken, await TestNafath.VerifyInBrowserAsync(client, verificationToken));
        Assert.Equal(HttpStatusCode.Created, verified.StatusCode);
        Session session = await verified.ReadAsync<Session>();
        Assert.True(session.User.MultiFactorAuthenticated);
        using HttpResponseMessage current = await client.GetWithTokenAsync(SessionApi.Current, session.AccessToken);
        Assert.True((await current.ReadAsync<CurrentSession>()).MultiFactorAuthenticated);
    }

    /// <summary>
    /// A session an external user began before the verification applied to them (the feature turned on since) is not
    /// continued by a refresh: they sign in again, and are verified.
    /// </summary>
    [Fact]
    public async Task ASessionThatSkippedTheVerificationIsNotRefreshed()
    {
        using HttpClient shared = host.Api.CreateClient();
        Session before = await shared.SignInOrFailAsync(8);
        await using IdentityApiFactory api = host.CreateApi(TestNafath.Settings(host.Nafath));
        using HttpClient client = api.CreateClient();

        using HttpResponseMessage refresh = await client.RefreshAsync(before.RefreshToken);

        using HttpResponseMessage signInAgain = await client.SignInAsync("local.r08", TestDirectory.PersonPassword);

        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
        Assert.Equal(HttpStatusCode.OK, signInAgain.StatusCode);
        Assert.NotNull((await signInAgain.ReadAsync<IdentityVerificationPending>()).IdentityVerificationToken);
    }

    private static async Task<string> VerificationTokenAsync(HttpClient client)
    {
        using HttpResponseMessage signIn = await client.SignInAsync("local.r08", TestDirectory.PersonPassword);
        Assert.Equal(HttpStatusCode.OK, signIn.StatusCode);
        return (await signIn.ReadAsync<IdentityVerificationPending>()).IdentityVerificationToken;
    }

    private static async Task AssertRetryStateAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("IDENTITY_VERIFICATION_UNAVAILABLE", (await response.ReadAsync<Problem>()).Code);
        Assert.Equal(TimeSpan.FromSeconds(30), response.Headers.RetryAfter?.Delta);
        Assert.DoesNotContain("accessToken", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    private static async Task AssertNotVerifiedAsync(HttpResponseMessage response)
    {
        using (response)
        {
            Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
            Assert.Equal("IDENTITY_VERIFICATION_FAILED", (await response.ReadAsync<Problem>()).Code);
        }
    }

    /// <summary>No verification recorded, the account untouched and active, and the person still admitted to the verification.</summary>
    private async Task AssertNothingGrantedOrHeldAsync()
    {
        Assert.Equal(
            ["ACTIVE|-|-|-"],
            await host.Database.QueryAsync($"""
                SELECT status || '|' || coalesce(nafath_verification_reference, '-') || '|' || coalesce(nafath_verified_at::text, '-') || '|' || coalesce(disabled_at::text, '-')
                FROM identity_access."user" WHERE id = '{ExternalUser}'
                """));
    }

    /// <summary>A second external user of the same active entity, for as long as <paramref name="test"/> runs.</summary>
    private async Task WithSecondExternalUserAsync(Func<string, Task> test)
    {
        const string otherUser = "00000000-0110-4000-8000-000000000068";
        await host.Database.ExecuteAsync($"""
            INSERT INTO identity_access."user" (id, user_type, username, display_name, email, external_entity_id, status, created_at, created_by, updated_at, updated_by)
            VALUES ('{otherUser}', 'EXTERNAL', 'nafath.other', 'Nafath Other', 'nafath.other@identity.test', '{IdentityDatabase.ActiveEntityId}', 'ACTIVE',
                    now(), '{IdentityDatabase.SeedPrincipalId}', now(), '{IdentityDatabase.SeedPrincipalId}')
            """);
        try
        {
            await test(otherUser);
        }
        finally
        {
            await host.Database.ExecuteAsync($"DELETE FROM identity_access.\"user\" WHERE id = '{otherUser}'");
        }
    }

    private Task ResetAsync() => TestNafath.ResetAsync(host);

    /// <summary>A verification token signed with the platform's own key, for a user no sign-in would issue one to.</summary>
    internal static string ForgeVerificationToken(string userId)
    {
        DateTime now = DateTime.UtcNow;
        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = "pmplatform",
            Audience = "pmplatform-session-identity-verification",
            Claims = new Dictionary<string, object>
            {
                ["sub"] = userId,
                ["amr"] = DirectoryMethod,
                ["auth_time"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            },
            IssuedAt = now,
            NotBefore = now,
            Expires = now.AddMinutes(10),
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(IdentityApiFactory.SigningKey)), SecurityAlgorithms.HmacSha256),
        });
    }
}
