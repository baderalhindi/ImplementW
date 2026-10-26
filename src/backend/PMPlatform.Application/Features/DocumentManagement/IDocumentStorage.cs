namespace PMPlatform.Application.Features.DocumentManagement;

/// <summary>
/// The private object store behind WF-12 (<c>DOCUMENT_STORAGE_CONNECTION_STRING</c>; CTL-20). Objects are written once and
/// never replaced or deleted by the platform: a version's bytes are its evidence.
/// </summary>
public interface IDocumentStorage
{
    /// <summary>False when no store is configured; uploads and downloads then answer 503.</summary>
    public bool IsConfigured { get; }

    /// <summary>Stores <paramref name="content"/> under a new key. An existing object under the key is never overwritten: that throws.</summary>
    public Task WriteAsync(string objectKey, Stream content, string contentType, CancellationToken cancellationToken);

    /// <summary>The object's bytes. The caller disposes the stream.</summary>
    public Task<Stream> OpenReadAsync(string objectKey, CancellationToken cancellationToken);
}
