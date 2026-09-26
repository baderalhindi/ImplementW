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
    public const string IdentityIntegration = "/api/v1/identity-integration";
    public const string IdentityIntegrationTest = "/api/v1/identity-integration/test";

    public static Task<HttpResponseMessage> SignInAsync(this HttpClient client, string username, string password) =>
        client.PostAsJsonAsync(Sessions, new { username, password });

    public static Task<HttpResponseMessage> RefreshAsync(this HttpClient client, string refreshToken) =>
        client.PostAsJsonAsync(Refresh, new { refreshToken });

    public static async Task<Session> SignInOrFailAsync(this HttpClient client, int person)
    {
        using HttpResponseMessage response = await client.SignInAsync($"local.r0{person}", TestDirectory.PersonPassword);
        Assert.Equal(System.Net.HttpStatusCode.Created, response.StatusCode);
        return await response.ReadAsync<Session>();
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

internal sealed record SessionUser(Guid Id, string UserType, string Username, string PreferredLanguage, string AuthenticationMethod, List<RoleAssignment> RoleAssignments);

internal sealed record RoleAssignment(string RoleCode, Guid PermissionProfileVersionId, Guid? DepartmentId, Guid? ExternalEntityId, Guid? ProjectId);

internal sealed record CurrentSession(Guid UserId, Guid SessionId, string UserType, string AuthenticationMethod, List<string> Roles, DateTimeOffset ExpiresAt);

internal sealed record Problem(string Type, string Title, int Status, string Instance, string Code, Guid CorrelationId, Guid? IdempotencyKey, DateTimeOffset Timestamp);
