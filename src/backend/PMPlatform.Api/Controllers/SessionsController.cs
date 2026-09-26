using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PMPlatform.Api.Errors;
using PMPlatform.Api.Models.IdentityAccess;
using PMPlatform.Application.Features.IdentityAccess.Contracts;
using PMPlatform.Domain.IdentityAccess;

namespace PMPlatform.Api.Controllers;

/// <summary>
/// Sign-in, SSO and session refresh (TASK-028). Every failed sign-in or refresh answers the same 401
/// <c>AUTHENTICATION_REQUIRED</c>, whatever the cause; only a platform-side failure (directory or identity provider
/// unreachable, method not configured) is a 503.
/// </summary>
[ApiController]
[Route("api/v1/sessions")]
[Tags("IdentityAccess")]
public sealed class SessionsController(IAuthenticationService authentication) : ControllerBase
{
    private const string CurrentSessionPath = "/api/v1/sessions/current";

    /// <summary>Signs in with directory credentials.</summary>
    [HttpPost]
    [AllowAnonymous]
    [EndpointName("IdentityAccess_CreateSession")]
    public async Task<IActionResult> Create(SessionCreateRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Validate() is { Count: > 0 } errors)
        {
            return ValidationFailed(errors);
        }

        AuthenticationResult result = await authentication.SignInWithPasswordAsync(request.Username!, request.Password!, cancellationToken);
        return SessionResponse(result, StatusCodes.Status201Created);
    }

    /// <summary>Starts an SSO sign-in. Nothing is stored server-side; the transaction travels with the client.</summary>
    [HttpGet("sso-authorization")]
    [AllowAnonymous]
    [EndpointName("IdentityAccess_GetSsoAuthorization")]
    public async Task<IActionResult> GetSsoAuthorization(CancellationToken cancellationToken)
    {
        SsoAuthorizationResult result = await authentication.BeginSsoSignInAsync(cancellationToken);
        NoStore();
        return result is { AuthorizationUrl: { } url, Transaction: { } transaction }
            ? Ok(new SsoAuthorizationDetail(url, transaction))
            : Unavailable();
    }

    /// <summary>Completes an SSO sign-in with the code the identity provider redirected back with.</summary>
    [HttpPost("sso")]
    [AllowAnonymous]
    [EndpointName("IdentityAccess_CreateSsoSession")]
    public async Task<IActionResult> CreateSso(SsoSessionCreateRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Validate() is { Count: > 0 } errors)
        {
            return ValidationFailed(errors);
        }

        AuthenticationResult result = await authentication.CompleteSsoSignInAsync(request.Code!, request.State!, request.Transaction!, cancellationToken);
        return SessionResponse(result, StatusCodes.Status201Created);
    }

    /// <summary>What the presented access token asserts. Requires a valid, unexpired access token.</summary>
    [HttpGet("current")]
    [EndpointName("IdentityAccess_GetCurrentSession")]
    public IActionResult GetCurrent()
    {
        ClaimsPrincipal user = User;
        return Ok(new CurrentSessionDetail(
            Guid.Parse(user.FindFirstValue(SessionTokenClaims.Subject)!),
            Guid.Parse(user.FindFirstValue(SessionTokenClaims.SessionId)!),
            Enum.Parse<UserType>(user.FindFirstValue(SessionTokenClaims.UserType)!, ignoreCase: true),
            SessionTokenClaims.ParseMethod(user.FindFirstValue(SessionTokenClaims.AuthenticationMethod)),
            [.. user.FindAll(SessionTokenClaims.Role).Select(c => c.Value)],
            DateTimeOffset.FromUnixTimeSeconds(long.Parse(user.FindFirstValue(SessionTokenClaims.ExpiresAt)!, CultureInfo.InvariantCulture))));
    }

    /// <summary>
    /// Exchanges a refresh token for a new token pair. Anonymous because the access token has usually expired by then;
    /// the refresh token is the credential. The user's status and assignments are read afresh.
    /// </summary>
    [HttpPost("current/refresh")]
    [AllowAnonymous]
    [EndpointName("IdentityAccess_RefreshSession")]
    public async Task<IActionResult> Refresh(SessionRefreshCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.Validate() is { Count: > 0 } errors)
        {
            return ValidationFailed(errors);
        }

        AuthenticationResult result = await authentication.RefreshAsync(command.RefreshToken!, cancellationToken);
        return SessionResponse(result, StatusCodes.Status200OK);
    }

    private IActionResult SessionResponse(AuthenticationResult result, int successStatus)
    {
        NoStore();
        if (result.Session is null)
        {
            return result.Failure == AuthenticationFailure.Rejected
                ? ApiProblem.Result(HttpContext, StatusCodes.Status401Unauthorized, ErrorCodes.AuthenticationRequired, "Authentication required.")
                : Unavailable();
        }

        SessionDetail body = SessionDetail.From(result.Session);
        return successStatus == StatusCodes.Status201Created ? Created(CurrentSessionPath, body) : Ok(body);
    }

    private ObjectResult Unavailable() =>
        ApiProblem.Result(HttpContext, StatusCodes.Status503ServiceUnavailable, ErrorCodes.Unavailable, "Sign-in is unavailable.");

    private ObjectResult ValidationFailed(IReadOnlyList<FieldError> errors) =>
        ApiProblem.Result(HttpContext, StatusCodes.Status400BadRequest, ErrorCodes.ValidationFailed, "Validation failed.", errors);

    /// <summary>RFC 6749 §5.1: a response carrying tokens is never cached.</summary>
    private void NoStore()
    {
        Response.Headers.CacheControl = "no-store";
        Response.Headers.Pragma = "no-cache";
    }
}
