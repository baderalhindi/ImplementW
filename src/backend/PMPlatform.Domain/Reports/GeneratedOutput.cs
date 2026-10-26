using PMPlatform.Domain.Common;

namespace PMPlatform.Domain.Reports;

/// <summary>
/// The file a COMPLETED job produced (FG-02 GeneratedReportOutput GEN-001–015): private storage under an opaque key, its size and SHA-256, its
/// classification and its expiry. <see cref="AuthorizationFootprint"/> records the projects and the cells it revealed, so a download is
/// authorised again on the same rules as the data, by the requester, at download time (TASK-071; api-conventions R-7). The file is purged at
/// expiry; the record stays. Delete policy: RETAIN.
/// </summary>
public sealed class GeneratedOutput : AuditedEntity
{
    public Guid ReportJobId { get; set; }

    /// <summary>Opaque: it names the stored bytes and nothing else, and is never a public path (GEN-008).</summary>
    public required string StorageObjectKey { get; set; }

    /// <summary>Safe for a <c>Content-Disposition</c> header: no path, no control character (US-RPT-SYS-058).</summary>
    public required string FileName { get; set; }

    public required string ContentType { get; set; }

    public long SizeBytes { get; set; }

    /// <summary>Lower-case hex SHA-256 of the stored bytes, checked before every download.</summary>
    public required string ChecksumSha256 { get; set; }

    public OutputSensitivity Sensitivity { get; set; }

    /// <summary>How many report rows the file holds.</summary>
    public int RowCount { get; set; }

    /// <summary>The least current as-of of the values it holds; null when it holds none.</summary>
    public DateTimeOffset? SourceAsOf { get; set; }

    /// <summary>The projects whose values the file holds and, for each, the columns it revealed, as JSON.</summary>
    public required string AuthorizationFootprint { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }

    public GeneratedOutputStatus Status { get; set; }

    public DateTimeOffset? PurgedAt { get; set; }
}
