using PMPlatform.Application.Features.DocumentManagement;

namespace PMPlatform.Infrastructure.DocumentManagement;

/// <summary>
/// A local directory as the document store, for DEV and tests (<c>file:///…</c>). An object is created once and never
/// replaced. Keys are the platform's own (<c>documents/&lt;document&gt;/&lt;version&gt;</c>); one that would resolve outside the
/// root is refused all the same.
/// </summary>
internal sealed class FileSystemDocumentStorage(string root) : IDocumentStorage
{
    private const int BufferSize = 81920;

    private readonly string _root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));

    public bool IsConfigured => true;

    public async Task WriteAsync(string objectKey, Stream content, string contentType, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);
        string path = PathOf(objectKey);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        FileStream file = new(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, BufferSize, FileOptions.Asynchronous);
        await using (file.ConfigureAwait(false))
        {
            await content.CopyToAsync(file, cancellationToken).ConfigureAwait(false);
        }
    }

    public Task<Stream> OpenReadAsync(string objectKey, CancellationToken cancellationToken) =>
        Task.FromResult<Stream>(new FileStream(PathOf(objectKey), FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize, FileOptions.Asynchronous));

    private string PathOf(string objectKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(objectKey);
        string path = Path.GetFullPath(Path.Combine(_root, objectKey));
        return path.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            ? path
            : throw new ArgumentException("The object key leaves the document store.", nameof(objectKey));
    }
}
