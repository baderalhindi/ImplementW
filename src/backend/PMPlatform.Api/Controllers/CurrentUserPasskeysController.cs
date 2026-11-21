using Microsoft.AspNetCore.Mvc;
using PMPlatform.Api.Authorization;
using PMPlatform.Api.Models.IdentityAccess;
using PMPlatform.Application.Features.IdentityAccess.Contracts;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;

namespace PMPlatform.Api.Controllers;

/// <summary>
/// The signed-in person's own passkeys (13 Nov 2026 review): Face ID, Touch ID or the device's unlock, for signing in without
/// a password. Anyone signed in manages their own and nobody else's. Adding one needs a sign-in within the last 15 minutes
/// (422 <c>IDENTITY_ACCESS_RECENT_SIGN_IN_REQUIRED</c>); 503 where this environment names no WebAuthn relying party.
/// </summary>
[Route(Collection)]
[Tags("IdentityAccess")]
public sealed class CurrentUserPasskeysController(IPasskeyService passkeys) : AdministrationControllerBase
{
    private const string Collection = "api/v1/users/current/passkeys";

    [HttpGet]
    [AllowAnyAuthenticatedUser]
    [ProducesResponseType<PasskeyList>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("IdentityAccess_ListPasskeys")]
    public async Task<IActionResult> List(CancellationToken cancellationToken) => Ok(await passkeys.ListAsync(CallerId, cancellationToken));

    /// <summary>The options for <c>navigator.credentials.create</c> on the device in hand. Nothing is stored until the device answers.</summary>
    [HttpGet("registration-options")]
    [AllowAnyAuthenticatedUser]
    [ProducesResponseType<PasskeyOptionsDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("IdentityAccess_GetPasskeyRegistrationOptions")]
    public async Task<IActionResult> GetRegistrationOptions(CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        AdministrationResult<PasskeyCeremony> result = await passkeys.BeginRegistrationAsync(CallerId, Session, cancellationToken);
        return result.Succeeded ? Ok(PasskeyOptionsDetail.From(result.Value)) : Failure(result.Error);
    }

    /// <summary>Stores the passkey the device created, once its answer verifies for this person's ceremony.</summary>
    [HttpPost]
    [AllowAnyAuthenticatedUser]
    [SensitiveWrite]
    [ProducesResponseType<PasskeySummary>(StatusCodes.Status201Created, "application/json")]
    [EndpointName("IdentityAccess_CreatePasskey")]
    public async Task<IActionResult> Create(PasskeyCreateRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Validate() is { Count: > 0 } errors)
        {
            return ValidationFailed(errors);
        }

        AdministrationResult<PasskeySummary> result = await passkeys.RegisterAsync(
            CallerId, Session, request.Transaction!, request.Credential!.ToJson(), request.Name!, cancellationToken);
        return result.Succeeded ? Created($"/{Collection}/{result.Value!.Id}", result.Value) : Failure(result.Error!);
    }

    /// <summary>Removes one of the person's passkeys; the device can no longer sign them in. Answers the passkeys left.</summary>
    [HttpPost("{passkeyId:guid}/delete")]
    [AllowAnyAuthenticatedUser]
    [SensitiveWrite]
    [ProducesResponseType<PasskeyList>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("IdentityAccess_DeletePasskey")]
    public async Task<IActionResult> Delete(Guid passkeyId, CancellationToken cancellationToken) =>
        Respond(await passkeys.RemoveAsync(CallerId, passkeyId, cancellationToken));

    private SessionAuthentication Session => SessionPrincipal.Authentication(User)!;
}
