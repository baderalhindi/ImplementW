using PMPlatform.Domain.Common;

namespace PMPlatform.Domain.Project;

/// <summary>
/// A batch of projects imported from RPMO's workbook (the 2 Nov 2026 review, R3-4): validated, its names mapped and each row's action chosen by
/// the importer, then submitted with a note. A second person approves it, which applies it, or returns it with a reason. The original file
/// is kept with the batch, and <see cref="Plan"/> records what the importer decided, so the approval applies exactly what was submitted. The
/// importer is <see cref="AuditedEntity.CreatedBy"/>. Delete policy: RETAIN.
/// </summary>
public sealed class ProjectImportBatch : AuditedEntity
{
    /// <summary>The batch's number, shown on its projects' Imported label: 1, 2, 3 …</summary>
    public int BatchNo { get; set; }

    public ProjectImportBatchStatus Status { get; set; }

    /// <summary>As uploaded; evidence only.</summary>
    public required string FileName { get; set; }

    public required string FileSha256 { get; set; }

    /// <summary>The workbook as uploaded, attached to the batch.</summary>
    public required byte[] FileContent { get; set; }

    public NarrativeText? SubmissionNote { get; set; }

    /// <summary>The name mappings and the row actions the importer submitted, as JSON.</summary>
    public required string Plan { get; set; }

    public int RowCount { get; set; }

    public int CreateCount { get; set; }

    public int UpdateCount { get; set; }

    public int SkipCount { get; set; }

    public int WarningCount { get; set; }

    /// <summary>Who approved or returned the batch: never its importer.</summary>
    public Guid? DecidedByUserId { get; set; }

    public DateTimeOffset? DecidedAt { get; set; }

    public NarrativeText? ReturnReason { get; set; }

    public Guid? RolledBackByUserId { get; set; }

    public DateTimeOffset? RolledBackAt { get; set; }
}

public enum ProjectImportBatchStatus
{
    /// <summary>Awaiting a second person's decision.</summary>
    Submitted = 1,

    /// <summary>Approved and applied: its projects exist.</summary>
    Approved = 2,

    /// <summary>Returned to the importer with a reason; nothing was applied.</summary>
    Returned = 3,

    /// <summary>Applied, then rolled back: the projects it created are removed.</summary>
    RolledBack = 4,
}
