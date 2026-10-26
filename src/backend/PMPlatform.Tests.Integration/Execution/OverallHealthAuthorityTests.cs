using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using PMPlatform.Application.Features.Progress.Contracts;
using PMPlatform.Tests.Integration.FinancialKpi;
using OpenApiDocument = PMPlatform.Tests.Integration.Project.OpenApiDocument;

namespace PMPlatform.Tests.Integration.Execution;

/// <summary>
/// Separation 1 (ICD-03, progress-update.md D-13, M-12): Overall Project Health is computed by WF-02 alone and never recomputed
/// elsewhere. <c>HealthOwnershipTests</c> (unit, architecture) fails when another module reaches WF-02's calculation; these
/// fail when another module holds or serves a health of its own, however it computed it, and when WF-14's figures — the input
/// WF-02 rates — make anyone but WF-02 write a health.
/// </summary>
[Collection(FinancialKpiSuite.Name)]
public sealed class OverallHealthAuthorityTests(FinancialKpiTestHost host)
{
    /// <summary>
    /// Every column whose name says health, in every schema. WF-02 stores Overall Health live and as published, with the Schedule
    /// Health it was computed from copied into the snapshot; WF-03 stores its own Schedule Health. Nothing else may.
    /// </summary>
    private static readonly string[] HealthColumns =
    [
        "progress.project_health_status.health_rule_configuration_version_id",
        "progress.project_health_status.overall_health",
        "progress.published_progress_snapshot.health_rule_configuration_version_id",
        "progress.published_progress_snapshot.overall_health",
        "progress.published_progress_snapshot.schedule_health",
        "schedule.schedule_health_status.health_rule_configuration_version_id",
        "schedule.schedule_health_status.schedule_health",
    ];

    /// <summary>Every response or request property whose name says health, by the module whose operations reach it.</summary>
    private static readonly Dictionary<string, string[]> HealthProperties = new()
    {
        ["Progress"] = ["healthRuleConfigurationVersionId", "overallHealth", "scheduleHealth"],
        ["Schedule"] = ["healthRuleConfigurationVersionId", "scheduleHealth"],
    };

    private static readonly Regex Health = new("health", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    [Fact]
    public async Task OnlyWf02AndWf03StoreAHealth()
    {
        IReadOnlyList<string> columns = await host.Database.QueryAsync("""
            SELECT table_schema || '.' || table_name || '.' || column_name FROM information_schema.columns
            WHERE column_name ~* 'health' AND table_schema NOT IN ('pg_catalog', 'information_schema')
            ORDER BY 1
            """);

        Assert.True(columns.SequenceEqual(HealthColumns), $"""
            Only WF-02 stores Overall Health and only WF-03 its Schedule Health (progress-update.md D-13, schedule-baseline.md D-17).
            Expected exactly: {string.Join(", ", HealthColumns)}
            Found:            {string.Join(", ", columns)}
            A consumer reads IProjectHealthReader or IScheduleHealthReader and stores no health of its own.
            """);
    }

    [Fact]
    public async Task OnlyWf02ServesOverallHealth()
    {
        using HttpClient client = host.Api.CreateClient();
        OpenApiDocument document = await OpenApiDocument.FetchAsync(client);
        IEnumerable<string> tags = document.Root["paths"]!.AsObject()
            .SelectMany(p => p.Value!.AsObject().Select(o => o.Value?["tags"]?.AsArray()))
            .SelectMany(t => t ?? [])
            .Select(t => t!.GetValue<string>())
            .Distinct();

        Dictionary<string, string[]> served = tags
            .Select(tag => (tag, Properties(document.Surface(tag)).Where(p => Health.IsMatch(p)).Distinct().Order(StringComparer.Ordinal).ToArray()))
            .Where(t => t.Item2.Length > 0)
            .ToDictionary(t => t.tag, t => t.Item2);

        Assert.True(served.Count == HealthProperties.Count && served.All(s => HealthProperties.TryGetValue(s.Key, out string[]? allowed) && s.Value.SequenceEqual(allowed)), $"""
            Only WF-02 serves Overall Health and only WF-03 its Schedule Health; every other API renders neither (M-12).
            Found: {string.Join("; ", served.Select(s => $"{s.Key}: {string.Join(", ", s.Value)}"))}
            """);
    }

    /// <summary>
    /// WF-14 publishes the financial status WF-02 rates and keeps its periods aligned with WF-02's (edge 13). Publishing it writes
    /// no Overall Health, live or published: only WF-02's own publication does.
    /// </summary>
    [Fact]
    public async Task PublishingWf14FiguresComputesNoOverallHealth()
    {
        using HttpClient client = host.Api.CreateClient();
        Sessions sessions = await client.SignInAsync();
        Guid projectId = await host.ProjectAsync();
        await host.PeriodsAsync(projectId, 1);
        await host.ActiveBudgetAsync(client, sessions, projectId, "1000000.00");

        await client.PublishedUpdateAsync(sessions, projectId, FinancialKpiDriver.Update("400000.00", "1200000.00", "MEASURED"));

        JsonObject snapshot = Assert.Single(await client.ItemsAsync(sessions.Portfolio, FinancialKpiDriver.Snapshots, $"projectId={projectId}"))!.AsObject();
        Assert.Equal("RED", snapshot.Text("financialStatus"));
        Assert.Equal(["0 0"], await host.Database.QueryAsync($"""
            SELECT (SELECT count(*) FROM progress.project_health_status WHERE project_id = '{projectId}') || ' ' ||
                   (SELECT count(*) FROM progress.published_progress_snapshot WHERE project_id = '{projectId}')
            """));
        await using AsyncServiceScope scope = host.Api.Services.CreateAsyncScope();
        Assert.Equal(new ProjectHealthView(null, null), await scope.ServiceProvider.GetRequiredService<IProjectHealthReader>().GetAsync(projectId, CancellationToken.None));
    }

    private static IEnumerable<string> Properties(JsonNode? node) => node switch
    {
        JsonObject o => (o["properties"] is JsonObject properties ? properties.Select(p => p.Key) : [])
            .Concat(o.SelectMany(p => Properties(p.Value))),
        JsonArray a => a.SelectMany(Properties),
        _ => [],
    };
}
