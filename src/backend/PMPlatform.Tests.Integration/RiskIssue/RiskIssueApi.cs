using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using PMPlatform.Tests.Integration.Identity;
using PMPlatform.Tests.Integration.Project;

namespace PMPlatform.Tests.Integration.RiskIssue;

/// <summary>What the TASK-059 suite shares: the two tags, the fields neither domain takes from a client, and the matrix in force.</summary>
internal static class RiskIssueApi
{
    public const string RiskTag = "Risk";
    public const string ConcernTag = "ManagementConcern";

    /// <summary>
    /// The fields WF-06 and WF-07 compute and never take as input (risk-management.md §4, management-concern.md D-4): a risk's rating,
    /// a concern's severity, the overall impact both derive, and the RISK_MATRIX version each pins. Matched in any request property.
    /// </summary>
    public static readonly Regex ComputedField = new("severity|rating|overallimpact|matrixconfigurationversion", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    /// <summary>
    /// <paramref name="body"/> as a client that tries to set the computed fields sends it: every shape a representation gives them,
    /// at the top level, claiming the severity <paramref name="severityItemId"/> (<paramref name="severityCode"/>), the rating
    /// <paramref name="ratingCode"/>, overall impact 5 and a matrix version that does not exist.
    /// </summary>
    public static JsonObject Claiming(object? body, Guid severityItemId, string severityCode, string ratingCode)
    {
        JsonObject json = body is null ? [] : JsonSerializer.SerializeToNode(body, SessionApi.Json)!.AsObject();
        string version = Guid.NewGuid().ToString();
        json["severityItemId"] = severityItemId.ToString();
        json["severity"] = severityCode;
        json["severityConfigurationVersionId"] = version;
        json["overallImpactLevel"] = 5;
        json["rating"] = new JsonObject { ["id"] = Guid.NewGuid().ToString(), ["code"] = ratingCode, ["label"] = new JsonObject { ["en"] = ratingCode } };
        json["ratingCode"] = ratingCode;
        json["riskRatingDefinitionId"] = Guid.NewGuid().ToString();
        json["matrixConfigurationVersionId"] = version;
        return json;
    }

    /// <summary>The RISK_MATRIX version in force now, the newest published, as FG-04's resolution chooses it.</summary>
    public static async Task<string> MatrixInForceAsync(this IdentityDatabase database) =>
        Assert.Single(await database.QueryAsync("""
            SELECT v.id::text FROM master_data_config.configuration_version v JOIN master_data_config.configuration_family f ON f.id = v.configuration_family_id
            WHERE f.code = 'RISK_MATRIX' AND v.lifecycle_state = 'PUBLISHED' AND v.effective_from <= now() ORDER BY v.effective_from DESC LIMIT 1
            """));

    /// <summary>The GET operations of <paramref name="tag"/> whose answers reach <paramref name="schema"/>, as <c>GET path</c>, in order.</summary>
    public static string[] ReadsServing(this OpenApiDocument document, string tag, string schema) =>
        [.. document.Operations(tag)
            .Where(o => o.Name.StartsWith("GET ", StringComparison.Ordinal) && document.SchemasReachedBy(o.Operation["responses"]!).Contains(schema))
            .Select(o => o.Name)
            .Order(StringComparer.Ordinal)];

    /// <summary>The operations of <paramref name="tag"/> that take a request body, as <c>METHOD path</c>.</summary>
    public static IEnumerable<string> Writes(this OpenApiDocument document, string tag) =>
        document.Operations(tag).Where(o => o.Operation["requestBody"] is not null).Select(o => o.Name);
}
