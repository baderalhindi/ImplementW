using System.Buffers.Text;
using System.Collections.Concurrent;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace PMPlatform.Tests.Integration.Identity;

/// <summary>
/// An MFA provider hosted in the test process on a loopback port, speaking the platform's provider contract
/// (mfa-privileged-access.md §4). It checks the API key, binds each challenge to the subject it was started for, lets a
/// challenge be verified once (right or wrong), expires it after five minutes, and accepts a verification challenge only
/// for a subject that has enrolled. The code for a challenge is <see cref="CodeFor"/>: the test's stand-in for the
/// person's authenticator.
/// </summary>
public sealed class TestMultiFactorProvider : IAsyncDisposable
{
    /// <summary>A test value, valid only against this in-process provider.</summary>
    public const string ApiKey = "test-mfa-provider-api-key";

    private static readonly byte[] AuthenticatorKey = Encoding.ASCII.GetBytes("test-authenticator-shared-secret");
    private static readonly TimeSpan ChallengeLifetime = TimeSpan.FromMinutes(5);

    private readonly WebApplication _app;
    private readonly ConcurrentDictionary<string, Challenge> _challenges = new();
    private readonly ConcurrentDictionary<string, bool> _enrolledSubjects = new();
    private readonly ConcurrentQueue<string> _requestBodies = new();

    private TestMultiFactorProvider(WebApplication app)
    {
        _app = app;
    }

    public Uri Endpoint { get; private set; } = null!;

    /// <summary>Every request body the provider received, so a test can check what the platform sends it.</summary>
    public IEnumerable<string> RequestBodies => _requestBodies;

    public IReadOnlyDictionary<string, string?> Settings => new Dictionary<string, string?>
    {
        ["MFA_PROVIDER_ENDPOINT"] = Endpoint.AbsoluteUri,
        ["MFA_PROVIDER_API_KEY"] = ApiKey,
        ["Identity:Mfa:Provider:RequireHttps"] = "false",
    };

    /// <summary>The code the person's authenticator shows for <paramref name="challengeId"/>: six digits, derived from a key only the test holds.</summary>
    public static string CodeFor(string challengeId)
    {
        byte[] mac = HMACSHA256.HashData(AuthenticatorKey, Encoding.UTF8.GetBytes(challengeId));
        return (BitConverter.ToUInt32(mac, 0) % 1_000_000).ToString("D6", CultureInfo.InvariantCulture);
    }

    /// <summary>A code that is not the one for <paramref name="challengeId"/>.</summary>
    public static string WrongCodeFor(string challengeId) =>
        ((int.Parse(CodeFor(challengeId), CultureInfo.InvariantCulture) + 1) % 1_000_000).ToString("D6", CultureInfo.InvariantCulture);

    public static async Task<TestMultiFactorProvider> StartAsync()
    {
        WebApplicationBuilder builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();
        WebApplication app = builder.Build();

        TestMultiFactorProvider provider = new(app);
        app.MapPost("/enrolments", context => provider.StartAsync(context, enrolment: true));
        app.MapPost("/challenges", context => provider.StartAsync(context, enrolment: false));
        app.MapPost("/enrolments/{challengeId}/verify", context => provider.VerifyAsync(context, enrolment: true));
        app.MapPost("/challenges/{challengeId}/verify", context => provider.VerifyAsync(context, enrolment: false));

        await app.StartAsync();
        string address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();
        provider.Endpoint = new Uri(address);
        return provider;
    }

    public async ValueTask DisposeAsync() => await _app.DisposeAsync();

    private async Task StartAsync(HttpContext context, bool enrolment)
    {
        if (await ReadAuthorizedAsync(context) is not { } body || !body.TryGetProperty("subject", out JsonElement subject) || subject.GetString() is not { Length: > 0 } subjectId)
        {
            return;
        }

        if (!enrolment && !_enrolledSubjects.ContainsKey(subjectId))
        {
            context.Response.StatusCode = StatusCodes.Status409Conflict;
            return;
        }

        string challengeId = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(18));
        DateTimeOffset expiresAt = DateTimeOffset.UtcNow + ChallengeLifetime;
        _challenges[challengeId] = new Challenge(subjectId, enrolment, expiresAt);
        await Results.Json(new
        {
            challengeId,
            expiresAt,
            provisioningUri = enrolment ? $"otpauth://totp/PMPlatform:{subjectId}?secret=TESTONLY&issuer=PMPlatform" : null,
        }).ExecuteAsync(context);
    }

    private async Task VerifyAsync(HttpContext context, bool enrolment)
    {
        if (await ReadAuthorizedAsync(context) is not { } body)
        {
            return;
        }

        // A challenge is verified once: it is removed whether or not the code is right.
        string challengeId = context.Request.RouteValues["challengeId"]!.ToString()!;
        bool verified = _challenges.TryRemove(challengeId, out Challenge? challenge)
                        && challenge.Enrolment == enrolment
                        && challenge.ExpiresAt > DateTimeOffset.UtcNow
                        && body.TryGetProperty("subject", out JsonElement subject) && subject.GetString() == challenge.Subject
                        && body.TryGetProperty("code", out JsonElement code) && code.GetString() == CodeFor(challengeId);
        if (!verified)
        {
            context.Response.StatusCode = StatusCodes.Status422UnprocessableEntity;
            return;
        }

        if (enrolment)
        {
            _enrolledSubjects[challenge!.Subject] = true;
        }

        await Results.Json(new { verified = true }).ExecuteAsync(context);
    }

    /// <summary>The JSON body, or null after answering 401 to a request without the API key.</summary>
    private async Task<JsonElement?> ReadAuthorizedAsync(HttpContext context)
    {
        if (context.Request.Headers.Authorization != $"Bearer {ApiKey}")
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return null;
        }

        using StreamReader reader = new(context.Request.Body);
        string text = await reader.ReadToEndAsync();
        _requestBodies.Enqueue(text);
        return JsonDocument.Parse(text).RootElement.Clone();
    }

    private sealed record Challenge(string Subject, bool Enrolment, DateTimeOffset ExpiresAt);
}
