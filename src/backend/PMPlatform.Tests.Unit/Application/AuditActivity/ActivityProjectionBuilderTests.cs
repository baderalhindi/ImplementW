using PMPlatform.Application.Features.AuditActivity;
using PMPlatform.Application.Features.AuditActivity.Contracts;
using PMPlatform.Domain.AuditActivity;
using PMPlatform.Domain.Common;
using PMPlatform.Tests.Unit.Application.IdentityAccess;

namespace PMPlatform.Tests.Unit.Application.AuditActivity;

/// <summary>
/// TASK-073 acceptance criterion 2, in memory: the live projection, built pass by pass as events arrive, equals what a rebuild
/// derives from the store; a check finds every way a line can differ, and a rebuild repairs it. The integration suite runs the same
/// rebuild-and-diff against PostgreSQL with events produced through the API (<c>ActivityRebuildTests</c>).
/// </summary>
public sealed class ActivityProjectionBuilderTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 12, 10, 0, 0, TimeSpan.Zero);

    private readonly InMemoryActivityProjection _store = new();

    [Fact]
    public async Task TheLiveProjectionIsWhatARebuildDerives()
    {
        ActivityProjectionBuilder builder = Builder();
        foreach (AuditEvent e in Mixed(40))
        {
            _store.Events.Add(e);
            if (_store.Events.Count % 7 == 0)
            {
                await builder.ProjectPendingAsync(batchSize: 3, CancellationToken.None);
            }
        }

        // Bounded: a pass that never moves the checkpoint would read the same batch forever.
        for (int pass = 0; pass < 100 && await builder.ProjectPendingAsync(batchSize: 3, CancellationToken.None) > 0; pass++)
        {
        }

        List<string> live = Lines();
        ActivityProjectionReport check = await builder.CheckAsync(CancellationToken.None);

        _store.Entries.Clear();
        _store.Checkpoint.LastRecordedAt = null;
        _store.Checkpoint.LastAuditEventId = null;
        ActivityProjectionReport rebuild = await builder.RebuildAsync(CancellationToken.None);

        Assert.True(check.Matches, $"{check.Missing} missing, {check.Unexpected} unexpected, {check.Changed} changed");
        Assert.Equal((40, 0), (check.EventsCompared, check.PendingEvents));
        Assert.Equal(live, Lines());
        Assert.Equal((live.Count, live.Count), (rebuild.Missing, rebuild.EntriesExpected));
        Assert.Equal(_store.Events[^1].RecordedAt, _store.Checkpoint.LastRecordedAt);
    }

    [Fact]
    public async Task ACheckFindsEveryDifferenceAndARebuildRepairsIt()
    {
        ActivityProjectionBuilder builder = Builder();
        _store.Events.AddRange(Mixed(12));
        await builder.ProjectPendingAsync(batchSize: 100, CancellationToken.None);
        List<string> live = Lines();

        BusinessActivityEntry[] lines = [.. _store.Entries.Values.OrderBy(e => e.OccurredAt)];
        lines[0].SummaryEn = "Tampered";
        _store.Entries.Remove(lines[1].Id);
        AuditEvent notActivity = _store.Events.First(e => ActivityProjection.Project(e, Now) is null);
        BusinessActivityEntry stray = ActivityProjection.Project(AuditEvents.Of("Risk.RiskClosed", AuditEventClass.LifecycleTransition), Now)!;
        stray.AuditEventId = notActivity.Id;
        _store.Entries.Add(stray.Id, stray);

        ActivityProjectionReport check = await builder.CheckAsync(CancellationToken.None);
        ActivityProjectionReport rebuild = await builder.RebuildAsync(CancellationToken.None);
        ActivityProjectionReport after = await builder.CheckAsync(CancellationToken.None);

        Assert.Equal((1, 1, 1), (check.Missing, check.Unexpected, check.Changed));
        Assert.Equal(
            [new ActivityDifference(lines[1].AuditEventId, ActivityDifferenceKind.Missing), new ActivityDifference(notActivity.Id, ActivityDifferenceKind.Unexpected),
             new ActivityDifference(lines[0].AuditEventId, ActivityDifferenceKind.Changed)],
            check.Differences.OrderBy(d => d.Kind));
        Assert.Equal((1, 1, 1, true), (rebuild.Missing, rebuild.Unexpected, rebuild.Changed, rebuild.Rebuilt));
        Assert.True(after.Matches);
        Assert.Equal(live, Lines());
    }

    /// <summary>A check compares only up to the checkpoint: events the live pass has not reached yet are lag, not differences.</summary>
    [Fact]
    public async Task EventsAfterTheCheckpointArePendingNotMissing()
    {
        ActivityProjectionBuilder builder = Builder();
        _store.Events.AddRange(Mixed(6));
        await builder.ProjectPendingAsync(batchSize: 100, CancellationToken.None);
        _store.Events.AddRange(Mixed(4));

        ActivityProjectionReport check = await builder.CheckAsync(CancellationToken.None);

        Assert.True(check.Matches);
        Assert.Equal((6, 4), (check.EventsCompared, check.PendingEvents));
    }

    private ActivityProjectionBuilder Builder() => new(_store, new FixedTimeProvider(Now));

    /// <summary>The projection's content, without row bookkeeping, in event order.</summary>
    private List<string> Lines() =>
        [.. _store.Entries.Values.OrderBy(e => e.OccurredAt).Select(e => $"{e.AuditEventId}|{e.ProjectId}|{e.OccurredAt:O}|{e.ActorUserId}|{e.ActivityKind}|{e.SummaryEn}|{e.SummaryAr}")];

    /// <summary>Activity and non-activity in turn: templated lifecycle events, refusals, recomputations and platform events.</summary>
    private static IEnumerable<AuditEvent> Mixed(int count)
    {
        (string Type, AuditEventClass Class, bool Project)[] kinds =
        [
            ("Risk.RiskRegistered", AuditEventClass.DataChange, true),
            ("Risk.AuthorityRefused", AuditEventClass.AuthorizationDenial, true),
            ("ProjectTask.TaskCompleted", AuditEventClass.LifecycleTransition, true),
            ("IdentityAccess.SignInFailed", AuditEventClass.Authentication, false),
            ("Progress.ProjectHealthRecomputed", AuditEventClass.DataChange, true),
            ("Approval.TaskApproved", AuditEventClass.ApprovalDecision, true),
        ];
        for (int i = 0; i < count; i++)
        {
            (string type, AuditEventClass eventClass, bool project) = kinds[i % kinds.Length];
            yield return AuditEvents.Of(type, eventClass, project: project);
        }
    }
}
