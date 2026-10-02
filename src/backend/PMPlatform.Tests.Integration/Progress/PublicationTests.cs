using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using PMPlatform.Application.Features.Progress.Contracts;
using PMPlatform.Domain.Progress;
using PMPlatform.Tests.Integration.Identity;

namespace PMPlatform.Tests.Integration.Progress;

/// <summary>
/// TASK-044's acceptance criteria: Overall Project Health is computed and stored by WF-02 at publication; a consumer reads the
/// published value and never recalculates it; publishing a period never alters an earlier published snapshot.
/// </summary>
[Collection(ProgressSuite.Name)]
public sealed class PublicationTests(ProgressTestHost host)
{
    /// <summary>
    /// Acceptance criterion 3, the immutability test: a second publication leaves the first snapshot — and the submission it
    /// published — the same to the byte, and the database refuses to update, delete or truncate a snapshot whoever asks.
    /// </summary>
    [Fact]
    public async Task PublishingAPeriodNeverAltersAnEarlierPublishedSnapshot()
    {
        using HttpClient client = host.Api.CreateClient();
        ProgressSessions sessions = await client.SignInAsync();
        Guid projectId = await host.ProjectAsync(activatedDaysAgo: 10);
        host.Inputs.Set(projectId, actualPercent: 40, baselineStart: ProgressDriver.Today.AddDays(-49));

        JsonObject first = await client.PublishedPeriodAsync(sessions.ProjectManager, sessions.Reviewer, projectId);
        JsonObject firstSnapshot = Assert.Single(await client.ItemsAsync(sessions.Reviewer, ProgressDriver.Snapshots, projectId))!.AsObject();
        Guid firstSnapshotId = AdministrationApi.IdOf(firstSnapshot);
        string snapshotBefore = await host.RowAsync("published_progress_snapshot", firstSnapshotId);
        string submissionBefore = await host.RowAsync("progress_submission", AdministrationApi.IdOf(first));

        // Everything the next publication reads has moved: the work, the schedule, the finances.
        host.Inputs.Set(projectId, actualPercent: 90, baselineStart: ProgressDriver.Today.AddDays(-49), schedule: HealthStatus.Red, financial: HealthStatus.Amber);
        await client.PublishedPeriodAsync(sessions.ProjectManager, sessions.Reviewer, projectId);

        JsonArray snapshots = await client.ItemsAsync(sessions.Reviewer, ProgressDriver.Snapshots, projectId);
        Assert.Equal(2, snapshots.Count);
        Assert.Equal((90m, "RED"), (snapshots[0]!.Percent("actualPercent"), snapshots[0]!["overallHealth"]!.GetValue<string>()));
        Assert.Equal(firstSnapshot.ToJsonString(), snapshots[1]!.ToJsonString());
        Assert.Equal(snapshotBefore, await host.RowAsync("published_progress_snapshot", firstSnapshotId));
        Assert.Equal(submissionBefore, await host.RowAsync("progress_submission", AdministrationApi.IdOf(first)));

        foreach (string change in new[]
        {
            $"UPDATE progress.published_progress_snapshot SET overall_health = 'GREEN' WHERE id = '{firstSnapshotId}'",
            $"UPDATE progress.published_progress_snapshot SET actual_percent = 99, updated_at = created_at WHERE id = '{firstSnapshotId}'",
            $"DELETE FROM progress.published_progress_snapshot WHERE id = '{firstSnapshotId}'",
            "TRUNCATE progress.published_progress_snapshot CASCADE",
            $"UPDATE progress.progress_submission SET narrative = 'rewritten', narrative_lang = 'en' WHERE id = '{AdministrationApi.IdOf(first)}'",
        })
        {
            PostgresException refused = await Assert.ThrowsAsync<PostgresException>(() => host.Database.ExecuteRolledBackAsync(change));
            Assert.Equal(PostgresErrorCodes.RestrictViolation, refused.SqlState);
        }

        Assert.Equal(snapshotBefore, await host.RowAsync("published_progress_snapshot", firstSnapshotId));
    }

    /// <summary>
    /// Acceptance criterion 1: the health is computed by WF-02 at publication — 10 points behind plan is AMBER under the
    /// configured thresholds — stored with the rule version that computed it, and the live projection is stored beside it.
    /// </summary>
    [Fact]
    public async Task OverallHealthIsComputedAndStoredByWf02AtPublication()
    {
        using HttpClient client = host.Api.CreateClient();
        ProgressSessions sessions = await client.SignInAsync();
        Guid projectId = await host.ProjectAsync();
        host.Inputs.Set(projectId, actualPercent: 40, baselineStart: ProgressDriver.Today.AddDays(-49));

        JsonObject published = await client.PublishedPeriodAsync(sessions.ProjectManager, sessions.Reviewer, projectId);

        Assert.Equal(("PUBLISHED", 40m, 50m), (published.Status(), published.Percent("actualPercent"), published.Percent("plannedPercent")));
        JsonNode snapshot = Assert.Single(await client.ItemsAsync(sessions.ProjectManager, ProgressDriver.Snapshots, projectId))!;
        Assert.Equal(
            ("AMBER", "GREEN", "GREEN", ProgressTestHost.WorkflowPolicyVersionId.ToString(), false),
            (snapshot["overallHealth"]!.GetValue<string>(), snapshot["scheduleHealth"]!.GetValue<string>(), snapshot["financialStatus"]!.GetValue<string>(),
             snapshot["healthRuleConfigurationVersionId"]!.GetValue<string>(), snapshot["isOverridden"]!.GetValue<bool>()));

        JsonNode live = Assert.Single(await client.ItemsAsync(sessions.ProjectManager, ProgressDriver.HealthStatuses, projectId))!;
        Assert.Equal(("AMBER", 40m, 50m), (live["overallHealth"]!.GetValue<string>(), live.Percent("actualPercent"), live.Percent("plannedPercent")));
        Assert.Contains("Progress.ProgressPublished", await host.AuditEventsAsync(AdministrationApi.IdOf(published)));
    }

    /// <summary>
    /// Acceptance criterion 2: what a consumer reads through ICD-03 is the stored published value. When the inputs and the
    /// health rule itself change afterwards, the published value read is still the one computed at publication, under the rule
    /// version it pinned, and it stays distinct from the current one.
    /// </summary>
    [Fact]
    public async Task AConsumerReadsThePublishedHealthAndNeverARecalculation()
    {
        using HttpClient client = host.Api.CreateClient();
        ProgressSessions sessions = await client.SignInAsync();
        Guid projectId = await host.ProjectAsync();
        host.Inputs.Set(projectId, actualPercent: 40, baselineStart: ProgressDriver.Today.AddDays(-49));
        await client.PublishedPeriodAsync(sessions.ProjectManager, sessions.Reviewer, projectId);

        // The inputs collapse, and nothing republishes: the reader shows the published AMBER, not the RED it would now compute.
        host.Inputs.Set(projectId, actualPercent: 0, baselineStart: ProgressDriver.Today.AddDays(-99), schedule: HealthStatus.Red, financial: HealthStatus.Red);
        ProjectHealthView view = await ReadAsync(projectId);

        Assert.Equal((HealthStatus.Amber, 40m, ProgressTestHost.WorkflowPolicyVersionId), (view.Published!.OverallHealth, view.Published.ActualPercent, view.Published.HealthRuleConfigurationVersionId));
        Assert.Equal(HealthStatus.Amber, view.Current!.OverallHealth);
        Assert.Equal(view.Published.OverallHealth.ToString(), Assert.Single(await host.Database.QueryAsync(
            $"SELECT initcap(overall_health) FROM progress.published_progress_snapshot WHERE project_id = '{projectId}'")));
    }

    private Task<ProjectHealthView> ReadAsync(Guid projectId) =>
        WithScopeAsync(services => services.GetRequiredService<IProjectHealthReader>().GetAsync(projectId, CancellationToken.None));

    private async Task<T> WithScopeAsync<T>(Func<IServiceProvider, Task<T>> action)
    {
        await using AsyncServiceScope scope = host.Api.Services.CreateAsyncScope();
        return await action(scope.ServiceProvider);
    }
}
