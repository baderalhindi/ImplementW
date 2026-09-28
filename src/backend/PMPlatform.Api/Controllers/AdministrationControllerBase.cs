using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using PMPlatform.Api.Errors;
using PMPlatform.Application.Features.IdentityAccess.Contracts;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;

namespace PMPlatform.Api.Controllers;

/// <summary>
/// What the FG-03 (TASK-031) and FG-04 (TASK-034) administration controllers share: the caller's id, the api-conventions
/// §4.4 answer to each refusal, and R-21 concurrency — an <c>ETag</c> on every single mutable resource, <c>If-Match</c> required on
/// <c>PUT</c> and honoured on a command when sent.
/// </summary>
[ApiController]
public abstract class AdministrationControllerBase : ControllerBase
{
    /// <summary>The <c>sub</c> of the validated access token; every administration endpoint requires one.</summary>
    protected Guid CallerId => Guid.Parse(User.FindFirstValue(SessionTokenClaims.Subject)!);

    private protected ObjectResult ValidationFailed(IReadOnlyList<FieldError> errors) =>
        ApiProblem.Result(HttpContext, StatusCodes.Status400BadRequest, ErrorCodes.ValidationFailed, "Validation failed.", errors);

    private protected IActionResult Respond<T>(AdministrationResult<T> result)
        where T : class =>
        result.Succeeded ? Ok(result.Value) : Failure(result.Error);

    private protected IActionResult Respond<T>(AdministrationResult<Versioned<T>> result)
        where T : class
    {
        if (!result.Succeeded)
        {
            return Failure(result.Error);
        }

        SetETag(result.Value.Version);
        return Ok(result.Value.Value);
    }

    /// <summary>201 with <c>Location</c> <paramref name="collectionPath"/>/{id} and the new resource's <c>ETag</c> (R-5).</summary>
    private protected IActionResult RespondCreated<T>(AdministrationResult<Versioned<T>> result, string collectionPath, Func<T, Guid> idOf)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(idOf);
        if (!result.Succeeded)
        {
            return Failure(result.Error);
        }

        SetETag(result.Value.Version);
        return Created($"{collectionPath}/{idOf(result.Value.Value)}", result.Value.Value);
    }

    /// <summary>
    /// The version <c>If-Match</c> names. Absent: 428 if <paramref name="required"/> (a <c>PUT</c>), else null. Anything
    /// but one strong ETag this API issued cannot match, so it is 412.
    /// </summary>
    private protected bool TryReadIfMatch(bool required, out uint? version, out IActionResult? problem)
    {
        version = null;
        problem = null;
        string? header = Request.Headers.IfMatch;
        if (string.IsNullOrEmpty(header))
        {
            if (required)
            {
                problem = ApiProblem.Result(HttpContext, StatusCodes.Status428PreconditionRequired, ErrorCodes.PreconditionRequired, "If-Match required.");
            }

            return !required;
        }

        if (header.Length > 2 && header[0] == '"' && header[^1] == '"'
            && uint.TryParse(header.AsSpan(1, header.Length - 2), NumberStyles.None, CultureInfo.InvariantCulture, out uint parsed))
        {
            version = parsed;
            return true;
        }

        problem = PreconditionFailed();
        return false;
    }

    private protected IActionResult Failure(AdministrationError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        List<FieldError> fields = [.. error.Fields.Select(f => new FieldError(f.Field, f.Code))];
        return error.Kind switch
        {
            AdministrationErrorKind.NotFound => ApiProblem.Result(HttpContext, StatusCodes.Status404NotFound, ErrorCodes.NotFound, "Not found."),
            AdministrationErrorKind.Forbidden => ApiProblem.Result(HttpContext, StatusCodes.Status403Forbidden, ErrorCodes.PermissionDenied, "Permission denied."),
            AdministrationErrorKind.RuleViolated => ApiProblem.Result(HttpContext, StatusCodes.Status422UnprocessableEntity, error.Code!, "Business rule violated.", fields),
            AdministrationErrorKind.Conflict => ApiProblem.Result(HttpContext, StatusCodes.Status409Conflict, error.Code!, "Conflict.", fields),
            AdministrationErrorKind.InvalidTransition => ApiProblem.Result(HttpContext, StatusCodes.Status409Conflict, ErrorCodes.InvalidTransition, "Invalid transition."),
            AdministrationErrorKind.TerminalState => ApiProblem.Result(HttpContext, StatusCodes.Status409Conflict, ErrorCodes.TerminalState, "Terminal state."),
            AdministrationErrorKind.PreconditionFailed => PreconditionFailed(),
            AdministrationErrorKind.Unavailable => ApiProblem.Result(HttpContext, StatusCodes.Status503ServiceUnavailable, ErrorCodes.Unavailable, "Unavailable."),
            _ => throw new InvalidOperationException($"Unknown administration error {error.Kind}."),
        };
    }

    private ObjectResult PreconditionFailed() =>
        ApiProblem.Result(HttpContext, StatusCodes.Status412PreconditionFailed, ErrorCodes.PreconditionFailed, "Precondition failed.");

    private void SetETag(uint version) => Response.Headers.ETag = string.Create(CultureInfo.InvariantCulture, $"\"{version}\"");
}
