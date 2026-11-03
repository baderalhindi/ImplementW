using PMPlatform.Application.Common.Auditing;
using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.Project.Contracts;
using PMPlatform.Domain.Project;
using ProjectEntity = PMPlatform.Domain.Project.Project;

namespace PMPlatform.Application.Features.Project.FollowUp;

/// <summary>
/// The follow-up of registered projects (R3-3, R3-5). It is RPMO's record between formal updates, not the project's governed state: changing it
/// moves no lifecycle state and no baseline, and it is kept from approval on, a CLOSED project excepted (terminal and read-only).
/// </summary>
internal sealed class ProjectFollowUpService(
    IProjectRepository repository,
    ProjectAccess access,
    IAuthorizationEngine engine,
    OnPlanPolicy onPlan,
    IAuditTrail audit,
    TimeProvider timeProvider) : IProjectFollowUpService
{
    public async Task<AdministrationResult<Versioned<ProjectDetail>>> UpdateAsync(
        Guid callerId, Guid projectId, ProjectFollowUpChanges changes, uint expectedVersion, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(changes);

        ProjectEntity? project = await repository.FindAsync(projectId, expectedVersion, cancellationToken).ConfigureAwait(false);
        if (project is null)
        {
            return AdministrationError.NotFound;
        }

        if (await access.CheckGateAsync(callerId, PermissionCatalogue.ProjectFollowUp, project, cancellationToken).ConfigureAwait(false) is { } refused)
        {
            return refused;
        }

        if (project.LifecycleState == ProjectLifecycleState.Closed)
        {
            return AdministrationError.TerminalState;
        }

        if (project.LifecycleState is not (ProjectLifecycleState.ApprovedPlanned or ProjectLifecycleState.Active or ProjectLifecycleState.Suspended
            or ProjectLifecycleState.Completed))
        {
            return AdministrationError.Conflict(ProjectErrorCodes.FollowUpNotOpen);
        }

        ProjectFollowUpDetail before = ProjectMapping.ToFollowUp(project);
        DateTimeOffset now = timeProvider.GetUtcNow();
        bool declared = (before.DeclaredPlannedPercent, before.DeclaredActualPercent, before.DeclaredPlannedSpendPercent, before.DeclaredActualSpendPercent, before.DeclaredAsOf)
                        != (changes.DeclaredPlannedPercent, changes.DeclaredActualPercent, changes.DeclaredPlannedSpendPercent, changes.DeclaredActualSpendPercent, changes.DeclaredAsOf);
        project.CurrentObstacle = changes.CurrentObstacle;
        project.NextAction = changes.NextAction;
        project.NextActionOwner = changes.NextActionOwner;
        project.NextActionDueDate = changes.NextActionDueDate;
        project.DeclaredPlannedPercent = changes.DeclaredPlannedPercent;
        project.DeclaredActualPercent = changes.DeclaredActualPercent;
        project.DeclaredPlannedSpendPercent = changes.DeclaredPlannedSpendPercent;
        project.DeclaredActualSpendPercent = changes.DeclaredActualSpendPercent;
        project.EntityUpdateCompliant = changes.EntityUpdateCompliant;
        if (declared)
        {
            bool any = changes.DeclaredPlannedPercent is not null || changes.DeclaredActualPercent is not null
                       || changes.DeclaredPlannedSpendPercent is not null || changes.DeclaredActualSpendPercent is not null;
            project.DeclaredAsOf = any ? changes.DeclaredAsOf ?? DateOnly.FromDateTime(now.UtcDateTime) : null;
            project.DeclaredSource = any ? DeclaredValueSource.Manual : null;
        }

        project.UpdatedAt = now;
        project.UpdatedBy = callerId;
        audit.Stage(ProjectAudit.FollowUpChanged(callerId, before, project));
        if (changes.Note is { } text)
        {
            ProjectFollowUpNote note = new()
            {
                Id = Guid.CreateVersion7(now),
                ProjectId = project.Id,
                Note = text,
                CreatedAt = now,
                CreatedBy = callerId,
                UpdatedAt = now,
                UpdatedBy = callerId,
            };
            repository.Add(note);
            audit.Stage(ProjectAudit.FollowUpNoteAdded(callerId, project, note));
        }

        if (await repository.SaveAsync(cancellationToken).ConfigureAwait(false) != ProjectSaveOutcome.Saved)
        {
            return AdministrationError.PreconditionFailed;
        }

        OnPlanThresholds thresholds = await onPlan.ThresholdsAsync(now, cancellationToken).ConfigureAwait(false);
        return new Versioned<ProjectDetail>(
            ProjectMapping.ToDetail(project, ProjectIndicatorCalculator.Of(project, thresholds, DateOnly.FromDateTime(now.UtcDateTime))),
            repository.RowVersionOf(project));
    }

    public async Task<AdministrationResult<ProjectFollowUpNotePage>> ListNotesAsync(
        Guid callerId, Guid projectId, PageRequest page, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(page);

        ProjectEntity? project = await repository.ReadAsync(projectId, cancellationToken).ConfigureAwait(false);
        if (project is null)
        {
            return AdministrationError.NotFound;
        }

        if (await access.CheckAsync(callerId, PermissionCatalogue.ProjectView, project, cancellationToken).ConfigureAwait(false) is { } refused)
        {
            return refused;
        }

        (IReadOnlyList<ProjectFollowUpNote> items, int total) = await repository.ListNotesAsync(projectId, page.Page, page.PageSize, cancellationToken).ConfigureAwait(false);
        return new ProjectFollowUpNotePage(
            [.. items.Select(n => new ProjectFollowUpNoteDetail(n.Id, n.ProjectId, n.Note, n.CreatedAt, n.CreatedBy))], page.Page, page.PageSize, total);
    }

    public async Task<ProjectFollowUpPage> ListAsync(Guid callerId, ProjectFollowUpQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        RecordScope scope = await engine.GetRecordScopeAsync(callerId, PermissionCatalogue.ProjectView, cancellationToken).ConfigureAwait(false);
        if (scope.IsEmpty)
        {
            return new ProjectFollowUpPage([], query.Page.Page, query.Page.PageSize, 0);
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        DateOnly today = DateOnly.FromDateTime(now.UtcDateTime);
        OnPlanThresholds thresholds = await onPlan.ThresholdsAsync(now, cancellationToken).ConfigureAwait(false);
        List<ProjectFollowUpItem> due = [.. (await repository.ListReachedAsync(scope, cancellationToken).ConfigureAwait(false))
            .Where(p => (query.DepartmentId is not { } department || p.DepartmentId == department)
                        && (query.FollowUpOwnerUserId is not { } owner || p.FollowUpOwnerUserId == owner))
            .Select(p => (Project: p, Indicators: ProjectIndicatorCalculator.Of(p, thresholds, today)))
            .Where(r => r.Indicators.FollowUpReasons.Count > 0 && (query.Reason is not { } reason || r.Indicators.FollowUpReasons.Contains(reason)))
            .OrderBy(r => r.Indicators.FollowUpReasons[0])
            .ThenBy(r => r.Project.NextActionDueDate ?? DateOnly.MaxValue)
            .ThenBy(r => r.Project.FormalProjectId ?? r.Project.LegacyReference, StringComparer.Ordinal)
            .ThenBy(r => r.Project.Id)
            .Select(r => ItemOf(r.Project, r.Indicators))];
        return new ProjectFollowUpPage([.. due.Skip(query.Page.Skip).Take(query.Page.PageSize)], query.Page.Page, query.Page.PageSize, due.Count);
    }

    private static ProjectFollowUpItem ItemOf(ProjectEntity p, ProjectIndicators indicators) => new(
        p.Id, p.FormalProjectId, p.LegacyReference, p.Title, p.LifecycleState, p.DepartmentId, p.ExternalEntityId, p.FollowUpOwnerUserId,
        p.CurrentObstacle, p.NextAction, p.NextActionOwner, p.NextActionDueDate, indicators);
}
