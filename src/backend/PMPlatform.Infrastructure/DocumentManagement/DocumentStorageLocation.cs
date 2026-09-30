namespace PMPlatform.Infrastructure.DocumentManagement;

/// <summary>
/// <c>DOCUMENT_STORAGE_CONNECTION_STRING</c>, parsed (document-management.md D-10). <c>gs://&lt;bucket&gt;[/&lt;prefix&gt;]</c>
/// names the environment's Cloud Storage bucket, reached with the runtime's own service account, so the value holds no
/// credential; <c>file:///&lt;absolute path&gt;</c> is a local directory for DEV and tests. Empty means not configured.
/// </summary>
internal sealed record DocumentStorageLocation(DocumentStorageKind Kind, string Root, string Prefix)
{
    public static DocumentStorageLocation None { get; } = new(DocumentStorageKind.None, string.Empty, string.Empty);

    /// <summary>The location, or null if <paramref name="value"/> is set but is neither form.</summary>
    public static DocumentStorageLocation? Parse(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? None
            : !Uri.TryCreate(value.Trim(), UriKind.Absolute, out Uri? uri) ? null
            : uri.Scheme switch
            {
                "gs" when IsBucketName(BucketAsWritten(value)) && string.IsNullOrEmpty(uri.Query) =>
                    new DocumentStorageLocation(DocumentStorageKind.CloudStorage, uri.Host, uri.AbsolutePath.Trim('/')),
                "file" when uri.IsFile && Path.IsPathFullyQualified(uri.LocalPath) =>
                    new DocumentStorageLocation(DocumentStorageKind.FileSystem, uri.LocalPath, string.Empty),
                _ => null,
            };
    }

    /// <summary><see cref="Uri"/> lower-cases the host, which would silently name another bucket; the rule is checked on the text.</summary>
    private static string BucketAsWritten(string value) => value.Trim()["gs://".Length..].Split('/', 2)[0];

    /// <summary>Cloud Storage's rule: 3–63 lower-case letters, digits, hyphens, underscores and dots, starting and ending alphanumeric.</summary>
    private static bool IsBucketName(string name) =>
        name.Length is >= 3 and <= 63
        && name.All(c => char.IsAsciiLetterLower(c) || char.IsAsciiDigit(c) || c is '-' or '_' or '.')
        && char.IsAsciiLetterOrDigit(name[0]) && char.IsAsciiLetterOrDigit(name[^1]);
}

internal enum DocumentStorageKind
{
    None = 0,
    FileSystem = 1,
    CloudStorage = 2,
}

/// <summary><c>DOCUMENT_STORAGE_CONNECTION_STRING</c> as options, so a malformed value stops the API at start-up without the value being logged.</summary>
internal sealed class DocumentStorageOptions
{
    public string? ConnectionString { get; set; }

    public DocumentStorageLocation? Location => DocumentStorageLocation.Parse(ConnectionString);
}
