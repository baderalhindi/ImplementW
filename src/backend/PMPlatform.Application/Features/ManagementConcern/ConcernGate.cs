using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.ManagementConcern.Contracts;
using PMPlatform.Application.Features.Project.Contracts;
using ConcernEntity = PMPlatform.Domain.ManagementConcern.ManagementConcern;

namespace PMPlatform.Application.Features.ManagementConcern;

/// <summary>
/// The way into a project's concerns every WF-07 operation shares: the project's facts (edge 4), the authorization check, and the
/// save that commits and answers with the concern as saved. A concern the caller may not see is one that does not exist (R-47).
/// </summary>
internal sealed class ConcernGate(IManagementConcernRepository repository, IProjectFactsReader projects, ConcernAccess access, ConcernViews views)
{
    /// <summary>The project, if the caller holds <paramref name="permissionCode"/> on it.</summary>
    public async Task<(ProjectFacts? Project, AdministrationError? Error)> ReachProjectAsync(
        Guid callerId, string permissionCode, Guid projectId, CancellationToken cancellationToken)
    {
        ProjectFacts? project = await projects.FindAsync(projectId, cancellationToken).ConfigureAwait(false);
        return project is null ? (null, AdministrationError.NotFound)
            : (project, await access.CheckAsync(callerId, permissionCode, project, null, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>The project, if the caller may see its concerns; for a collection, so nothing is recorded (R-3).</summary>
    public async Task<ProjectFacts?> ViewableProjectAsync(Guid callerId, Guid projectId, CancellationToken cancellationToken) =>
        await projects.FindAsync(projectId, cancellationToken).ConfigureAwait(false) is { } project
        && await access.CanViewAsync(callerId, project, null, cancellationToken).ConfigureAwait(false)
            ? project
            : null;

    /// <summary>The concern, tracked, and its project, if the caller holds <paramref name="permissionCode"/> on it.</summary>
    public async Task<LoadedConcern> LoadAsync(Guid callerId, string permissionCode, Guid concernId, uint? expectedVersion, CancellationToken cancellationToken)
    {
        (ConcernEntity? concern, ProjectFacts? project) = await FindAsync(concernId, expectedVersion, cancellationToken).ConfigureAwait(false);
        return project is null
            ? new LoadedConcern(null, null, AdministrationError.NotFound)
            : new LoadedConcern(project, concern, await access.CheckAsync(callerId, permissionCode, project, concern!.AssigneeUserId, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>
    /// As <see cref="LoadAsync"/>, for management or escalation: AHDA's side, so an external user is refused whatever they hold and the
    /// refusal is audited (ADR-013: intake and visibility only).
    /// </summary>
    public async Task<LoadedConcern> LoadInternalAsync(Guid callerId, string permissionCode, Guid concernId, uint? expectedVersion, CancellationToken cancellationToken)
    {
        (ConcernEntity? concern, ProjectFacts? project) = await FindAsync(concernId, expectedVersion, cancellationToken).ConfigureAwait(false);
        return project is null
            ? new LoadedConcern(null, null, AdministrationError.NotFound)
            : new LoadedConcern(project, concern, await access.CheckInternalAsync(
                callerId, permissionCode, project, concern!.AssigneeUserId,
                reason => ConcernAudit.AuthorityRefused(callerId, project, concernId, permissionCode, reason), cancellationToken).ConfigureAwait(false));
    }

    /// <summary>The concern and its project, if the caller may see the project's concerns; for a collection, so nothing is recorded (R-3).</summary>
    public async Task<(ConcernEntity Concern, ProjectFacts Project)?> ViewableConcernAsync(Guid callerId, Guid concernId, CancellationToken cancellationToken)
    {
        (ConcernEntity? concern, ProjectFacts? project) = await FindAsync(concernId, null, cancellationToken).ConfigureAwait(false);
        return project is not null && await access.CanViewAsync(callerId, project, concern!.AssigneeUserId, cancellationToken).ConfigureAwait(false) ? (concern, project) : null;
    }

    /// <summary>Saves, commits what was saved and answers with the concern as now saved. A row changed meanwhile is a stale version (412).</summary>
    public async Task<AdministrationResult<Versioned<ConcernDetail>>> SaveConcernAsync(IConcernWork work, ConcernEntity concern, CancellationToken cancellationToken) =>
        await SaveAsync(work, cancellationToken).ConfigureAwait(false) == ConcernSaveOutcome.Saved
            ? new Versioned<ConcernDetail>(await views.DetailAsync(concern, cancellationToken).ConfigureAwait(false), repository.RowVersionOf(concern))
            : AdministrationError.PreconditionFailed;

    /// <summary>Saves and, when saved, commits; otherwise nothing was written.</summary>
    public async Task<ConcernSaveOutcome> SaveAsync(IConcernWork work, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(work);
        ConcernSaveOutcome saved = await repository.SaveAsync(cancellationToken).ConfigureAwait(false);
        if (saved == ConcernSaveOutcome.Saved)
        {
            await work.CommitAsync(cancellationToken).ConfigureAwait(false);
        }

        return saved;
    }

    /// <summary>Stamps a changed row with who changed it and when.</summary>
    public static void Touch(Domain.Common.AuditedEntity row, Guid actorId, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(row);
        row.UpdatedAt = now;
        row.UpdatedBy = actorId;
    }

    private async Task<(ConcernEntity? Concern, ProjectFacts? Project)> FindAsync(Guid concernId, uint? expectedVersion, CancellationToken cancellationToken)
    {
        ConcernEntity? concern = await repository.FindAsync(concernId, expectedVersion, cancellationToken).ConfigureAwait(false);
        return (concern, concern is null ? null : await projects.FindAsync(concern.ProjectId, cancellationToken).ConfigureAwait(false));
    }
}

internal sealed record LoadedConcern(ProjectFacts? Project, ConcernEntity? Concern, AdministrationError? Error);
