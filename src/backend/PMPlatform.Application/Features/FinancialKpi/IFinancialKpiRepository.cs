using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Domain.Common;
using PMPlatform.Domain.FinancialKpi;

namespace PMPlatform.Application.Features.FinancialKpi;

/// <summary>The <c>financial_kpi</c> schema (TASK-052). Finds that return rows to change track them; the others do not.</summary>
public interface IFinancialKpiRepository
{
    /// <summary>A transaction for the writes that follow, or the caller's own when one is open already (an outcome dispatch).</summary>
    public Task<IFinancialKpiWork> BeginAsync(CancellationToken cancellationToken);

    /// <summary>The row version of a tracked row, as last read or saved: its ETag.</summary>
    public uint RowVersionOf(AuditedEntity entity);

    public void Add(AuditedEntity entity);

    /// <summary>HARD_DRAFT rows only; the database refuses any other.</summary>
    public void Remove(AuditedEntity entity);

    /// <summary>
    /// Saves the tracked changes and the audit events this unit of work staged. A row changed since it was read, and a unique key
    /// another request took first, are answers, not faults; after either nothing stays tracked.
    /// </summary>
    public Task<FinancialKpiSaveOutcome> SaveAsync(CancellationToken cancellationToken);

    // Financial Progress.

    /// <summary>The project's configured source modes. Not tracked.</summary>
    public Task<IReadOnlyList<FinancialSourceMode>> ListSourceModesAsync(Guid projectId, CancellationToken cancellationToken);

    /// <summary>Tracked; the next save is conditional on <paramref name="expectedVersion"/> (R-21).</summary>
    public Task<FinancialSourceMode?> FindSourceModeAsync(Guid sourceModeId, uint? expectedVersion, CancellationToken cancellationToken);

    /// <summary>The project's versions of one commitment type. Not tracked.</summary>
    public Task<IReadOnlyList<FinancialCommitment>> ListCommitmentsAsync(Guid projectId, CommitmentType type, CancellationToken cancellationToken);

    /// <summary>One page of every version of the project, newest first, with the total. Not tracked.</summary>
    public Task<(IReadOnlyList<FinancialCommitment> Items, int TotalCount)> PageCommitmentsAsync(Guid projectId, PageRequest page, CancellationToken cancellationToken);

    /// <summary>Tracked; with <paramref name="expectedVersion"/>, the next save is conditional on it (R-21).</summary>
    public Task<FinancialCommitment?> FindCommitmentAsync(Guid commitmentId, uint? expectedVersion, CancellationToken cancellationToken);

    /// <summary>The ACTIVE version of the type, the record in force; tracked when <paramref name="track"/>.</summary>
    public Task<FinancialCommitment?> FindActiveCommitmentAsync(Guid projectId, CommitmentType type, bool track, CancellationToken cancellationToken);

    /// <summary>The ACTIVE APPROVED_BUDGET of each project named that has one. Not tracked.</summary>
    public Task<IReadOnlyList<FinancialCommitment>> ListActiveBudgetsAsync(IReadOnlyCollection<Guid> projectIds, CancellationToken cancellationToken);

    public Task<(IReadOnlyList<FinancialProgressUpdate> Items, int TotalCount)> PageUpdatesAsync(Guid projectId, PageRequest page, CancellationToken cancellationToken);

    public Task<FinancialProgressUpdate?> FindUpdateAsync(Guid updateId, uint? expectedVersion, CancellationToken cancellationToken);

    /// <summary>Every revision of every period of the project. Not tracked.</summary>
    public Task<IReadOnlyList<FinancialProgressUpdate>> ListUpdatesAsync(Guid projectId, CancellationToken cancellationToken);

    /// <summary>
    /// The latest revision of each project named that was submitted for review or published — the figures the live position shows —
    /// by its submission time. Not tracked.
    /// </summary>
    public Task<IReadOnlyList<FinancialProgressUpdate>> ListLatestReportedUpdatesAsync(IReadOnlyCollection<Guid> projectIds, CancellationToken cancellationToken);

    public Task<(IReadOnlyList<PublishedFinancialSnapshot> Items, int TotalCount)> PageSnapshotsAsync(Guid projectId, PageRequest page, CancellationToken cancellationToken);

    /// <summary>The latest published snapshot of each project named that has one. Not tracked.</summary>
    public Task<IReadOnlyList<PublishedFinancialSnapshot>> ListLatestSnapshotsAsync(IReadOnlyCollection<Guid> projectIds, CancellationToken cancellationToken);

    // KPI Performance.

    public Task<(IReadOnlyList<KpiAssignment> Items, int TotalCount)> PageAssignmentsAsync(Guid projectId, PageRequest page, CancellationToken cancellationToken);

    /// <summary>Tracked; with <paramref name="expectedVersion"/>, the next save is conditional on it (R-21).</summary>
    public Task<KpiAssignment?> FindAssignmentAsync(Guid assignmentId, uint? expectedVersion, CancellationToken cancellationToken);

    /// <summary>The assignments of the KPIs named on the projects named. Not tracked.</summary>
    public Task<IReadOnlyList<KpiAssignment>> ListAssignmentsAsync(
        IReadOnlyCollection<Guid> projectIds, IReadOnlyCollection<Guid> kpiDefinitionIds, CancellationToken cancellationToken);

    public Task<(IReadOnlyList<KpiTargetVersion> Items, int TotalCount)> PageTargetsAsync(Guid assignmentId, PageRequest page, CancellationToken cancellationToken);

    /// <summary>Every version of the assignment's target. Not tracked.</summary>
    public Task<IReadOnlyList<KpiTargetVersion>> ListTargetsAsync(Guid assignmentId, CancellationToken cancellationToken);

    public Task<KpiTargetVersion?> FindTargetAsync(Guid targetVersionId, uint? expectedVersion, CancellationToken cancellationToken);

    /// <summary>The assignment's ACTIVE target version; tracked when <paramref name="track"/>.</summary>
    public Task<KpiTargetVersion?> FindActiveTargetAsync(Guid assignmentId, bool track, CancellationToken cancellationToken);

    /// <summary>The target versions named. Not tracked.</summary>
    public Task<IReadOnlyList<KpiTargetVersion>> ListTargetsByIdAsync(IReadOnlyCollection<Guid> targetVersionIds, CancellationToken cancellationToken);

    /// <summary>One page of the assignment's measurements, latest period first, with the total. Not tracked.</summary>
    public Task<(IReadOnlyList<KpiMeasurement> Items, int TotalCount)> PageMeasurementsAsync(Guid assignmentId, PageRequest page, CancellationToken cancellationToken);

    public Task<KpiMeasurement?> FindMeasurementAsync(Guid measurementId, uint? expectedVersion, CancellationToken cancellationToken);

    /// <summary>The latest PUBLISHED measurement, by period, of each assignment named that has one. Not tracked.</summary>
    public Task<IReadOnlyList<KpiMeasurement>> ListLatestPublishedMeasurementsAsync(IReadOnlyCollection<Guid> assignmentIds, CancellationToken cancellationToken);
}

/// <summary>A unit of work over the financial_kpi schema; disposing it without <see cref="CommitAsync"/> rolls it back.</summary>
public interface IFinancialKpiWork : IAsyncDisposable
{
    public Task CommitAsync(CancellationToken cancellationToken);
}

public enum FinancialKpiSaveOutcome
{
    Saved = 1,

    /// <summary>The row changed since it was read (R-21).</summary>
    ConcurrencyConflict = 2,

    /// <summary>A unique key another request took first: a version number, an open or ACTIVE version, a period's revision, an assignment.</summary>
    Duplicate = 3,
}
