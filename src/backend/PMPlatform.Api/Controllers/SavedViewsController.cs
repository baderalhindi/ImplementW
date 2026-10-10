using Microsoft.AspNetCore.Mvc;
using PMPlatform.Api.Authorization;
using PMPlatform.Api.Errors;
using PMPlatform.Api.Models.IdentityAccess;
using PMPlatform.Api.Models.Reports;
using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.Reports.Contracts;

namespace PMPlatform.Api.Controllers;

/// <summary>
/// SCR-139 Saved Reports / Views (FG-02 §10.2; ADR-019; TASK-071): a person's private configurations — a report's parameters, or an SCR-138
/// composition — never a row. Another person's view does not exist for them (R-47). Every read says whether the view still fits what is in force;
/// every run plans it again and runs it as its owner may see now. Under <c>REPORT_COMPOSE</c> (R02, R03, R07).
/// </summary>
[Route(Collection)]
[Tags("Reports")]
public sealed class SavedViewsController(ISavedViewService views) : AdministrationControllerBase
{
    private const string Collection = "api/v1/saved-views";

    [HttpGet]
    [RequirePermission(PermissionCatalogue.ReportCompose)]
    [ProducesResponseType<SavedViewPage>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("Reports_ListSavedViews")]
    public async Task<IActionResult> List([FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken cancellationToken)
    {
        List<FieldError> errors = [];
        PageRequest paging = QueryParameters.Page(page, pageSize, errors);
        return errors.Count > 0 ? ValidationFailed(errors) : Ok(await views.ListAsync(CallerId, paging, cancellationToken));
    }

    [HttpPost]
    [RequirePermission(PermissionCatalogue.ReportCompose)]
    [SensitiveWrite]
    [ProducesResponseType<SavedViewDetail>(StatusCodes.Status201Created, "application/json")]
    [EndpointName("Reports_CreateSavedView")]
    public async Task<IActionResult> Create(SavedViewRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return request.Validate(out SavedViewInput? input) is { Count: > 0 } errors
            ? ValidationFailed(errors)
            : RespondCreated(await views.CreateAsync(CallerId, input!, cancellationToken), $"/{Collection}", v => v.Id);
    }

    [HttpGet("{savedViewId:guid}")]
    [RequirePermission(PermissionCatalogue.ReportCompose)]
    [ProducesResponseType<SavedViewDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("Reports_GetSavedView")]
    public async Task<IActionResult> Get(Guid savedViewId, CancellationToken cancellationToken) =>
        Respond(await views.GetAsync(CallerId, savedViewId, cancellationToken));

    /// <summary>The view, whole, by its owner. Requires <c>If-Match</c> (R-21).</summary>
    [HttpPut("{savedViewId:guid}")]
    [RequirePermission(PermissionCatalogue.ReportCompose)]
    [SensitiveWrite]
    [ProducesResponseType<SavedViewDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("Reports_UpdateSavedView")]
    public async Task<IActionResult> Update(Guid savedViewId, SavedViewRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return !TryReadIfMatch(required: true, out uint? version, out IActionResult? problem) ? problem!
            : request.Validate(out SavedViewInput? input) is { Count: > 0 } errors ? ValidationFailed(errors)
            : Respond(await views.UpdateAsync(CallerId, savedViewId, input!, version!.Value, cancellationToken));
    }

    /// <summary>HARD_OWNER: the owner's view is deleted; a view that is not theirs, or no longer exists, is 204 all the same (R-40).</summary>
    [HttpDelete("{savedViewId:guid}")]
    [RequirePermission(PermissionCatalogue.ReportCompose)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [EndpointName("Reports_DeleteSavedView")]
    public async Task<IActionResult> Delete(Guid savedViewId, CancellationToken cancellationToken)
    {
        AdministrationError? error = await views.DeleteAsync(CallerId, savedViewId, cancellationToken);
        return error is null || error.Kind == AdministrationErrorKind.NotFound ? NoContent() : Failure(error);
    }

    /// <summary>The view run now, as its owner may see now (RPT-API-023); 422 <c>REPORT_SAVED_VIEW_INCOMPATIBLE</c> names what no longer fits.</summary>
    [HttpPost("{savedViewId:guid}/run")]
    [RequirePermission(PermissionCatalogue.ReportCompose)]
    [NonSensitiveWrite("R-35: runs a saved view; reads the owner's authorised rows and stores nothing")]
    [ProducesResponseType<ReportResultPage>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("Reports_RunSavedView")]
    public async Task<IActionResult> Run(Guid savedViewId, [FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken cancellationToken)
    {
        List<FieldError> errors = [];
        PageRequest paging = QueryParameters.Page(page, pageSize, errors);
        return errors.Count > 0 ? ValidationFailed(errors) : Respond(await views.RunAsync(CallerId, savedViewId, paging, cancellationToken));
    }
}
