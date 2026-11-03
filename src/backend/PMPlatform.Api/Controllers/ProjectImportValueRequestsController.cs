using Microsoft.AspNetCore.Mvc;
using PMPlatform.Api.Authorization;
using PMPlatform.Api.Errors;
using PMPlatform.Api.Models.IdentityAccess;
using PMPlatform.Api.Models.Project;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.Project.Contracts;
using PMPlatform.Domain.Common;
using PMPlatform.Domain.Project;

namespace PMPlatform.Api.Controllers;

/// <summary>
/// Requests to add a value an import's workbook names and the platform's lists lack (the 2 Nov 2026 review §3): an importer asks, the
/// administrators are notified and mark each added or declined, and the importer is notified in turn. Who may do which is the service's: an
/// importer holds PROJECT_IMPORT, an administrator MASTER_DATA_MANAGE or ORGANIZATION_MANAGE.
/// </summary>
[Route(Collection)]
[Tags("Project")]
public sealed class ProjectImportValueRequestsController(IProjectImportValueRequestService requests) : AdministrationControllerBase
{
    private const string Collection = "api/v1/project-import-value-requests";

    /// <summary>Asks for the value. An OPEN request for the same name is answered instead of a second one.</summary>
    [HttpPost]
    [AllowAnyAuthenticatedUser]
    [SensitiveWrite]
    [ProducesResponseType<ProjectImportValueRequestDetail>(StatusCodes.Status201Created, "application/json")]
    [EndpointName("Project_RequestProjectImportValue")]
    public async Task<IActionResult> Ask(ProjectImportValueRequestRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Validate(out ProjectImportDimension? dimension) is { Count: > 0 } errors)
        {
            return ValidationFailed(errors);
        }

        AdministrationResult<ProjectImportValueRequestDetail> result = await requests.RequestAsync(CallerId, dimension!.Value, request.SourceText!, cancellationToken);
        return result.Succeeded ? Created($"/{Collection}/{result.Value.Id}", result.Value) : Failure(result.Error);
    }

    /// <summary>Oldest first. Filter: <c>status</c>.</summary>
    [HttpGet]
    [AllowAnyAuthenticatedUser]
    [ProducesResponseType<ProjectImportValueRequestPage>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("Project_ListProjectImportValueRequests")]
    public async Task<IActionResult> List([FromQuery] string? status, [FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken cancellationToken)
    {
        List<FieldError> errors = [];
        IReadOnlyCollection<ProjectImportValueRequestStatus> statuses = QueryParameters.EnumSet<ProjectImportValueRequestStatus>(status, "status", errors);
        if (statuses.Count > 1)
        {
            errors.Add(new FieldError("status", FieldError.Malformed));
        }

        PageRequest paging = QueryParameters.Page(page, pageSize, errors);
        return errors.Count > 0
            ? ValidationFailed(errors)
            : Respond(await requests.ListAsync(CallerId, statuses.Count == 1 ? statuses.First() : null, paging, cancellationToken));
    }

    /// <summary>OPEN → ADDED or DECLINED, by an administrator; the requester is notified.</summary>
    [HttpPost("{requestId:guid}/resolve")]
    [AllowAnyAuthenticatedUser]
    [SensitiveWrite]
    [ProducesResponseType<ProjectImportValueRequestDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("Project_ResolveProjectImportValueRequest")]
    public async Task<IActionResult> Resolve(Guid requestId, ProjectImportValueResolveCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        return !TryReadIfMatch(required: false, out uint? version, out IActionResult? problem) ? problem!
            : command.Validate(out ProjectImportValueRequestStatus? status, out NarrativeText? note) is { Count: > 0 } errors ? ValidationFailed(errors)
            : Respond(await requests.ResolveAsync(CallerId, requestId, status!.Value, note, version, cancellationToken));
    }
}
