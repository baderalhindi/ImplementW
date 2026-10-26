using PMPlatform.Application.Features.DocumentManagement.Contracts;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Domain.DocumentManagement;

namespace PMPlatform.Application.Features.DocumentManagement;

/// <summary>
/// Secure upload (CTL-20): the file is checked against the upload policy, stored under a key the platform chooses, hashed
/// as it is stored, and recorded as a version that is SCAN_PENDING until the scanner decides.
/// </summary>
internal sealed class DocumentUploads(IDocumentStorage storage, DocumentUploadPolicy policy)
{
    /// <summary>Null when the file may be stored.</summary>
    public AdministrationError? Check(DocumentFile file)
    {
        ArgumentNullException.ThrowIfNull(file);
        return !storage.IsConfigured ? AdministrationError.Unavailable
            : file.Length > policy.MaxFileSizeBytes ? AdministrationError.PayloadTooLarge
            : !policy.Allows(file.ContentType) ? AdministrationError.UnsupportedMediaType
            : null;
    }

    /// <summary>
    /// Stores the bytes and returns the version, not yet saved. If the save that follows fails, the object stays in the
    /// store unreferenced (document-management.md F-9).
    /// </summary>
    public async Task<DocumentVersion> StoreAsync(Guid documentId, int versionNo, DocumentFile file, Guid uploaderId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        Guid versionId = Guid.CreateVersion7(now);
        string objectKey = $"documents/{documentId:N}/{versionId:N}";
        string mediaType = file.ContentType.Split(';', 2)[0].Trim().ToLowerInvariant();

        await using HashingReadStream hashing = new(file.Content);
        await storage.WriteAsync(objectKey, hashing, mediaType, cancellationToken).ConfigureAwait(false);

        return new DocumentVersion
        {
            Id = versionId,
            DocumentId = documentId,
            VersionNo = versionNo,
            StorageObjectKey = objectKey,
            FileName = DocumentFileNames.Clean(file.FileName),
            ContentType = mediaType,
            SizeBytes = hashing.BytesRead,
            ChecksumSha256 = hashing.Sha256Hex(),
            UploadedByUserId = uploaderId,
            UploadedAt = now,
            ScanState = ScanState.ScanPending,
            CreatedAt = now,
            CreatedBy = uploaderId,
            UpdatedAt = now,
            UpdatedBy = uploaderId,
        };
    }
}
