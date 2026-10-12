using PMPlatform.Application.Common.Auditing;
using PMPlatform.Application.Features.AuditActivity;
using PMPlatform.Domain.Common;
using PMPlatform.Infrastructure.Audit;
using PMPlatform.Tests.Integration.Identity;

namespace PMPlatform.Tests.Integration.AuditActivity;

/// <summary>
/// TASK-073 acceptance criterion 2 and the second half of its validation check: the Activity projection built live, at commit, as
/// events arrive, is exactly what a rebuild derives from the audit store — the rebuild-and-diff test — and a check finds every line
/// that differs, with zero differences on an untouched store. Run through the API and through the release image's commands.
/// </summary>
[Collection(AuditActivitySuite.Name)]
public sealed class ActivityRebuildTests(AuditActivityTestHost host)
{
    [Fact]
    public async Task RebuildingTheProjectionFromTheAuditStoreReproducesTheLiveProjection()
    {
        await ProduceActivityAsync();
        using HttpClient operator1 = await host.ClientAsync(1);
        IReadOnlyList<string> live = await ProjectionAsync();

        System.Text.Json.Nodes.JsonObject check = await (await operator1.PostSensitiveAsync(AuditActivityApi.ProjectionChecks)).OkAsync();

        // The projection is lost, as an index can be (FG-06 §4.5): it is rebuilt from the store by the release image's command.
        await host.Database.ExecuteAsync("""
            DELETE FROM audit_activity.business_activity_entry;
            UPDATE audit_activity.activity_projection_checkpoint SET last_recorded_at = NULL, last_audit_event_id = NULL;
            """);
        int rebuilt = await host.Api.Services.RunAuditCommandAsync(AuditCommands.RebuildProjectionCommand);
        IReadOnlyList<string> derived = await ProjectionAsync();
        int checkedAgain = await host.Api.Services.RunAuditCommandAsync(AuditCommands.CheckProjectionCommand);

        Assert.True(check["matches"]!.GetValue<bool>(), check.ToJsonString());
        Assert.Equal(0, check["pendingEvents"]!.GetValue<int>());
        Assert.True(live.Count >= 8, $"Only {live.Count} lines were projected.");
        Assert.Equal((0, 0), (rebuilt, checkedAgain));
        Assert.Equal(live, derived);
        IReadOnlyList<string> operations = await host.Database.QueryAsync("""
            SELECT DISTINCT concat_ws('|', event_type, outcome, actor_type, actor_user_id) FROM audit_activity.audit_event
            WHERE event_type IN ('AuditActivity.ActivityProjectionChecked', 'AuditActivity.ActivityProjectionRebuilt')
            """);
        Assert.Contains($"AuditActivity.ActivityProjectionChecked|SUCCESS|USER|{IdentityDatabase.UserId(1)}", operations);
        Assert.Contains($"AuditActivity.ActivityProjectionRebuilt|SUCCESS|SERVICE|{AuditCapturePrincipal.Id}", operations);
    }

    [Fact]
    public async Task ACheckFindsEveryLineThatDiffersAndARebuildRepairsIt()
    {
        Guid[] subjects = await ProduceActivityAsync();
        using HttpClient operator1 = await host.ClientAsync(1);
        IReadOnlyList<string> live = await ProjectionAsync();
        Guid changed = await host.EventIdAsync(subjects[0]);
        Guid removed = await host.EventIdAsync(subjects[1]);
        Guid notActivity = await host.EventIdAsync(subjects[^1]);

        await host.Database.ExecuteAsync($"""
            UPDATE audit_activity.business_activity_entry SET summary_en = 'Tampered' WHERE audit_event_id = '{changed}';
            DELETE FROM audit_activity.business_activity_entry WHERE audit_event_id = '{removed}';
            INSERT INTO audit_activity.business_activity_entry (id, audit_event_id, project_id, occurred_at, activity_kind, summary_ar, summary_en, created_at, created_by, updated_at, updated_by)
            VALUES (gen_random_uuid(), '{notActivity}', '{AuditActivityTestHost.ProjectA}', now(), 'RISK', 'دخيل', 'Stray', now(), '{AuditCapturePrincipal.Id}', now(), '{AuditCapturePrincipal.Id}');
            """);
        System.Text.Json.Nodes.JsonObject check = await (await operator1.PostSensitiveAsync(AuditActivityApi.ProjectionChecks)).OkAsync();
        int commandExit = await host.Api.Services.RunAuditCommandAsync(AuditCommands.CheckProjectionCommand);
        System.Text.Json.Nodes.JsonObject rebuild = await (await operator1.PostSensitiveAsync(AuditActivityApi.ProjectionRebuilds)).OkAsync();
        System.Text.Json.Nodes.JsonObject after = await (await operator1.PostSensitiveAsync(AuditActivityApi.ProjectionChecks)).OkAsync();

        Assert.Equal((1, 1, 1), (check["missing"]!.GetValue<int>(), check["unexpected"]!.GetValue<int>(), check["changed"]!.GetValue<int>()));
        Assert.Equal(
            new[] { $"{changed}|CHANGED", $"{removed}|MISSING", $"{notActivity}|UNEXPECTED" }.Order(StringComparer.Ordinal),
            check["differences"]!.AsArray().Select(d => $"{d!["auditEventId"]}|{d["kind"]}").Order(StringComparer.Ordinal));
        Assert.Equal(1, commandExit);
        Assert.Equal((1, 1, 1), (rebuild["missing"]!.GetValue<int>(), rebuild["unexpected"]!.GetValue<int>(), rebuild["changed"]!.GetValue<int>()));
        Assert.True(after["matches"]!.GetValue<bool>());
        Assert.Equal(live, await ProjectionAsync());
        Assert.Contains("AuditActivity.ActivityProjectionChecked|FAILED", await host.Database.QueryAsync(
            "SELECT event_type || '|' || outcome FROM audit_activity.audit_event WHERE event_type = 'AuditActivity.ActivityProjectionChecked'"));
    }

    /// <summary>
    /// Activity from several domains on both projects, through the API and through producers' trail, mixed with what is not activity:
    /// a refused change, a failed outcome, a recomputation, a failed sign-in, an administration event. The last subject is not activity.
    /// </summary>
    private async Task<Guid[]> ProduceActivityAsync()
    {
        Guid[] subjects = [.. Enumerable.Range(0, 9).Select(_ => Guid.NewGuid())];
        using (HttpClient anonymous = host.Api.CreateClient())
        {
            (await anonymous.SignInAsync("local.r02", "not the password")).Dispose();
        }

        await host.StageAsync(
            AuditActivityTestHost.Business("Risk.RiskRegistered", AuditActivityTestHost.ProjectA, subjects[0], 8, AuditEventClass.DataChange, entityId: AuditActivityTestHost.EntityA),
            AuditActivityTestHost.Business("ProjectTask.TaskCompleted", AuditActivityTestHost.ProjectA, subjects[1], 8),
            AuditActivityTestHost.Business("Approval.TaskApproved", AuditActivityTestHost.ProjectA, subjects[2], 2, AuditEventClass.ApprovalDecision),
            AuditActivityTestHost.Business("ChangeRequest.ChangeRequestSubmitted", AuditActivityTestHost.ProjectB, subjects[3], 2),
            AuditActivityTestHost.Business("FinancialKpi.VersionActivated", AuditActivityTestHost.ProjectB, subjects[4], 2, classification: AuditActivityTestHost.HighClassification),
            AuditActivityTestHost.Business("DocumentManagement.DocumentUploaded", AuditActivityTestHost.ProjectB, subjects[5], 2, AuditEventClass.DataChange));
        await host.StageAsync(
            AuditActivityTestHost.Business("Progress.ProjectHealthRecomputed", AuditActivityTestHost.ProjectA, subjects[6], 2, AuditEventClass.DataChange),
            AuditActivityTestHost.Business("Schedule.BaselineActivated", AuditActivityTestHost.ProjectB, subjects[7], 2) with { Outcome = AuditOutcome.Failed },
            new AuditEntry(AuditEventClass.AuthorizationDenial, "Risk.AuthorityRefused", AuditOutcome.Denied)
            {
                ActorUserId = AuditActivityTestHost.Person(8),
                Subject = new AuditSubject("Risk", "TestRecord", subjects[8]),
                ScopeProjectId = AuditActivityTestHost.ProjectA,
            });
        return subjects;
    }

    /// <summary>The projection's content, without row bookkeeping, in event order.</summary>
    private Task<IReadOnlyList<string>> ProjectionAsync() => host.Database.QueryAsync("""
        SELECT concat_ws('|', a.audit_event_id, a.project_id, to_char(a.occurred_at AT TIME ZONE 'UTC', 'YYYY-MM-DD"T"HH24:MI:SS.US'), a.actor_user_id,
                         a.activity_kind, a.summary_en, a.summary_ar)
        FROM audit_activity.business_activity_entry a JOIN audit_activity.audit_event e ON e.id = a.audit_event_id
        ORDER BY e.recorded_at
        """);
}
