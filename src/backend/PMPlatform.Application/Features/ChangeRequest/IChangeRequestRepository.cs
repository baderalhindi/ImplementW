using PMPlatform.Application.Features.ChangeRequest.Contracts;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Domain.ChangeRequest;
using PMPlatform.Domain.Common;
using ChangeRequestEntity = PMPlatform.Domain.ChangeRequest.ChangeRequest;

namespace PMPlatform.Application.Features.ChangeRequest;

/// <summary>
/// The <c>change_request</c> schema (TASK-060). Finds that return rows to change track them; the others do not. A request's row
/// version serialises its commands (R-21); an authorisation's serialises its application, so two target modules applying one
/// authorisation at once cannot both succeed.
/// </summary>
public interface IChangeRequestRepository
{
    public Task<IChangeRequestWork> BeginAsync(CancellationToken cancellationToken);

    /// <summary>Tracked. With <paramref name="expectedVersion"/>, the next save is conditional on it (R-21).</summary>
    public Task<ChangeRequestEntity?> FindAsync(Guid changeRequestId, uint? expectedVersion, CancellationToken cancellationToken);

    /// <summary>One page of the project's requests the query selects, most recently changed first, with the total. Not tracked.</summary>
    public Task<(IReadOnlyList<ChangeRequestEntity> Items, int TotalCount)> PageAsync(ChangeRequestQuery query, PageRequest page, CancellationToken cancellationToken);

    /// <summary>The latest evaluation of each of the requests that has one. Not tracked.</summary>
    public Task<IReadOnlyList<MaterialityEvaluation>> ListLatestEvaluationsAsync(IReadOnlyCollection<Guid> changeRequestIds, CancellationToken cancellationToken);

    /// <summary>
    /// The cost and schedule impacts of the project's approved requests — APPROVED, IMPLEMENTATION, IMPLEMENTED, CLOSED — whose latest
    /// evaluation was against <paramref name="projectBaselineId"/> (null: against no baseline), other than <paramref name="excludingId"/>:
    /// the changes that accumulate against the active baseline (ADR-016). Not tracked.
    /// </summary>
    public Task<IReadOnlyList<(Money? CostImpactSar, int? ScheduleImpactDays)>> ListApprovedImpactsAsync(
        Guid projectId, Guid? projectBaselineId, Guid excludingId, CancellationToken cancellationToken);

    /// <summary>The authorisations of the requests, oldest first; those issued together by scope. Not tracked.</summary>
    public Task<IReadOnlyList<ChangeAuthorization>> ListAuthorizationsAsync(IReadOnlyCollection<Guid> changeRequestIds, CancellationToken cancellationToken);

    /// <summary>Tracked when <paramref name="track"/>.</summary>
    public Task<ChangeAuthorization?> FindAuthorizationAsync(Guid changeAuthorizationId, bool track, CancellationToken cancellationToken);

    /// <summary>One page of the project's authorisations the query selects, most recently issued first, with the total. Not tracked.</summary>
    public Task<(IReadOnlyList<ChangeAuthorization> Items, int TotalCount)> PageAuthorizationsAsync(
        ChangeAuthorizationQuery query, PageRequest page, CancellationToken cancellationToken);

    /// <summary>The row version of a tracked request, as last read or saved: its ETag.</summary>
    public uint RowVersionOf(ChangeRequestEntity changeRequest);

    public void Add(ChangeRequestEntity changeRequest);

    public void Add(MaterialityEvaluation evaluation);

    public void Add(ChangeAuthorization authorization);

    public void Remove(ChangeRequestEntity changeRequest);

    /// <summary>
    /// Saves the tracked changes and what this unit of work staged — audit events, WF-11 runs. A row changed since it was read, and a
    /// unique key another request took first, are answers, not faults; after either nothing stays tracked.
    /// </summary>
    public Task<ChangeRequestSaveOutcome> SaveAsync(CancellationToken cancellationToken);
}

/// <summary>A unit of work over the schema; disposing it without <see cref="CommitAsync"/> rolls it back. Joins a transaction already open.</summary>
public interface IChangeRequestWork : IAsyncDisposable
{
    public Task CommitAsync(CancellationToken cancellationToken);
}

public enum ChangeRequestSaveOutcome
{
    Saved = 1,

    /// <summary>The row changed since it was read (R-21).</summary>
    ConcurrencyConflict = 2,

    /// <summary>A unique key another request took first: an authorisation's issuance key.</summary>
    Duplicate = 3,
}
