using Microsoft.AspNetCore.Mvc;
using PMPlatform.Api.Authorization;
using PMPlatform.Api.Errors;
using PMPlatform.Api.Models.Closure;
using PMPlatform.Api.Models.IdentityAccess;
using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Features.Closure.Contracts;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Domain.Closure;

namespace PMPlatform.Api.Controllers;

/// <summary>
/// WF-10 (TASK-063): a project's post-project obligations — responsibilities that survive completion, open while the project is COMPLETED
/// and settled before it closes. Each command is one edge (R-4) and answers with the obligation.
/// </summary>
[Route(Collection)]
[Tags("Closure")]
public sealed class PostProjectObligationsController(IPostProjectObligationService obligations) : AdministrationControllerBase
{
    private const string Collection = "api/v1/post-project-obligations";

    /// <summary>The project's obligations, most recently changed first. <c>projectId</c> is required (R-3); <c>status</c> filters (a set).</summary>
    [HttpGet]
    [RequirePermission(PermissionCatalogue.CloseoutView)]
    [ProducesResponseType<PostProjectObligationPage>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("Closure_ListPostProjectObligations")]
    public async Task<IActionResult> List([FromQuery] Guid? projectId, [FromQuery] string? status, [FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken cancellationToken)
    {
        List<FieldError> errors = [];
        RequestValidation.RequireId(projectId, "projectId", errors);
        IReadOnlyCollection<PostProjectObligationStatus> statuses = QueryParameters.EnumSet<PostProjectObligationStatus>(status, "status", errors);
        PageRequest paging = QueryParameters.Page(page, pageSize, errors);
        return errors.Count > 0
            ? ValidationFailed(errors)
            : Ok(await obligations.ListAsync(CallerId, new PostProjectObligationQuery(projectId!.Value, statuses), paging, cancellationToken));
    }

    [HttpGet("{obligationId:guid}")]
    [RequirePermission(PermissionCatalogue.CloseoutView)]
    [ProducesResponseType<PostProjectObligationDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("Closure_GetPostProjectObligation")]
    public async Task<IActionResult> Get(Guid obligationId, CancellationToken cancellationToken) =>
        Respond(await obligations.GetAsync(CallerId, obligationId, cancellationToken));

    /// <summary>Records an OPEN obligation against a completion case — open or effected — or an open terminal closure case.</summary>
    [HttpPost]
    [RequirePermission(PermissionCatalogue.CloseoutRaise)]
    [SensitiveWrite]
    [ProducesResponseType<PostProjectObligationDetail>(StatusCodes.Status201Created, "application/json")]
    [EndpointName("Closure_CreatePostProjectObligation")]
    public async Task<IActionResult> Create(PostProjectObligationCreateRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return request.Validate(out PostProjectObligationDraft? draft) is { Count: > 0 } errors
            ? ValidationFailed(errors)
            : RespondCreated(await obligations.CreateAsync(CallerId, draft!, cancellationToken), $"/{Collection}", o => o.Id);
    }

    /// <summary>An open obligation's own fields, as a whole. Requires <c>If-Match</c> (R-21).</summary>
    [HttpPut("{obligationId:guid}")]
    [RequirePermission(PermissionCatalogue.CloseoutRaise)]
    [SensitiveWrite]
    [ProducesResponseType<PostProjectObligationDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("Closure_UpdatePostProjectObligation")]
    public async Task<IActionResult> Update(Guid obligationId, PostProjectObligationRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return !TryReadIfMatch(required: true, out uint? version, out IActionResult? problem) ? problem!
            : request.Validate(out PostProjectObligationChanges? changes) is { Count: > 0 } errors ? ValidationFailed(errors)
            : Respond(await obligations.UpdateAsync(CallerId, obligationId, changes!, version!.Value, cancellationToken));
    }

    /// <summary>OPEN → IN_PROGRESS.</summary>
    [HttpPost("{obligationId:guid}/start")]
    [RequirePermission(PermissionCatalogue.CloseoutRaise)]
    [SensitiveWrite]
    [ProducesResponseType<PostProjectObligationDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("Closure_StartPostProjectObligation")]
    public Task<IActionResult> Start(Guid obligationId, CancellationToken cancellationToken) =>
        CommandAsync((version, ct) => obligations.StartAsync(CallerId, obligationId, version, ct), cancellationToken);

    /// <summary>OPEN or IN_PROGRESS → SATISFIED.</summary>
    [HttpPost("{obligationId:guid}/satisfy")]
    [RequirePermission(PermissionCatalogue.CloseoutRaise)]
    [SensitiveWrite]
    [ProducesResponseType<PostProjectObligationDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("Closure_SatisfyPostProjectObligation")]
    public Task<IActionResult> Satisfy(Guid obligationId, CancellationToken cancellationToken) =>
        CommandAsync((version, ct) => obligations.SatisfyAsync(CallerId, obligationId, version, ct), cancellationToken);

    /// <summary>OPEN or IN_PROGRESS → CANCELLED: recorded in error, or no longer owed.</summary>
    [HttpPost("{obligationId:guid}/cancel")]
    [RequirePermission(PermissionCatalogue.CloseoutRaise)]
    [SensitiveWrite]
    [ProducesResponseType<PostProjectObligationDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("Closure_CancelPostProjectObligation")]
    public Task<IActionResult> Cancel(Guid obligationId, CancellationToken cancellationToken) =>
        CommandAsync((version, ct) => obligations.CancelAsync(CallerId, obligationId, version, ct), cancellationToken);

    /// <summary>OPEN or IN_PROGRESS → WAIVED: AHDA releases it. Internal users only.</summary>
    [HttpPost("{obligationId:guid}/waive")]
    [RequirePermission(PermissionCatalogue.CloseoutWaive)]
    [SensitiveWrite]
    [ProducesResponseType<PostProjectObligationDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("Closure_WaivePostProjectObligation")]
    public Task<IActionResult> Waive(Guid obligationId, CancellationToken cancellationToken) =>
        CommandAsync((version, ct) => obligations.WaiveAsync(CallerId, obligationId, version, ct), cancellationToken);

    /// <summary>A command with the caller's version when they send one (R-21).</summary>
    private async Task<IActionResult> CommandAsync(
        Func<uint?, CancellationToken, Task<AdministrationResult<Versioned<PostProjectObligationDetail>>>> command, CancellationToken cancellationToken) =>
        TryReadIfMatch(required: false, out uint? version, out IActionResult? problem)
            ? Respond(await command(version, cancellationToken))
            : problem!;
}
