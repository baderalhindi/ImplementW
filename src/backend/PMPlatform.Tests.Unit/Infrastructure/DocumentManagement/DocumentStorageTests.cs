using PMPlatform.Infrastructure.DocumentManagement;

namespace PMPlatform.Tests.Unit.Infrastructure.DocumentManagement;

/// <summary><c>DOCUMENT_STORAGE_CONNECTION_STRING</c> and the local store (TASK-037): a version's object is written once, inside the store.</summary>
public sealed class DocumentStorageTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "pmplatform-documents-" + Guid.NewGuid().ToString("N"));

    [Theory]
    [InlineData("gs://pmplatform-dev-documents", "CloudStorage", "pmplatform-dev-documents", "")]
    [InlineData("gs://pmplatform-dev-documents/wf12/", "CloudStorage", "pmplatform-dev-documents", "wf12")]
    [InlineData("file:///var/lib/pmplatform/documents", "FileSystem", "/var/lib/pmplatform/documents", "")]
    [InlineData("", "None", "", "")]
    [InlineData(null, "None", "", "")]
    public void AConnectionStringNamesABucketOrALocalDirectory(string? value, string kind, string root, string prefix) =>
        Assert.Equal(new DocumentStorageLocation(Enum.Parse<DocumentStorageKind>(kind), root, prefix), DocumentStorageLocation.Parse(value));

    [Theory]
    [InlineData("s3://bucket")]
    [InlineData("gs://Upper-Case")]
    [InlineData("gs://ab")]
    [InlineData("gs://bucket?key=secret")]
    [InlineData("file://relative/path")]
    [InlineData("not a uri")]
    [InlineData("AccountName=x;AccountKey=y")]
    public void AnythingElseIsRefused(string value) => Assert.Null(DocumentStorageLocation.Parse(value));

    [Fact]
    public async Task AnObjectIsWrittenOnceAndReadBack()
    {
        FileSystemDocumentStorage storage = new(_root);
        await using (MemoryStream first = new([1, 2, 3]))
        {
            await storage.WriteAsync("documents/a/b", first, "application/pdf", CancellationToken.None);
        }

        await using (MemoryStream second = new([9]))
        {
            await Assert.ThrowsAsync<IOException>(() => storage.WriteAsync("documents/a/b", second, "application/pdf", CancellationToken.None));
        }

        await using Stream read = await storage.OpenReadAsync("documents/a/b", CancellationToken.None);
        await using MemoryStream copy = new();
        await read.CopyToAsync(copy);
        Assert.Equal([1, 2, 3], copy.ToArray());
    }

    [Fact]
    public async Task ARootGivenWithATrailingSeparatorStoresAllTheSame()
    {
        FileSystemDocumentStorage storage = new(_root + Path.DirectorySeparatorChar);
        await using MemoryStream content = new([7]);

        await storage.WriteAsync("documents/c/d", content, "text/plain", CancellationToken.None);

        Assert.True(File.Exists(Path.Combine(_root, "documents", "c", "d")));
    }

    [Theory]
    [InlineData("../outside")]
    [InlineData("/etc/passwd")]
    public async Task AKeyThatLeavesTheStoreIsRefused(string key)
    {
        FileSystemDocumentStorage storage = new(_root);
        await using MemoryStream content = new([1]);

        await Assert.ThrowsAsync<ArgumentException>(() => storage.WriteAsync(key, content, "text/plain", CancellationToken.None));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
