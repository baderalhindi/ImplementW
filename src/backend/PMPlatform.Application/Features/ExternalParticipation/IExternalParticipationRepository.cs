using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Features.ExternalParticipation.Contracts;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Domain.ExternalParticipation;

namespace PMPlatform.Application.Features.ExternalParticipation;

/// <summary>
/// The <c>external_participation</c> schema (TASK-066). Finds that return rows to change track them; the others do not. A write to a
/// revision's values also touches the revision row, so the revision's row version serialises them (R-21); a request's revisions meet on
/// their revision numbers and the one-open-revision key, and a revision's attempts on their numbers, their keys and the one-applied key.
/// </summary>
public interface IExternalParticipationRepository
{
    public Task<IExternalParticipationWork> BeginAsync(CancellationToken cancellationToken);

    /// <summary>Tracked. With <paramref name="expectedVersion"/>, the next save is conditional on it (R-21).</summary>
    public Task<ExternalUpdateRequest?> FindRequestAsync(Guid requestId, uint? expectedVersion, CancellationToken cancellationToken);

    /// <summary>
    /// One page of the requests <paramref name="scope"/> reaches — decided in the query on the request's entity and project and its project's
    /// department and Project Manager — that the query selects, most recently changed first, with the total; DRAFTs excluded when
    /// <paramref name="issuedOnly"/>. Not tracked.
    /// </summary>
    public Task<(IReadOnlyList<ExternalUpdateRequest> Items, int TotalCount)> PageRequestsAsync(
        RecordScope scope, ExternalUpdateRequestQuery query, bool issuedOnly, PageRequest page, CancellationToken cancellationToken);

    /// <summary>Tracked. With <paramref name="expectedVersion"/>, the next save is conditional on it (R-21).</summary>
    public Task<ExternalContribution?> FindContributionAsync(Guid contributionId, uint? expectedVersion, CancellationToken cancellationToken);

    /// <summary>One page of the request's revisions, newest first, with the total. Not tracked.</summary>
    public Task<(IReadOnlyList<ExternalContribution> Items, int TotalCount)> PageRevisionsAsync(Guid requestId, PageRequest page, CancellationToken cancellationToken);

    /// <summary>The values of the revisions. Not tracked.</summary>
    public Task<IReadOnlyList<ExternalContributionField>> ListFieldsAsync(IReadOnlyCollection<Guid> contributionIds, CancellationToken cancellationToken);

    /// <summary>The revision's values. Tracked, to be replaced.</summary>
    public Task<IReadOnlyList<ExternalContributionField>> FindFieldsAsync(Guid contributionId, CancellationToken cancellationToken);

    /// <summary>Tracked. With <paramref name="expectedVersion"/>, the next save is conditional on it (R-21).</summary>
    public Task<SourceApplication?> FindApplicationAsync(Guid applicationId, uint? expectedVersion, CancellationToken cancellationToken);

    /// <summary>The attempt made with this key, if any. Not tracked.</summary>
    public Task<SourceApplication?> FindApplicationByKeyAsync(string idempotencyKey, CancellationToken cancellationToken);

    /// <summary>The revision's attempts, oldest first. Not tracked.</summary>
    public Task<IReadOnlyList<SourceApplication>> ListApplicationsAsync(Guid contributionId, CancellationToken cancellationToken);

    /// <summary>One page of the revision's attempts, newest first, with the total. Not tracked.</summary>
    public Task<(IReadOnlyList<SourceApplication> Items, int TotalCount)> PageApplicationsAsync(Guid contributionId, PageRequest page, CancellationToken cancellationToken);

    /// <summary>The row version of a tracked row, as last read or saved: its ETag.</summary>
    public uint RowVersionOf(ExternalUpdateRequest request);

    public uint RowVersionOf(ExternalContribution contribution);

    public uint RowVersionOf(SourceApplication application);

    public void Add(ExternalUpdateRequest request);

    public void Add(ExternalContribution contribution);

    public void Add(ExternalContributionField field);

    public void Add(SourceApplication application);

    /// <summary>HARD_DRAFT.</summary>
    public void Remove(ExternalUpdateRequest request);

    /// <summary>A DRAFT revision's value, replaced.</summary>
    public void Remove(ExternalContributionField field);

    /// <summary>
    /// Saves the tracked changes and what this unit of work staged — audit events and the source module's change. A row changed since it
    /// was read, and a unique key another request took first, are answers, not faults; after either nothing stays tracked.
    /// </summary>
    public Task<ExternalParticipationSaveOutcome> SaveAsync(CancellationToken cancellationToken);
}

/// <summary>A unit of work over the schema; disposing it without <see cref="CommitAsync"/> rolls it back. Joins a transaction already open.</summary>
public interface IExternalParticipationWork : IAsyncDisposable
{
    public Task CommitAsync(CancellationToken cancellationToken);
}

public enum ExternalParticipationSaveOutcome
{
    Saved = 1,

    /// <summary>A row changed since it was read (R-21): the request, the revision, or the source record an attempt changed.</summary>
    ConcurrencyConflict = 2,

    /// <summary>
    /// A unique key another request took first: a revision number, the request's one open revision, an attempt number, an
    /// <c>Idempotency-Key</c>, or the revision's one applied attempt.
    /// </summary>
    Duplicate = 3,
}
