using System.Diagnostics;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using PMPlatform.Tests.Integration.Persistence;

namespace PMPlatform.Tests.Integration.Project;

/// <summary>
/// An OpenAPI 3.1 document as the contract tests read it (api-conventions §9 row 5): the operations one module's tag
/// carries, the schemas they reach, and each schema with its <c>$ref</c> followed and its nullability made explicit.
/// </summary>
internal sealed class OpenApiDocument(JsonObject root)
{
    /// <summary>The committed contract snapshot (api-conventions R-11), updated deliberately in the PR that changes the API.</summary>
    public const string SnapshotPath = "docs/api/openapi.v1.json";

    /// <summary>Set to 1 to rewrite the snapshot from the API as built; the diff of the file is then what review sees.</summary>
    public const string UpdateVariable = "UPDATE_OPENAPI_SNAPSHOT";

    /// <summary>
    /// The TASK-009 lint's findings that no module can clear yet, because the API emits no R-52 extension, no <c>default</c>
    /// response and no response header (project-lifecycle-contract-tests.md F-1).
    /// </summary>
    public static readonly IReadOnlyList<Regex> OpenPlatformFindings =
    [
        KnownFinding(@"C-4: .*: x-module must equal the module tag"),
        KnownFinding(@"C-5: .*: responses\.default \(ProblemDetails\) is required \(R-53\)"),
        KnownFinding(@"C-7: .*: x-write-class must be 'sensitive' or 'non-sensitive' \(R-35\)"),
        KnownFinding(@"C-7: .*: a command endpoint is always a sensitive write \(R-4, R-35\)"),
        KnownFinding(@"C-8: PUT .*: PUT without a required If-Match header \(R-21\)"),
        KnownFinding(@"C-12: .*: X-Correlation-Id response header not declared \(R-41\)"),
        KnownFinding(@"C-12: .*: Location header not declared \(R-5\)"),

        // R-8's binary download, GET .../content (WF-12's versions, FG-02's outputs): the checker reads it as a sub-collection of its resource.
        KnownFinding(@"C-2: /api/v1/[a-z-]+/\{[A-Za-z]+\}(/[a-z-]+/\{[A-Za-z]+\})?/content: a segment after \{id\} must be a POST-only command or a child collection \(R-4\)"),
        KnownFinding(@"C-9: GET /api/v1/[a-z-]+/\{[A-Za-z]+\}(/[a-z-]+/\{[A-Za-z]+\})?/content: (collection GET without page/pageSize or cursor/pageSize \(R-28\)|200 schema is not a page envelope .*)"),
    ];

    private static readonly string[] Methods = ["get", "put", "post", "delete", "patch", "head", "options"];

    private static readonly JsonSerializerOptions Written = new() { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    public JsonObject Root => root;

    public static OpenApiDocument Parse(string json) => new(JsonNode.Parse(json)!.AsObject());

    /// <summary>The committed snapshot, read fresh so a test may change its copy.</summary>
    public static OpenApiDocument Snapshot() => Parse(File.ReadAllText(RepositoryFile.Path(SnapshotPath)));

    /// <summary>The document the API serves, without <c>servers</c>: the host it was fetched from is not part of the contract.</summary>
    public static async Task<OpenApiDocument> FetchAsync(HttpClient client)
    {
        using HttpResponseMessage response = await client.GetAsync("/openapi/v1.json");
        response.EnsureSuccessStatusCode();
        OpenApiDocument document = Parse(await response.Content.ReadAsStringAsync());
        document.Root.Remove("servers");
        return document;
    }

    /// <summary>The document the API serves; with <see cref="UpdateVariable"/> set, also written as the snapshot.</summary>
    public static async Task<OpenApiDocument> BuiltAsync(HttpClient client)
    {
        OpenApiDocument built = await FetchAsync(client);
        if (Environment.GetEnvironmentVariable(UpdateVariable) == "1")
        {
            string snapshot = Path.Combine(Path.GetDirectoryName(RepositoryFile.Path("global.json"))!, SnapshotPath);
            Directory.CreateDirectory(Path.GetDirectoryName(snapshot)!);
            built.Write(snapshot);
        }

        return built;
    }

    /// <summary>
    /// TASK-009's lint, <c>contract-check.py</c>, run over this document: its findings on the operations <paramref name="tag"/>
    /// carries and on the schemas they reach.
    /// </summary>
    public async Task<IReadOnlyList<string>> LintAsync(string tag)
    {
        string document = Path.Combine(AppContext.BaseDirectory, $"openapi.v1.{Guid.NewGuid():N}.json");
        Write(document);
        ProcessStartInfo start = new("python3") { RedirectStandardOutput = true, RedirectStandardError = true };
        start.ArgumentList.Add(RepositoryFile.Path("docs/architecture/contract-check.py"));
        start.ArgumentList.Add(document);
        (int exitCode, string output) = (0, "");
        try
        {
            using Process process = Process.Start(start)!;
            Task<string> standardOutput = process.StandardOutput.ReadToEndAsync();
            Task<string> standardError = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            (exitCode, output) = (process.ExitCode, await standardOutput + await standardError);
        }
        finally
        {
            File.Delete(document);
        }

        Assert.True(exitCode is 0 or 1, $"contract-check.py did not run: {output}");
        Assert.Contains(" operations, ", output, StringComparison.Ordinal);
        string paths = string.Join('|', Operations(tag).Select(o => Regex.Escape(o.Path)).Distinct());
        string schemas = string.Join('|', Surface(tag)["schemas"]!.AsObject().Select(s => Regex.Escape(s.Key)));
        Regex onSurface = new($@"^C-\d+: ((?:[A-Z]+ )?(?:{paths})[: ]|(?:{schemas})[.: ])", RegexOptions.CultureInvariant);
        return [.. output.Split('\n').Where(line => onSurface.IsMatch(line))];
    }

    public void Write(string path) => File.WriteAllText(path, Root.ToJsonString(Written) + "\n");

    /// <summary>Every operation tagged <paramref name="tag"/>, as <c>METHOD path</c> with its path item and operation.</summary>
    public IEnumerable<(string Name, string Path, JsonObject PathItem, JsonObject Operation)> Operations(string tag)
    {
        foreach ((string path, JsonNode? item) in Root["paths"]?.AsObject() ?? [])
        {
            foreach (string method in Methods)
            {
                if (item?[method] is JsonObject operation && operation["tags"]?.AsArray().Any(t => t?.GetValue<string>() == tag) == true)
                {
                    yield return ($"{method.ToUpperInvariant()} {path}", path, item.AsObject(), operation);
                }
            }
        }
    }

    public JsonObject? Operation(string path, string method) => Root["paths"]?[path]?[method.ToLowerInvariant()] as JsonObject;

    /// <summary>
    /// The part of the document <paramref name="tag"/> owns: its operations, and every component schema they reach, by name.
    /// Two documents with equal surfaces describe the same contract for that module.
    /// </summary>
    public JsonObject Surface(string tag)
    {
        JsonObject paths = [];
        foreach ((string name, _, _, JsonObject operation) in Operations(tag))
        {
            paths[name] = operation.DeepClone();
        }

        JsonObject schemas = [];
        foreach (string name in SchemasReachedBy([.. paths.Select(p => p.Value!)]))
        {
            schemas[name] = SchemaNamed(name)?.DeepClone();
        }

        return new JsonObject { ["paths"] = paths, ["schemas"] = schemas };
    }

    /// <summary>The names of every component schema <paramref name="nodes"/> reach, through <c>$ref</c>s followed to any depth.</summary>
    public IReadOnlySet<string> SchemasReachedBy(params JsonNode[] nodes)
    {
        SortedSet<string> reached = [];
        Queue<JsonNode> pending = new(nodes);
        while (pending.TryDequeue(out JsonNode? node))
        {
            foreach (string name in References(node))
            {
                if (reached.Add(name) && SchemaNamed(name) is { } schema)
                {
                    pending.Enqueue(schema);
                }
            }
        }

        return reached;
    }

    /// <summary>
    /// Every property name a client can send in <paramref name="operation"/>'s request body: those of its schema and of every
    /// schema it reaches, at any depth. Empty when the operation takes no body.
    /// </summary>
    public IReadOnlySet<string> RequestPropertyNames(JsonObject operation)
    {
        if (operation["requestBody"] is not JsonObject body)
        {
            return new SortedSet<string>();
        }

        SortedSet<string> names = [];
        Queue<JsonNode> pending = new([body, .. SchemasReachedBy(body).Select(SchemaNamed).OfType<JsonObject>()]);
        while (pending.TryDequeue(out JsonNode? node))
        {
            if (node is JsonObject o)
            {
                foreach ((string key, JsonNode? value) in o)
                {
                    if (key == "properties" && value is JsonObject properties)
                    {
                        names.UnionWith(properties.Select(p => p.Key));
                    }

                    if (value is not null && key != "$ref")
                    {
                        pending.Enqueue(value);
                    }
                }
            }
            else if (node is JsonArray a)
            {
                foreach (JsonNode? item in a.Where(item => item is not null))
                {
                    pending.Enqueue(item!);
                }
            }
        }

        return names;
    }

    /// <summary>
    /// <paramref name="node"/> with each <c>$ref</c> followed and a nullable union — <c>type: [null, …]</c>, or
    /// <c>oneOf</c>/<c>anyOf</c> of <c>{type: null}</c> and one schema — reduced to the schema and a flag. Null when a
    /// reference names no schema.
    /// </summary>
    public Schema? Resolve(JsonNode? node)
    {
        string? name = null;
        bool nullable = false;
        for (int depth = 0; node is JsonObject schema && depth < 32; depth++)
        {
            if (schema["$ref"]?.GetValue<string>() is { } reference)
            {
                name = reference[(reference.LastIndexOf('/') + 1)..];
                node = SchemaNamed(name);
                continue;
            }

            if ((schema["oneOf"] ?? schema["anyOf"]) is JsonArray { Count: 2 } union && union.Count(IsNullType) == 1)
            {
                nullable = true;
                node = union.First(a => !IsNullType(a));
                continue;
            }

            SortedSet<string> types = schema["type"] switch
            {
                JsonArray many => [.. many.Select(t => t!.GetValue<string>())],
                JsonValue one => [one.GetValue<string>()],
                _ => [],
            };
            nullable |= types.Remove("null");
            return new Schema(schema, name, types, nullable);
        }

        return null;
    }

    /// <summary>
    /// Where <paramref name="value"/>, as the API sent it, departs from the schema the document gives it: a JSON type the
    /// schema does not allow, a value outside its enum, a required property missing, or a property it does not document.
    /// </summary>
    public IReadOnlyList<string> Violations(JsonNode? value, JsonNode? schemaNode, string at = "$")
    {
        if (Resolve(schemaNode) is not { } schema)
        {
            return [$"{at}: no schema"];
        }

        if (value is null)
        {
            return schema.Nullable ? [] : [$"{at}: null, documented as not nullable"];
        }

        string kind = value.GetValueKind() switch
        {
            JsonValueKind.Object => "object",
            JsonValueKind.Array => "array",
            JsonValueKind.String => "string",
            JsonValueKind.True or JsonValueKind.False => "boolean",
            JsonValueKind.Number => decimal.IsInteger(value.GetValue<decimal>()) && schema.Types.Contains("integer") ? "integer" : "number",
            JsonValueKind.Undefined or JsonValueKind.Null => "null",
            _ => throw new ArgumentOutOfRangeException(nameof(value)),
        };
        if (schema.Types.Count > 0 && !schema.Types.Contains(kind))
        {
            return [$"{at}: {kind}, documented as {schema.Describe()}"];
        }

        List<string> violations = [];
        if (schema.Enum.Count > 0 && !schema.Enum.Contains(value.ToJsonString()))
        {
            violations.Add($"{at}: {value.ToJsonString()} is not one of the documented values");
        }

        if (value is JsonObject body)
        {
            violations.AddRange(schema.Required.Where(name => !body.ContainsKey(name)).Select(name => $"{at}.{name}: required, absent"));
            foreach ((string name, JsonNode? property) in body)
            {
                violations.AddRange(schema.Properties.ContainsKey(name)
                    ? Violations(property, schema.Properties[name], $"{at}.{name}")
                    : [$"{at}.{name}: not documented"]);
            }
        }
        else if (value is JsonArray items)
        {
            violations.AddRange(items.SelectMany((item, i) => Violations(item, schema.Node["items"], $"{at}[{i}]")));
        }

        return violations;
    }

    private JsonObject? SchemaNamed(string name) => Root["components"]?["schemas"]?[name] as JsonObject;

    private static bool IsNullType(JsonNode? node) => node is JsonObject { Count: 1 } only && only["type"]?.GetValueKind() == JsonValueKind.String && only["type"]!.GetValue<string>() == "null";

    private static IEnumerable<string> References(JsonNode node) => node switch
    {
        JsonObject o => o.SelectMany(p => p.Key == "$ref" && p.Value is JsonValue v
            ? [v.GetValue<string>()[(v.GetValue<string>().LastIndexOf('/') + 1)..]]
            : p.Value is null ? [] : References(p.Value)),
        JsonArray a => a.Where(e => e is not null).SelectMany(e => References(e!)),
        _ => [],
    };

    private static Regex KnownFinding(string pattern) => new($"^{pattern}$", RegexOptions.CultureInvariant);
}

/// <summary>A schema with its reference followed: its component name if it has one, its JSON types without <c>null</c>, and whether null is allowed.</summary>
internal sealed record Schema(JsonObject Node, string? Name, IReadOnlySet<string> Types, bool Nullable)
{
    public string? Format => Node["format"]?.GetValue<string>();

    public IReadOnlyList<string> Enum => Node["enum"]?.AsArray().Select(v => v?.ToJsonString() ?? "null").ToList() ?? [];

    public IReadOnlySet<string> Required => Node["required"]?.AsArray().Select(r => r!.GetValue<string>()).ToHashSet() ?? [];

    public JsonObject Properties => Node["properties"] as JsonObject ?? [];

    public string Describe() => Types.Count == 0 ? Name ?? "any" : string.Join('|', Types) + (Format is null ? "" : $" ({Format})");
}
