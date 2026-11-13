using Microsoft.AspNetCore.Mvc;
using PMPlatform.Api.Authorization;
using PMPlatform.Api.Errors;
using PMPlatform.Api.Models.DocumentManagement;
using PMPlatform.Api.Models.IdentityAccess;
using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Common.DataImport;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Domain.IdentityAccess;

namespace PMPlatform.Api.Controllers;

/// <summary>
/// ADM-002–005 and MOD-080 (TASK-031): users, whose row is never removed (ERD: RETAIN); <c>disable</c> changes nothing but the user's
/// status, <c>delete</c> also takes them out of administration for good. The two <c>current/</c> endpoints are the user's own mobile verification
/// (ADR-004), open to any signed-in user for their own record only.
/// </summary>
[Route(Collection)]
[Tags("IdentityAccess")]
public sealed class UsersController(IUserAdministrationService users, IUserPermissionOverrideService permissionOverrides, IMobileNumberVerificationService mobileVerification, IDataImportService imports) : AdministrationControllerBase
{
    private const string Collection = "api/v1/users";
    private const string ImportRegister = ImportRegisterCodes.User;

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

    /// <summary>TASK-102: the migration workbook template, with the columns a row must fill and the codes a reference may name.</summary>
    [HttpGet("import-template/content")]
    [RequirePermission(PermissionCatalogue.DataImport)]
    [ProducesResponseType<FileStreamResult>(StatusCodes.Status200OK, "application/octet-stream")]
    [EndpointName("IdentityAccess_DownloadUserImportTemplate")]
    public Task<IActionResult> DownloadImportTemplate(CancellationToken cancellationToken) => ImportTemplateAsync(imports, ImportRegister, cancellationToken);

    /// <summary>TASK-102: validates the workbook in part <c>file</c>, or with <c>commit=true</c> imports it, all rows or none.</summary>
    [HttpPost("imports")]
    [RequirePermission(PermissionCatalogue.DataImport)]
    [SensitiveWrite]
    [DisableFormValueModelBinding]
    [ProducesResponseType<ImportReport>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("IdentityAccess_ImportUsers")]
    public Task<IActionResult> Import([FromQuery] bool? commit, CancellationToken cancellationToken) =>
        ImportAsync(imports, ImportRegister, commit == true, cancellationToken);

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

    /// <summary>The 5 Nov 2026 review §6.1: a local account's new temporary password, typed or generated, returned this once.</summary>
    [HttpPost("{userId:guid}/reset-password")]
    [RequirePermission(PermissionCatalogue.UserManage)]
    [SensitiveWrite]
    [ProducesResponseType<UserDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("IdentityAccess_ResetUserPassword")]
    public async Task<IActionResult> ResetPassword(Guid userId, UserPasswordResetRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return TryReadIfMatch(required: false, out uint? version, out IActionResult? problem)
            ? Respond(await users.ResetPasswordAsync(CallerId, userId, request.Password, version, cancellationToken))
            : problem!;
    }

    /// <summary>The 5 Nov 2026 review §6.1: ends a local account's lockout.</summary>
    [HttpPost("{userId:guid}/unlock")]
    [RequirePermission(PermissionCatalogue.UserManage)]
    [SensitiveWrite]
    [ProducesResponseType<UserDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("IdentityAccess_UnlockUser")]
    public async Task<IActionResult> Unlock(Guid userId, CancellationToken cancellationToken) =>
        TryReadIfMatch(required: false, out uint? version, out IActionResult? problem)
            ? Respond(await users.UnlockAsync(CallerId, userId, version, cancellationToken))
            : problem!;

    /// <summary>The 5 Nov 2026 review §6.1: the account's last usable day; empty makes it open-ended.</summary>
    [HttpPost("{userId:guid}/set-end-date")]
    [RequirePermission(PermissionCatalogue.UserManage)]
    [SensitiveWrite]
    [ProducesResponseType<UserDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("IdentityAccess_SetUserEndDate")]
    public async Task<IActionResult> SetEndDate(Guid userId, UserEndDateRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return TryReadIfMatch(required: false, out uint? version, out IActionResult? problem)
            ? Respond(await users.SetEndDateAsync(CallerId, userId, request.EndsOn, version, cancellationToken))
            : problem!;
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

    /// <summary>Client review, item 5: takes the user out of the platform for good; their history stays. A command, as the row is kept. R-40: a user who is not there is 204 as well.</summary>
    [HttpPost("{userId:guid}/delete")]
    [RequirePermission(PermissionCatalogue.UserManage)]
    [SensitiveWrite]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [EndpointName("IdentityAccess_DeleteUser")]
    public async Task<IActionResult> Delete(Guid userId, CancellationToken cancellationToken)
    {
        if (!TryReadIfMatch(required: false, out uint? version, out IActionResult? problem))
        {
            return problem!;
        }

        AdministrationError? error = await users.DeleteAsync(CallerId, userId, version, cancellationToken);
        return error is null || error.Kind == AdministrationErrorKind.NotFound ? NoContent() : Failure(error);
    }

    /// <summary>Client review, item 7: records, replaces or removes (null) the user's National ID or Iqama number. The response shows it masked.</summary>
    [HttpPut("{userId:guid}/national-id")]
    [RequirePermission(PermissionCatalogue.UserManage)]
    [SensitiveWrite]
    [ProducesResponseType<UserDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("IdentityAccess_SetUserNationalId")]
    public async Task<IActionResult> SetNationalId(Guid userId, UserNationalIdRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return !TryReadIfMatch(required: false, out uint? version, out IActionResult? problem) ? problem!
            : request.Validate(out string? nationalId) is { Count: > 0 } errors ? ValidationFailed(errors)
            : Respond(await users.SetNationalIdAsync(CallerId, userId, nationalId, version, cancellationToken));
    }

    /// <summary>The full number, for a holder of USER_NATIONAL_ID_VIEW; every reveal is audited and the response is never cached.</summary>
    [HttpGet("{userId:guid}/national-id")]
    [RequirePermission(PermissionCatalogue.UserNationalIdView)]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    [ProducesResponseType<NationalIdDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("IdentityAccess_RevealUserNationalId")]
    public async Task<IActionResult> RevealNationalId(Guid userId, CancellationToken cancellationToken) =>
        Respond(await users.RevealNationalIdAsync(CallerId, userId, cancellationToken));

    /// <summary>What the user's roles give them and what has been adjusted for them, permission by permission (client review, item 5).</summary>
    [HttpGet("{userId:guid}/permissions")]
    [RequirePermission(PermissionCatalogue.RoleView)]
    [ProducesResponseType<UserPermissions>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("IdentityAccess_ListUserPermissions")]
    public async Task<IActionResult> ListPermissions(Guid userId, CancellationToken cancellationToken) =>
        Respond(await permissionOverrides.ListAsync(CallerId, userId, cancellationToken));

    /// <summary>Grants or removes one permission for this user, on top of their roles. Applies at once; the reason is required and audited.</summary>
    [HttpPut("{userId:guid}/permissions/{permissionCode}")]
    [RequirePermission(PermissionCatalogue.RoleAssign)]
    [SensitiveWrite]
    [ProducesResponseType<UserPermissions>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("IdentityAccess_SetUserPermissionOverride")]
    public async Task<IActionResult> SetPermissionOverride(Guid userId, string permissionCode, UserPermissionOverrideRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return request.Validate(out UserPermissionOverrideChange? change) is { Count: > 0 } errors
            ? ValidationFailed(errors)
            : Respond(await permissionOverrides.SetAsync(CallerId, userId, permissionCode, change!, cancellationToken));
    }

    /// <summary>Puts one permission back to what the user's roles give. The reason is required and audited.</summary>
    [HttpPost("{userId:guid}/permissions/{permissionCode}/clear")]
    [RequirePermission(PermissionCatalogue.RoleAssign)]
    [SensitiveWrite]
    [ProducesResponseType<UserPermissions>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("IdentityAccess_ClearUserPermissionOverride")]
    public async Task<IActionResult> ClearPermissionOverride(Guid userId, string permissionCode, UserPermissionOverrideClearRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return request.Validate(out Domain.Common.NarrativeText? reason) is { Count: > 0 } errors
            ? ValidationFailed(errors)
            : Respond(await permissionOverrides.ClearAsync(CallerId, userId, permissionCode, reason!, cancellationToken));
    }

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

    /// <summary>The caller's own mobile number and whether it is verified, for the account page's sign-in methods (13 Nov 2026 review).</summary>
    [HttpGet("current/mobile-number")]
    [AllowAnyAuthenticatedUser]
    [ProducesResponseType<MobileNumberStatus>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("IdentityAccess_GetOwnMobileNumber")]
    public async Task<IActionResult> GetOwnMobileNumber(CancellationToken cancellationToken) =>
        Respond(await mobileVerification.GetAsync(CallerId, cancellationToken));

    /// <summary>ADR-004: sends a code to the caller's own mobile number. 503 where no SMS gateway is configured.</summary>
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
