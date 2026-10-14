using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;
using PMPlatform.Api.Authorization;
using PMPlatform.Api.Correlation;
using PMPlatform.Api.Errors;

namespace PMPlatform.Api.Middleware;

/// <summary>
/// The CORS policy (TASK-078, CTL-12): only the origins <c>CORS_ALLOWED_ORIGINS</c> lists may call the API from a browser. Under
/// TASK-016 D-1 the SPA and the API share one origin per environment, so the list holds that origin (<c>APP_BASE_URL</c>) — a
/// browser sends <c>Origin</c> on a same-origin write too. A request whose <c>Origin</c> is not listed is refused here with
/// 403 <c>ORIGIN_NOT_ALLOWED</c> before anything runs, preflight or not: CORS alone only withholds the response from the page,
/// and the request — a sign-in attempt from a hostile page, say — would still execute. A request without <c>Origin</c>
/// (curl, a server, a same-origin read) is not a cross-origin browser request and is not affected.
/// </summary>
internal sealed class OriginAllowlist(RequestDelegate next, IOptions<AllowedOrigins> allowed)
{
    public const string Variable = "CORS_ALLOWED_ORIGINS";

    /// <summary>The request headers the SPA sends (httpClient.ts) and the conventions define.</summary>
    private static readonly string[] AllowedHeaders =
        [HeaderNames.Authorization, HeaderNames.ContentType, HeaderNames.IfMatch, SensitiveWriteAttribute.HeaderName, CorrelationId.HeaderName];

    /// <summary>The response headers the SPA reads: versions (R-21), created resources, throttling (R-48), downloads and correlation (R-41).</summary>
    private static readonly string[] ExposedHeaders =
        [HeaderNames.ETag, HeaderNames.Location, HeaderNames.RetryAfter, HeaderNames.ContentDisposition, CorrelationId.HeaderName];

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Headers.Origin is { Count: > 0 } origin && !allowed.Value.Origins.Contains(origin.ToString()))
        {
            await ApiProblem.WriteAsync(context, StatusCodes.Status403Forbidden, ErrorCodes.OriginNotAllowed, "Origin not allowed.").ConfigureAwait(false);
            return;
        }

        await next(context).ConfigureAwait(false);
    }

    /// <summary>
    /// The allowlist: comma-separated origins, each <c>scheme://host[:port]</c> with nothing after it, compared as the browser
    /// serialises <c>Origin</c> (lower case, default port omitted). <c>https</c> only, except in Development, where the local
    /// stack serves <c>http://localhost:5173</c>. A wildcard, a path or a malformed entry stops the API at start-up rather than
    /// widening the policy. Empty means no browser origin may call the API.
    /// </summary>
    public static IReadOnlySet<string> Parse(string? value, bool allowHttp)
    {
        HashSet<string> origins = new(StringComparer.Ordinal);
        foreach (string entry in (value ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!Uri.TryCreate(entry, UriKind.Absolute, out Uri? uri)
                || !(uri.Scheme == Uri.UriSchemeHttps || (allowHttp && uri.Scheme == Uri.UriSchemeHttp))
                || entry.Contains('*', StringComparison.Ordinal)
                || uri.UserInfo.Length > 0
                || uri.PathAndQuery != "/" || uri.Fragment.Length > 0 || entry.EndsWith('/'))
            {
                throw new InvalidOperationException(
                    $"{Variable} entry '{entry}' is not an origin: give scheme://host[:port] with no path or wildcard, https{(allowHttp ? " or http" : string.Empty)} only.");
            }

            origins.Add(uri.GetLeftPart(UriPartial.Authority).ToLowerInvariant());
        }

        return origins;
    }

    /// <summary>The CORS policy the browser is told for an allowed origin. No credentials: the API authenticates by bearer token, never by cookie.</summary>
    public static CorsPolicy Policy(IReadOnlySet<string> allowedOrigins) => new CorsPolicyBuilder()
        .WithOrigins([.. allowedOrigins])
        .WithMethods(HttpMethods.Get, HttpMethods.Post, HttpMethods.Put, HttpMethods.Patch, HttpMethods.Delete)
        .WithHeaders(AllowedHeaders)
        .WithExposedHeaders(ExposedHeaders)
        .SetPreflightMaxAge(TimeSpan.FromMinutes(10))
        .Build();
}

/// <summary><c>CORS_ALLOWED_ORIGINS</c>, parsed (<see cref="OriginAllowlist.Parse"/>). Validated at start-up, so a bad entry stops the API.</summary>
internal sealed class AllowedOrigins
{
    public IReadOnlySet<string> Origins { get; set; } = new HashSet<string>();
}
