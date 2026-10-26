namespace PMPlatform.Domain.Common;

/// <summary>
/// One committed migration import (TASK-102): a workbook of one register, loaded in a single transaction. Infrastructure, not a
/// module, like the outbox: every register's import writes one. Delete policy: APPEND_ONLY.
/// </summary>
public sealed class ImportBatch : AuditedEntity
{
    /// <summary>The register the workbook loaded, e.g. <c>DEPARTMENT</c>.</summary>
    public required string RegisterCode { get; set; }

    /// <summary>The file name as uploaded; evidence only.</summary>
    public required string FileName { get; set; }

    /// <summary>SHA-256 of the uploaded bytes, lower-case hex: which file was loaded, for the cutover reconciliation.</summary>
    public required string FileSha256 { get; set; }

    public int RowCount { get; set; }

    public int CreatedCount { get; set; }

    /// <summary>Rows whose legacy ID an earlier batch already loaded.</summary>
    public int SkippedCount { get; set; }
}
