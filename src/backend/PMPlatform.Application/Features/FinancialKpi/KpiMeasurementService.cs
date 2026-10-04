using PMPlatform.Application.Common.Auditing;
using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Features.FinancialKpi.Contracts;
using PMPlatform.Application.Features.FinancialKpi.Contracts.Events;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.MasterDataConfig.Contracts;
using PMPlatform.Application.Features.Project.Contracts;
using PMPlatform.Domain.FinancialKpi;

namespace PMPlatform.Application.Features.FinancialKpi;

/// <summary>
/// An assignment's measurements (TASK-052). Recording one pins the assignment's ACTIVE target version and rates the value against
/// it, by the KPI's direction. A DRAFT is re-rated against the same pinned version when its value changes; nothing ever re-pins
/// it, so a later target version leaves every earlier measurement — its target and its RAG — as recorded. AHDA publishes.
/// </summary>
internal sealed class KpiMeasurementService(
    IFinancialKpiRepository repository,
    KpiScope scope,
    KpiDefinitions definitions,
    FinancialKpiAccess access,
    IAuditTrail audit,
    TimeProvider timeProvider) : IKpiMeasurementService
{
    public async Task<KpiMeasurementPage> ListAsync(Guid callerId, Guid assignmentId, PageRequest page, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(page);
        if (await repository.FindAssignmentAsync(assignmentId, null, cancellationToken).ConfigureAwait(false) is not { } assignment
            || !await scope.CanAsync(callerId, PermissionCatalogue.KpiView, assignmentId, cancellationToken).ConfigureAwait(false))
        {
            return new KpiMeasurementPage([], page.Page, page.PageSize, 0);
        }

        (IReadOnlyList<KpiMeasurement> items, int total) = await repository.PageMeasurementsAsync(assignmentId, page, cancellationToken).ConfigureAwait(false);
        IReadOnlyList<KpiTargetVersion> pinned = await repository.ListTargetsByIdAsync([.. items.Select(m => m.KpiTargetVersionId).Distinct()], cancellationToken).ConfigureAwait(false);
        FieldMask mask = await MaskAsync(callerId, cancellationToken).ConfigureAwait(false);
        return new KpiMeasurementPage(
            [.. items.Select(m => FinancialKpiMapping.ToDetail(m, assignment.ProjectId, pinned.Single(t => t.Id == m.KpiTargetVersionId), mask))], page.Page, page.PageSize, total);
    }

    public async Task<AdministrationResult<Versioned<KpiMeasurementDetail>>> GetAsync(Guid callerId, Guid measurementId, CancellationToken cancellationToken)
    {
        Loaded loaded = await LoadAsync(callerId, PermissionCatalogue.KpiView, measurementId, null, cancellationToken).ConfigureAwait(false);
        return loaded.Error is { } refused ? refused : await VersionedAsync(callerId, loaded.Project!, loaded.Measurement!, cancellationToken).ConfigureAwait(false);
    }

    public async Task<AdministrationResult<Versioned<KpiMeasurementDetail>>> CreateAsync(Guid callerId, KpiMeasurementDraft draft, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(draft);
        KpiScope.Scoped scoped = await scope.LoadAsync(callerId, PermissionCatalogue.KpiRecord, draft.KpiAssignmentId, cancellationToken).ConfigureAwait(false);
        if (scoped.Error is { } refused)
        {
            return refused;
        }

        (ProjectFacts project, KpiAssignment assignment) = (scoped.Project!, scoped.Assignment!);
        DateTimeOffset now = timeProvider.GetUtcNow();
        if ((ProjectEligibility.ReportingRefused(project)
             ?? (assignment.Status != KpiAssignmentStatus.Active ? AdministrationError.Rule(FinancialKpiErrorCodes.AssignmentNotActive) : null)
             ?? (draft.PeriodEnd < draft.PeriodStart ? AdministrationError.Rule(FinancialKpiErrorCodes.PeriodInvalid, new FieldIssue("periodEnd", FieldIssue.BeforeStart)) : null)
             ?? ValueRules.MeasurementRefused(draft.MeasuredValue, draft.ValueStatus, draft.AsOfDate, ValueRules.Today(now))) is { } invalid)
        {
            return invalid;
        }

        // The target version in force now is pinned, for good (ERD: "pinned at record time; never rewritten").
        if (await repository.FindActiveTargetAsync(assignment.Id, track: false, cancellationToken).ConfigureAwait(false) is not { } target)
        {
            return AdministrationError.Rule(FinancialKpiErrorCodes.TargetNotApproved);
        }

        KpiDefinitionDetail kpi = await definitions.RequireAsync(assignment.KpiDefinitionId, cancellationToken).ConfigureAwait(false);
        KpiMeasurement measurement = new()
        {
            Id = Guid.CreateVersion7(now),
            KpiAssignmentId = assignment.Id,
            KpiTargetVersionId = target.Id,
            PeriodStart = draft.PeriodStart,
            PeriodEnd = draft.PeriodEnd,
            MeasuredValue = draft.MeasuredValue,
            ValueStatus = draft.ValueStatus,
            RagStatus = KpiRagRule.Rate(draft.MeasuredValue, draft.ValueStatus, kpi.Direction, target.TargetValue, target.GreenThreshold, target.AmberThreshold),
            AsOfDate = draft.AsOfDate,
            RecordedByUserId = callerId,
            Narrative = draft.Narrative,
            Status = KpiMeasurementStatus.Draft,
            CreatedAt = now,
            CreatedBy = callerId,
            UpdatedAt = now,
            UpdatedBy = callerId,
        };
        repository.Add(measurement);
        audit.Stage(FinancialKpiAudit.MeasurementRecorded(callerId, project, measurement));
        return await repository.SaveAsync(cancellationToken).ConfigureAwait(false) switch
        {
            FinancialKpiSaveOutcome.Saved => await VersionedAsync(callerId, project, measurement, cancellationToken).ConfigureAwait(false),
            FinancialKpiSaveOutcome.Duplicate => AdministrationError.Conflict(FinancialKpiErrorCodes.MeasurementExists, new FieldIssue("periodStart", FieldIssue.Duplicate)),
            FinancialKpiSaveOutcome.ConcurrencyConflict => AdministrationError.PreconditionFailed,
            _ => throw new InvalidOperationException("Unknown save outcome."),
        };
    }

    public async Task<AdministrationResult<Versioned<KpiMeasurementDetail>>> UpdateAsync(
        Guid callerId, Guid measurementId, KpiMeasurementChanges changes, uint expectedVersion, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(changes);
        Loaded loaded = await LoadAsync(callerId, PermissionCatalogue.KpiRecord, measurementId, expectedVersion, cancellationToken).ConfigureAwait(false);
        if (loaded.Error is { } refused)
        {
            return refused;
        }

        (ProjectFacts project, KpiAssignment assignment, KpiMeasurement measurement) = (loaded.Project!, loaded.Assignment!, loaded.Measurement!);
        if (measurement.Status != KpiMeasurementStatus.Draft)
        {
            return AdministrationError.Conflict(FinancialKpiErrorCodes.NotEditable);
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        if ((ProjectEligibility.ReportingRefused(project) ?? ValueRules.MeasurementRefused(changes.MeasuredValue, changes.ValueStatus, changes.AsOfDate, ValueRules.Today(now))) is { } invalid)
        {
            return invalid;
        }

        // Re-rated against the version it is pinned to — never the one in force now.
        KpiTargetVersion pinned = await PinnedAsync(measurement, cancellationToken).ConfigureAwait(false);
        KpiDefinitionDetail kpi = await definitions.RequireAsync(assignment.KpiDefinitionId, cancellationToken).ConfigureAwait(false);
        MeasurementFigures before = MeasurementFigures.Of(measurement);
        measurement.MeasuredValue = changes.MeasuredValue;
        measurement.ValueStatus = changes.ValueStatus;
        measurement.RagStatus = KpiRagRule.Rate(changes.MeasuredValue, changes.ValueStatus, kpi.Direction, pinned.TargetValue, pinned.GreenThreshold, pinned.AmberThreshold);
        measurement.AsOfDate = changes.AsOfDate;
        measurement.Narrative = changes.Narrative;
        measurement.RecordedByUserId = callerId;
        Touch(measurement, callerId, now);
        audit.Stage(FinancialKpiAudit.MeasurementChanged(callerId, project, before, measurement));
        return await SaveAsync(callerId, project, measurement, cancellationToken).ConfigureAwait(false);
    }

    public async Task<AdministrationError?> DeleteAsync(Guid callerId, Guid measurementId, uint? expectedVersion, CancellationToken cancellationToken)
    {
        Loaded loaded = await LoadAsync(callerId, PermissionCatalogue.KpiRecord, measurementId, expectedVersion, cancellationToken).ConfigureAwait(false);
        if (loaded.Error is { } refused)
        {
            return refused;
        }

        if (loaded.Measurement!.Status != KpiMeasurementStatus.Draft)
        {
            return AdministrationError.Conflict(FinancialKpiErrorCodes.NotEditable);
        }

        audit.Stage(FinancialKpiAudit.MeasurementDeleted(callerId, loaded.Project!, loaded.Measurement));
        repository.Remove(loaded.Measurement);
        return await repository.SaveAsync(cancellationToken).ConfigureAwait(false) == FinancialKpiSaveOutcome.Saved ? null : AdministrationError.PreconditionFailed;
    }

    public async Task<AdministrationResult<Versioned<KpiMeasurementDetail>>> SubmitAsync(Guid callerId, Guid measurementId, uint? expectedVersion, CancellationToken cancellationToken)
    {
        Loaded loaded = await LoadAsync(callerId, PermissionCatalogue.KpiRecord, measurementId, expectedVersion, cancellationToken).ConfigureAwait(false);
        if (loaded.Error is { } refused)
        {
            return refused;
        }

        (ProjectFacts project, KpiMeasurement measurement) = (loaded.Project!, loaded.Measurement!);
        if (!KpiMeasurementWorkflow.Allows(measurement.Status, KpiMeasurementStatus.Submitted))
        {
            return AdministrationError.InvalidTransition;
        }

        if (ProjectEligibility.ReportingRefused(project) is { } notEligible)
        {
            return notEligible;
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        measurement.Status = KpiMeasurementStatus.Submitted;
        measurement.SubmittedAt = now;
        Touch(measurement, callerId, now);
        audit.Stage(FinancialKpiAudit.MeasurementTransition(FinancialKpiAuditEvents.MeasurementSubmitted, callerId, project, measurement, KpiMeasurementStatus.Draft));
        return await SaveAsync(callerId, project, measurement, cancellationToken).ConfigureAwait(false);
    }

    public async Task<AdministrationResult<Versioned<KpiMeasurementDetail>>> PublishAsync(Guid callerId, Guid measurementId, uint? expectedVersion, CancellationToken cancellationToken)
    {
        KpiMeasurement? measurement = await repository.FindMeasurementAsync(measurementId, expectedVersion, cancellationToken).ConfigureAwait(false);
        KpiScope.Scoped scoped = measurement is null
            ? new KpiScope.Scoped(null, null, AdministrationError.NotFound)
            : await scope.LoadAsync(callerId, PermissionCatalogue.KpiView, measurement.KpiAssignmentId, cancellationToken).ConfigureAwait(false);
        if (scoped.Error is { } invisible)
        {
            return invisible;
        }

        // AHDA's gate (ADR-013): internal reviewers only, never the person who recorded the value.
        ProjectFacts project = scoped.Project!;
        if (await access.CheckGateAsync(
                callerId, PermissionCatalogue.KpiReview, project, measurement!.RecordedByUserId,
                reason => FinancialKpiAudit.ReviewRefused(callerId, project, FinancialKpiAudit.MeasurementType, measurement.Id, reason), cancellationToken).ConfigureAwait(false) is { } refused)
        {
            return refused;
        }

        if (!KpiMeasurementWorkflow.Allows(measurement.Status, KpiMeasurementStatus.Published))
        {
            return AdministrationError.InvalidTransition;
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        measurement.Status = KpiMeasurementStatus.Published;
        measurement.PublishedByUserId = callerId;
        measurement.PublishedAt = now;
        Touch(measurement, callerId, now);
        audit.Stage(FinancialKpiAudit.MeasurementTransition(FinancialKpiAuditEvents.MeasurementPublished, callerId, project, measurement, KpiMeasurementStatus.Submitted));
        return await SaveAsync(callerId, project, measurement, cancellationToken).ConfigureAwait(false);
    }

    private async Task<KpiTargetVersion> PinnedAsync(KpiMeasurement measurement, CancellationToken cancellationToken) =>
        (await repository.ListTargetsByIdAsync([measurement.KpiTargetVersionId], cancellationToken).ConfigureAwait(false)).SingleOrDefault()
        ?? throw new InvalidOperationException($"Measurement {measurement.Id} is pinned to no target version.");

    private async Task<Loaded> LoadAsync(Guid callerId, string permissionCode, Guid measurementId, uint? expectedVersion, CancellationToken cancellationToken)
    {
        KpiMeasurement? measurement = await repository.FindMeasurementAsync(measurementId, expectedVersion, cancellationToken).ConfigureAwait(false);
        if (measurement is null)
        {
            return new Loaded(null, null, null, AdministrationError.NotFound);
        }

        KpiScope.Scoped scoped = await scope.LoadAsync(callerId, permissionCode, measurement.KpiAssignmentId, cancellationToken).ConfigureAwait(false);
        return new Loaded(scoped.Project, scoped.Assignment, measurement, scoped.Error);
    }

    private Task<FieldMask> MaskAsync(Guid callerId, CancellationToken cancellationToken) =>
        access.MaskAsync(callerId, PermissionCatalogue.KpiView, FinancialKpiMasking.Measurement, cancellationToken);

    private async Task<AdministrationResult<Versioned<KpiMeasurementDetail>>> SaveAsync(Guid callerId, ProjectFacts project, KpiMeasurement measurement, CancellationToken cancellationToken) =>
        await repository.SaveAsync(cancellationToken).ConfigureAwait(false) == FinancialKpiSaveOutcome.Saved
            ? await VersionedAsync(callerId, project, measurement, cancellationToken).ConfigureAwait(false)
            : AdministrationError.PreconditionFailed;

    private async Task<Versioned<KpiMeasurementDetail>> VersionedAsync(Guid callerId, ProjectFacts project, KpiMeasurement measurement, CancellationToken cancellationToken) =>
        new(FinancialKpiMapping.ToDetail(measurement, project.Id, await PinnedAsync(measurement, cancellationToken).ConfigureAwait(false), await MaskAsync(callerId, cancellationToken).ConfigureAwait(false)),
            repository.RowVersionOf(measurement));

    private static void Touch(KpiMeasurement measurement, Guid by, DateTimeOffset at)
    {
        measurement.UpdatedAt = at;
        measurement.UpdatedBy = by;
    }

    private sealed record Loaded(ProjectFacts? Project, KpiAssignment? Assignment, KpiMeasurement? Measurement, AdministrationError? Error);
}
