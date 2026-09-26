using System.Text.RegularExpressions;

namespace PMPlatform.Tests.Integration.Persistence;

/// <summary>One row of the index register in <c>docs/architecture/indexing-strategy.md</c> §4: an index by its key columns.</summary>
internal sealed record SpecifiedIndex(string Id, string Table, IReadOnlyList<string> Columns);

/// <summary>
/// Reads the index register from <c>docs/architecture/indexing-strategy.md</c> (TASK-026), so the document that gives
/// each index its rationale is also the list the tests hold the database to. A row reads
/// <c>| I-01 | `project.project` | `updated_at, id` | … |</c>.
/// </summary>
internal static partial class IndexingStrategy
{
    private static readonly Lazy<IReadOnlyList<SpecifiedIndex>> AllIndexes = new(Parse);

    public static IReadOnlyList<SpecifiedIndex> Indexes => AllIndexes.Value;

    private static List<SpecifiedIndex> Parse() =>
    [
        .. File.ReadLines(RepositoryFile.Path("docs/architecture/indexing-strategy.md"))
            .Select(line => Row().Match(line))
            .Where(row => row.Success)
            .Select(row => new SpecifiedIndex(
                row.Groups["id"].Value,
                row.Groups["table"].Value,
                row.Groups["columns"].Value.Split(',', StringSplitOptions.TrimEntries))),
    ];

    [GeneratedRegex(@"^\| (?<id>I-\d{2}) \| `(?<table>\w+\.\w+)` \| `(?<columns>[\w, ]+)` \|", RegexOptions.CultureInvariant)]
    private static partial Regex Row();
}
