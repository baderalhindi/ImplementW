using System.Text.Json;

namespace PMPlatform.Tests.Integration.Persistence;

/// <summary>A node of a PostgreSQL <c>EXPLAIN (FORMAT JSON)</c> plan.</summary>
internal sealed record PlanNode(string NodeType, string? RelationName, string? IndexName);

internal static class QueryPlan
{
    /// <summary>Every node of <paramref name="plan"/>, the <c>Plan</c> element of an <c>EXPLAIN (FORMAT JSON)</c> result, depth first.</summary>
    public static List<PlanNode> Nodes(JsonElement plan)
    {
        List<PlanNode> nodes = [];
        Collect(plan, nodes);
        return nodes;
    }

    private static void Collect(JsonElement node, List<PlanNode> nodes)
    {
        nodes.Add(new PlanNode(
            node.GetProperty("Node Type").GetString()!,
            node.TryGetProperty("Relation Name", out JsonElement relation) ? relation.GetString() : null,
            node.TryGetProperty("Index Name", out JsonElement index) ? index.GetString() : null));
        if (node.TryGetProperty("Plans", out JsonElement children))
        {
            foreach (JsonElement child in children.EnumerateArray())
            {
                Collect(child, nodes);
            }
        }
    }
}
