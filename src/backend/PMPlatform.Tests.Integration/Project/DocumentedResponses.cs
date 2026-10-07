using System.Globalization;
using System.Text.Json.Nodes;
using PMPlatform.Tests.Integration.Identity;

namespace PMPlatform.Tests.Integration.Project;

/// <summary>
/// api-conventions R-55's replay, in process: each response a contract test receives, checked against the schema its operation
/// documents for that status, and recorded by operation, so the test can assert that every operation of a tag was called.
/// </summary>
internal sealed class DocumentedResponses(OpenApiDocument document)
{
    private readonly Dictionary<string, IReadOnlyList<string>> _checked = [];

    /// <summary>The operations checked so far, as <c>METHOD path</c>.</summary>
    public IEnumerable<string> Operations => _checked.Keys;

    /// <summary>Where a response departed from its documented schema; empty when every response matched.</summary>
    public IEnumerable<string> Violations => _checked.Values.SelectMany(v => v);

    /// <summary>
    /// Checks <paramref name="response"/> as the answer of <paramref name="method"/> <paramref name="template"/> and returns its
    /// body, or null when it has none. A status the operation does not document fails at once.
    /// </summary>
    public async Task<JsonObject?> CheckAsync(string method, string template, HttpResponseMessage response)
    {
        string name = $"{method} {template}";
        string status = ((int)response.StatusCode).ToString(CultureInfo.InvariantCulture);
        JsonObject? documented = document.Operation(template, method)?["responses"]?[status] as JsonObject;
        Assert.True(documented is not null, $"{name} answered {status}, which it does not document: {await response.Content.ReadAsStringAsync()}");
        JsonObject? body = response.Content.Headers.ContentLength == 0 ? null : await response.ReadObjectAsync();
        _checked[name] = body is null
            ? documented["content"] is null ? [] : [$"{name} {status}: no body, documented with one"]
            : document.Violations(body, documented["content"]?["application/json"]?["schema"], $"{name} {status}");
        return body;
    }
}
