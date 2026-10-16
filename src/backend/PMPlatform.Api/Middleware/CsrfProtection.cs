using Microsoft.Net.Http.Headers;
using PMPlatform.Api.Errors;

namespace PMPlatform.Api.Middleware;

/// <summary>
/// CSRF protection (TASK-079, CTL-13) for every state-changing request, present and future. The platform's session is its
/// access token, which the SPA holds and sends as <c>Authorization: Bearer</c> (TASK-028 D-3); no browser attaches it on its
/// own, so it is the anti-forgery token. The anonymous session endpoints carry their credential — password, MFA token, refresh
/// token — in a JSON body for the same reason. The API sets no cookie, so nothing in a forged request authenticates it.
/// <para>
/// This middleware keeps that true for any endpoint, written now or later: a request with an unsafe method is refused with 403
/// <c>CROSS_SITE_REQUEST_REFUSED</c> before anything runs, unless it carries what a page on another origin cannot send without
/// the API's CORS consent — a <c>Bearer</c> <c>Authorization</c> header, or a body whose media type is not one an HTML form or a
/// no-cors <c>fetch</c> may send. Such a request is preflighted, and <see cref="OriginAllowlist"/> refuses every origin but the
/// platform's own. A <c>Basic</c> or <c>Negotiate</c> header does not count: browsers can attach those themselves.
/// </para>
/// The strategy and the rules for new endpoints: <c>docs/architecture/csrf-protection.md</c>.
/// </summary>
internal sealed class CsrfProtection(RequestDelegate next)
{
    /// <summary>The media types the Fetch standard lets a cross-origin page send without a preflight (CORS-safelisted Content-Type).</summary>
    private static readonly string[] SafelistedMediaTypes = ["application/x-www-form-urlencoded", "multipart/form-data", "text/plain"];

    public async Task InvokeAsync(HttpContext context)
    {
        if (IsForgeable(context.Request))
        {
            await ApiProblem.WriteAsync(context, StatusCodes.Status403Forbidden, ErrorCodes.CrossSiteRequestRefused, "Cross-site request refused.")
                .ConfigureAwait(false);
            return;
        }

        await next(context).ConfigureAwait(false);
    }

    /// <summary>True when a page on another origin could have made the browser send this request without a preflight.</summary>
    public static bool IsForgeable(HttpRequest request) =>
        !IsSafeMethod(request.Method) && !CarriesBearerToken(request) && !HasPreflightedBody(request);

    /// <summary>GET, HEAD, OPTIONS and TRACE change nothing (api-conventions R-40); any other method, PATCH or one not yet used, is checked.</summary>
    private static bool IsSafeMethod(string method) =>
        HttpMethods.IsGet(method) || HttpMethods.IsHead(method) || HttpMethods.IsOptions(method) || HttpMethods.IsTrace(method);

    private static bool CarriesBearerToken(HttpRequest request) =>
        request.Headers.Authorization.ToString().StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// A body a browser sends cross-origin only after a preflight. A Content-Type that does not parse is treated as forgeable: if a
    /// browser had judged it unsafelisted it would have preflighted, so refusing it costs a legitimate client nothing.
    /// </summary>
    private static bool HasPreflightedBody(HttpRequest request) =>
        MediaTypeHeaderValue.TryParse(request.ContentType, out MediaTypeHeaderValue? contentType)
        && !SafelistedMediaTypes.Contains(contentType.MediaType.ToString(), StringComparer.OrdinalIgnoreCase);
}
