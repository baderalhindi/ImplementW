using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Domain.Common;

namespace PMPlatform.Application.Features.Progress.Contracts;

/// <summary>
/// WF-02 progress reporting (TASK-044): reporting periods, the submission of a period's progress, its review and
/// publication, and the Overall Project Health that WF-02 alone computes (ICD-03). Progress is derived, never typed in at
/// project level (ADR-009); publishing writes an immutable snapshot and changes no earlier one. Each operation is decided
/// by the authorization engine on the project's anchors. A collection is of one project, and is empty for a project the
/// caller may not see; a submission the caller may not see is 404 (R-47).
/// </summary>
public interface IProgressService
{
    /// <summary>The project's reporting periods, earliest first. Reading generates nothing.</summary>
    public Task<ReportingCyclePage> ListCyclesAsync(Guid callerId, Guid projectId, PageRequest page, CancellationToken cancellationToken);

    /// <summary>SCR-070: every revision of every period, newest first.</summary>
    public Task<ProgressSubmissionPage> ListSubmissionsAsync(Guid callerId, Guid projectId, PageRequest page, CancellationToken cancellationToken);

    public Task<AdministrationResult<Versioned<ProgressSubmissionDetail>>> GetSubmissionAsync(Guid callerId, Guid submissionId, CancellationToken cancellationToken);

    /// <summary>
    /// Starts the progress update of an ACTIVE project's earliest OPEN period that has begun, generating the periods due
    /// from its governance profile's cadence. The new DRAFT carries the derived actual and planned figures and is pre-filled
    /// from the last published period, so confirming is the default action (ADR-017).
    /// </summary>
    public Task<AdministrationResult<Versioned<ProgressSubmissionDetail>>> StartAsync(Guid callerId, Guid projectId, CancellationToken cancellationToken);

    /// <summary>Replaces the narrative and the override of a DRAFT. Requires the caller's version (R-21).</summary>
    public Task<AdministrationResult<Versioned<ProgressSubmissionDetail>>> UpdateAsync(
        Guid callerId, Guid submissionId, ProgressSubmissionChanges changes, uint expectedVersion, CancellationToken cancellationToken);

    /// <summary>DRAFT → SUBMITTED: the derived figures are calculated again and fixed, and the live health recomputed.</summary>
    public Task<AdministrationResult<Versioned<ProgressSubmissionDetail>>> SubmitAsync(Guid callerId, Guid submissionId, uint? expectedVersion, CancellationToken cancellationToken);

    /// <summary>SUBMITTED → UNDER_REVIEW, by AHDA. An external user, or the person who submitted it, is refused.</summary>
    public Task<AdministrationResult<Versioned<ProgressSubmissionDetail>>> StartReviewAsync(Guid callerId, Guid submissionId, uint? expectedVersion, CancellationToken cancellationToken);

    /// <summary>UNDER_REVIEW → RETURNED with a reason, opening revision + 1 as a DRAFT pre-filled from this one.</summary>
    public Task<AdministrationResult<Versioned<ProgressSubmissionDetail>>> ReturnAsync(
        Guid callerId, Guid submissionId, NarrativeText reason, uint? expectedVersion, CancellationToken cancellationToken);

    /// <summary>
    /// UNDER_REVIEW → PUBLISHED, by AHDA: writes the period's immutable snapshot with the Overall Project Health computed
    /// under the health rule in force, closes the period, and recomputes the live health.
    /// </summary>
    public Task<AdministrationResult<Versioned<ProgressSubmissionDetail>>> PublishAsync(Guid callerId, Guid submissionId, uint? expectedVersion, CancellationToken cancellationToken);

    /// <summary>The project's PUBLISHED/OFFICIAL snapshots, with their Overall Project Health, latest first.</summary>
    public Task<PublishedProgressSnapshotPage> ListSnapshotsAsync(Guid callerId, Guid projectId, PageRequest page, CancellationToken cancellationToken);

    /// <summary>The project's CURRENT/LIVE Overall Project Health.</summary>
    public Task<ProjectHealthStatusPage> ListHealthStatusesAsync(Guid callerId, Guid projectId, PageRequest page, CancellationToken cancellationToken);
}
