using PMPlatform.Application.Features.DocumentManagement;

namespace PMPlatform.Infrastructure.DocumentManagement;

/// <summary>No <c>DOCUMENT_STORAGE_CONNECTION_STRING</c>: uploads and downloads answer 503 and nothing is scanned.</summary>
internal sealed class UnconfiguredDocumentStorage : IDocumentStorage
{
    public bool IsConfigured => false;

    public Task WriteAsync(string objectKey, Stream content, string contentType, CancellationToken cancellationToken) =>
        throw new InvalidOperationException("No document store is configured.");

    public Task<Stream> OpenReadAsync(string objectKey, CancellationToken cancellationToken) =>
        throw new InvalidOperationException("No document store is configured.");
}
