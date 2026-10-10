using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Domain.Progress;

namespace PMPlatform.Application.Features.Progress;

/// <summary>The <c>progress</c> schema (TASK-044). Finds that return rows to change track them; the others do not.</summary>
public interface IProgressRepository
{
    /// <summary>The project's reporting periods, earliest first. Not tracked.</summary>
    public Task<IReadOnlyList<ReportingCycle>> ListCyclesAsync(Guid projectId, CancellationToken cancellationToken);

    /// <summary>One page of them, earliest first, with the total. Not tracked.</summary>
    public Task<(IReadOnlyList<ReportingCycle> Items, int TotalCount)> PageCyclesAsync(Guid projectId, PageRequest page, CancellationToken cancellationToken);

    /// <summary>Tracked.</summary>
    public Task<ReportingCycle?> FindCycleAsync(Guid reportingCycleId, CancellationToken cancellationToken);

    /// <summary>Tracked. With <paramref name="expectedVersion"/>, the next save is conditional on it (R-21).</summary>
    public Task<ProgressSubmission?> FindSubmissionAsync(Guid submissionId, uint? expectedVersion, CancellationToken cancellationToken);

    /// <summary>How many of the project's progress submissions are in each status, for WF-10's readiness (TASK-063). Not tracked.</summary>
    public Task<IReadOnlyDictionary<ProgressSubmissionStatus, int>> CountByStatusAsync(Guid projectId, CancellationToken cancellationToken);

    /// <summary>The row version of a tracked submission, as last read or saved: its ETag.</summary>
    public uint RowVersionOf(ProgressSubmission submission);

    /// <summary>One page of every revision of every period of the project, newest first, with the total. Not tracked.</summary>
    public Task<(IReadOnlyList<ProgressSubmission> Items, int TotalCount)> PageSubmissionsAsync(Guid projectId, PageRequest page, CancellationToken cancellationToken);

    /// <summary>The revisions of one period. Not tracked.</summary>
    public Task<IReadOnlyList<ProgressSubmission>> ListRevisionsAsync(Guid reportingCycleId, CancellationToken cancellationToken);

    /// <summary>The project's most recently published revision. Not tracked.</summary>
    public Task<ProgressSubmission?> FindLatestPublishedSubmissionAsync(Guid projectId, CancellationToken cancellationToken);

    public Task<bool> HasOpeningPositionAsync(Guid projectIntakeId, CancellationToken cancellationToken);

    /// <summary>One page of the project's published snapshots, latest first, with the total. Not tracked.</summary>
    public Task<(IReadOnlyList<PublishedProgressSnapshot> Items, int TotalCount)> PageSnapshotsAsync(Guid projectId, PageRequest page, CancellationToken cancellationToken);

    /// <summary>Not tracked.</summary>
    public Task<PublishedProgressSnapshot?> FindLatestSnapshotAsync(Guid projectId, CancellationToken cancellationToken);

    /// <summary>The project's live health row; tracked when <paramref name="track"/>.</summary>
    public Task<ProjectHealthStatus?> FindHealthStatusAsync(Guid projectId, bool track, CancellationToken cancellationToken);

    /// <summary>The reporting periods of the projects, each project's earliest first (FG-01, TASK-069). Not tracked.</summary>
    public Task<IReadOnlyList<ReportingCycle>> ListCyclesAsync(IReadOnlyCollection<Guid> projectIds, CancellationToken cancellationToken);

    /// <summary>The latest published snapshot of each of the projects that has one (FG-01, TASK-069). Not tracked.</summary>
    public Task<IReadOnlyList<PublishedProgressSnapshot>> ListLatestSnapshotsAsync(IReadOnlyCollection<Guid> projectIds, CancellationToken cancellationToken);

    /// <summary>The live health row of each of the projects that has one (FG-01, TASK-069). Not tracked.</summary>
    public Task<IReadOnlyList<ProjectHealthStatus>> ListHealthStatusesAsync(IReadOnlyCollection<Guid> projectIds, CancellationToken cancellationToken);

    public void Add(ReportingCycle cycle);

    public void Add(ProgressSubmission submission);

    public void Add(PublishedProgressSnapshot snapshot);

    public void Add(ProjectHealthStatus healthStatus);

    /// <summary>
    /// Saves the tracked changes and the audit events this unit of work staged. A row changed since it was read, and a
    /// period, revision or opening position another request wrote first, are answers, not faults; after either nothing stays tracked.
    /// </summary>
    public Task<ProgressSaveOutcome> SaveAsync(CancellationToken cancellationToken);
}
