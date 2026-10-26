using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace PMPlatform.Tests.Integration.Identity;

/// <summary>The FG-03 administration endpoints (TASK-031) as a client calls them: bearer token, Idempotency-Key, If-Match.</summary>
internal static class AdministrationApi
{
    public const string Users = "/api/v1/users";
    public const string AccessRelationships = "/api/v1/access-relationships";
    public const string Roles = "/api/v1/roles";
    public const string Permissions = "/api/v1/permissions";
    public const string PermissionProfiles = "/api/v1/permission-profiles";
    public const string Departments = "/api/v1/departments";
    public const string ExternalEntities = "/api/v1/external-entities";
    public const string MobileVerificationChallenge = "/api/v1/users/current/mobile-verification-challenge";
    public const string MobileVerification = "/api/v1/users/current/mobile-verification";

    /// <summary>
    /// A request as the SPA sends it: a fresh Idempotency-Key on every POST and PUT unless <paramref name="idempotencyKey"/>
    /// says otherwise ("" sends none).
    /// </summary>
    public static Task<HttpResponseMessage> SendAsync(
        this HttpClient client, HttpMethod method, string path, string accessToken, object? body = null, string? ifMatch = null, string? idempotencyKey = null)
    {
        HttpRequestMessage request = new(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        if (method == HttpMethod.Post || method == HttpMethod.Put)
        {
            idempotencyKey ??= Guid.NewGuid().ToString();
            if (idempotencyKey.Length > 0)
            {
                request.Headers.TryAddWithoutValidation("Idempotency-Key", idempotencyKey);
            }
        }

        if (ifMatch is not null)
        {
            request.Headers.TryAddWithoutValidation("If-Match", ifMatch);
        }

        if (body is not null)
        {
            request.Content = JsonContent.Create(body, options: SessionApi.Json);
        }

        return client.SendAsync(request);
    }

    public static Task<HttpResponseMessage> GetAsync(this HttpClient client, string path, string accessToken) =>
        client.SendAsync(HttpMethod.Get, path, accessToken);

    public static Task<HttpResponseMessage> PostAsync(this HttpClient client, string path, string accessToken, object? body = null, string? ifMatch = null) =>
        client.SendAsync(HttpMethod.Post, path, accessToken, body, ifMatch);

    public static Task<HttpResponseMessage> PutAsync(this HttpClient client, string path, string accessToken, object body, string? ifMatch) =>
        client.SendAsync(HttpMethod.Put, path, accessToken, body, ifMatch);

    public static async Task<JsonObject> ReadObjectAsync(this HttpResponseMessage response) =>
        JsonNode.Parse(await response.Content.ReadAsStringAsync())!.AsObject();

    public static async Task<JsonArray> ReadArrayAsync(this HttpResponseMessage response) =>
        JsonNode.Parse(await response.Content.ReadAsStringAsync())!.AsArray();

    /// <summary>The <c>errors[]</c> of a problem as "field CODE" pairs, in order.</summary>
    public static async Task<string[]> ReadFieldErrorsAsync(this HttpResponseMessage response) =>
        [.. ((await response.ReadObjectAsync())["errors"]?.AsArray() ?? []).Select(e => $"{e!["field"]} {e["code"]}")];

    public static string ETagOf(HttpResponseMessage response) => response.Headers.ETag!.Tag;

    public static Guid IdOf(JsonObject body) => Guid.Parse(body["id"]!.GetValue<string>());
}
