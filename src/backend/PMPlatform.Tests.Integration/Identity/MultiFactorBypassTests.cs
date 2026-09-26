using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace PMPlatform.Tests.Integration.Identity;

/// <summary>
/// TASK-029 acceptance criterion 3 and validation check 2: MFA bypass is not possible via any documented API path.
/// Every endpoint the API maps is called, so an endpoint added later is covered without editing this test.
/// </summary>
[Collection(IdentitySuite.Name)]
public sealed class MultiFactorBypassTests(IdentityTestHost host) : IDisposable
{
    private static readonly string[] AdministratorRole = ["R01"];
    private static readonly string[] PortfolioManagerRole = ["R02"];

    public void Dispose() => host.Clock.Reset();

    /// <summary>
    /// Holding the password and an MFA token, but not the second factor, every endpoint is called with the MFA token as
    /// the bearer and in every token field of the body. None answers with a session, and every protected one is a 401.
    /// </summary>
    [Fact]
    public async Task NoEndpointIssuesASessionOrServesAPersonWhoHasNotPassedTheSecondFactor()
    {
        using HttpClient client = host.Api.CreateClient();
        string mfaToken = await client.MfaTokenOrFailAsync("local.r01");
        MfaChallenge challenge = await client.StartChallengeOrFailAsync(SessionApi.MfaChallenge, new { mfaToken });
        object body = new
        {
            username = "local.r01",
            password = TestDirectory.PersonPassword,
            mfaToken,
            refreshToken = mfaToken,
            challengeId = challenge.ChallengeId,
            code = TestMultiFactorProvider.WrongCodeFor(challenge.ChallengeId),
            state = "state",
            transaction = mfaToken,
        };

        IReadOnlyList<ApiOperation> operations = Operations();
        Assert.True(operations.Count >= 13, $"only {operations.Count} operations found");
        foreach (ApiOperation operation in operations)
        {
            using HttpResponseMessage response = await SendAsync(client, operation, mfaToken, body);
            string content = await response.Content.ReadAsStringAsync();

            Assert.DoesNotContain("accessToken", content, StringComparison.Ordinal);
            if (!operation.AllowsAnonymous)
            {
                Assert.True(response.StatusCode == HttpStatusCode.Unauthorized, $"{operation}: {(int)response.StatusCode}");
            }
        }
    }

    /// <summary>
    /// Sign-in cannot issue an R01 access token that skipped MFA, so one is forged with the platform's own key, as a
    /// token minted before the policy grew would be. Every protected endpoint refuses it as unauthenticated.
    /// </summary>
    [Fact]
    public async Task AnAccessTokenThatSkippedMfaIsRefusedByEveryProtectedEndpoint()
    {
        using HttpClient client = host.Api.CreateClient();
        string forged = Forge("pmplatform-api", IdentityDatabase.UserId(1), ["pwd"], new Dictionary<string, object>
        {
            ["sid"] = Guid.NewGuid().ToString(),
            ["user_type"] = "INTERNAL",
            ["role"] = AdministratorRole,
        });
        string withoutAuthenticationContext = Forge("pmplatform-api", IdentityDatabase.UserId(2), methods: null, new Dictionary<string, object>
        {
            ["sid"] = Guid.NewGuid().ToString(),
            ["user_type"] = "INTERNAL",
            ["role"] = PortfolioManagerRole,
        });

        foreach (ApiOperation operation in Operations().Where(o => !o.AllowsAnonymous))
        {
            using HttpResponseMessage skippedMfa = await SendAsync(client, operation, forged, new { });
            using HttpResponseMessage noContext = await SendAsync(client, operation, withoutAuthenticationContext, new { });

            Assert.True(skippedMfa.StatusCode == HttpStatusCode.Unauthorized, $"{operation}: {(int)skippedMfa.StatusCode}");
            Assert.True(noContext.StatusCode == HttpStatusCode.Unauthorized, $"{operation}: {(int)noContext.StatusCode}");
        }
    }

    [Fact]
    public async Task ARefreshTokenThatSkippedMfaIsNotRefreshed()
    {
        using HttpClient client = host.Api.CreateClient();
        string forged = Forge("pmplatform-session-refresh", IdentityDatabase.UserId(1), ["pwd"], new Dictionary<string, object>
        {
            ["sid"] = Guid.NewGuid().ToString(),
            ["session_exp"] = DateTimeOffset.UtcNow.AddHours(8).ToUnixTimeSeconds(),
        });

        using HttpResponseMessage response = await client.RefreshAsync(forged);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>R01 assigned to a user mid-session does not reach them through a refresh of a session that never passed MFA.</summary>
    [Fact]
    public async Task ARoleThatRequiresMfaIsNotHandedOverByARefresh()
    {
        using HttpClient client = host.Api.CreateClient();
        Session session = await client.SignInOrFailAsync(2);
        const string assignmentId = "00000000-0111-4000-8000-000000000291";
        try
        {
            await host.Database.ExecuteAsync($"""
                INSERT INTO identity_access.access_relationship (id, user_id, permission_profile_version_id, starts_at, status, created_at, created_by, updated_at, updated_by)
                VALUES ('{assignmentId}', '{IdentityDatabase.UserId(2)}', '{IdentityDatabase.ProfileVersionId(1)}', now() - interval '1 minute', 'ACTIVE',
                        now(), '{IdentityDatabase.SeedPrincipalId}', now(), '{IdentityDatabase.SeedPrincipalId}')
                """);

            using HttpResponseMessage response = await client.RefreshAsync(session.RefreshToken);

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
        finally
        {
            await host.Database.ExecuteAsync($"DELETE FROM identity_access.access_relationship WHERE id = '{assignmentId}'");
        }
    }

    /// <summary>A wrong code is the generic 401 after the same 1 s floor as a wrong password, and the challenge is then spent.</summary>
    [Fact]
    public async Task AWrongCodeIsTheGeneric401AndSpendsTheChallenge()
    {
        using HttpClient client = host.Api.CreateClient();
        string mfaToken = await client.MfaTokenOrFailAsync("local.r01");
        MfaChallenge challenge = await client.StartChallengeOrFailAsync(SessionApi.MfaChallenge, new { mfaToken });

        long started = TimeProvider.System.GetTimestamp();
        using HttpResponseMessage wrong = await client.PostAsJsonAsync(
            SessionApi.MfaSessions, new { mfaToken, challengeId = challenge.ChallengeId, code = TestMultiFactorProvider.WrongCodeFor(challenge.ChallengeId) });
        TimeSpan elapsed = TimeProvider.System.GetElapsedTime(started);
        using HttpResponseMessage rightButSpent = await client.PostAsJsonAsync(
            SessionApi.MfaSessions, new { mfaToken, challengeId = challenge.ChallengeId, code = TestMultiFactorProvider.CodeFor(challenge.ChallengeId) });
        using HttpResponseMessage wrongPassword = await client.SignInAsync("local.r01", "not-the-password");

        Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
        Assert.True(elapsed >= TimeSpan.FromSeconds(1), $"answered after {elapsed}");
        Assert.Equal(HttpStatusCode.Unauthorized, rightButSpent.StatusCode);
        Problem problem = await wrong.ReadAsync<Problem>();
        Problem passwordProblem = await wrongPassword.ReadAsync<Problem>();
        Assert.Equal((passwordProblem.Code, passwordProblem.Title, passwordProblem.Type), (problem.Code, problem.Title, problem.Type));
    }

    /// <summary>The provider binds a challenge to its person: someone else's challenge and code do not complete this sign-in.</summary>
    [Fact]
    public async Task AnotherPersonsChallengeDoesNotCompleteASignIn()
    {
        await using IdentityApiFactory api = host.CreateApi(new Dictionary<string, string?> { ["Identity:Mfa:RequiredRoles:0"] = "R03" });
        using HttpClient client = api.CreateClient();
        string administratorToken = await client.MfaTokenOrFailAsync("local.r01");
        string managerToken = await client.MfaTokenOrFailAsync("local.r03");
        try
        {
            // User 3 enrols on this sign-in; user 1 may already be enrolled. Either way the challenge belongs to user 3.
            MfaChallenge managersChallenge = await client.StartChallengeOrFailAsync(SessionApi.MfaChallenge, new { mfaToken = managerToken });

            using HttpResponseMessage response = await client.PostAsJsonAsync(SessionApi.MfaSessions, new
            {
                mfaToken = administratorToken,
                challengeId = managersChallenge.ChallengeId,
                code = TestMultiFactorProvider.CodeFor(managersChallenge.ChallengeId),
            });

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
        finally
        {
            await host.Database.ExecuteAsync($"UPDATE identity_access.\"user\" SET mfa_enrolled_at = NULL WHERE id = '{IdentityDatabase.UserId(3)}'");
        }
    }

    [Fact]
    public async Task AnMfaTokenExpiresAfterFiveMinutes()
    {
        using HttpClient client = host.Api.CreateClient();
        string mfaToken = await client.MfaTokenOrFailAsync("local.r01");

        host.Clock.Advance(TimeSpan.FromMinutes(5) + TimeSpan.FromSeconds(1));
        using HttpResponseMessage response = await client.PostAsJsonAsync(SessionApi.MfaChallenge, new { mfaToken });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>The second factor re-reads the user: one disabled after their password was accepted gets no session.</summary>
    [Fact]
    public async Task AUserDisabledBetweenTheFactorsGetsNoSession()
    {
        using HttpClient client = host.Api.CreateClient();
        string mfaToken = await client.MfaTokenOrFailAsync("local.r01");
        MfaChallenge challenge = await client.StartChallengeOrFailAsync(SessionApi.MfaChallenge, new { mfaToken });
        try
        {
            await host.Database.ExecuteAsync($"UPDATE identity_access.\"user\" SET status = 'DISABLED' WHERE id = '{IdentityDatabase.UserId(1)}'");

            using HttpResponseMessage response = await client.PostAsJsonAsync(
                SessionApi.MfaSessions, new { mfaToken, challengeId = challenge.ChallengeId, code = TestMultiFactorProvider.CodeFor(challenge.ChallengeId) });

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
        finally
        {
            await host.Database.ExecuteAsync($"UPDATE identity_access.\"user\" SET status = 'ACTIVE' WHERE id = '{IdentityDatabase.UserId(1)}'");
        }
    }

    /// <summary>Every endpoint the API maps, with its methods and whether it admits an anonymous caller.</summary>
    private IReadOnlyList<ApiOperation> Operations() =>
        [.. host.Api.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .SelectMany(endpoint => (endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? ["GET"])
                .Select(method => new ApiOperation(
                    method,
                    "/" + endpoint.RoutePattern.RawText!.TrimStart('/'),
                    endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null)))];

    private static Task<HttpResponseMessage> SendAsync(HttpClient client, ApiOperation operation, string bearer, object body)
    {
        HttpRequestMessage request = new(new HttpMethod(operation.Method), operation.Path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
        if (operation.Method is "POST" or "PUT" or "PATCH")
        {
            request.Content = JsonContent.Create(body);
        }

        return client.SendAsync(request);
    }

    /// <summary>A token signed with the platform's own key; <paramref name="methods"/> null leaves out amr and auth_time altogether.</summary>
    private static string Forge(string audience, string userId, string[]? methods, Dictionary<string, object> claims)
    {
        claims["sub"] = userId;
        if (methods is not null)
        {
            claims["amr"] = methods;
            claims["auth_time"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }

        DateTime now = DateTime.UtcNow;
        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = "pmplatform",
            Audience = audience,
            Claims = claims,
            IssuedAt = now,
            NotBefore = now,
            Expires = now.AddMinutes(10),
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(IdentityApiFactory.SigningKey)), SecurityAlgorithms.HmacSha256),
        });
    }

    private sealed record ApiOperation(string Method, string Path, bool AllowsAnonymous)
    {
        public override string ToString() => $"{Method} {Path}";
    }
}
