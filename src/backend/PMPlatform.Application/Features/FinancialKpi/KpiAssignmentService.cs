using PMPlatform.Application.Common.Auditing;
using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Features.FinancialKpi.Contracts;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.MasterDataConfig.Contracts;
using PMPlatform.Application.Features.MasterDataConfig.Contracts.Resolution;
using PMPlatform.Application.Features.Project.Contracts;
using PMPlatform.Domain.Common;
using PMPlatform.Domain.FinancialKpi;

namespace PMPlatform.Application.Features.FinancialKpi;

/// <summary>
/// KPI Performance (TASK-052): a PUBLISHED catalogue KPI assigned to a project once, ACTIVE, SUSPENDED or RETIRED; and the
/// portfolio aggregate of the KPIs' latest published measurements, combined only within one unit.
/// </summary>
internal sealed class KpiAssignmentService(
    IFinancialKpiRepository repository,
    IProjectFactsReader projects,
    FinancialKpiAccess access,
    KpiDefinitions definitions,
    IMasterDataResolver masterData,
    IAuditTrail audit,
    TimeProvider timeProvider) : IKpiAssignmentService
{
    public async Task<KpiAssignmentPage> ListAsync(Guid callerId, Guid projectId, PageRequest page, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(page);
        if (await projects.FindAsync(projectId, cancellationToken).ConfigureAwait(false) is not { } project
            || !await access.CanAsync(callerId, PermissionCatalogue.KpiView, project, cancellationToken).ConfigureAwait(false))
        {
            return new KpiAssignmentPage([], page.Page, page.PageSize, 0);
        }

        (IReadOnlyList<KpiAssignment> items, int total) = await repository.PageAssignmentsAsync(projectId, page, cancellationToken).ConfigureAwait(false);
        IReadOnlyDictionary<Guid, KpiDefinitionDetail> kpis = await definitions.FindAllAsync(items.Select(a => a.KpiDefinitionId), cancellationToken).ConfigureAwait(false);
        return new KpiAssignmentPage([.. items.Select(a => FinancialKpiMapping.ToDetail(a, kpis[a.KpiDefinitionId].UnitItemId))], page.Page, page.PageSize, total);
    }

    public async Task<AdministrationResult<Versioned<KpiAssignmentDetail>>> GetAsync(Guid callerId, Guid assignmentId, CancellationToken cancellationToken)
    {
        Loaded loaded = await LoadAsync(callerId, PermissionCatalogue.KpiView, assignmentId, null, cancellationToken).ConfigureAwait(false);
        return loaded.Error is { } refused ? refused : await VersionedAsync(loaded.Assignment!, cancellationToken).ConfigureAwait(false);
    }

    public async Task<AdministrationResult<Versioned<KpiAssignmentDetail>>> CreateAsync(Guid callerId, KpiAssignmentDraft draft, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ProjectFacts? project = await projects.FindAsync(draft.ProjectId, cancellationToken).ConfigureAwait(false);
        if ((project is null ? AdministrationError.NotFound
                : await access.CheckAsync(callerId, PermissionCatalogue.KpiManage, project, cancellationToken).ConfigureAwait(false)
                  ?? ProjectEligibility.PlanningRefused(project)) is { } refused)
        {
            return refused;
        }

        if (await definitions.FindAsync(draft.KpiDefinitionId, cancellationToken).ConfigureAwait(false) is not { Governance.LifecycleState: GovernedLifecycleState.Published })
        {
            return AdministrationError.Rule(FinancialKpiErrorCodes.KpiReferenceInvalid, new FieldIssue("kpiDefinitionId", FieldIssue.NotAllowed));
        }

        if (await ReferencesRefusedAsync(draft.OwnerUserId, draft.MeasurementFrequencyItemId, cancellationToken).ConfigureAwait(false) is { } invalid)
        {
            return invalid;
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        KpiAssignment assignment = new()
        {
            Id = Guid.CreateVersion7(now),
            ProjectId = project!.Id,
            KpiDefinitionId = draft.KpiDefinitionId,
            OwnerUserId = draft.OwnerUserId,
            MeasurementFrequencyItemId = draft.MeasurementFrequencyItemId,
            Status = KpiAssignmentStatus.Active,
            AssignedAt = now,
            CreatedAt = now,
            CreatedBy = callerId,
            UpdatedAt = now,
            UpdatedBy = callerId,
        };
        repository.Add(assignment);
        audit.Stage(FinancialKpiAudit.Assigned(callerId, project, assignment));
        return await repository.SaveAsync(cancellationToken).ConfigureAwait(false) switch
        {
            FinancialKpiSaveOutcome.Saved => await VersionedAsync(assignment, cancellationToken).ConfigureAwait(false),
            FinancialKpiSaveOutcome.Duplicate => AdministrationError.Conflict(FinancialKpiErrorCodes.KpiAlreadyAssigned, new FieldIssue("kpiDefinitionId", FieldIssue.Duplicate)),
            FinancialKpiSaveOutcome.ConcurrencyConflict => AdministrationError.PreconditionFailed,
            _ => throw new InvalidOperationException("Unknown save outcome."),
        };
    }

    public async Task<AdministrationResult<Versioned<KpiAssignmentDetail>>> UpdateAsync(
        Guid callerId, Guid assignmentId, KpiAssignmentChanges changes, uint expectedVersion, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(changes);
        Loaded loaded = await LoadAsync(callerId, PermissionCatalogue.KpiManage, assignmentId, expectedVersion, cancellationToken).ConfigureAwait(false);
        if (loaded.Error is { } refused)
        {
            return refused;
        }

        (ProjectFacts project, KpiAssignment assignment) = (loaded.Project!, loaded.Assignment!);
        if (assignment.Status == KpiAssignmentStatus.Retired)
        {
            return AdministrationError.TerminalState;
        }

        if (await ReferencesRefusedAsync(changes.OwnerUserId, changes.MeasurementFrequencyItemId, cancellationToken).ConfigureAwait(false) is { } invalid)
        {
            return invalid;
        }

        (Guid? owner, Guid frequency) = (assignment.OwnerUserId, assignment.MeasurementFrequencyItemId);
        assignment.OwnerUserId = changes.OwnerUserId;
        assignment.MeasurementFrequencyItemId = changes.MeasurementFrequencyItemId;
        Touch(assignment, callerId);
        audit.Stage(FinancialKpiAudit.AssignmentChanged(callerId, project, assignment, owner, frequency));
        return await SaveAsync(assignment, cancellationToken).ConfigureAwait(false);
    }

    public async Task<AdministrationResult<Versioned<KpiAssignmentDetail>>> TransitionAsync(
        Guid callerId, Guid assignmentId, KpiAssignmentStatus status, uint? expectedVersion, CancellationToken cancellationToken)
    {
        Loaded loaded = await LoadAsync(callerId, PermissionCatalogue.KpiManage, assignmentId, expectedVersion, cancellationToken).ConfigureAwait(false);
        if (loaded.Error is { } refused)
        {
            return refused;
        }

        (ProjectFacts project, KpiAssignment assignment) = (loaded.Project!, loaded.Assignment!);
        if (!KpiAssignmentWorkflow.Allows(assignment.Status, status))
        {
            return assignment.Status == KpiAssignmentStatus.Retired ? AdministrationError.TerminalState : AdministrationError.InvalidTransition;
        }

        KpiAssignmentStatus from = assignment.Status;
        assignment.Status = status;
        Touch(assignment, callerId);
        audit.Stage(FinancialKpiAudit.AssignmentTransitioned(callerId, project, assignment, from));
        return await SaveAsync(assignment, cancellationToken).ConfigureAwait(false);
    }

    public async Task<KpiPortfolioAggregate> AggregateAsync(
        Guid callerId, IReadOnlyList<Guid> kpiDefinitionIds, IReadOnlyList<Guid> projectIds, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(kpiDefinitionIds);
        ArgumentNullException.ThrowIfNull(projectIds);
        List<Guid> kpiIds = [.. kpiDefinitionIds.Distinct()];
        List<Guid> visible = [];
        List<AggregateExclusion> unavailable = [];
        foreach (Guid projectId in projectIds.Distinct())
        {
            if (await projects.FindAsync(projectId, cancellationToken).ConfigureAwait(false) is { } project
                && await access.CanAsync(callerId, PermissionCatalogue.KpiView, project, cancellationToken).ConfigureAwait(false))
            {
                visible.Add(projectId);
            }
            else
            {
                unavailable.Add(new AggregateExclusion(projectId, null, AggregateExclusionReason.NotAvailable));
            }
        }

        IReadOnlyList<KpiAssignment> assignments = await repository.ListAssignmentsAsync(visible, kpiIds, cancellationToken).ConfigureAwait(false);
        IReadOnlyList<KpiMeasurement> latest = await repository.ListLatestPublishedMeasurementsAsync([.. assignments.Select(a => a.Id)], cancellationToken).ConfigureAwait(false);
        IReadOnlyDictionary<Guid, KpiDefinitionDetail> kpis = await definitions.FindAllAsync(kpiIds, cancellationToken).ConfigureAwait(false);
        bool masked = !(await access.MaskAsync(callerId, PermissionCatalogue.KpiView, FinancialKpiMasking.Measurement, cancellationToken).ConfigureAwait(false))
            .Reveals(FinancialKpiMasking.MeasuredValue);

        List<KpiFigure> figures = [];
        foreach (Guid projectId in visible)
        {
            foreach (Guid kpiId in kpiIds)
            {
                KpiAssignment? assignment = assignments.SingleOrDefault(a => a.ProjectId == projectId && a.KpiDefinitionId == kpiId);
                KpiMeasurement? measurement = assignment is null ? null : latest.SingleOrDefault(m => m.KpiAssignmentId == assignment.Id);
                if (measurement is null || !kpis.TryGetValue(kpiId, out KpiDefinitionDetail? kpi))
                {
                    unavailable.Add(new AggregateExclusion(projectId, kpiId, AggregateExclusionReason.NoPublishedFigure));
                    continue;
                }

                figures.Add(new KpiFigure(projectId, kpiId, kpi.UnitItemId, measurement.MeasuredValue, measurement.ValueStatus, measurement.RagStatus, masked));
            }
        }

        return KpiAggregation.Aggregate(figures, unavailable);
    }

    /// <summary>The owner, when named, is an active user; the frequency is a PUBLISHED MEASUREMENT_FREQUENCY item.</summary>
    private async Task<AdministrationError?> ReferencesRefusedAsync(Guid? ownerUserId, Guid frequencyItemId, CancellationToken cancellationToken)
    {
        if (ownerUserId is { } owner && !await access.IsActiveUserAsync(owner, cancellationToken).ConfigureAwait(false))
        {
            return AdministrationError.Rule(FinancialKpiErrorCodes.KpiReferenceInvalid, new FieldIssue("ownerUserId", FieldIssue.NotFound));
        }

        try
        {
            await masterData.RequirePublishedItemAsync(MasterDataCatalogueCodes.MeasurementFrequency, frequencyItemId, cancellationToken).ConfigureAwait(false);
            return null;
        }
        catch (ConfigurationMissingException)
        {
            return AdministrationError.Rule(FinancialKpiErrorCodes.KpiReferenceInvalid, new FieldIssue("measurementFrequencyItemId", FieldIssue.NotAllowed));
        }
    }

    private async Task<Loaded> LoadAsync(Guid callerId, string permissionCode, Guid assignmentId, uint? expectedVersion, CancellationToken cancellationToken)
    {
        KpiAssignment? assignment = await repository.FindAssignmentAsync(assignmentId, expectedVersion, cancellationToken).ConfigureAwait(false);
        ProjectFacts? project = assignment is null ? null : await projects.FindAsync(assignment.ProjectId, cancellationToken).ConfigureAwait(false);
        return project is null
            ? new Loaded(null, null, AdministrationError.NotFound)
            : new Loaded(project, assignment, await access.CheckAsync(callerId, permissionCode, project, cancellationToken).ConfigureAwait(false));
    }

    private async Task<AdministrationResult<Versioned<KpiAssignmentDetail>>> SaveAsync(KpiAssignment assignment, CancellationToken cancellationToken) =>
        await repository.SaveAsync(cancellationToken).ConfigureAwait(false) == FinancialKpiSaveOutcome.Saved
            ? await VersionedAsync(assignment, cancellationToken).ConfigureAwait(false)
            : AdministrationError.PreconditionFailed;

    private async Task<Versioned<KpiAssignmentDetail>> VersionedAsync(KpiAssignment assignment, CancellationToken cancellationToken) =>
        new(FinancialKpiMapping.ToDetail(assignment, (await definitions.RequireAsync(assignment.KpiDefinitionId, cancellationToken).ConfigureAwait(false)).UnitItemId),
            repository.RowVersionOf(assignment));

    private void Touch(KpiAssignment assignment, Guid by)
    {
        assignment.UpdatedAt = timeProvider.GetUtcNow();
        assignment.UpdatedBy = by;
    }

    private sealed record Loaded(ProjectFacts? Project, KpiAssignment? Assignment, AdministrationError? Error);
}
