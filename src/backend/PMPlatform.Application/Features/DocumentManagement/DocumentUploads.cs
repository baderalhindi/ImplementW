using PMPlatform.Application.Features.DocumentManagement.Contracts;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Domain.DocumentManagement;

namespace PMPlatform.Application.Features.DocumentManagement;

/// <summary>
/// Secure upload (CTL-20): the file is checked against the upload policy and its content rules, stored under a key the platform chooses, hashed
/// as it is stored, and recorded as a version that is SCAN_PENDING until the scanner decides.
/// </summary>
internal sealed class DocumentUploads(IDocumentStorage storage, DocumentUploadPolicy policy)
{
    /// <summary>
    /// The file to store when it may be stored: its size, declared media type, file-name extension and leading bytes all
    /// agree with the policy and <see cref="DocumentContentRules"/> (SR-38). The leading bytes are read here, so the file
    /// returned replays them before the rest of the original stream; store that one, not the original.
    /// </summary>
    public async Task<AdministrationResult<DocumentFile>> CheckAsync(DocumentFile file, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(file);
        if (!storage.IsConfigured)
        {
            return AdministrationError.Unavailable;
        }

        if (file.Length > policy.MaxFileSizeBytes)
        {
            return AdministrationError.PayloadTooLarge;
        }

        string mediaType = MediaTypeOf(file.ContentType);
        if (!policy.Allows(mediaType) || !DocumentContentRules.ExtensionMatches(mediaType, DocumentFileNames.Clean(file.FileName)))
        {
            return AdministrationError.UnsupportedMediaType;
        }

        byte[] header = new byte[DocumentContentRules.HeaderLength];
        int read = await file.Content.ReadAtLeastAsync(header, header.Length, throwOnEndOfStream: false, cancellationToken).ConfigureAwait(false);
        return DocumentContentRules.ContentMatches(mediaType, header.AsSpan(0, read))
            ? file with { Content = new PrefixedReadStream(header.AsMemory(0, read), file.Content) }
            : AdministrationError.UnsupportedMediaType;
    }

    /// <summary>
    /// Stores the bytes and returns the version, not yet saved. If the save that follows fails, the object stays in the
    /// store unreferenced (document-management.md F-9).
    /// </summary>
    public async Task<DocumentVersion> StoreAsync(Guid documentId, int versionNo, DocumentFile file, Guid uploaderId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        Guid versionId = Guid.CreateVersion7(now);
        string objectKey = $"documents/{documentId:N}/{versionId:N}";
        string mediaType = MediaTypeOf(file.ContentType);

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

    /// <summary>The declared media type without parameters, in lower case.</summary>
    private static string MediaTypeOf(string contentType) => contentType.Split(';', 2)[0].Trim().ToLowerInvariant();
}
