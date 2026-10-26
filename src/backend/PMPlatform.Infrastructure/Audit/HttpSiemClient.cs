using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PMPlatform.Application.Features.AuditActivity;

namespace PMPlatform.Infrastructure.Audit;

/// <summary>
/// AHDA's SIEM over HTTPS (TASK-033, PTBC-029): one <c>POST</c> of a <see cref="SiemEvent"/> as JSON per event to
/// <c>SIEM_ENDPOINT_URL</c>, authenticated with <c>Authorization: Bearer SIEM_API_TOKEN</c>. Any 2xx is acceptance. The
/// SIEM product is not yet named, so this is the platform's contract (record §4); the selected product's own ingestion
/// API is fitted to it by an adapter of its own. Nothing about a failure is logged but its status code or exception
/// type: never the token, and never the event.
/// </summary>
internal sealed partial class HttpSiemClient(
    IConfiguration configuration,
    IOptions<SiemOptions> options,
    IHttpClientFactory httpClients,
    ILogger<HttpSiemClient> logger) : ISiemClient
{
    public const string HttpClientName = "PMPlatform.Audit.Siem";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public bool IsConfigured => Endpoint() is not null;

    public async Task<bool> SendAsync(SiemEvent siemEvent, CancellationToken cancellationToken)
    {
        if (Endpoint() is not { } endpoint)
        {
            return false;
        }

        try
        {
            using HttpRequestMessage request = new(HttpMethod.Post, endpoint.Url) { Content = JsonContent.Create(siemEvent, options: Json) };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", endpoint.ApiToken);
            using HttpResponseMessage response = await httpClients.CreateClient(HttpClientName).SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
            {
                return true;
            }

            LogRefused(logger, (int)response.StatusCode);
            return false;
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException or TaskCanceledException or TimeoutException
                                          && !cancellationToken.IsCancellationRequested)
        {
            LogUnreachable(logger, exception.GetType().Name);
            return false;
        }
    }

    private SiemEndpoint? Endpoint() => SiemEndpoint.Read(configuration, options.Value.RequireHttps);

    [LoggerMessage(Level = LogLevel.Warning, Message = "SIEM refused an audit event: HTTP {StatusCode}.")]
    private static partial void LogRefused(ILogger logger, int statusCode);

    [LoggerMessage(Level = LogLevel.Warning, Message = "SIEM unreachable: {Reason}.")]
    private static partial void LogUnreachable(ILogger logger, string reason);
}
