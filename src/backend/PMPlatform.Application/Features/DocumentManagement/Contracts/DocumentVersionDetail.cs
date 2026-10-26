using PMPlatform.Domain.DocumentManagement;

namespace PMPlatform.Application.Features.DocumentManagement.Contracts;

/// <summary>
/// One version (SCR-124). Its content is served only while <see cref="ScanState"/> is CLEAN. The storage key is never
/// part of any representation.
/// </summary>
public sealed record DocumentVersionDetail(
    Guid Id,
    Guid DocumentId,
    int VersionNo,
    string FileName,
    string ContentType,
    long SizeBytes,
    string ChecksumSha256,
    Guid UploadedByUserId,
    DateTimeOffset UploadedAt,
    ScanState ScanState,
    DateTimeOffset? ScanCompletedAt);
