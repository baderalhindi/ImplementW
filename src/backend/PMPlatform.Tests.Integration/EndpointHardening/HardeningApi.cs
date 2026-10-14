using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using PMPlatform.Tests.Integration.Identity;

namespace PMPlatform.Tests.Integration.EndpointHardening;

/// <summary>
/// TASK-078 requests as they arrive behind AHDA's load balancer: the client in <c>X-Forwarded-For</c>, followed by the load
/// balancer's own address. Each test takes fresh client addresses, so its counters start at zero whatever ran before it.
/// </summary>
internal static class HardeningApi
{
    public const string LoadBalancer = "10.129.0.7";

    /// <summary>The API under test trusts the load balancer's hop and the client's, as appsettings.json does for the deployed ones.</summary>
    public static readonly IReadOnlyDictionary<string, string?> BehindTheLoadBalancer = new Dictionary<string, string?>
    {
        ["ClientAddress:TrustedProxyHops"] = "2",
    };

    /// <summary>An address from the IPv6 documentation range that no other test uses.</summary>
    public static string NewClientAddress() => $"2001:db8::{RandomNumberGenerator.GetHexString(4, lowercase: true)}:{RandomNumberGenerator.GetHexString(4, lowercase: true)}";

    public static IReadOnlyDictionary<string, string?> Settings(params (string Key, string Value)[] settings)
    {
        Dictionary<string, string?> all = new(BehindTheLoadBalancer);
        foreach ((string key, string value) in settings)
        {
            all[key] = value;
        }

        return all;
    }

    /// <summary>
    /// <paramref name="spoofed"/> is what a client may write into the header itself; the load balancer appends the real address
    /// after it, so the API must ignore it.
    /// </summary>
    public static HttpRequestMessage From(this HttpRequestMessage request, string clientAddress, string? spoofed = null)
    {
        request.Headers.TryAddWithoutValidation("X-Forwarded-For", spoofed is null ? $"{clientAddress}, {LoadBalancer}" : $"{spoofed}, {clientAddress}, {LoadBalancer}");
        return request;
    }

    public static Task<HttpResponseMessage> SignInFromAsync(this HttpClient client, string clientAddress, string username, string password, string? spoofed = null) =>
        client.SendAsync(new HttpRequestMessage(HttpMethod.Post, SessionApi.Sessions) { Content = JsonContent.Create(new { username, password }) }
            .From(clientAddress, spoofed));

    /// <summary>A sensitive write any signed-in user may make (ADR-004's mobile verification: 503 until the SMS provider exists).</summary>
    public static Task<HttpResponseMessage> SensitiveWriteAsync(this HttpClient client, string accessToken, string clientAddress)
    {
        HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, AdministrationApi.MobileVerificationChallenge).From(clientAddress);
        request.Headers.Authorization = new("Bearer", accessToken);
        request.Headers.TryAddWithoutValidation("Idempotency-Key", Guid.NewGuid().ToString());
        return client.SendAsync(request);
    }

    public static async Task<string?> CodeAsync(this HttpResponseMessage response) =>
        response.Content.Headers.ContentType?.MediaType == "application/problem+json"
            ? (await response.ReadObjectAsync())["code"]?.GetValue<string>()
            : null;

    public static async Task<(HttpStatusCode Status, string? Code)> OutcomeAsync(this Task<HttpResponseMessage> sending)
    {
        using HttpResponseMessage response = await sending;
        return (response.StatusCode, await response.CodeAsync());
    }
}
