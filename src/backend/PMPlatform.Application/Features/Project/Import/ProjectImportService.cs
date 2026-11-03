using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using PMPlatform.Application.Common.Auditing;
using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Common.DataImport;
using PMPlatform.Application.Common.Events;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.MasterDataConfig.Contracts;
using PMPlatform.Application.Features.MasterDataConfig.Contracts.Resolution;
using PMPlatform.Application.Features.Project.Contracts;
using PMPlatform.Application.Features.Project.Contracts.Events;
using PMPlatform.Application.Features.Project.EventHandlers;
using PMPlatform.Domain.Common;
using PMPlatform.Domain.Project;
using ProjectEntity = PMPlatform.Domain.Project.Project;

namespace PMPlatform.Application.Features.Project.Import;

/// <summary>
/// Project import batches from RPMO's workbook (the 2 Nov 2026 review, R3-4). A batch is validated on upload, again on submission and once more
/// on approval, against the master data and the projects as they are then, so the approval applies only what still holds; all of it, in one
/// transaction, or none. The projects it creates enter in their mapped state as in-flight intake (ADR-014): ACTIVE, SUSPENDED, COMPLETED or
/// CLOSED with an activation stamp, APPROVED_PLANNED without, each with its Formal Project ID; no registration approval runs again — the
/// batch's approval covers them. Imported progress and spend are stored as declared, source IMPORT.
/// </summary>
internal sealed class ProjectImportService(
    IProjectImportWorkbook workbook,
    IProjectImportRepository batches,
    IProjectRepository projects,
    IMasterDataResolver masterData,
    IOrganizationDirectory organizations,
    IUserLookup users,
    IRoleDirectory roles,
    IRoleHolderDirectory holders,
    IAuthorizationEngine engine,
    IAuditTrail audit,
    IOutbox outbox,
    TimeProvider timeProvider) : IProjectImportService, IProjectImportValidationRunner
{
    private static readonly JsonSerializerOptions PlanJson = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseUpper) },
    };

    public async Task<AdministrationResult<byte[]>> TemplateAsync(Guid callerId, CancellationToken cancellationToken)
    {
        if ((await engine.GetRecordScopeAsync(callerId, PermissionCatalogue.ProjectImport, cancellationToken).ConfigureAwait(false)).IsEmpty)
        {
            return AdministrationError.Forbidden;
        }

        ProjectImportReferences references = await ReferencesAsync(ProjectImportPlan.Empty, cancellationToken).ConfigureAwait(false);
        Dictionary<string, IReadOnlyList<string>> lists = new(StringComparer.Ordinal)
        {
            [ProjectImportLayout.Entity] = Labels(references, ProjectImportDimension.Entity),
            [ProjectImportLayout.Department] = Labels(references, ProjectImportDimension.Department),
            [ProjectImportLayout.Sector] = Labels(references, ProjectImportDimension.Sector),
            [ProjectImportLayout.Portfolio] = CodedLabels(references, MasterDataCatalogueCodes.StrategicPortfolio),
            [ProjectImportLayout.Initiative] = CodedLabels(references, MasterDataCatalogueCodes.StrategicInitiative),
            [ProjectImportLayout.Status] = Labels(references, ProjectImportDimension.Status),
            [ProjectImportLayout.Governance] = Labels(references, ProjectImportDimension.Governance),
            [ProjectImportLayout.Priority] = Labels(references, ProjectImportDimension.Priority),
            [ProjectImportLayout.Funding] = ["نعم", "جزئي", "لا"],
            [ProjectImportLayout.Region] = Labels(references, ProjectImportDimension.Region),
            [ProjectImportLayout.City] = Labels(references, ProjectImportDimension.City),
            [ProjectImportLayout.EntityCommits] = ["نعم", "لا"],
        };
        return workbook.WriteTemplate(new ProjectImportTemplate(
            [.. ProjectImportLayout.Columns.Select(c => new ProjectImportTemplateColumn(c.Column.Key, c.HeaderAr, c.Column.Required))],
            lists,
            ProjectImportLayout.TemplateVersion,
            ProjectImportLayout.MaxRows));
    }

    public async Task<AdministrationResult<ProjectImportValidationOutcome>> ValidateAsync(
        Guid callerId, ProjectImportUpload upload, ProjectImportPlan plan, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(upload);
        ArgumentNullException.ThrowIfNull(plan);

        RecordScope scope = await engine.GetRecordScopeAsync(callerId, PermissionCatalogue.ProjectImport, cancellationToken).ConfigureAwait(false);
        if (scope.IsEmpty)
        {
            return AdministrationError.Forbidden;
        }

        // Review §4, step 3: a workbook of more than 200 rows is validated in the background, and its importer notified when it is done.
        if (RowsOf(upload.Content) is { } rows && rows > ProjectImportLayout.BackgroundRows)
        {
            DateTimeOffset now = timeProvider.GetUtcNow();
            ProjectImportValidation queued = new()
            {
                Id = Guid.CreateVersion7(now),
                Status = ProjectImportValidationStatus.Queued,
                FileName = Path.GetFileName(upload.FileName),
                FileContent = upload.Content,
                Plan = JsonSerializer.Serialize(plan, PlanJson),
                RowCount = rows,
                CreatedAt = now,
                CreatedBy = callerId,
                UpdatedAt = now,
                UpdatedBy = callerId,
            };
            batches.Add(queued);
            return await batches.SaveAsync(cancellationToken).ConfigureAwait(false)
                ? new ProjectImportValidationOutcome(null, ValidationDetailOf(queued))
                : AdministrationError.PreconditionFailed;
        }

        AdministrationResult<ProjectImportEvaluation> evaluation = await EvaluateAsync(upload.Content, plan, scope, cancellationToken).ConfigureAwait(false);
        return evaluation.Succeeded ? new ProjectImportValidationOutcome(evaluation.Value.Preview, null) : evaluation.Error;
    }

    public async Task<AdministrationResult<ProjectImportValidationDetail>> GetValidationAsync(Guid callerId, Guid validationId, CancellationToken cancellationToken)
    {
        ProjectImportValidation? validation = await batches.FindValidationAsync(validationId, null, cancellationToken).ConfigureAwait(false);
        return validation is null || validation.CreatedBy != callerId ? AdministrationError.NotFound : ValidationDetailOf(validation);
    }

    public async Task<int> RunAsync(int batchSize, CancellationToken cancellationToken)
    {
        int done = 0;
        foreach ((Guid id, uint version) in await batches.ListQueuedValidationsAsync(batchSize, cancellationToken).ConfigureAwait(false))
        {
            batches.Clear();

            // Claimed by saving it RUNNING unchanged since read, so two passes never validate one workbook twice.
            ProjectImportValidation? validation = await batches.FindValidationAsync(id, version, cancellationToken).ConfigureAwait(false);
            if (validation is not { Status: ProjectImportValidationStatus.Queued })
            {
                continue;
            }

            validation.Status = ProjectImportValidationStatus.Running;
            validation.UpdatedAt = timeProvider.GetUtcNow();
            if (!await batches.SaveAsync(cancellationToken).ConfigureAwait(false))
            {
                continue;
            }

            RecordScope scope = await engine.GetRecordScopeAsync(validation.CreatedBy, PermissionCatalogue.ProjectImport, cancellationToken).ConfigureAwait(false);
            AdministrationResult<ProjectImportEvaluation> evaluated = scope.IsEmpty
                ? AdministrationError.Forbidden
                : await EvaluateAsync(validation.FileContent, PlanOf(validation.Plan), scope, cancellationToken).ConfigureAwait(false);
            DateTimeOffset now = timeProvider.GetUtcNow();
            validation.Status = evaluated.Succeeded ? ProjectImportValidationStatus.Completed : ProjectImportValidationStatus.Failed;
            validation.Result = evaluated.Succeeded ? JsonSerializer.Serialize(evaluated.Value.Preview, PlanJson) : null;
            validation.FailureCode = evaluated.Succeeded ? null : evaluated.Error.Code ?? ProjectImportErrorCodes.NotWorkbook;
            validation.FileContent = [];
            validation.CompletedAt = now;
            validation.UpdatedAt = now;
            outbox.Stage(ImportNotifications.Validated(
                validation.Id, validation.CreatedBy, scope.Clauses.Count > 0 ? scope.Clauses[0].DepartmentId : null, $"/projects/import/validations/{validation.Id:D}",
                [
                    new NotificationParameter("fileName", validation.FileName),
                    new NotificationParameter("rowCount", validation.RowCount.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                    new NotificationParameter("outcome", validation.Status == ProjectImportValidationStatus.Completed ? "COMPLETED" : "FAILED"),
                ],
                now));
            if (await batches.SaveAsync(cancellationToken).ConfigureAwait(false))
            {
                done++;
            }
        }

        return done;
    }

    /// <summary>The rows after the key row, or null when the file is not a workbook the import reads (the validation then says so).</summary>
    private int? RowsOf(byte[] content)
    {
        if (content.LongLength > ProjectImportLayout.MaxBytes)
        {
            return null;
        }

        using MemoryStream stream = new(content, writable: false);
        return workbook.Read(stream, ProjectImportLayout.MaxRows + 10) is { } sheet && ProjectImportEvaluator.KeysOf(sheet) is { } keys
            ? sheet.Rows.Count(r => r.RowNumber > keys.KeyRow)
            : null;
    }

    private static ProjectImportValidationDetail ValidationDetailOf(ProjectImportValidation v) => new(
        v.Id, v.Status, v.FileName, v.RowCount, v.CreatedAt, v.CompletedAt, v.FailureCode,
        v.Result is null ? null : JsonSerializer.Deserialize<ProjectImportPreview>(v.Result, PlanJson));

    public async Task<AdministrationResult<ProjectImportBatchDetail>> SubmitAsync(
        Guid callerId, ProjectImportUpload upload, ProjectImportPlan plan, NarrativeText? note, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(upload);
        ArgumentNullException.ThrowIfNull(plan);

        RecordScope scope = await engine.GetRecordScopeAsync(callerId, PermissionCatalogue.ProjectImport, cancellationToken).ConfigureAwait(false);
        if (scope.IsEmpty)
        {
            return AdministrationError.Forbidden;
        }

        AdministrationResult<ProjectImportEvaluation> evaluated = await EvaluateAsync(upload.Content, plan, scope, cancellationToken).ConfigureAwait(false);
        if (!evaluated.Succeeded)
        {
            return evaluated.Error;
        }

        ProjectImportPreview preview = evaluated.Value.Preview;
        if (!preview.Submittable)
        {
            return AdministrationError.Rule(ProjectImportErrorCodes.NotSubmittable);
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        ProjectImportBatch batch = new()
        {
            Id = Guid.CreateVersion7(now),
            BatchNo = await batches.NextBatchNoAsync(cancellationToken).ConfigureAwait(false),
            Status = ProjectImportBatchStatus.Submitted,
            FileName = Path.GetFileName(upload.FileName),
            FileSha256 = preview.FileSha256,
            FileContent = upload.Content,
            SubmissionNote = note,
            Plan = JsonSerializer.Serialize(plan, PlanJson),
            RowCount = preview.RowCount,
            CreateCount = preview.CreateCount,
            UpdateCount = preview.UpdateCount,
            SkipCount = preview.SkipCount,
            WarningCount = preview.WarningCount,
            CreatedAt = now,
            CreatedBy = callerId,
            UpdatedAt = now,
            UpdatedBy = callerId,
        };
        batches.Add(batch);
        await RememberAsync(plan, callerId, now, cancellationToken).ConfigureAwait(false);
        audit.Stage(BatchEntry(AuditEventClass.DataChange, ProjectAuditEvents.ImportBatchSubmitted, callerId, batch,
        [
            AuditAttribute.Of(ProjectAuditAttributes.FileSha256, batch.FileSha256),
            AuditAttribute.Of(ProjectAuditAttributes.RowCount, batch.RowCount),
            AuditAttribute.Of(ProjectAuditAttributes.CreateCount, batch.CreateCount),
            AuditAttribute.Of(ProjectAuditAttributes.UpdateCount, batch.UpdateCount),
            AuditAttribute.Of(ProjectAuditAttributes.SkipCount, batch.SkipCount),
        ]));
        return await batches.SaveAsync(cancellationToken).ConfigureAwait(false)
            ? await DetailAsync(callerId, batch, cancellationToken).ConfigureAwait(false)
            : AdministrationError.PreconditionFailed;
    }

    public async Task<ProjectImportBatchPage> ListAsync(Guid callerId, ProjectImportBatchQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (!await MayReadAsync(callerId, cancellationToken).ConfigureAwait(false))
        {
            return new ProjectImportBatchPage([], query.Page, query.PageSize, 0);
        }

        (IReadOnlyList<ProjectImportBatch> items, int total) = await batches.ListAsync(query.Status, query.Page, query.PageSize, cancellationToken).ConfigureAwait(false);
        List<ProjectImportBatchDetail> details = [];
        foreach (ProjectImportBatch batch in items)
        {
            details.Add(await DetailAsync(callerId, batch, cancellationToken).ConfigureAwait(false));
        }

        return new ProjectImportBatchPage(details, query.Page, query.PageSize, total);
    }

    public async Task<AdministrationResult<Versioned<ProjectImportBatchDetail>>> GetAsync(Guid callerId, Guid batchId, CancellationToken cancellationToken)
    {
        ProjectImportBatch? batch = await batches.FindAsync(batchId, null, cancellationToken).ConfigureAwait(false);
        return batch is null || !await MayReadAsync(callerId, cancellationToken).ConfigureAwait(false)
            ? AdministrationError.NotFound
            : await VersionedAsync(callerId, batch, cancellationToken).ConfigureAwait(false);
    }

    public async Task<AdministrationResult<ProjectImportPreview>> PreviewAsync(Guid callerId, Guid batchId, CancellationToken cancellationToken)
    {
        ProjectImportBatch? batch = await batches.FindAsync(batchId, null, cancellationToken).ConfigureAwait(false);
        if (batch is null || !await MayReadAsync(callerId, cancellationToken).ConfigureAwait(false))
        {
            return AdministrationError.NotFound;
        }

        AdministrationResult<ProjectImportEvaluation> evaluated = await EvaluateAsync(
            batch.FileContent, PlanOf(batch), await ImporterScopeAsync(batch, cancellationToken).ConfigureAwait(false), cancellationToken).ConfigureAwait(false);
        return evaluated.Succeeded ? evaluated.Value.Preview : evaluated.Error;
    }

    public async Task<AdministrationResult<ProjectImportFile>> FileAsync(Guid callerId, Guid batchId, CancellationToken cancellationToken)
    {
        ProjectImportBatch? batch = await batches.FindAsync(batchId, null, cancellationToken).ConfigureAwait(false);
        return batch is null || !await MayReadAsync(callerId, cancellationToken).ConfigureAwait(false)
            ? AdministrationError.NotFound
            : new ProjectImportFile(batch.FileName, batch.FileContent);
    }

    public async Task<AdministrationResult<Versioned<ProjectImportBatchDetail>>> ApproveAsync(
        Guid callerId, Guid batchId, uint? expectedVersion, CancellationToken cancellationToken)
    {
        ProjectImportBatch? batch = await batches.FindAsync(batchId, expectedVersion, cancellationToken).ConfigureAwait(false);
        if (await DecisionRefusedAsync(callerId, batch, cancellationToken).ConfigureAwait(false) is { } refused)
        {
            return refused;
        }

        // Validated again as the importer's: the approval applies what the importer could import, now.
        RecordScope importerScope = await ImporterScopeAsync(batch!, cancellationToken).ConfigureAwait(false);
        RecordScope approverScope = await engine.GetRecordScopeAsync(callerId, PermissionCatalogue.ProjectImportApprove, cancellationToken).ConfigureAwait(false);
        AdministrationResult<ProjectImportEvaluation> evaluated = await EvaluateAsync(batch!.FileContent, PlanOf(batch), importerScope, cancellationToken).ConfigureAwait(false);
        if (!evaluated.Succeeded)
        {
            return evaluated.Error;
        }

        if (!evaluated.Value.Preview.Submittable)
        {
            return AdministrationError.Rule(ProjectImportErrorCodes.NotSubmittable);
        }

        IReadOnlyList<ResolvedRow> rows = evaluated.Value.Rows;
        if (rows.Any(r => !approverScope.Matches(new AuthorizationSubject { DepartmentId = r.DepartmentId, ExternalEntityId = r.ExternalEntityId })))
        {
            return AdministrationError.Forbidden;
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        await using IAsyncDisposable work = await batches.BeginAsync(cancellationToken).ConfigureAwait(false);
        int created = 0;
        int updated = 0;
        List<Guid> toUpdate = [.. rows.Where(r => r.Action == ProjectImportRowAction.Update).Select(r => r.Existing!.Id)];
        Dictionary<Guid, ProjectEntity> tracked = (await projects.FindManyAsync(toUpdate, cancellationToken).ConfigureAwait(false)).ToDictionary(p => p.Id);
        foreach (ResolvedRow row in rows)
        {
            if (row.Action == ProjectImportRowAction.Create)
            {
                await CreateAsync(row, batch, now, cancellationToken).ConfigureAwait(false);
                created++;
            }
            else if (row.Action == ProjectImportRowAction.Update && tracked.TryGetValue(row.Existing!.Id, out ProjectEntity? project))
            {
                Update(row, project, batch, callerId, now);
                updated++;
            }
        }

        batch.Status = ProjectImportBatchStatus.Approved;
        batch.DecidedByUserId = callerId;
        batch.DecidedAt = now;
        batch.CreateCount = created;
        batch.UpdateCount = updated;
        batch.SkipCount = batch.RowCount - created - updated;
        batch.UpdatedAt = now;
        batch.UpdatedBy = callerId;
        audit.Stage(BatchEntry(AuditEventClass.LifecycleTransition, ProjectAuditEvents.ImportBatchApproved, callerId, batch,
        [
            AuditAttribute.Change(ProjectAuditAttributes.Status, ProjectImportBatchStatus.Submitted, batch.Status)!,
            AuditAttribute.Of(ProjectAuditAttributes.CreateCount, created),
            AuditAttribute.Of(ProjectAuditAttributes.UpdateCount, updated),
            AuditAttribute.Of(ProjectAuditAttributes.SkipCount, batch.SkipCount),
        ]));
        if (!await batches.SaveAsync(cancellationToken).ConfigureAwait(false))
        {
            return AdministrationError.PreconditionFailed;
        }

        await batches.CommitAsync(cancellationToken).ConfigureAwait(false);
        return await VersionedAsync(callerId, batch, cancellationToken).ConfigureAwait(false);
    }

    public async Task<AdministrationResult<Versioned<ProjectImportBatchDetail>>> ReturnAsync(
        Guid callerId, Guid batchId, NarrativeText reason, uint? expectedVersion, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(reason);
        ProjectImportBatch? batch = await batches.FindAsync(batchId, expectedVersion, cancellationToken).ConfigureAwait(false);
        if (await DecisionRefusedAsync(callerId, batch, cancellationToken).ConfigureAwait(false) is { } refused)
        {
            return refused;
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        batch!.Status = ProjectImportBatchStatus.Returned;
        batch.DecidedByUserId = callerId;
        batch.DecidedAt = now;
        batch.ReturnReason = reason;
        batch.UpdatedAt = now;
        batch.UpdatedBy = callerId;
        audit.Stage(BatchEntry(AuditEventClass.LifecycleTransition, ProjectAuditEvents.ImportBatchReturned, callerId, batch,
            [AuditAttribute.Change(ProjectAuditAttributes.Status, ProjectImportBatchStatus.Submitted, batch.Status)!]));
        return await batches.SaveAsync(cancellationToken).ConfigureAwait(false)
            ? await VersionedAsync(callerId, batch, cancellationToken).ConfigureAwait(false)
            : AdministrationError.PreconditionFailed;
    }

    public async Task<AdministrationResult<Versioned<ProjectImportBatchDetail>>> RollBackAsync(
        Guid callerId, Guid batchId, uint? expectedVersion, CancellationToken cancellationToken)
    {
        ProjectImportBatch? batch = await batches.FindAsync(batchId, expectedVersion, cancellationToken).ConfigureAwait(false);
        if (batch is null || !await MayReadAsync(callerId, cancellationToken).ConfigureAwait(false))
        {
            return AdministrationError.NotFound;
        }

        if ((await engine.GetRecordScopeAsync(callerId, PermissionCatalogue.ProjectImportApprove, cancellationToken).ConfigureAwait(false)).IsEmpty)
        {
            return AdministrationError.Forbidden;
        }

        if (batch.Status != ProjectImportBatchStatus.Approved)
        {
            return AdministrationError.Conflict(ProjectImportErrorCodes.InvalidState);
        }

        IReadOnlyList<ProjectEntity> created = await projects.ListCreatedByImportAsync(batch.Id, cancellationToken).ConfigureAwait(false);
        if (!Untouched(batch, created))
        {
            return AdministrationError.Conflict(ProjectImportErrorCodes.RollbackRefused);
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        await using IAsyncDisposable work = await batches.BeginAsync(cancellationToken).ConfigureAwait(false);

        // The project guard deletes a non-DRAFT project only when its batch is ROLLED_BACK, so the batch's state is saved first.
        batch.Status = ProjectImportBatchStatus.RolledBack;
        batch.RolledBackByUserId = callerId;
        batch.RolledBackAt = now;
        batch.UpdatedAt = now;
        batch.UpdatedBy = callerId;
        audit.Stage(BatchEntry(AuditEventClass.LifecycleTransition, ProjectAuditEvents.ImportBatchRolledBack, callerId, batch,
        [
            AuditAttribute.Change(ProjectAuditAttributes.Status, ProjectImportBatchStatus.Approved, batch.Status)!,
            AuditAttribute.Of(ProjectAuditAttributes.RemovedCount, created.Count),
        ]));
        if (!await batches.SaveAsync(cancellationToken).ConfigureAwait(false))
        {
            return AdministrationError.PreconditionFailed;
        }

        await projects.RemoveFollowUpNotesAsync([.. created.Select(p => p.Id)], cancellationToken).ConfigureAwait(false);
        foreach (ProjectEntity project in created)
        {
            projects.Remove(project);
        }

        switch (await projects.SaveAsync(cancellationToken).ConfigureAwait(false))
        {
            case ProjectSaveOutcome.Saved:
                break;
            case ProjectSaveOutcome.InUse:
                // A record of another module refers to one of the projects: it has been used, so the batch stays.
                return AdministrationError.Conflict(ProjectImportErrorCodes.RollbackRefused);
            case ProjectSaveOutcome.ConcurrencyConflict:
            default:
                return AdministrationError.PreconditionFailed;
        }

        await batches.CommitAsync(cancellationToken).ConfigureAwait(false);
        ProjectImportBatch? saved = await batches.FindAsync(batch.Id, null, cancellationToken).ConfigureAwait(false);
        return await VersionedAsync(callerId, saved!, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>A batch is rolled back only while it updated no existing project and none it created has changed since it was applied.</summary>
    private static bool Untouched(ProjectImportBatch batch, IReadOnlyList<ProjectEntity> created) =>
        batch.UpdateCount == 0 && created.All(p => p.UpdatedAt <= batch.DecidedAt && p.UpdatedBy == batch.CreatedBy);

    private async Task CreateAsync(ResolvedRow row, ProjectImportBatch batch, DateTimeOffset now, CancellationToken cancellationToken)
    {
        bool started = row.Status != ProjectLifecycleState.ApprovedPlanned;
        bool declared = row.PlannedPercent is not null || row.ActualPercent is not null || row.PlannedSpendPercent is not null || row.ActualSpendPercent is not null;
        ProjectEntity project = new()
        {
            Id = Guid.CreateVersion7(now),
            FormalProjectId = ProjectApprovalOutcomeHandler.FormalProjectIdOf(await projects.NextFormalProjectNumberAsync(cancellationToken).ConfigureAwait(false)),
            Title = row.Title,
            Description = row.Description,
            ClassificationItemId = row.ClassificationItemId,
            DepartmentId = row.DepartmentId,
            ExternalEntityId = row.ExternalEntityId,
            ProjectManagerUserId = row.ProjectManagerUserId,
            LifecycleState = row.Status,
            RevisionNo = 1,
            GovernanceProfileItemId = row.GovernanceProfileItemId,
            ParticipationMode = ParticipationMode.AhdaManaged,
            RegistrationBudgetSar = row.Budget,
            PlannedStartDate = row.Start,
            PlannedEndDate = row.End,
            RegionItemId = row.RegionItemId,
            CityItemId = row.CityItemId,
            ActivatedAt = started ? now : null,
            ClosedAt = row.Status == ProjectLifecycleState.Closed ? now : null,
            StrategicPortfolioItemId = row.StrategicPortfolioItemId,
            StrategicInitiativeItemId = row.StrategicInitiativeItemId,
            PriorityItemId = row.PriorityItemId,
            FundingSource = row.Funding,
            LegacyReference = row.LegacyReference,
            FollowUpOwnerUserId = row.FollowUpOwnerUserId,
            ProjectImportBatchId = batch.Id,
            CurrentObstacle = row.Obstacle,
            NextAction = row.NextAction,
            NextActionOwner = row.ActionOwner,
            NextActionDueDate = row.ActionDue,
            DeclaredPlannedPercent = row.PlannedPercent,
            DeclaredActualPercent = row.ActualPercent,
            DeclaredPlannedSpendPercent = row.PlannedSpendPercent,
            DeclaredActualSpendPercent = row.ActualSpendPercent,
            DeclaredAsOf = declared ? DateOnly.FromDateTime(batch.CreatedAt.UtcDateTime) : null,
            DeclaredSource = declared ? DeclaredValueSource.Import : null,
            EntityUpdateCompliant = row.EntityCompliant,

            // The importer created it; the batch's approval is what applied it.
            CreatedAt = now,
            CreatedBy = batch.CreatedBy,
            UpdatedAt = now,
            UpdatedBy = batch.CreatedBy,
        };
        projects.Add(project);
        if (row.Note is { } note)
        {
            projects.Add(new ProjectFollowUpNote
            {
                Id = Guid.CreateVersion7(now),
                ProjectId = project.Id,
                Note = note,
                CreatedAt = now,
                CreatedBy = batch.CreatedBy,
                UpdatedAt = now,
                UpdatedBy = batch.CreatedBy,
            });
        }
    }

    /// <summary>What an import may change on a project already registered: its strategy classification and follow-up, never a governed field.</summary>
    private void Update(ResolvedRow row, ProjectEntity project, ProjectImportBatch batch, Guid approverId, DateTimeOffset now)
    {
        ProjectDraft before = ProjectMapping.ToDraft(project);
        ProjectFollowUpDetail followUpBefore = ProjectMapping.ToFollowUp(project);
        project.StrategicPortfolioItemId = row.StrategicPortfolioItemId;
        project.StrategicInitiativeItemId = row.StrategicInitiativeItemId;
        project.PriorityItemId = row.PriorityItemId;
        project.FundingSource = row.Funding;
        project.FollowUpOwnerUserId = row.FollowUpOwnerUserId;
        project.CurrentObstacle = row.Obstacle;
        project.NextAction = row.NextAction;
        project.NextActionOwner = row.ActionOwner;
        project.NextActionDueDate = row.ActionDue;
        if ((project.DeclaredPlannedPercent, project.DeclaredActualPercent, project.DeclaredPlannedSpendPercent, project.DeclaredActualSpendPercent)
            != (row.PlannedPercent, row.ActualPercent, row.PlannedSpendPercent, row.ActualSpendPercent))
        {
            project.DeclaredPlannedPercent = row.PlannedPercent;
            project.DeclaredActualPercent = row.ActualPercent;
            project.DeclaredPlannedSpendPercent = row.PlannedSpendPercent;
            project.DeclaredActualSpendPercent = row.ActualSpendPercent;
            project.DeclaredAsOf = DateOnly.FromDateTime(batch.CreatedAt.UtcDateTime);
            project.DeclaredSource = DeclaredValueSource.Import;
        }

        project.EntityUpdateCompliant = row.EntityCompliant;
        project.UpdatedAt = now;
        project.UpdatedBy = approverId;
        audit.Stage(ProjectAudit.UpdatedByImport(approverId, before, followUpBefore, project, batch.Id));
    }

    private async Task<AdministrationResult<ProjectImportEvaluation>> EvaluateAsync(
        byte[] content, ProjectImportPlan plan, RecordScope scope, CancellationToken cancellationToken)
    {
        if (content.LongLength > ProjectImportLayout.MaxBytes)
        {
            return AdministrationError.Rule(ProjectImportErrorCodes.TooLarge);
        }

        using MemoryStream stream = new(content, writable: false);
        if (workbook.Read(stream, ProjectImportLayout.MaxRows + 10) is not { } sheet || ProjectImportEvaluator.KeysOf(sheet) is not { } keys)
        {
            return AdministrationError.Rule(ProjectImportErrorCodes.NotWorkbook);
        }

        if (sheet.Rows.Count(r => r.RowNumber > keys.KeyRow) > ProjectImportLayout.MaxRows)
        {
            return AdministrationError.Rule(ProjectImportErrorCodes.TooLarge);
        }

        ProjectImportReferences references = await ReferencesAsync(plan, cancellationToken).ConfigureAwait(false);
        HashSet<string> legacy = new(StringComparer.Ordinal);
        int legacyIndex = keys.Keys.FirstOrDefault(k => k.Value == ProjectImportLayout.LegacyRef).Key;
        foreach (ImportSheetRow row in sheet.Rows.Where(r => r.RowNumber > keys.KeyRow))
        {
            if (row.Cells.TryGetValue(legacyIndex, out string? value) && !string.IsNullOrWhiteSpace(value))
            {
                legacy.Add(value.Trim());
            }
        }

        RoleSummary projectManager = (await roles.ListRolesAsync(cancellationToken).ConfigureAwait(false)).Single(r => r.Code == ProjectManagerEligibility.RoleCode);
        // R04 holders over each department, as the Project Manager of a new project there must be (ProjectManagerEligibility).
        Dictionary<Guid, HashSet<Guid>> managers = [];
        foreach (ProjectImportOption department in references.Options(ProjectImportDimension.Department))
        {
            Guid departmentId = Guid.Parse(department.Value);
            managers[departmentId] = [.. (await holders.FindHoldersAsync([projectManager.Id], new RoleHolderScope(null, departmentId, null), cancellationToken)
                .ConfigureAwait(false)).Select(h => h.UserId)];
        }

        IReadOnlyList<ProjectEntity> reachable = await projects.ListReachedAsync(scope, cancellationToken).ConfigureAwait(false);
        ProjectImportEvaluator.Context context = new(
            references,
            (department, entity) => scope.Matches(new AuthorizationSubject { DepartmentId = department, ExternalEntityId = entity }),
            (user, department) => department is { } d && managers.TryGetValue(d, out HashSet<Guid>? holding) && holding.Contains(user),
            await projects.ListByLegacyReferencesAsync(legacy, cancellationToken).ConfigureAwait(false),
            await projects.ListByFormalProjectIdsAsync(legacy, cancellationToken).ConfigureAwait(false),
            [.. reachable.Select(p => (ImportNames.Key(p.Title.Text), p.ExternalEntityId))],
            plan,
            DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime));
        string sha256 = Convert.ToHexStringLower(SHA256.HashData(content));
        return ProjectImportEvaluator.Evaluate(sheet, sha256, context);
    }

    private async Task<ProjectImportReferences> ReferencesAsync(ProjectImportPlan plan, CancellationToken cancellationToken) =>
        await ProjectImportReferences.LoadAsync(
            masterData, organizations, users, await batches.ListMappingsAsync(cancellationToken).ConfigureAwait(false), plan, cancellationToken).ConfigureAwait(false);

    /// <summary>The submitted mappings, remembered for the next import; a name mapped otherwise before takes the new value.</summary>
    private async Task RememberAsync(ProjectImportPlan plan, Guid actorId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        IReadOnlyList<ProjectImportNameMapping> known = await batches.ListMappingsAsync(cancellationToken).ConfigureAwait(false);
        foreach (ProjectImportMapping mapping in plan.Mappings)
        {
            string dimension = ProjectImportReferences.DimensionCode(mapping.Dimension);
            string key = ImportNames.Key(mapping.SourceText);
            if (known.FirstOrDefault(m => m.Dimension == dimension && m.SourceKey == key) is { } existing)
            {
                if (existing.TargetValue != mapping.TargetValue)
                {
                    existing.TargetValue = mapping.TargetValue;
                    existing.UpdatedAt = now;
                    existing.UpdatedBy = actorId;
                }

                continue;
            }

            batches.Add(new ProjectImportNameMapping
            {
                Id = Guid.CreateVersion7(now),
                Dimension = dimension,
                SourceKey = key,
                TargetValue = mapping.TargetValue,
                CreatedAt = now,
                CreatedBy = actorId,
                UpdatedAt = now,
                UpdatedBy = actorId,
            });
        }
    }

    private async Task<AdministrationError?> DecisionRefusedAsync(Guid callerId, ProjectImportBatch? batch, CancellationToken cancellationToken)
    {
        return batch is null || !await MayReadAsync(callerId, cancellationToken).ConfigureAwait(false) ? AdministrationError.NotFound
            : (await engine.GetRecordScopeAsync(callerId, PermissionCatalogue.ProjectImportApprove, cancellationToken).ConfigureAwait(false)).IsEmpty
            ? AdministrationError.Forbidden
            : batch.CreatedBy == callerId ? AdministrationError.Rule(ProjectImportErrorCodes.OwnBatch)
            : batch.Status == ProjectImportBatchStatus.Submitted ? null
            : AdministrationError.Conflict(ProjectImportErrorCodes.InvalidState);
    }

    /// <summary>Anyone who prepares or decides batches sees them all: a batch has no department until it is read.</summary>
    private async Task<bool> MayReadAsync(Guid callerId, CancellationToken cancellationToken) =>
        !(await engine.GetRecordScopeAsync(callerId, PermissionCatalogue.ProjectImport, cancellationToken).ConfigureAwait(false)).IsEmpty
        || !(await engine.GetRecordScopeAsync(callerId, PermissionCatalogue.ProjectImportApprove, cancellationToken).ConfigureAwait(false)).IsEmpty;

    private Task<RecordScope> ImporterScopeAsync(ProjectImportBatch batch, CancellationToken cancellationToken) =>
        engine.GetRecordScopeAsync(batch.CreatedBy, PermissionCatalogue.ProjectImport, cancellationToken);

    private static ProjectImportPlan PlanOf(ProjectImportBatch batch) => PlanOf(batch.Plan);

    private static ProjectImportPlan PlanOf(string json) => JsonSerializer.Deserialize<ProjectImportPlan>(json, PlanJson) ?? ProjectImportPlan.Empty;

    private async Task<Versioned<ProjectImportBatchDetail>> VersionedAsync(Guid callerId, ProjectImportBatch batch, CancellationToken cancellationToken) =>
        new(await DetailAsync(callerId, batch, cancellationToken).ConfigureAwait(false), batches.RowVersionOf(batch));

    private async Task<ProjectImportBatchDetail> DetailAsync(Guid callerId, ProjectImportBatch b, CancellationToken cancellationToken)
    {
        bool approver = !(await engine.GetRecordScopeAsync(callerId, PermissionCatalogue.ProjectImportApprove, cancellationToken).ConfigureAwait(false)).IsEmpty;
        return new ProjectImportBatchDetail(
            b.Id, b.BatchNo, b.Status, b.FileName, b.FileSha256, b.SubmissionNote, b.RowCount, b.CreateCount, b.UpdateCount, b.SkipCount, b.WarningCount,
            b.CreatedBy, b.CreatedAt, b.DecidedByUserId, b.DecidedAt, b.ReturnReason, b.RolledBackByUserId, b.RolledBackAt,
            MayDecide: approver && b.Status == ProjectImportBatchStatus.Submitted && b.CreatedBy != callerId,
            MayRollBack: approver && b.Status == ProjectImportBatchStatus.Approved && b.UpdateCount == 0);
    }

    private static AuditEntry BatchEntry(AuditEventClass eventClass, string eventType, Guid actorId, ProjectImportBatch batch, IEnumerable<AuditAttribute> attributes) =>
        new(eventClass, eventType, AuditOutcome.Success)
        {
            ActorUserId = actorId,
            Subject = new AuditSubject("Project", nameof(ProjectImportBatch), batch.Id),
            Attributes = [AuditAttribute.Of(ProjectAuditAttributes.BatchNo, batch.BatchNo), .. attributes],
        };

    private static List<string> Labels(ProjectImportReferences references, ProjectImportDimension dimension) =>
        [.. references.Options(dimension).Select(o => o.Label.Ar)];

    /// <summary>A portfolio or initiative as RPMO writes it: "PF-02 — وجهة ثقافية".</summary>
    private static List<string> CodedLabels(ProjectImportReferences references, string catalogueCode) =>
        [.. references.Items.Values.Where(i => i.CatalogueCode == catalogueCode).OrderBy(i => i.SortOrder).ThenBy(i => i.Code, StringComparer.Ordinal)
            .Select(i => $"{i.Code} — {i.Label.Ar}")];
}
