using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.Risk.Contracts;
using PMPlatform.Domain.Risk;
using RiskEntity = PMPlatform.Domain.Risk.Risk;

namespace PMPlatform.Application.Features.Risk;

/// <summary>
/// The <c>risk</c> schema (TASK-055). Finds that return rows to change track them; the others do not. Every write to a risk's
/// assessments, acceptances or actions also touches the risk row, so the row version of the risk serialises the writes to one
/// risk: the second of two concurrent writers meets a changed row (R-21).
/// </summary>
public interface IRiskRepository
{
    public Task<IRiskWork> BeginAsync(CancellationToken cancellationToken);

    /// <summary>Tracked. With <paramref name="expectedVersion"/>, the next save is conditional on it (R-21).</summary>
    public Task<RiskEntity?> FindRiskAsync(Guid riskId, uint? expectedVersion, CancellationToken cancellationToken);

    /// <summary>One page of the project's risks the query selects, most recently changed first, with the total. Not tracked.</summary>
    public Task<(IReadOnlyList<RiskEntity> Items, int TotalCount)> PageRisksAsync(RiskQuery query, PageRequest page, CancellationToken cancellationToken);

    /// <summary>The status of the risk now, or null when there is none. Not tracked.</summary>
    public Task<RiskStatus?> FindStatusAsync(Guid riskId, CancellationToken cancellationToken);

    /// <summary>The latest assessment version of each of the risks that has one. Not tracked.</summary>
    public Task<IReadOnlyList<RiskAssessmentVersion>> ListLatestAssessmentsAsync(IReadOnlyCollection<Guid> riskIds, CancellationToken cancellationToken);

    /// <summary>One page of the risk's assessment versions, newest first, with the total. Not tracked.</summary>
    public Task<(IReadOnlyList<RiskAssessmentVersion> Items, int TotalCount)> PageAssessmentsAsync(Guid riskId, PageRequest page, CancellationToken cancellationToken);

    /// <summary>Not tracked.</summary>
    public Task<RiskAssessmentVersion?> FindAssessmentAsync(Guid assessmentId, CancellationToken cancellationToken);

    /// <summary>The impacts of the assessment versions. Not tracked.</summary>
    public Task<IReadOnlyList<RiskAssessmentImpact>> ListImpactsAsync(IReadOnlyCollection<Guid> assessmentIds, CancellationToken cancellationToken);

    /// <summary>The ACTIVE acceptance of each of the risks that has one. Not tracked.</summary>
    public Task<IReadOnlyList<RiskAcceptance>> ListActiveAcceptancesAsync(IReadOnlyCollection<Guid> riskIds, CancellationToken cancellationToken);

    /// <summary>The risk's ACTIVE acceptance. Tracked.</summary>
    public Task<RiskAcceptance?> FindActiveAcceptanceAsync(Guid riskId, CancellationToken cancellationToken);

    /// <summary>One page of the risk's acceptances, newest first, with the total. Not tracked.</summary>
    public Task<(IReadOnlyList<RiskAcceptance> Items, int TotalCount)> PageAcceptancesAsync(Guid riskId, PageRequest page, CancellationToken cancellationToken);

    /// <summary>
    /// Up to <paramref name="batchSize"/> ACTIVE acceptances whose expiry is on or before <paramref name="today"/>, earliest first — after
    /// <paramref name="after"/> in that order, when given, so a pass can step past the ones it leaves. Not tracked: each is handled in its
    /// own unit of work.
    /// </summary>
    public Task<IReadOnlyList<RiskAcceptance>> ListLapsedAcceptancesAsync(DateOnly today, RiskAcceptance? after, int batchSize, CancellationToken cancellationToken);

    /// <summary>Whether the risk has a PLANNED or IN_PROGRESS action.</summary>
    public Task<bool> HasOpenActionAsync(Guid riskId, CancellationToken cancellationToken);

    /// <summary>One page of the risk's actions, oldest first, with the total. Not tracked.</summary>
    public Task<(IReadOnlyList<RiskTreatmentAction> Items, int TotalCount)> PageActionsAsync(Guid riskId, PageRequest page, CancellationToken cancellationToken);

    /// <summary>Tracked. With <paramref name="expectedVersion"/>, the next save is conditional on it (R-21).</summary>
    public Task<RiskTreatmentAction?> FindActionAsync(Guid actionId, uint? expectedVersion, CancellationToken cancellationToken);

    /// <summary>The project's risks that are not CLOSED and carry no ACTIVE acceptance, for WF-10's readiness (TASK-063).</summary>
    public Task<int> CountOpenUnacceptedAsync(Guid projectId, CancellationToken cancellationToken);

    /// <summary>The row version of a tracked row, as last read or saved: its ETag.</summary>
    public uint RowVersionOf(RiskEntity risk);

    public uint RowVersionOf(RiskTreatmentAction action);

    public void Add(RiskEntity risk);

    public void Add(RiskAssessmentVersion assessment);

    public void Add(RiskAssessmentImpact impact);

    public void Add(RiskTreatmentAction action);

    public void Add(RiskAcceptance acceptance);

    /// <summary>
    /// Saves the tracked changes and the audit events this unit of work staged. A row changed since it was read, and a unique key
    /// another request took first, are answers, not faults; after either nothing stays tracked and the unit of work is lost.
    /// </summary>
    public Task<RiskSaveOutcome> SaveAsync(CancellationToken cancellationToken);
}

/// <summary>A unit of work over the risk schema; disposing it without <see cref="CommitAsync"/> rolls it back.</summary>
public interface IRiskWork : IAsyncDisposable
{
    public Task CommitAsync(CancellationToken cancellationToken);
}

public enum RiskSaveOutcome
{
    Saved = 1,

    /// <summary>The row changed since it was read (R-21).</summary>
    ConcurrencyConflict = 2,

    /// <summary>A unique key another request took first: an assessment's version number, or a risk's one ACTIVE acceptance.</summary>
    Duplicate = 3,
}
