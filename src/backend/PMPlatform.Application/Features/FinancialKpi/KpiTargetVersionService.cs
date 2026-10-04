using PMPlatform.Application.Common.Auditing;
using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Features.Approval.Contracts;
using PMPlatform.Application.Features.FinancialKpi.Contracts;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.MasterDataConfig.Contracts;
using PMPlatform.Application.Features.Project.Contracts;
using PMPlatform.Domain.FinancialKpi;

namespace PMPlatform.Application.Features.FinancialKpi;

/// <summary>
/// An assignment's target versions (TASK-052). A version is opened DRAFT, its thresholds checked against the KPI's direction, and
/// submitted to WF-11 (edge 26); <see cref="EventHandlers.FinancialKpiApprovalOutcomeHandler"/> makes it ACTIVE and supersedes
/// the previous one. From ACTIVE on it is never edited, so a measurement pinned to it keeps what it was measured against.
/// </summary>
internal sealed class KpiTargetVersionService(
    IFinancialKpiRepository repository,
    KpiScope scope,
    KpiDefinitions definitions,
    IApprovalRequests approvals,
    IAuditTrail audit,
    TimeProvider timeProvider) : IKpiTargetVersionService
{
    public async Task<KpiTargetVersionPage> ListAsync(Guid callerId, Guid assignmentId, PageRequest page, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(page);
        if (!await scope.CanAsync(callerId, PermissionCatalogue.KpiView, assignmentId, cancellationToken).ConfigureAwait(false))
        {
            return new KpiTargetVersionPage([], page.Page, page.PageSize, 0);
        }

        (IReadOnlyList<KpiTargetVersion> items, int total) = await repository.PageTargetsAsync(assignmentId, page, cancellationToken).ConfigureAwait(false);
        return new KpiTargetVersionPage([.. items.Select(FinancialKpiMapping.ToDetail)], page.Page, page.PageSize, total);
    }

    public async Task<AdministrationResult<Versioned<KpiTargetVersionDetail>>> GetAsync(Guid callerId, Guid targetVersionId, CancellationToken cancellationToken)
    {
        Loaded loaded = await LoadAsync(callerId, PermissionCatalogue.KpiView, targetVersionId, null, cancellationToken).ConfigureAwait(false);
        return loaded.Error is { } refused ? refused : Versioned(loaded.Target!);
    }

    public async Task<AdministrationResult<Versioned<KpiTargetVersionDetail>>> CreateAsync(Guid callerId, KpiTargetVersionDraft draft, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(draft);
        KpiScope.Scoped scoped = await scope.LoadAsync(callerId, PermissionCatalogue.KpiManage, draft.KpiAssignmentId, cancellationToken).ConfigureAwait(false);
        if (scoped.Error is { } refused)
        {
            return refused;
        }

        (ProjectFacts project, KpiAssignment assignment) = (scoped.Project!, scoped.Assignment!);
        if (await TargetRefusedAsync(project, assignment, draft.GreenThreshold, draft.AmberThreshold, cancellationToken).ConfigureAwait(false) is { } invalid)
        {
            return invalid;
        }

        IReadOnlyList<KpiTargetVersion> versions = await repository.ListTargetsAsync(assignment.Id, cancellationToken).ConfigureAwait(false);
        if (versions.Any(v => ApprovedVersionWorkflow.IsOpen(v.Status)))
        {
            return AdministrationError.Conflict(FinancialKpiErrorCodes.TargetOpen);
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        KpiTargetVersion target = new()
        {
            Id = Guid.CreateVersion7(now),
            KpiAssignmentId = assignment.Id,
            VersionNo = versions.Count == 0 ? 1 : versions.Max(v => v.VersionNo) + 1,
            Status = ApprovedVersionStatus.Draft,
            TargetValue = draft.TargetValue,
            GreenThreshold = draft.GreenThreshold,
            AmberThreshold = draft.AmberThreshold,
            EffectiveFrom = draft.EffectiveFrom,
            CreatedAt = now,
            CreatedBy = callerId,
            UpdatedAt = now,
            UpdatedBy = callerId,
        };
        repository.Add(target);
        audit.Stage(FinancialKpiAudit.VersionCreated(callerId, project, VersionFacts.Of(target)));
        return await repository.SaveAsync(cancellationToken).ConfigureAwait(false) switch
        {
            FinancialKpiSaveOutcome.Saved => Versioned(target),
            FinancialKpiSaveOutcome.Duplicate => AdministrationError.Conflict(FinancialKpiErrorCodes.TargetOpen),
            FinancialKpiSaveOutcome.ConcurrencyConflict => AdministrationError.PreconditionFailed,
            _ => throw new InvalidOperationException("Unknown save outcome."),
        };
    }

    public async Task<AdministrationResult<Versioned<KpiTargetVersionDetail>>> UpdateAsync(
        Guid callerId, Guid targetVersionId, KpiTargetVersionChanges changes, uint expectedVersion, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(changes);
        Loaded loaded = await LoadAsync(callerId, PermissionCatalogue.KpiManage, targetVersionId, expectedVersion, cancellationToken).ConfigureAwait(false);
        if (loaded.Error is { } refused)
        {
            return refused;
        }

        (ProjectFacts project, KpiTargetVersion target) = (loaded.Project!, loaded.Target!);
        if (!ApprovedVersionWorkflow.IsEditable(target.Status))
        {
            return AdministrationError.Conflict(FinancialKpiErrorCodes.VersionNotEditable);
        }

        if (await TargetRefusedAsync(project, loaded.Assignment!, changes.GreenThreshold, changes.AmberThreshold, cancellationToken).ConfigureAwait(false) is { } invalid)
        {
            return invalid;
        }

        VersionFacts before = VersionFacts.Of(target);
        target.TargetValue = changes.TargetValue;
        target.GreenThreshold = changes.GreenThreshold;
        target.AmberThreshold = changes.AmberThreshold;
        target.EffectiveFrom = changes.EffectiveFrom;
        Touch(target, callerId);
        audit.Stage(FinancialKpiAudit.VersionChanged(callerId, project, before, VersionFacts.Of(target)));
        return await SaveAsync(target, cancellationToken).ConfigureAwait(false);
    }

    public async Task<AdministrationError?> DeleteAsync(Guid callerId, Guid targetVersionId, uint? expectedVersion, CancellationToken cancellationToken)
    {
        Loaded loaded = await LoadAsync(callerId, PermissionCatalogue.KpiManage, targetVersionId, expectedVersion, cancellationToken).ConfigureAwait(false);
        if (loaded.Error is { } refused)
        {
            return refused;
        }

        if (loaded.Target!.Status != ApprovedVersionStatus.Draft)
        {
            return AdministrationError.Conflict(FinancialKpiErrorCodes.VersionNotEditable);
        }

        audit.Stage(FinancialKpiAudit.VersionDeleted(callerId, loaded.Project!, VersionFacts.Of(loaded.Target)));
        repository.Remove(loaded.Target);
        return await repository.SaveAsync(cancellationToken).ConfigureAwait(false) == FinancialKpiSaveOutcome.Saved ? null : AdministrationError.PreconditionFailed;
    }

    public async Task<AdministrationResult<Versioned<KpiTargetVersionDetail>>> SubmitAsync(Guid callerId, Guid targetVersionId, uint? expectedVersion, CancellationToken cancellationToken)
    {
        Loaded loaded = await LoadAsync(callerId, PermissionCatalogue.KpiManage, targetVersionId, expectedVersion, cancellationToken).ConfigureAwait(false);
        if (loaded.Error is { } refused)
        {
            return refused;
        }

        (ProjectFacts project, KpiTargetVersion target) = (loaded.Project!, loaded.Target!);
        if (!ApprovedVersionWorkflow.Allows(target.Status, ApprovedVersionStatus.Submitted))
        {
            return AdministrationError.InvalidTransition;
        }

        if (await TargetRefusedAsync(project, loaded.Assignment!, target.GreenThreshold, target.AmberThreshold, cancellationToken).ConfigureAwait(false) is { } invalid)
        {
            return invalid;
        }

        ApprovedVersionStatus from = target.Status;
        if (from == ApprovedVersionStatus.Returned)
        {
            target.RevisionNo++;
        }

        AdministrationResult<ApprovalInstanceDetail> run = await approvals.StartAsync(
            new ApprovalStart(
                new ApprovalSubject(FinancialKpiApprovalRouting.SubjectModule, FinancialKpiApprovalRouting.TargetVersionType, target.Id, target.RevisionNo),
                FinancialKpiApprovalRouting.TargetRoutingKey,
                callerId,
                project.Id,
                project.DepartmentId,
                project.GovernanceProfileItemId,
                null,
                null),
            cancellationToken).ConfigureAwait(false);
        if (!run.Succeeded)
        {
            return run.Error;
        }

        target.Status = ApprovedVersionStatus.Submitted;
        Touch(target, callerId);
        audit.Stage(FinancialKpiAudit.VersionSubmitted(callerId, project, VersionFacts.Of(target), from, run.Value.Id));
        return await SaveAsync(target, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>The project admits planning, the assignment is ACTIVE, and the thresholds order as the KPI's direction requires.</summary>
    private async Task<AdministrationError?> TargetRefusedAsync(ProjectFacts project, KpiAssignment assignment, decimal? green, decimal? amber, CancellationToken cancellationToken)
    {
        if (ProjectEligibility.PlanningRefused(project) is { } notEligible)
        {
            return notEligible;
        }

        if (assignment.Status != KpiAssignmentStatus.Active)
        {
            return AdministrationError.Rule(FinancialKpiErrorCodes.AssignmentNotActive);
        }

        KpiDefinitionDetail kpi = await definitions.RequireAsync(assignment.KpiDefinitionId, cancellationToken).ConfigureAwait(false);
        return KpiRagRule.AreCoherent(kpi.Direction, green, amber)
            ? null
            : AdministrationError.Rule(FinancialKpiErrorCodes.ThresholdsInvalid, new FieldIssue(green is null ? "greenThreshold" : "amberThreshold", FieldIssue.NotAllowed));
    }

    private async Task<Loaded> LoadAsync(Guid callerId, string permissionCode, Guid targetVersionId, uint? expectedVersion, CancellationToken cancellationToken)
    {
        KpiTargetVersion? target = await repository.FindTargetAsync(targetVersionId, expectedVersion, cancellationToken).ConfigureAwait(false);
        if (target is null)
        {
            return new Loaded(null, null, null, AdministrationError.NotFound);
        }

        KpiScope.Scoped scoped = await scope.LoadAsync(callerId, permissionCode, target.KpiAssignmentId, cancellationToken).ConfigureAwait(false);
        return new Loaded(scoped.Project, scoped.Assignment, target, scoped.Error);
    }

    private async Task<AdministrationResult<Versioned<KpiTargetVersionDetail>>> SaveAsync(KpiTargetVersion target, CancellationToken cancellationToken) =>
        await repository.SaveAsync(cancellationToken).ConfigureAwait(false) == FinancialKpiSaveOutcome.Saved ? Versioned(target) : AdministrationError.PreconditionFailed;

    private Versioned<KpiTargetVersionDetail> Versioned(KpiTargetVersion target) => new(FinancialKpiMapping.ToDetail(target), repository.RowVersionOf(target));

    private void Touch(KpiTargetVersion target, Guid by)
    {
        target.UpdatedAt = timeProvider.GetUtcNow();
        target.UpdatedBy = by;
    }

    private sealed record Loaded(ProjectFacts? Project, KpiAssignment? Assignment, KpiTargetVersion? Target, AdministrationError? Error);
}
