using System.Text.RegularExpressions;

namespace PMPlatform.Tests.Integration.Persistence;

/// <summary>
/// Reads the tables of the given schemas from <c>docs/architecture/erd.dbml</c> — the canonical ERD (TASK-008) — as
/// sorted text lines in the form <see cref="CoreSchemaTests"/> reads the database: one line per column (type and
/// nullability), per unique key and per foreign key. Column order is not compared.
/// </summary>
internal static partial class ErdModel
{
    public static IReadOnlyList<string> Lines(IReadOnlyCollection<string> schemas)
    {
        List<string> lines = [];
        string? table = null;
        bool inIndexes = false;

        foreach (string line in File.ReadLines(DbmlPath()))
        {
            if (TableStart().Match(line) is { Success: true } start)
            {
                table = schemas.Contains(start.Groups["schema"].Value) ? $"{start.Groups["schema"].Value}.{start.Groups["table"].Value}" : null;
                inIndexes = false;
            }
            else if (table is null)
            {
                continue;
            }
            else if (line == "}")
            {
                table = null;
            }
            else if (line.Trim() == "indexes {")
            {
                inIndexes = true;
            }
            else if (inIndexes && line.Trim() == "}")
            {
                inIndexes = false;
            }
            else if (inIndexes && UniqueIndex().Match(line) is { Success: true } index)
            {
                lines.Add($"unique|{table}|{string.Join(',', index.Groups["columns"].Value.Split(',', StringSplitOptions.TrimEntries))}");
            }
            else if (!inIndexes && Column().Match(line) is { Success: true } column)
            {
                AddColumn(lines, table, column.Groups["name"].Value, column.Groups["type"].Value, column.Groups["attributes"].Value);
            }
        }

        return [.. lines.Order(StringComparer.Ordinal)];
    }

    private static void AddColumn(List<string> lines, string table, string name, string type, string attributeText)
    {
        string[] attributes = [.. Note().Replace(attributeText, string.Empty).Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)];
        bool notNull = attributes.Any(a => a is "pk" or "not null");

        lines.Add($"column|{table}.{name}|{PostgreSqlType(type)}|{(notNull ? "not null" : "null")}");

        if (attributes.Contains("unique"))
        {
            lines.Add($"unique|{table}|{name}");
        }

        if (attributes.FirstOrDefault(a => a.StartsWith("ref:", StringComparison.Ordinal)) is { } reference)
        {
            string target = reference["ref:".Length..].Trim().TrimStart('>').Trim();
            lines.Add($"fk|{table}.{name}|{target[..target.LastIndexOf('.')]}");
        }
    }

    /// <summary>The type as PostgreSQL's <c>format_type</c> prints it.</summary>
    private static string PostgreSqlType(string dbmlType) => dbmlType switch
    {
        "timestamptz" => "timestamp with time zone",
        _ when dbmlType.StartsWith("varchar(", StringComparison.Ordinal) => $"character varying{dbmlType["varchar".Length..]}",
        _ when dbmlType.StartsWith("char(", StringComparison.Ordinal) => $"character{dbmlType["char".Length..]}",
        _ => dbmlType,
    };

    private static string DbmlPath()
    {
        for (DirectoryInfo? directory = new(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            string candidate = Path.Combine(directory.FullName, "docs", "architecture", "erd.dbml");
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new FileNotFoundException("docs/architecture/erd.dbml not found above the test output directory.");
    }

    [GeneratedRegex(@"^Table (?<schema>\w+)\.(?<table>\w+) \{$", RegexOptions.CultureInvariant)]
    private static partial Regex TableStart();

    [GeneratedRegex(@"^  (?<name>\w+) (?<type>[\w(),]+)(?: \[(?<attributes>.*)\])?$", RegexOptions.CultureInvariant)]
    private static partial Regex Column();

    [GeneratedRegex(@"^\s+\((?<columns>[\w, ]+)\) \[unique\]$", RegexOptions.CultureInvariant)]
    private static partial Regex UniqueIndex();

    /// <summary>A note's text can contain any word, including "unique", so notes are removed before attributes are read.</summary>
    [GeneratedRegex(@"note:\s*'(?:[^'\\]|\\.)*'", RegexOptions.CultureInvariant)]
    private static partial Regex Note();
}
