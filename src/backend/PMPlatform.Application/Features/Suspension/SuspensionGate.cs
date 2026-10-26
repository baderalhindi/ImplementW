using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.Project.Contracts;
using PMPlatform.Application.Features.Suspension.Contracts;
using PMPlatform.Domain.Common;
using PMPlatform.Domain.Suspension;

namespace PMPlatform.Application.Features.Suspension;

/// <summary>
/// The way into a project's suspension records every WF-09 operation shares: the project's facts (Project's query contract), the
/// authorization check, and the save that commits and answers with the request as saved. A request the caller may not see is one that
/// does not exist (R-47).
/// </summary>
internal sealed class SuspensionGate(ISuspensionRepository repository, IProjectFactsReader projects, SuspensionAccess access, SuspensionViews views)
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

    /// <summary>The project, if the caller may see its suspension records; for a collection, so nothing is recorded (R-3).</summary>
    public async Task<ProjectFacts?> ViewableProjectAsync(Guid callerId, Guid projectId, CancellationToken cancellationToken) =>
        await projects.FindAsync(projectId, cancellationToken).ConfigureAwait(false) is { } project
        && await access.CanViewAsync(callerId, project, cancellationToken).ConfigureAwait(false)
            ? project
            : null;

    /// <summary>
    /// The request, tracked, and its project, if the caller holds <paramref name="permissionCode"/> on it; with <paramref name="internalOnly"/>,
    /// AHDA's side, so an external user is refused whatever they hold and the refusal is audited (ADR-013).
    /// </summary>
    public async Task<LoadedSuspensionRequest> LoadAsync(
        Guid callerId, string permissionCode, Guid suspensionRequestId, uint? expectedVersion, bool internalOnly, CancellationToken cancellationToken)
    {
        SuspensionRequest? request = await repository.FindAsync(suspensionRequestId, expectedVersion, cancellationToken).ConfigureAwait(false);
        ProjectFacts? project = request is null ? null : await projects.FindAsync(request.ProjectId, cancellationToken).ConfigureAwait(false);
        if (project is null)
        {
            return new LoadedSuspensionRequest(null, null, AdministrationError.NotFound);
        }

        AdministrationError? refused = internalOnly
            ? await access.CheckInternalAsync(callerId, permissionCode, project, suspensionRequestId, cancellationToken).ConfigureAwait(false)
            : await access.CheckAsync(callerId, permissionCode, project, cancellationToken).ConfigureAwait(false);
        return new LoadedSuspensionRequest(project, request, refused);
    }

    /// <summary>
    /// Saves, commits what was saved and answers with the request as now saved. A row changed meanwhile is a stale version (412); a
    /// single-instance key another request took first is the conflict that key stands for (409).
    /// </summary>
    public async Task<AdministrationResult<Versioned<SuspensionRequestDetail>>> SaveRequestAsync(ISuspensionWork work, SuspensionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return await SaveAsync(work, cancellationToken).ConfigureAwait(false) switch
        {
            SuspensionSaveOutcome.Saved => await VersionedAsync(request, cancellationToken).ConfigureAwait(false),
            SuspensionSaveOutcome.Duplicate => SuspensionEligibility.DuplicateOf(request.RequestType),
            SuspensionSaveOutcome.ConcurrencyConflict => AdministrationError.PreconditionFailed,
            var outcome => throw new InvalidOperationException($"Unknown save outcome {outcome}."),
        };
    }

    /// <summary>Saves and, when saved, commits; otherwise nothing was written.</summary>
    public async Task<SuspensionSaveOutcome> SaveAsync(ISuspensionWork work, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(work);
        SuspensionSaveOutcome saved = await repository.SaveAsync(cancellationToken).ConfigureAwait(false);
        if (saved == SuspensionSaveOutcome.Saved)
        {
            await work.CommitAsync(cancellationToken).ConfigureAwait(false);
        }

        return saved;
    }

    public async Task<Versioned<SuspensionRequestDetail>> VersionedAsync(SuspensionRequest request, CancellationToken cancellationToken) =>
        new(await views.DetailAsync(request, cancellationToken).ConfigureAwait(false), repository.RowVersionOf(request));

    /// <summary>Stamps a changed row with who changed it and when.</summary>
    public static void Touch(AuditedEntity row, Guid actorId, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(row);
        row.UpdatedAt = now;
        row.UpdatedBy = actorId;
    }
}

internal sealed record LoadedSuspensionRequest(ProjectFacts? Project, SuspensionRequest? Request, AdministrationError? Error);
