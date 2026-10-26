using PMPlatform.Application.Common.Auditing;
using PMPlatform.Application.Features.AuditActivity;
using PMPlatform.Domain.AuditActivity;
using PMPlatform.Domain.Common;

namespace PMPlatform.Tests.Unit.Application.Auditing;

/// <summary>TASK-073: the rows a validated capture is stored as.</summary>
public sealed class AuditRecordFactoryTests
{
    private static readonly DateTimeOffset At = new(2026, 10, 12, 9, 0, 0, TimeSpan.Zero);

    /// <summary>PTBC-029: the event of a forwarded class is queued in the same unit of work; any other is only stored.</summary>
    [Theory]
    [InlineData(AuditEventClass.Authentication, true)]
    [InlineData(AuditEventClass.PermissionChange, true)]
    [InlineData(AuditEventClass.DataChange, false)]
    public void OnlyAForwardedClassIsQueuedForTheSiem(AuditEventClass eventClass, bool queued)
    {
        AuditRecord record = AuditTrailTests.Factory().Create(Capture(eventClass));

        Assert.Equal(queued, record.Forwarding is { Status: AuditForwardingStatus.Pending });
        Assert.Equal(queued ? record.Event.Id : null, record.Forwarding?.AuditEventId);
    }

    /// <summary>The capture's id is the stored event's: a second delivery of the message names an event that already exists (AUD-CC-07).</summary>
    [Fact]
    public void TheStoredEventHasTheCapturesIdAndTheDigestOfItsAttributes()
    {
        AuditCapture capture = Capture(AuditEventClass.DataChange) with
        {
            Attributes = [AuditAttribute.Of("title", null), AuditAttribute.Change("status", "OPEN", "CLOSED")!],
        };

        AuditRecord record = AuditTrailTests.Factory().Create(capture);

        Assert.Equal(capture.EventId, record.Event.Id);
        Assert.Equal(AuditAttributeDigest.Of(record.Attributes), record.Event.AttributeDigest);
        Assert.Equal(AuditAttributeDigest.Of([("status", "OPEN", "CLOSED"), ("title", null, null)]), record.Event.AttributeDigest);
    }

    private static AuditCapture Capture(AuditEventClass eventClass) =>
        new(Guid.CreateVersion7(At), AuditCapture.CurrentSchemaVersion, At, eventClass, "Test.Event", AuditOutcome.Success, AuditActorType.User,
            null, null, null, null, null, Guid.NewGuid(), []);
}
