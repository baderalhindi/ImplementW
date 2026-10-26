using PMPlatform.Domain.Common;

namespace PMPlatform.Domain.DocumentManagement;

/// <summary>
/// One immutable file of a document. A new file is a new version; an existing version's file, name, size and checksum
/// never change, so evidence pinned to it stays what it was (TASK-037). Only the malware scan's result is written
/// after upload. Delete policy: RETAIN.
/// </summary>
public sealed class DocumentVersion : AuditedEntity
{
    public Guid DocumentId { get; set; }

    /// <summary>1 for the first file, then one more than the document's latest.</summary>
    public int VersionNo { get; set; }

    /// <summary>The object's key in the private document store; chosen by the platform, never by the uploader.</summary>
    public required string StorageObjectKey { get; set; }

    /// <summary>The uploader's file name, without any path.</summary>
    public required string FileName { get; set; }

    /// <summary>The media type the uploader declared. Downloads are always served as <c>application/octet-stream</c>.</summary>
    public required string ContentType { get; set; }

    public long SizeBytes { get; set; }

    /// <summary>Lower-case hexadecimal SHA-256 of the stored bytes.</summary>
    public required string ChecksumSha256 { get; set; }

    public Guid UploadedByUserId { get; set; }

    public DateTimeOffset UploadedAt { get; set; }

    public ScanState ScanState { get; set; }

    public DateTimeOffset? ScanCompletedAt { get; set; }

    /// <summary>The scanner's reference for the verdict: its scan id, or the signature it matched.</summary>
    public string? ScanReference { get; set; }
}
