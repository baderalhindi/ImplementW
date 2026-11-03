using PMPlatform.Domain.Common;

namespace PMPlatform.Domain.Project;

/// <summary>
/// A workbook of more than 200 rows validated in the background (the 2 Nov 2026 review §4, step 3): queued by its importer
/// (<see cref="AuditedEntity.CreatedBy"/>), validated by a worker as the importer may import, and its result kept for them to read; they are
/// notified when it is done. The file is cleared once validated: submitting the batch uploads it again. Delete policy: RETAIN.
/// </summary>
public sealed class ProjectImportValidation : AuditedEntity
{
    public ProjectImportValidationStatus Status { get; set; }

    public required string FileName { get; set; }

    public required byte[] FileContent { get; set; }

    /// <summary>The name mappings and skipped rows the importer validated with, as JSON.</summary>
    public required string Plan { get; set; }

    public int RowCount { get; set; }

    /// <summary>The preview, as JSON, once COMPLETED.</summary>
    public string? Result { get; set; }

    /// <summary>Why it FAILED: an error code the importer reads.</summary>
    public string? FailureCode { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }
}

public enum ProjectImportValidationStatus
{
    Queued = 1,
    Running = 2,
    Completed = 3,
    Failed = 4,
}
