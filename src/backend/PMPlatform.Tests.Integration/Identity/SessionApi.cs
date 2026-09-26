using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace PMPlatform.Tests.Integration.Identity;

/// <summary>The session endpoints as a client calls them, and their response bodies.</summary>
internal static class SessionApi
{
    public const string Sessions = "/api/v1/sessions";
    public const string Current = "/api/v1/sessions/current";
    public const string Refresh = "/api/v1/sessions/current/refresh";
    public const string SsoAuthorization = "/api/v1/sessions/sso-authorization";
    public const string SsoSessions = "/api/v1/sessions/sso";
    public const string MfaChallenge = "/api/v1/sessions/mfa-challenge";
    public const string MfaSessions = "/api/v1/sessions/mfa";
    public const string StepUpChallenge = "/api/v1/sessions/current/step-up-challenge";
    public const string StepUp = "/api/v1/sessions/current/step-up";
    public const string IdentityIntegration = "/api/v1/identity-integration";
    public const string IdentityIntegrationTest = "/api/v1/identity-integration/test";

    public static Task<HttpResponseMessage> SignInAsync(this HttpClient client, string username, string password) =>
        client.PostAsJsonAsync(Sessions, new { username, password });

    public static Task<HttpResponseMessage> RefreshAsync(this HttpClient client, string refreshToken) =>
        client.PostAsJsonAsync(Refresh, new { refreshToken });

    /// <summary>Signs local.r0<paramref name="person"/> in, passing the second factor when the platform asks for one.</summary>
    public static async Task<Session> SignInOrFailAsync(this HttpClient client, int person)
    {
        using HttpResponseMessage response = await client.SignInAsync($"local.r0{person}", TestDirectory.PersonPassword);
        return await client.SessionOrSecondFactorAsync(response);
    }

    /// <summary>The session of a 201, or of the second factor a 200 asks for.</summary>
    public static async Task<Session> SessionOrSecondFactorAsync(this HttpClient client, HttpResponseMessage signIn)
    {
        if (signIn.StatusCode == System.Net.HttpStatusCode.Created)
        {
            return await signIn.ReadAsync<Session>();
        }

        Assert.Equal(System.Net.HttpStatusCode.OK, signIn.StatusCode);
        return await client.PassSecondFactorOrFailAsync((await signIn.ReadAsync<MfaPending>()).MfaToken);
    }

    public static async Task<Session> PassSecondFactorOrFailAsync(this HttpClient client, string mfaToken)
    {
        using HttpResponseMessage response = await client.CompleteSecondFactorAsync(mfaToken);
        Assert.Equal(System.Net.HttpStatusCode.Created, response.StatusCode);
        return await response.ReadAsync<Session>();
    }

    /// <summary>Starts the second factor and answers it with the right code, as the person's authenticator would.</summary>
    public static async Task<HttpResponseMessage> CompleteSecondFactorAsync(this HttpClient client, string mfaToken)
    {
        MfaChallenge challenge = await client.StartChallengeOrFailAsync(MfaChallenge, new { mfaToken });
        return await client.PostAsJsonAsync(
            MfaSessions, new { mfaToken, challengeId = challenge.ChallengeId, code = TestMultiFactorProvider.CodeFor(challenge.ChallengeId) });
    }

    /// <summary>Steps the session up and returns its next token pair.</summary>
    public static async Task<Session> StepUpOrFailAsync(this HttpClient client, string refreshToken)
    {
        MfaChallenge challenge = await client.StartChallengeOrFailAsync(StepUpChallenge, new { refreshToken });
        using HttpResponseMessage response = await client.PostAsJsonAsync(
            StepUp, new { refreshToken, challengeId = challenge.ChallengeId, code = TestMultiFactorProvider.CodeFor(challenge.ChallengeId) });
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        return await response.ReadAsync<Session>();
    }

    /// <summary>Signs in a person who requires MFA and returns the MFA token, failing unless the platform asked for the second factor.</summary>
    public static async Task<string> MfaTokenOrFailAsync(this HttpClient client, string username)
    {
        using HttpResponseMessage response = await client.SignInAsync(username, TestDirectory.PersonPassword);
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        return (await response.ReadAsync<MfaPending>()).MfaToken;
    }

    public static async Task<MfaChallenge> StartChallengeOrFailAsync(this HttpClient client, string path, object body)
    {
        using HttpResponseMessage response = await client.PostAsJsonAsync(path, body);
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        return await response.ReadAsync<MfaChallenge>();
    }

    public static Task<HttpResponseMessage> GetWithTokenAsync(this HttpClient client, string path, string accessToken)
    {
        HttpRequestMessage request = new(HttpMethod.Get, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return client.SendAsync(request);
    }

    public static Task<HttpResponseMessage> PostWithTokenAsync(this HttpClient client, string path, string accessToken)
    {
        HttpRequestMessage request = new(HttpMethod.Post, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return client.SendAsync(request);
    }

    public static async Task<T> ReadAsync<T>(this HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<T>(Json))!;

    /// <summary>A problem body without the members that differ on every response, so two can be compared.</summary>
    public static async Task<string> ReadProblemShapeAsync(this HttpResponseMessage response)
    {
        JsonObject body = JsonNode.Parse(await response.Content.ReadAsStringAsync())!.AsObject();
        body.Remove("correlationId");
        body.Remove("timestamp");
        return body.ToJsonString();
    }

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
}

internal sealed record Session(
    string TokenType,
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAt,
    DateTimeOffset SessionExpiresAt,
    SessionUser User);

internal sealed record SessionUser(
    Guid Id, string UserType, string Username, string PreferredLanguage, string AuthenticationMethod, bool MultiFactorAuthenticated, DateTimeOffset AuthenticatedAt,
    List<RoleAssignment> RoleAssignments);

internal sealed record RoleAssignment(string RoleCode, Guid PermissionProfileVersionId, Guid? DepartmentId, Guid? ExternalEntityId, Guid? ProjectId);

internal sealed record CurrentSession(
    Guid UserId, Guid SessionId, string UserType, string AuthenticationMethod, bool MultiFactorAuthenticated, DateTimeOffset? AuthenticatedAt, List<string> Roles,
    DateTimeOffset ExpiresAt);

internal sealed record MfaPending(string MfaToken, DateTimeOffset MfaTokenExpiresAt, bool EnrolmentRequired);

internal sealed record MfaChallenge(string ChallengeId, DateTimeOffset? ExpiresAt, string? ProvisioningUri);

internal sealed record Problem(string Type, string Title, int Status, string Instance, string Code, Guid CorrelationId, Guid? IdempotencyKey, DateTimeOffset Timestamp);
