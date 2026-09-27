using Microsoft.AspNetCore.Mvc;
using PMPlatform.Api.Authorization;
using PMPlatform.Api.Errors;
using PMPlatform.Api.Models.IdentityAccess;
using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Domain.IdentityAccess;

namespace PMPlatform.Api.Controllers;

/// <summary>
/// ADM-002–005 and MOD-080 (TASK-031): users, never deleted (ERD: RETAIN); <c>disable</c> is the terminal-for-now command
/// and changes nothing but the user's status. The two <c>current/</c> endpoints are the user's own mobile verification
/// (ADR-004), open to any signed-in user for their own record only.
/// </summary>
[Route(Collection)]
[Tags("IdentityAccess")]
public sealed class UsersController(IUserAdministrationService users, IMobileNumberVerificationService mobileVerification) : IdentityAccessControllerBase
{
    private const string Collection = "api/v1/users";

    /// <summary>ADM-002. Filters: <c>status</c>, <c>userType</c> (sets), <c>departmentId</c>, <c>externalEntityId</c>, <c>q</c>; sort <c>displayName</c> or <c>username</c>.</summary>
    [HttpGet]
    [RequirePermission(PermissionCatalogue.UserView)]
    [ProducesResponseType<UserPage>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("IdentityAccess_ListUsers")]
    public async Task<IActionResult> List(
        [FromQuery] string? status,
        [FromQuery] string? userType,
        [FromQuery] Guid? departmentId,
        [FromQuery] Guid? externalEntityId,
        [FromQuery] string? q,
        [FromQuery] string? sort,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        CancellationToken cancellationToken)
    {
        List<FieldError> errors = [];
        UserQuery query = new(
            QueryParameters.EnumSet<UserStatus>(status, "status", errors),
            QueryParameters.EnumSet<UserType>(userType, "userType", errors),
            departmentId,
            externalEntityId,
            q,
            QueryParameters.UserSortOf(sort, errors),
            QueryParameters.Page(page, pageSize, errors));
        return errors.Count > 0 ? ValidationFailed(errors) : Respond(await users.ListAsync(CallerId, query, cancellationToken));
    }

    /// <summary>ADM-003. A disabled user is returned like any other, so history can still name them.</summary>
    [HttpGet("{userId:guid}")]
    [RequirePermission(PermissionCatalogue.UserView)]
    [ProducesResponseType<UserDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("IdentityAccess_GetUser")]
    public async Task<IActionResult> Get(Guid userId, CancellationToken cancellationToken) =>
        Respond(await users.GetAsync(CallerId, userId, cancellationToken));

    /// <summary>ADM-004.</summary>
    [HttpPost]
    [RequirePermission(PermissionCatalogue.UserManage)]
    [SensitiveWrite]
    [ProducesResponseType<UserDetail>(StatusCodes.Status201Created, "application/json")]
    [EndpointName("IdentityAccess_CreateUser")]
    public async Task<IActionResult> Create(UserCreateRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return request.Validate(out UserDraft? draft) is { Count: > 0 } errors
            ? ValidationFailed(errors)
            : RespondCreated(await users.CreateAsync(CallerId, draft!, cancellationToken), $"/{Collection}", u => u.Id);
    }

    /// <summary>ADM-005. Requires <c>If-Match</c> (R-21). A changed mobile number is unverified until its holder confirms it.</summary>
    [HttpPut("{userId:guid}")]
    [RequirePermission(PermissionCatalogue.UserManage)]
    [SensitiveWrite]
    [ProducesResponseType<UserDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("IdentityAccess_UpdateUser")]
    public async Task<IActionResult> Update(Guid userId, UserUpdateRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return !TryReadIfMatch(required: true, out uint? version, out IActionResult? problem) ? problem!
            : request.Validate(out UserChanges? changes) is { Count: > 0 } errors ? ValidationFailed(errors)
            : Respond(await users.UpdateAsync(CallerId, userId, changes!, version!.Value, cancellationToken));
    }

    /// <summary>MOD-080: DISABLED → ACTIVE.</summary>
    [HttpPost("{userId:guid}/activate")]
    [RequirePermission(PermissionCatalogue.UserManage)]
    [SensitiveWrite]
    [ProducesResponseType<UserDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("IdentityAccess_ActivateUser")]
    public async Task<IActionResult> Activate(Guid userId, CancellationToken cancellationToken) =>
        TryReadIfMatch(required: false, out uint? version, out IActionResult? problem)
            ? Respond(await users.ActivateAsync(CallerId, userId, version, cancellationToken))
            : problem!;

    /// <summary>MOD-080: ACTIVE → DISABLED. Takes effect on the user's next request; nothing they owned or did is changed.</summary>
    [HttpPost("{userId:guid}/disable")]
    [RequirePermission(PermissionCatalogue.UserManage)]
    [SensitiveWrite]
    [ProducesResponseType<UserDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("IdentityAccess_DisableUser")]
    public async Task<IActionResult> Disable(Guid userId, CancellationToken cancellationToken) =>
        TryReadIfMatch(required: false, out uint? version, out IActionResult? problem)
            ? Respond(await users.DisableAsync(CallerId, userId, version, cancellationToken))
            : problem!;

    /// <summary>ADR-004: sends a code to the caller's own mobile number. 503 until the SMS provider exists (TASK-103).</summary>
    [HttpPost("current/mobile-verification-challenge")]
    [AllowAnyAuthenticatedUser]
    [SensitiveWrite]
    [ProducesResponseType<MobileVerificationChallenge>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("IdentityAccess_CreateMobileVerificationChallenge")]
    public async Task<IActionResult> CreateMobileVerificationChallenge(CancellationToken cancellationToken) =>
        Respond(await mobileVerification.StartAsync(CallerId, cancellationToken));

    /// <summary>ADR-004: confirms the caller's own mobile number with the code sent to it.</summary>
    [HttpPost("current/mobile-verification")]
    [AllowAnyAuthenticatedUser]
    [SensitiveWrite]
    [ProducesResponseType<MobileVerificationResult>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("IdentityAccess_VerifyMobileNumber")]
    public async Task<IActionResult> VerifyMobileNumber(UserMobileVerificationCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        return command.Validate() is { Count: > 0 } errors
            ? ValidationFailed(errors)
            : Respond(await mobileVerification.CompleteAsync(CallerId, command.ChallengeId!, command.Code!, cancellationToken));
    }
}
