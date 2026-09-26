using System.Text.RegularExpressions;

namespace PMPlatform.Tests.Integration.Persistence;

/// <summary>A column as <c>erd.dbml</c> declares it; <see cref="Type"/> is as PostgreSQL's <c>format_type</c> prints it.</summary>
internal sealed record ErdColumn(string Name, string Type, bool NotNull, bool Unique, string? References);

/// <summary>A table of <c>erd.dbml</c>, named <c>schema.table</c>, with its columns and its multi-column unique keys.</summary>
internal sealed record ErdTable(string Name, IReadOnlyList<ErdColumn> Columns, IReadOnlyList<IReadOnlyList<string>> UniqueKeys);

/// <summary>
/// Reads <c>docs/architecture/erd.dbml</c> — the canonical ERD (TASK-008). <see cref="Lines"/> gives the tables of the
/// given schemas as sorted text lines in the form <see cref="CoreSchemaTests"/> reads the database: one line per column
/// (type and nullability), per unique key and per foreign key. Column order is not compared.
/// </summary>
internal static partial class ErdModel
{
    private static readonly Lazy<IReadOnlyList<ErdTable>> AllTables = new(Parse);

    public static IReadOnlyList<string> Lines(IReadOnlyCollection<string> schemas)
    {
        List<string> lines = [];
        foreach (ErdTable table in AllTables.Value.Where(t => schemas.Contains(t.Name[..t.Name.IndexOf('.')])))
        {
            foreach (ErdColumn column in table.Columns)
            {
                lines.Add($"column|{table.Name}.{column.Name}|{column.Type}|{(column.NotNull ? "not null" : "null")}");
                if (column.Unique)
                {
                    lines.Add($"unique|{table.Name}|{column.Name}");
                }

                if (column.References is { } target)
                {
                    lines.Add($"fk|{table.Name}.{column.Name}|{target}");
                }
            }

            lines.AddRange(table.UniqueKeys.Select(key => $"unique|{table.Name}|{string.Join(',', key)}"));
        }

        return [.. lines.Order(StringComparer.Ordinal)];
    }

    /// <summary>The table named <c>schema.table</c>.</summary>
    public static ErdTable Table(string name) =>
        AllTables.Value.SingleOrDefault(t => t.Name == name) ?? throw new KeyNotFoundException($"{name} is not a table of erd.dbml.");

    private static List<ErdTable> Parse()
    {
        List<ErdTable> tables = [];
        string? table = null;
        List<ErdColumn> columns = [];
        List<IReadOnlyList<string>> uniqueKeys = [];
        bool inIndexes = false;

        foreach (string line in File.ReadLines(RepositoryFile.Path("docs/architecture/erd.dbml")))
        {
            if (TableStart().Match(line) is { Success: true } start)
            {
                table = $"{start.Groups["schema"].Value}.{start.Groups["table"].Value}";
                columns = [];
                uniqueKeys = [];
                inIndexes = false;
            }
            else if (table is null)
            {
                continue;
            }
            else if (line == "}")
            {
                tables.Add(new ErdTable(table, columns, uniqueKeys));
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
                uniqueKeys.Add(index.Groups["columns"].Value.Split(',', StringSplitOptions.TrimEntries));
            }
            else if (!inIndexes && Column().Match(line) is { Success: true } column)
            {
                columns.Add(ParseColumn(column.Groups["name"].Value, column.Groups["type"].Value, column.Groups["attributes"].Value));
            }
        }

        return tables;
    }

    private static ErdColumn ParseColumn(string name, string type, string attributeText)
    {
        string[] attributes = [.. Note().Replace(attributeText, string.Empty).Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)];
        string? references = null;

        if (attributes.FirstOrDefault(a => a.StartsWith("ref:", StringComparison.Ordinal)) is { } reference)
        {
            string target = reference["ref:".Length..].Trim().TrimStart('>').Trim();
            references = target[..target.LastIndexOf('.')];
        }

        return new ErdColumn(name, PostgreSqlType(type), attributes.Any(a => a is "pk" or "not null"), attributes.Contains("unique"), references);
    }

    /// <summary>The type as PostgreSQL's <c>format_type</c> prints it.</summary>
    private static string PostgreSqlType(string dbmlType) => dbmlType switch
    {
        "timestamptz" => "timestamp with time zone",
        _ when dbmlType.StartsWith("varchar(", StringComparison.Ordinal) => $"character varying{dbmlType["varchar".Length..]}",
        _ when dbmlType.StartsWith("char(", StringComparison.Ordinal) => $"character{dbmlType["char".Length..]}",
        _ => dbmlType,
    };

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
