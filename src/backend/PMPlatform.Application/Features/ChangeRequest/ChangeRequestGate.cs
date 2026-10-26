using PMPlatform.Application.Features.ChangeRequest.Contracts;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.Project.Contracts;
using ChangeRequestEntity = PMPlatform.Domain.ChangeRequest.ChangeRequest;

namespace PMPlatform.Application.Features.ChangeRequest;

/// <summary>
/// The way into a project's change requests every WF-08 operation shares: the project's facts (edge 6), the authorization check, and
/// the save that commits and answers with the request as saved. A request the caller may not see is one that does not exist (R-47).
/// </summary>
internal sealed class ChangeRequestGate(IChangeRequestRepository repository, IProjectFactsReader projects, ChangeRequestAccess access, ChangeRequestViews views)
{
    /// <summary>The project, if the caller holds <paramref name="permissionCode"/> on it.</summary>
    public async Task<(ProjectFacts? Project, AdministrationError? Error)> ReachProjectAsync(
        Guid callerId, string permissionCode, Guid projectId, CancellationToken cancellationToken)
    {
        ProjectFacts? project = await projects.FindAsync(projectId, cancellationToken).ConfigureAwait(false);
        return project is null
            ? (null, AdministrationError.NotFound)
            : (project, await access.CheckAsync(callerId, permissionCode, project, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>The project, if the caller may see its change requests; for a collection, so nothing is recorded (R-3).</summary>
    public async Task<ProjectFacts?> ViewableProjectAsync(Guid callerId, Guid projectId, CancellationToken cancellationToken) =>
        await projects.FindAsync(projectId, cancellationToken).ConfigureAwait(false) is { } project
        && await access.CanViewAsync(callerId, project, cancellationToken).ConfigureAwait(false)
            ? project
            : null;

    /// <summary>
    /// The request, tracked, and its project, if the caller holds <paramref name="permissionCode"/> on it; with <paramref name="internalOnly"/>,
    /// AHDA's side, so an external user is refused whatever they hold and the refusal is audited (ADR-013).
    /// </summary>
    public async Task<LoadedChangeRequest> LoadAsync(
        Guid callerId, string permissionCode, Guid changeRequestId, uint? expectedVersion, bool internalOnly, CancellationToken cancellationToken)
    {
        ChangeRequestEntity? request = await repository.FindAsync(changeRequestId, expectedVersion, cancellationToken).ConfigureAwait(false);
        ProjectFacts? project = request is null ? null : await projects.FindAsync(request.ProjectId, cancellationToken).ConfigureAwait(false);
        if (project is null)
        {
            return new LoadedChangeRequest(null, null, AdministrationError.NotFound);
        }

        AdministrationError? refused = internalOnly
            ? await access.CheckInternalAsync(callerId, permissionCode, project, changeRequestId, cancellationToken).ConfigureAwait(false)
            : await access.CheckAsync(callerId, permissionCode, project, cancellationToken).ConfigureAwait(false);
        return new LoadedChangeRequest(project, request, refused);
    }

    /// <summary>The project of a request, if the caller may see the project's change requests; for a read that records nothing (R-3).</summary>
    public async Task<ProjectFacts?> ViewableProjectOfAsync(Guid callerId, Guid changeRequestId, CancellationToken cancellationToken) =>
        await repository.FindAsync(changeRequestId, null, cancellationToken).ConfigureAwait(false) is { } request
            ? await ViewableProjectAsync(callerId, request.ProjectId, cancellationToken).ConfigureAwait(false)
            : null;

    /// <summary>Saves, commits what was saved and answers with the request as now saved. A row changed meanwhile is a stale version (412).</summary>
    public async Task<AdministrationResult<Versioned<ChangeRequestDetail>>> SaveRequestAsync(IChangeRequestWork work, ChangeRequestEntity request, CancellationToken cancellationToken) =>
        await SaveAsync(work, cancellationToken).ConfigureAwait(false) == ChangeRequestSaveOutcome.Saved
            ? await VersionedAsync(request, cancellationToken).ConfigureAwait(false)
            : AdministrationError.PreconditionFailed;

    /// <summary>Saves and, when saved, commits; otherwise nothing was written.</summary>
    public async Task<ChangeRequestSaveOutcome> SaveAsync(IChangeRequestWork work, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(work);
        ChangeRequestSaveOutcome saved = await repository.SaveAsync(cancellationToken).ConfigureAwait(false);
        if (saved == ChangeRequestSaveOutcome.Saved)
        {
            await work.CommitAsync(cancellationToken).ConfigureAwait(false);
        }

        return saved;
    }

    public async Task<Versioned<ChangeRequestDetail>> VersionedAsync(ChangeRequestEntity request, CancellationToken cancellationToken) =>
        new(await views.DetailAsync(request, cancellationToken).ConfigureAwait(false), repository.RowVersionOf(request));

    /// <summary>Stamps a changed row with who changed it and when.</summary>
    public static void Touch(Domain.Common.AuditedEntity row, Guid actorId, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(row);
        row.UpdatedAt = now;
        row.UpdatedBy = actorId;
    }
}

internal sealed record LoadedChangeRequest(ProjectFacts? Project, ChangeRequestEntity? Request, AdministrationError? Error);
