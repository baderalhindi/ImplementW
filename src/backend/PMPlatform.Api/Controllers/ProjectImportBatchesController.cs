using Microsoft.AspNetCore.Mvc;
using PMPlatform.Api.Authorization;
using PMPlatform.Api.Errors;
using PMPlatform.Api.Models.DocumentManagement;
using PMPlatform.Api.Models.IdentityAccess;
using PMPlatform.Api.Models.Project;
using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.Project.Contracts;
using PMPlatform.Domain.Common;
using PMPlatform.Domain.Project;

namespace PMPlatform.Api.Controllers;

/// <summary>
/// Project import from RPMO's workbook (the 2 Nov 2026 review, R3-4): Projects → Import. Download the template, validate a workbook and map its
/// names, submit it as one batch; a second person approves the batch, which applies it, or returns it. An applied batch is rolled back whole
/// while its projects are untouched.
/// </summary>
[Route(Collection)]
[Tags("Project")]
public sealed class ProjectImportBatchesController(IProjectImportService imports) : AdministrationControllerBase
{
    private const string Collection = "api/v1/project-import-batches";
    private const long MaxFileBytes = 10L * 1024 * 1024;

    /// <summary>The .xlsx template, its drop-downs from live master data, with a hidden version stamp.</summary>
    [HttpGet("import-template/content")]
    [RequirePermission(PermissionCatalogue.ProjectImport)]
    [ProducesResponseType<FileStreamResult>(StatusCodes.Status200OK, "application/octet-stream")]
    [EndpointName("Project_DownloadProjectImportBatchTemplate")]
    public async Task<IActionResult> DownloadTemplate(CancellationToken cancellationToken)
    {
        AdministrationResult<byte[]> template = await imports.TemplateAsync(CallerId, cancellationToken);
        return template.Succeeded ? Attachment(new ProjectImportFile("project-import-template.xlsx", template.Value)) : Failure(template.Error);
    }

    /// <summary>What the workbook in part <c>file</c> would do with the decisions in part <c>plan</c>: every row's action, issues and changes.</summary>
    [HttpPost("validations")]
    [RequirePermission(PermissionCatalogue.ProjectImport)]
    [DisableFormValueModelBinding]
    [ProducesResponseType<ProjectImportPreview>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("Project_ValidateProjectImport")]
    public async Task<IActionResult> Validate(CancellationToken cancellationToken)
    {
        (IFormCollection? form, IActionResult? refused) = await FormAsync(cancellationToken);
        if (refused is not null)
        {
            return refused;
        }

        List<FieldError> errors = [];
        (ProjectImportUpload? upload, ProjectImportPlan? plan, _) = ProjectImportForm.Read(form!, errors);
        return errors.Count > 0 ? ValidationFailed(errors) : Respond(await imports.ValidateAsync(CallerId, upload!, plan!, cancellationToken));
    }

    /// <summary>Submits the workbook, its plan and a note as one SUBMITTED batch, when it validates with no error and no unmatched name.</summary>
    [HttpPost]
    [RequirePermission(PermissionCatalogue.ProjectImport)]
    [SensitiveWrite]
    [DisableFormValueModelBinding]
    [ProducesResponseType<ProjectImportBatchDetail>(StatusCodes.Status201Created, "application/json")]
    [EndpointName("Project_SubmitProjectImportBatch")]
    public async Task<IActionResult> Submit(CancellationToken cancellationToken)
    {
        (IFormCollection? form, IActionResult? refused) = await FormAsync(cancellationToken);
        if (refused is not null)
        {
            return refused;
        }

        List<FieldError> errors = [];
        (ProjectImportUpload? upload, ProjectImportPlan? plan, NarrativeText? note) = ProjectImportForm.Read(form!, errors);
        if (errors.Count > 0)
        {
            return ValidationFailed(errors);
        }

        AdministrationResult<ProjectImportBatchDetail> result = await imports.SubmitAsync(CallerId, upload!, plan!, note, cancellationToken);
        return result.Succeeded ? Created($"/{Collection}/{result.Value.Id}", result.Value) : Failure(result.Error);
    }

    /// <summary>Newest first. Filter: <c>status</c>.</summary>
    [HttpGet]
    [RequirePermission(PermissionCatalogue.ProjectView)]
    [ProducesResponseType<ProjectImportBatchPage>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("Project_ListProjectImportBatches")]
    public async Task<IActionResult> List([FromQuery] string? status, [FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken cancellationToken)
    {
        List<FieldError> errors = [];
        IReadOnlyCollection<ProjectImportBatchStatus> statuses = QueryParameters.EnumSet<ProjectImportBatchStatus>(status, "status", errors);
        if (statuses.Count > 1)
        {
            errors.Add(new FieldError("status", FieldError.Malformed));
        }

        PageRequest paging = QueryParameters.Page(page, pageSize, errors);
        return errors.Count > 0
            ? ValidationFailed(errors)
            : Ok(await imports.ListAsync(CallerId, new ProjectImportBatchQuery(statuses.Count == 1 ? statuses.First() : null, paging.Page, paging.PageSize), cancellationToken));
    }

    [HttpGet("{batchId:guid}")]
    [RequirePermission(PermissionCatalogue.ProjectView)]
    [ProducesResponseType<ProjectImportBatchDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("Project_GetProjectImportBatch")]
    public async Task<IActionResult> Get(Guid batchId, CancellationToken cancellationToken) => Respond(await imports.GetAsync(CallerId, batchId, cancellationToken));

    /// <summary>
    /// What a batch would do if it were approved now: its plan validated again against today's master data and projects, row by row, for the
    /// approver to review.
    /// </summary>
    [HttpGet("~/api/v1/project-import-batch-previews/{batchId:guid}")]
    [RequirePermission(PermissionCatalogue.ProjectView)]
    [ProducesResponseType<ProjectImportPreview>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("Project_GetProjectImportBatchPreview")]
    public async Task<IActionResult> Preview(Guid batchId, CancellationToken cancellationToken) => Respond(await imports.PreviewAsync(CallerId, batchId, cancellationToken));

    /// <summary>The workbook the batch was submitted from, as uploaded.</summary>
    [HttpGet("{batchId:guid}/content")]
    [RequirePermission(PermissionCatalogue.ProjectView)]
    [ProducesResponseType<FileStreamResult>(StatusCodes.Status200OK, "application/octet-stream")]
    [EndpointName("Project_DownloadProjectImportBatchFile")]
    public async Task<IActionResult> DownloadFile(Guid batchId, CancellationToken cancellationToken) =>
        Attachment(await imports.FileAsync(CallerId, batchId, cancellationToken));

    /// <summary>SUBMITTED → APPROVED: applies the batch, all of it or none. Never by its importer.</summary>
    [HttpPost("{batchId:guid}/approve")]
    [RequirePermission(PermissionCatalogue.ProjectImportApprove)]
    [SensitiveWrite]
    [ProducesResponseType<ProjectImportBatchDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("Project_ApproveProjectImportBatch")]
    public async Task<IActionResult> Approve(Guid batchId, CancellationToken cancellationToken) =>
        TryReadIfMatch(required: false, out uint? version, out IActionResult? problem)
            ? Respond(await imports.ApproveAsync(CallerId, batchId, version, cancellationToken))
            : problem!;

    /// <summary>SUBMITTED → RETURNED, with a reason. Never by its importer.</summary>
    [HttpPost("{batchId:guid}/return")]
    [RequirePermission(PermissionCatalogue.ProjectImportApprove)]
    [SensitiveWrite]
    [ProducesResponseType<ProjectImportBatchDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("Project_ReturnProjectImportBatch")]
    public async Task<IActionResult> Return(Guid batchId, ProjectImportReturnCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        return !TryReadIfMatch(required: false, out uint? version, out IActionResult? problem) ? problem!
            : command.Validate(out NarrativeText? reason) is { Count: > 0 } errors ? ValidationFailed(errors)
            : Respond(await imports.ReturnAsync(CallerId, batchId, reason!, version, cancellationToken));
    }

    /// <summary>APPROVED → ROLLED_BACK: removes the projects the batch created, while none has been touched.</summary>
    [HttpPost("{batchId:guid}/rollback")]
    [RequirePermission(PermissionCatalogue.ProjectImportApprove)]
    [SensitiveWrite]
    [ProducesResponseType<ProjectImportBatchDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("Project_RollBackProjectImportBatch")]
    public async Task<IActionResult> RollBack(Guid batchId, CancellationToken cancellationToken) =>
        TryReadIfMatch(required: false, out uint? version, out IActionResult? problem)
            ? Respond(await imports.RollBackAsync(CallerId, batchId, version, cancellationToken))
            : problem!;

    private async Task<(IFormCollection? Form, IActionResult? Refused)> FormAsync(CancellationToken cancellationToken)
    {
        (IFormCollection? form, DocumentUploadForm.Refusal refusal) = await DocumentUploadForm.ReadAsync(HttpContext, MaxFileBytes, cancellationToken);
        return refusal == DocumentUploadForm.Refusal.None
            ? (form, null)
            : (null, Failure(refusal == DocumentUploadForm.Refusal.TooLarge ? AdministrationError.PayloadTooLarge : AdministrationError.UnsupportedMediaType));
    }
}
