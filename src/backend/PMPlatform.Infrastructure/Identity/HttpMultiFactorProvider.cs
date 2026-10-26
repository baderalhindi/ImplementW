using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PMPlatform.Application.Features.IdentityAccess.Authentication;
using PMPlatform.Application.Features.IdentityAccess.Contracts;

namespace PMPlatform.Infrastructure.Identity;

/// <summary>
/// The MFA verification provider over HTTPS (TASK-029), authenticated with <c>Authorization: Bearer
/// MFA_PROVIDER_API_KEY</c>. The provider is not yet selected (PTBC-027), so this adapter speaks the platform's own
/// four-call contract (docs/architecture/mfa-privileged-access.md §4): <c>POST enrolments</c> and <c>POST challenges</c>
/// with <c>{ subject }</c> start a challenge; <c>POST enrolments/{id}/verify</c> and <c>POST challenges/{id}/verify</c>
/// with <c>{ subject, code }</c> check it. The selected product is fitted to it by an adapter of its own (F-1). The
/// subject is the platform user id, nothing else.
/// </summary>
internal sealed partial class HttpMultiFactorProvider(
    IConfiguration configuration,
    IOptions<MultiFactorProviderOptions> options,
    IHttpClientFactory httpClients,
    ILogger<HttpMultiFactorProvider> logger) : IMultiFactorProvider
{
    public const string HttpClientName = "PMPlatform.Identity.Mfa";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public bool IsConfigured => Client() is not null;

    public async Task<MultiFactorChallengeResult> StartAsync(Guid userId, MultiFactorPurpose purpose, CancellationToken cancellationToken)
    {
        if (Client() is not { } client)
        {
            return MultiFactorChallengeResult.Failed(AuthenticationFailure.NotConfigured);
        }

        try
        {
            using HttpResponseMessage response = await SendAsync(client, Collection(purpose), new { subject = SubjectOf(userId) }, cancellationToken)
                .ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                LogUnavailable(logger, "challenge start", $"HTTP {(int)response.StatusCode}");
                return MultiFactorChallengeResult.Failed(AuthenticationFailure.ProviderUnavailable);
            }

            ChallengeBody? body = await ReadAsync<ChallengeBody>(response, cancellationToken).ConfigureAwait(false);
            if (body is not { ChallengeId.Length: > 0 } || (purpose == MultiFactorPurpose.Enrolment && string.IsNullOrEmpty(body.ProvisioningUri)))
            {
                LogUnavailable(logger, "challenge start", "response without a challenge id or, for an enrolment, a provisioning URI");
                return MultiFactorChallengeResult.Failed(AuthenticationFailure.ProviderUnavailable);
            }

            return new MultiFactorChallengeResult(
                body.ChallengeId, body.ExpiresAt, purpose == MultiFactorPurpose.Enrolment ? body.ProvisioningUri : null, Failure: null);
        }
        catch (Exception exception) when (IsUnavailable(exception))
        {
            LogUnavailable(logger, "challenge start", exception.GetType().Name);
            return MultiFactorChallengeResult.Failed(AuthenticationFailure.ProviderUnavailable);
        }
    }

    public async Task<MultiFactorVerification> CompleteAsync(Guid userId, MultiFactorPurpose purpose, string challengeId, string code, CancellationToken cancellationToken)
    {
        if (Client() is not { } client)
        {
            return MultiFactorVerification.Failed(AuthenticationFailure.NotConfigured);
        }

        if (string.IsNullOrEmpty(challengeId) || string.IsNullOrEmpty(code))
        {
            return MultiFactorVerification.Failed(AuthenticationFailure.Rejected);
        }

        try
        {
            // The challenge id is the client's input: escaped, so it names one path segment and nothing else.
            string path = $"{Collection(purpose)}/{Uri.EscapeDataString(challengeId)}/verify";
            using HttpResponseMessage response = await SendAsync(client, path, new { subject = SubjectOf(userId), code }, cancellationToken)
                .ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
            {
                // Only an explicit "verified": true passes. An empty or unexpected body fails closed.
                VerificationBody? body = await ReadAsync<VerificationBody>(response, cancellationToken).ConfigureAwait(false);
                return body is { Verified: true } ? MultiFactorVerification.Passed : MultiFactorVerification.Failed(AuthenticationFailure.Rejected);
            }

            // A wrong code, an expired, used or unknown challenge, another person's challenge, or too many attempts
            // concern the person. Anything else (a refused API key above all) is the integration failing.
            if (response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.NotFound or HttpStatusCode.Conflict
                or HttpStatusCode.Gone or HttpStatusCode.UnprocessableEntity or HttpStatusCode.TooManyRequests)
            {
                return MultiFactorVerification.Failed(AuthenticationFailure.Rejected);
            }

            LogUnavailable(logger, "verification", $"HTTP {(int)response.StatusCode}");
            return MultiFactorVerification.Failed(AuthenticationFailure.ProviderUnavailable);
        }
        catch (Exception exception) when (IsUnavailable(exception))
        {
            LogUnavailable(logger, "verification", exception.GetType().Name);
            return MultiFactorVerification.Failed(AuthenticationFailure.ProviderUnavailable);
        }
    }

    private MultiFactorProviderClient? Client() => MultiFactorProviderClient.Read(configuration, options.Value.RequireHttps);

    private async Task<HttpResponseMessage> SendAsync(MultiFactorProviderClient client, string path, object body, CancellationToken cancellationToken)
    {
        using HttpRequestMessage request = new(HttpMethod.Post, new Uri(client.Endpoint, path)) { Content = JsonContent.Create(body, options: Json) };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", client.ApiKey);
        return await httpClients.CreateClient(HttpClientName).SendAsync(request, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<T?> ReadAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
        where T : class
    {
        try
        {
            return await response.Content.ReadFromJsonAsync<T>(Json, cancellationToken).ConfigureAwait(false);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string Collection(MultiFactorPurpose purpose) => purpose == MultiFactorPurpose.Enrolment ? "enrolments" : "challenges";

    private static string SubjectOf(Guid userId) => userId.ToString("D");

    private static bool IsUnavailable(Exception exception) =>
        exception is HttpRequestException or IOException or TaskCanceledException or TimeoutException or NotSupportedException;

    private sealed record ChallengeBody(
        [property: JsonPropertyName("challengeId")] string? ChallengeId,
        [property: JsonPropertyName("expiresAt")] DateTimeOffset? ExpiresAt,
        [property: JsonPropertyName("provisioningUri")] string? ProvisioningUri);

    private sealed record VerificationBody([property: JsonPropertyName("verified")] bool? Verified);

    [LoggerMessage(Level = LogLevel.Error, Message = "MFA provider unavailable during {Operation}: {Reason}.")]
    private static partial void LogUnavailable(ILogger logger, string operation, string reason);
}
