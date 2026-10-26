using System.Text.Json.Nodes;

namespace PMPlatform.Tests.Integration.Project;

/// <summary>
/// The R-10 diff proven on the committed snapshot: each kind of break, applied to a copy, is reported, and each kind of
/// addition is not. Without this, a diff that reports nothing would pass <see cref="ProjectContractTests"/> unnoticed.
/// A break in a shared schema is reported once, at the first operation that reaches it, so most are matched by their tail.
/// </summary>
public sealed class OpenApiBreakingChangeTests
{
    private const string Item = "/api/v1/projects/{projectId}";

    private static readonly Dictionary<string, (Action<JsonObject> Change, string? Reported)> Changes = new()
    {
        ["operation removed"] = (d => Paths(d)[Item]!.AsObject().Remove("delete"), $"DELETE {Item}: operation removed"),
        ["success status changed"] = (d => Rename(Operation(d, "/api/v1/projects", "post")["responses"]!.AsObject(), "201", "200"), "POST /api/v1/projects: response 201 no longer returned"),
        ["response property removed"] = (d => Properties(d, "ProjectDetail").Remove("formalProjectId"), ".formalProjectId: response property removed"),
        ["property type changed"] = (d => Properties(d, "ProjectDetail")["createdAt"] = new JsonObject { ["type"] = "integer" }, ".createdAt: type string (date-time) became integer"),
        ["property format changed"] = (d => Properties(d, "ProjectDetail")["id"]!.AsObject().Remove("format"), ".id: format uuid became none"),
        ["nullability changed"] = (d => Properties(d, "ProjectDetail")["formalProjectId"]!["type"] = "string", ".formalProjectId: no longer nullable"),
        ["enum value removed"] = (d => RemoveEnumValue(Schemas(d)["ProjectLifecycleState"]!.AsObject(), "ACTIVE"), ".status: enum value \"ACTIVE\" removed"),
        ["response property no longer always present"] = (d => RemoveRequired(Schemas(d)["ProjectDetail"]!.AsObject(), "status"), ".status: no longer always present"),
        ["required request property added"] = (d => AddProperty(Schemas(d)["ProjectSubmitCommand"]!.AsObject(), "reviewNote", required: true), $"POST {Item}/submit request.reviewNote: required request property added"),
        ["required parameter added"] = (d => Operation(d, Item, "get")["parameters"]!.AsArray().Add(new JsonObject { ["name"] = "If-None-Match", ["in"] = "header", ["required"] = true, ["schema"] = new JsonObject { ["type"] = "string" } }), $"GET {Item}: parameter header:If-None-Match is now required"),
        ["request validation tightened"] = (d => Properties(d, "ProjectRequest")["cityItemId"]!["maxLength"] = 10, "request.cityItemId: maxLength lowered to 10"),
        ["optional request property added"] = (d => AddProperty(Schemas(d)["ProjectSubmitCommand"]!.AsObject(), "reviewNote", required: false), null),
        ["response property added"] = (d => AddProperty(Schemas(d)["ProjectDetail"]!.AsObject(), "programmeCode", required: true), null),
        ["enum value added"] = (d => Schemas(d)["ProjectLifecycleState"]!["enum"]!.AsArray().Add("ARCHIVED"), null),
        ["operation added"] = (d => Paths(d)[$"{Item}/archive"] = new JsonObject { ["post"] = Operation(d, $"{Item}/activate", "post").DeepClone() }, null),
        ["another module's operation removed"] = (d => Paths(d).Remove("/api/v1/approval-tasks"), null),
    };

    public static TheoryData<string> Cases => [.. Changes.Keys];

    [Theory]
    [MemberData(nameof(Cases))]
    public void AChangeIsReportedExactlyWhenItBreaksAClient(string change)
    {
        OpenApiDocument snapshot = OpenApiDocument.Snapshot();
        OpenApiDocument changed = OpenApiDocument.Snapshot();
        (Action<JsonObject> apply, string? reported) = Changes[change];
        apply(changed.Root);

        IReadOnlyList<string> breaks = OpenApiBreakingChanges.Find(snapshot, changed, "Project");

        if (reported is null)
        {
            Assert.Empty(breaks);
        }
        else
        {
            Assert.Contains(breaks, b => b.EndsWith(reported, StringComparison.Ordinal));
        }
    }

    [Fact]
    public void TheSnapshotKeepsItsOwnContract() =>
        Assert.Empty(OpenApiBreakingChanges.Find(OpenApiDocument.Snapshot(), OpenApiDocument.Snapshot(), "Project"));

    private static JsonObject Paths(JsonObject document) => document["paths"]!.AsObject();

    private static JsonObject Schemas(JsonObject document) => document["components"]!["schemas"]!.AsObject();

    private static JsonObject Properties(JsonObject document, string schema) => Schemas(document)[schema]!["properties"]!.AsObject();

    private static JsonObject Operation(JsonObject document, string path, string method) => Paths(document)[path]![method]!.AsObject();

    private static void Rename(JsonObject responses, string from, string to)
    {
        JsonNode? response = responses[from];
        responses.Remove(from);
        responses[to] = response;
    }

    private static void RemoveEnumValue(JsonObject schema, string value)
    {
        JsonArray values = schema["enum"]!.AsArray();
        values.Remove(values.Single(v => v!.GetValue<string>() == value));
    }

    private static void RemoveRequired(JsonObject schema, string property)
    {
        JsonArray required = schema["required"]!.AsArray();
        required.Remove(required.Single(r => r!.GetValue<string>() == property));
    }

    private static void AddProperty(JsonObject schema, string name, bool required)
    {
        schema["properties"]![name] = new JsonObject { ["type"] = "string" };
        if (required)
        {
            schema["required"]!.AsArray().Add(name);
        }
    }
}
