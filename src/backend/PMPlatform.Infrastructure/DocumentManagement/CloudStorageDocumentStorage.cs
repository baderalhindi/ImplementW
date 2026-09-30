using Google.Cloud.Storage.V1;
using PMPlatform.Application.Features.DocumentManagement;

namespace PMPlatform.Infrastructure.DocumentManagement;

/// <summary>
/// The environment's private Cloud Storage bucket (<c>infra/terraform/storage</c>; CTL-20, ADR-001 C-4), reached with the
/// runtime's service account through Application Default Credentials, so no key is configured. An object is created only
/// if none exists under its name (generation precondition 0), so a version's bytes are never replaced. Not exercised
/// against a bucket yet: no environment exists (document-management.md F-3).
/// </summary>
internal sealed class CloudStorageDocumentStorage(string bucket, string prefix) : IDocumentStorage, IDisposable
{
    private const int BufferSize = 81920;

    private readonly SemaphoreSlim _clientLock = new(1, 1);
    private StorageClient? _client;

    public bool IsConfigured => true;

    public async Task WriteAsync(string objectKey, Stream content, string contentType, CancellationToken cancellationToken)
    {
        StorageClient client = await ClientAsync(cancellationToken).ConfigureAwait(false);
        await client.UploadObjectAsync(bucket, NameOf(objectKey), contentType, content, new UploadObjectOptions { IfGenerationMatch = 0 }, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>The object is downloaded to a temporary file that is deleted when the stream is disposed.</summary>
    public async Task<Stream> OpenReadAsync(string objectKey, CancellationToken cancellationToken)
    {
        StorageClient client = await ClientAsync(cancellationToken).ConfigureAwait(false);
        FileStream buffer = new(
            Path.GetTempFileName(), FileMode.Create, FileAccess.ReadWrite, FileShare.None, BufferSize, FileOptions.DeleteOnClose | FileOptions.Asynchronous);
        try
        {
            await client.DownloadObjectAsync(bucket, NameOf(objectKey), buffer, cancellationToken: cancellationToken).ConfigureAwait(false);
            buffer.Position = 0;
            return buffer;
        }
        catch
        {
            await buffer.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    public void Dispose()
    {
        _client?.Dispose();
        _clientLock.Dispose();
    }

    private string NameOf(string objectKey) => prefix.Length == 0 ? objectKey : $"{prefix}/{objectKey}";

    private async Task<StorageClient> ClientAsync(CancellationToken cancellationToken)
    {
        if (_client is not null)
        {
            return _client;
        }

        await _clientLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return _client ??= await StorageClient.CreateAsync().ConfigureAwait(false);
        }
        finally
        {
            _clientLock.Release();
        }
    }
}
