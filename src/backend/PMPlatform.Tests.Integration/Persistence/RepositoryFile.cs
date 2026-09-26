namespace PMPlatform.Tests.Integration.Persistence;

/// <summary>Finds a file checked into the repository from the test output directory.</summary>
internal static class RepositoryFile
{
    /// <summary>The absolute path of <paramref name="relativePath"/>, e.g. <c>docs/architecture/erd.dbml</c>.</summary>
    public static string Path(string relativePath)
    {
        for (DirectoryInfo? directory = new(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            string candidate = System.IO.Path.Combine(directory.FullName, relativePath);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new FileNotFoundException($"{relativePath} not found above the test output directory.");
    }
}
