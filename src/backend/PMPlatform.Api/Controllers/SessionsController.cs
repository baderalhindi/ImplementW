using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PMPlatform.Api.Authorization;
using PMPlatform.Api.Errors;
using PMPlatform.Api.Models.IdentityAccess;
using PMPlatform.Application.Features.IdentityAccess.Contracts;
using PMPlatform.Domain.IdentityAccess;

namespace PMPlatform.Api.Controllers;

/// <summary>
/// Sign-in, SSO and session refresh (TASK-028); the second factor and step-up (TASK-029). Every failed sign-in, second
/// factor, refresh or step-up answers the same 401 <c>AUTHENTICATION_REQUIRED</c>, whatever the cause; only a
/// platform-side failure (directory, identity provider or MFA provider unreachable, method not configured) is a 503.
/// A sign-in by a person who requires MFA answers 200 with an MFA token instead of 201 with a session.
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

    /// <summary>Starts the second factor of a sign-in: an enrolment for a person with no factor yet, else a verification.</summary>
    [HttpPost("mfa-challenge")]
    [AllowAnonymous]
    [EndpointName("IdentityAccess_CreateMfaChallenge")]
    public async Task<IActionResult> CreateMfaChallenge(MfaChallengeCreateRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Validate() is { Count: > 0 } errors)
        {
            return ValidationFailed(errors);
        }

        MultiFactorChallengeResult result = await authentication.BeginMultiFactorSignInAsync(request.MfaToken!, cancellationToken);
        return ChallengeResponse(result);
    }

    /// <summary>Completes a sign-in with the second factor. The session is issued only here, once the code is verified.</summary>
    [HttpPost("mfa")]
    [AllowAnonymous]
    [EndpointName("IdentityAccess_CreateMfaSession")]
    public async Task<IActionResult> CreateMfa(MfaSessionCreateRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Validate() is { Count: > 0 } errors)
        {
            return ValidationFailed(errors);
        }

        AuthenticationResult result = await authentication.CompleteMultiFactorSignInAsync(request.MfaToken!, request.ChallengeId!, request.Code!, cancellationToken);
        return SessionResponse(result, StatusCodes.Status201Created);
    }

    /// <summary>What the presented access token asserts. Requires a valid, unexpired access token.</summary>
    [HttpGet("current")]
    [AllowAnyAuthenticatedUser]
    [EndpointName("IdentityAccess_GetCurrentSession")]
    public IActionResult GetCurrent()
    {
        ClaimsPrincipal user = User;
        SessionAuthentication? session = SessionPrincipal.Authentication(user);
        return Ok(new CurrentSessionDetail(
            Guid.Parse(user.FindFirstValue(SessionTokenClaims.Subject)!),
            Guid.Parse(user.FindFirstValue(SessionTokenClaims.SessionId)!),
            Enum.Parse<UserType>(user.FindFirstValue(SessionTokenClaims.UserType)!, ignoreCase: true),
            session?.Method,
            session?.MultiFactor == true,
            session?.AuthenticatedAt,
            [.. SessionPrincipal.Roles(user)],
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

    /// <summary>
    /// Starts a step-up (ADR-010) after a <c>STEP_UP_REQUIRED</c>. Anonymous like the refresh: the refresh token is the
    /// credential, and the session it continues is the one stepped up.
    /// </summary>
    [HttpPost("current/step-up-challenge")]
    [AllowAnonymous]
    [EndpointName("IdentityAccess_CreateStepUpChallenge")]
    public async Task<IActionResult> CreateStepUpChallenge(StepUpChallengeCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.Validate() is { Count: > 0 } errors)
        {
            return ValidationFailed(errors);
        }

        MultiFactorChallengeResult result = await authentication.BeginStepUpAsync(command.RefreshToken!, cancellationToken);
        return ChallengeResponse(result);
    }

    /// <summary>Completes a step-up: the session's next token pair, authenticated now. Session id and absolute expiry are unchanged.</summary>
    [HttpPost("current/step-up")]
    [AllowAnonymous]
    [EndpointName("IdentityAccess_StepUpSession")]
    public async Task<IActionResult> StepUp(StepUpCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.Validate() is { Count: > 0 } errors)
        {
            return ValidationFailed(errors);
        }

        AuthenticationResult result = await authentication.CompleteStepUpAsync(command.RefreshToken!, command.ChallengeId!, command.Code!, cancellationToken);
        return SessionResponse(result, StatusCodes.Status200OK);
    }

    private IActionResult SessionResponse(AuthenticationResult result, int successStatus)
    {
        NoStore();
        if (result.MultiFactorPending is { } pending)
        {
            return Ok(new MfaPendingDetail(pending.MfaToken, pending.MfaTokenExpiresAt, pending.EnrolmentRequired));
        }

        if (result.Session is null)
        {
            return Failure(result.Failure);
        }

        SessionDetail body = SessionDetail.From(result.Session);
        return successStatus == StatusCodes.Status201Created ? Created(CurrentSessionPath, body) : Ok(body);
    }

    private IActionResult ChallengeResponse(MultiFactorChallengeResult result)
    {
        NoStore();
        return result.ChallengeId is { } challengeId
            ? Ok(new MfaChallengeDetail(challengeId, result.ExpiresAt, result.ProvisioningUri))
            : Failure(result.Failure);
    }

    private ObjectResult Failure(AuthenticationFailure? failure) =>
        failure == AuthenticationFailure.Rejected
            ? ApiProblem.Result(HttpContext, StatusCodes.Status401Unauthorized, ErrorCodes.AuthenticationRequired, "Authentication required.")
            : Unavailable();

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
