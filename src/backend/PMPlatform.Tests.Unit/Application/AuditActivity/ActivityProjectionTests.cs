using PMPlatform.Application.Features.AuditActivity;
using PMPlatform.Domain.AuditActivity;
using PMPlatform.Domain.Common;

namespace PMPlatform.Tests.Unit.Application.AuditActivity;

/// <summary>TASK-073: FG-06 §7.1 — which audit events become Activity, and what the line says.</summary>
public sealed class ActivityProjectionTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 12, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ASuccessfulTemplatedProjectEventBecomesItsLine()
    {
        AuditEvent closed = AuditEvents.Of("Risk.RiskClosed", AuditEventClass.LifecycleTransition);

        BusinessActivityEntry line = Assert.IsType<BusinessActivityEntry>(ActivityProjection.Project(closed, Now));

        Assert.Equal((closed.Id, closed.ScopeProjectId, closed.OccurredAt, closed.ActorUserId), (line.AuditEventId, line.ProjectId, line.OccurredAt, line.ActorUserId));
        Assert.Equal(("RISK", "Risk closed", "تم إغلاق خطر"), (line.ActivityKind, line.SummaryEn, line.SummaryAr));
    }

    public static TheoryData<string> NotActivity() => ["no project", "denied", "failed", "security class", "untemplated type", "excluded module"];

    [Theory]
    [MemberData(nameof(NotActivity))]
    public void AnythingElseIsLeftOut(string why)
    {
        AuditEvent e = why switch
        {
            "no project" => AuditEvents.Of("Risk.RiskClosed", AuditEventClass.LifecycleTransition, project: false),
            "denied" => AuditEvents.Of("Risk.RiskClosed", AuditEventClass.LifecycleTransition, AuditOutcome.Denied),
            "failed" => AuditEvents.Of("Risk.RiskClosed", AuditEventClass.LifecycleTransition, AuditOutcome.Failed),
            "security class" => AuditEvents.Of("Risk.RiskClosed", AuditEventClass.AuthorizationDenial),
            "untemplated type" => AuditEvents.Of("Progress.ProjectHealthRecomputed", AuditEventClass.DataChange),
            "excluded module" => AuditEvents.Of("Reports.JobRequested", AuditEventClass.LifecycleTransition),
            _ => throw new ArgumentOutOfRangeException(nameof(why)),
        };

        Assert.Null(ActivityProjection.Project(e, Now));
    }

    [Theory]
    [InlineData("ManagementConcern.ConcernRaised", "MANAGEMENT_CONCERN")]
    [InlineData("ProjectTask.TaskStarted", "PROJECT_TASK")]
    [InlineData("Risk.RiskRegistered", "RISK")]
    public void TheKindIsTheSourceDomain(string eventType, string kind) => Assert.Equal(kind, ActivityProjection.KindOf(eventType));
}

/// <summary>Stored audit events as tests need them, each recorded a millisecond after the last.</summary>
internal static class AuditEvents
{
    private static readonly DateTimeOffset Start = new(2026, 10, 12, 8, 0, 0, TimeSpan.Zero);
    private static int _sequence;

    public static readonly Guid ProjectId = Guid.Parse("00000000-0073-4000-8000-0000000000a1");

    public static AuditEvent Of(string eventType, AuditEventClass eventClass, AuditOutcome outcome = AuditOutcome.Success, bool project = true)
    {
        int n = Interlocked.Increment(ref _sequence);
        DateTimeOffset at = Start.AddMilliseconds(n);
        return new AuditEvent
        {
            Id = Guid.CreateVersion7(at),
            OccurredAt = at,
            RecordedAt = at,
            EventClass = eventClass,
            EventType = eventType,
            ActorType = AuditActorType.User,
            ActorUserId = Guid.Parse("00000000-0073-4000-8000-0000000000b1"),
            ScopeProjectId = project ? ProjectId : null,
            CorrelationId = Guid.NewGuid(),
            Outcome = outcome,
            EventHash = new string('0', 64),
        };
    }
}
