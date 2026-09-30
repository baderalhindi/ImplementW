namespace PMPlatform.Application.Features.DocumentManagement;

/// <summary>An uploader's file name as stored and offered back on download: the last path segment, without control characters.</summary>
internal static class DocumentFileNames
{
    public const int MaxLength = 500;
    public const string Fallback = "document";

    public static string Clean(string? fileName)
    {
        string name = fileName ?? string.Empty;
        int separator = name.LastIndexOfAny(['/', '\\']);
        name = new string([.. name[(separator + 1)..].Where(c => !char.IsControl(c))]).Trim();
        return name.Length == 0 || name is "." or ".." ? Fallback
            : name.Length <= MaxLength ? name
            : name[..MaxLength];
    }
}
