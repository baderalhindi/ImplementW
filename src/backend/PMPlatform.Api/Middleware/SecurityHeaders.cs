using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;
using PMPlatform.Api.ApiDocumentation;

namespace PMPlatform.Api.Middleware;

/// <summary>
/// The security response headers (TASK-078, CTL-12) on every response the API writes — successes, refusals, 404s, 429s and the
/// exception handler's 500 alike: the headers are set as the response starts, after anything that cleared it. The API serves
/// JSON and files, never a page, so its Content-Security-Policy allows nothing to load and nothing to frame it; the SPA's own
/// policy is set where the SPA is served (<c>endpoint-hardening.md</c> §3.2). The one page the API serves is the docs UI where it is
/// enabled (TASK-094), which is given <see cref="ApiDocumentationEndpoints.UiContentSecurityPolicy"/> instead.
/// </summary>
internal sealed class SecurityHeaders(RequestDelegate next, IOptions<ApiDocumentationOptions> documentation)
{
    /// <summary>Header name and value, in the order a scanner lists them. The five the workbook names come first.</summary>
    public static readonly IReadOnlyList<KeyValuePair<string, StringValues>> Headers =
    [
        new(HeaderNames.ContentSecurityPolicy, "default-src 'none'; frame-ancestors 'none'; base-uri 'none'; form-action 'none'"),
        new(HeaderNames.XContentTypeOptions, "nosniff"),
        new(HeaderNames.XFrameOptions, "DENY"),

        // Browsers ignore it over plain HTTP, so it is sent whatever the scheme the API sees behind the load balancer. No
        // `preload` until AHDA's domain is named and submitted to the list (network-security.md N-F1).
        new(HeaderNames.StrictTransportSecurity, "max-age=31536000; includeSubDomains"),
        new("Referrer-Policy", "no-referrer"),

        // Beyond the workbook's five: the headers TASK-077's ZAP scan reported missing (input-validation.md F-1).
        new("Permissions-Policy", "accelerometer=(), camera=(), geolocation=(), gyroscope=(), magnetometer=(), microphone=(), payment=(), usb=()"),
        new("Cross-Origin-Opener-Policy", "same-origin"),
        new("Cross-Origin-Resource-Policy", "same-origin"),
        new("Cross-Origin-Embedder-Policy", "require-corp"),
    ];

    public Task InvokeAsync(HttpContext context)
    {
        bool documentationUi = documentation.Value.Enabled && context.Request.Path.StartsWithSegments(ApiDocumentationEndpoints.UiPath);
        context.Response.OnStarting(() =>
        {
            foreach ((string name, StringValues value) in Headers)
            {
                context.Response.Headers[name] = value;
            }

            if (documentationUi)
            {
                context.Response.Headers.ContentSecurityPolicy = ApiDocumentationEndpoints.UiContentSecurityPolicy;
            }

            return Task.CompletedTask;
        });
        return next(context);
    }
}
