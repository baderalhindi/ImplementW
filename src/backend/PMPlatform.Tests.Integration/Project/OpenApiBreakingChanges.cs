using System.Text.Json.Nodes;

namespace PMPlatform.Tests.Integration.Project;

/// <summary>
/// api-conventions R-10 as a diff of two documents over one module's operations: what a client built against
/// <c>baseline</c> would find broken in <c>current</c>. Breaking: an operation, a status code, a response property or an
/// enum value removed; a property's type, format or nullability changed; a response property no longer always present; a
/// required request property, parameter or body added; validation tightened on a request value. Additions are not breaks.
/// A change of meaning without a change of shape is not visible in a document, and is left to review (§8.3).
/// </summary>
internal sealed class OpenApiBreakingChanges(OpenApiDocument baseline, OpenApiDocument current)
{
    private enum Direction
    {
        Request,
        Response,
    }

    private readonly List<string> _breaks = [];
    private readonly HashSet<(string, string, Direction)> _compared = [];

    /// <summary>Each break, as <c>where: what</c>; empty when <paramref name="current"/> keeps every promise <paramref name="baseline"/> made.</summary>
    public static IReadOnlyList<string> Find(OpenApiDocument baseline, OpenApiDocument current, string tag)
    {
        OpenApiBreakingChanges diff = new(baseline, current);
        foreach ((string name, string path, JsonObject pathItem, JsonObject operation) in baseline.Operations(tag))
        {
            string method = name[..name.IndexOf(' ', StringComparison.Ordinal)];
            if (current.Operation(path, method) is { } now)
            {
                diff.CompareOperation(name, Parameters(pathItem, operation), operation, Parameters(current.Root["paths"]![path]!.AsObject(), now), now);
            }
            else
            {
                diff.Break(name, "operation removed");
            }
        }

        return diff._breaks;
    }

    private void CompareOperation(string at, Dictionary<string, JsonObject> oldParameters, JsonObject old, Dictionary<string, JsonObject> newParameters, JsonObject now)
    {
        foreach ((string key, JsonObject parameter) in newParameters)
        {
            bool wasRequired = oldParameters.TryGetValue(key, out JsonObject? before) && IsRequired(before);
            if (IsRequired(parameter) && !wasRequired)
            {
                Break(at, $"parameter {key} is now required");
            }

            if (before is not null)
            {
                CompareSchema($"{at} parameter {key}", before["schema"], parameter["schema"], Direction.Request);
            }
        }

        CompareRequestBody(at, old["requestBody"] as JsonObject, now["requestBody"] as JsonObject);

        JsonObject newResponses = now["responses"] as JsonObject ?? [];
        foreach ((string status, JsonNode? response) in old["responses"] as JsonObject ?? [])
        {
            if (newResponses[status] is not JsonObject replacement)
            {
                Break(at, $"response {status} no longer returned");
                continue;
            }

            CompareContent($"{at} {status}", response?["content"] as JsonObject, replacement["content"] as JsonObject, Direction.Response);
        }
    }

    private void CompareRequestBody(string at, JsonObject? old, JsonObject? now)
    {
        bool required = now?["required"]?.GetValue<bool>() == true;
        if (required && old?["required"]?.GetValue<bool>() != true)
        {
            Break(at, "request body is now required");
        }

        if (old is not null && now is not null)
        {
            CompareContent($"{at} request", old["content"] as JsonObject, now["content"] as JsonObject, Direction.Request);
        }
    }

    private void CompareContent(string at, JsonObject? old, JsonObject? now, Direction direction)
    {
        foreach ((string mediaType, JsonNode? media) in old ?? [])
        {
            if (now?[mediaType] is not JsonObject replacement)
            {
                Break(at, $"{mediaType} no longer {(direction == Direction.Request ? "accepted" : "returned")}");
                continue;
            }

            CompareSchema(at, media?["schema"], replacement["schema"], direction);
        }
    }

    private void CompareSchema(string at, JsonNode? oldNode, JsonNode? newNode, Direction direction)
    {
        if (oldNode is null || baseline.Resolve(oldNode) is not { } old)
        {
            return;
        }

        if (newNode is null || current.Resolve(newNode) is not { } now)
        {
            Break(at, $"schema {old.Name ?? "inline"} removed");
            return;
        }

        // A named schema reached again in the same direction was compared already; this also ends recursion.
        if (old.Name is not null && !_compared.Add((old.Name, now.Name ?? "", direction)))
        {
            return;
        }

        if (old.Types.Count > 0 && !old.Types.SetEquals(now.Types))
        {
            Break(at, $"type {old.Describe()} became {now.Describe()}");
        }
        else if (old.Format != now.Format)
        {
            Break(at, $"format {old.Format ?? "none"} became {now.Format ?? "none"}");
        }

        if (old.Nullable != now.Nullable)
        {
            Break(at, old.Nullable ? "no longer nullable" : "now nullable");
        }

        foreach (string value in old.Enum.Except(now.Enum))
        {
            Break(at, $"enum value {value} removed");
        }

        if (direction == Direction.Request)
        {
            CompareValidation(at, old, now);
        }

        CompareProperties(at, old, now, direction);

        if (old.Node["items"] is { } items)
        {
            CompareSchema($"{at}[]", items, now.Node["items"], direction);
        }

        if (old.Node["additionalProperties"] is JsonObject values)
        {
            CompareSchema($"{at}{{*}}", values, now.Node["additionalProperties"], direction);
        }
    }

    private void CompareProperties(string at, Schema old, Schema now, Direction direction)
    {
        foreach ((string name, JsonNode? property) in old.Properties)
        {
            string where = $"{at}.{name}";
            if (now.Properties[name] is not { } replacement)
            {
                if (direction == Direction.Response)
                {
                    Break(where, "response property removed");
                }

                continue;
            }

            if (direction == Direction.Response && old.Required.Contains(name) && !now.Required.Contains(name))
            {
                Break(where, "no longer always present");
            }

            CompareSchema(where, property, replacement, direction);
        }

        if (direction == Direction.Request)
        {
            foreach (string name in now.Required.Except(old.Required))
            {
                Break($"{at}.{name}", "required request property added");
            }
        }
    }

    /// <summary>R-10 "tightening validation on an existing property": a value the old document allowed that the new one refuses.</summary>
    private void CompareValidation(string at, Schema old, Schema now)
    {
        if (now.Node["pattern"]?.GetValue<string>() is { } pattern && pattern != old.Node["pattern"]?.GetValue<string>())
        {
            Break(at, $"pattern {pattern} added or changed");
        }

        if (old.Enum.Count == 0 && now.Enum.Count > 0)
        {
            Break(at, "values restricted to an enum");
        }

        foreach (string bound in new[] { "maxLength", "maxItems", "maximum" })
        {
            if (Number(now, bound) is { } limit && !(Number(old, bound) <= limit))
            {
                Break(at, $"{bound} lowered to {limit}");
            }
        }

        foreach (string bound in new[] { "minLength", "minItems", "minimum" })
        {
            if (Number(now, bound) is { } limit && !(Number(old, bound) >= limit))
            {
                Break(at, $"{bound} raised to {limit}");
            }
        }
    }

    private void Break(string at, string what) => _breaks.Add($"{at}: {what}");

    private static decimal? Number(Schema schema, string keyword) =>
        schema.Node[keyword] is { } value ? decimal.Parse(value.ToJsonString(), System.Globalization.CultureInfo.InvariantCulture) : null;

    private static bool IsRequired(JsonObject parameter) => parameter["required"]?.GetValue<bool>() == true;

    /// <summary>The path item's parameters overridden by the operation's, keyed <c>in:name</c>.</summary>
    private static Dictionary<string, JsonObject> Parameters(JsonObject pathItem, JsonObject operation) =>
        (pathItem["parameters"]?.AsArray() ?? []).Concat(operation["parameters"]?.AsArray() ?? [])
            .OfType<JsonObject>()
            .GroupBy(p => $"{p["in"]}:{p["name"]}")
            .ToDictionary(g => g.Key, g => g.Last());
}
