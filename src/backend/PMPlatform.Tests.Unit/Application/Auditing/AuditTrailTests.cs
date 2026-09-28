using PMPlatform.Application.Common.Auditing;
using PMPlatform.Application.Features.AuditActivity;
using PMPlatform.Domain.AuditActivity;
using PMPlatform.Domain.Common;
using PMPlatform.Tests.Unit.Application.IdentityAccess;

namespace PMPlatform.Tests.Unit.Application.Auditing;

/// <summary>TASK-033: what the trail adds to a producer's entry, and which events it queues for the SIEM.</summary>
public sealed class AuditTrailTests
{
    private static readonly Guid CorrelationId = Guid.Parse("00000000-0000-4000-8000-0000000c0001");
    private static readonly Guid UserId = Guid.Parse("00000000-0000-4000-8000-0000000c0002");
    private static readonly DateTimeOffset Now = new(2026, 9, 28, 9, 0, 0, TimeSpan.Zero);

    private readonly FakeAuditEventRepository _events = new();

    [Fact]
    public async Task AnEventWithNoKnownUserIsAttributedToTheAuditCapturePrincipal()
    {
        await Trail().RecordAsync(new AuditEntry(AuditEventClass.Authentication, "IdentityAccess.SignInFailed", AuditOutcome.Failed));

        AuditEvent recorded = Assert.Single(_events.Appended).Event;
        Assert.Null(recorded.ActorUserId);
        Assert.Equal((AuditTrail.AuditCapturePrincipalId, AuditTrail.AuditCapturePrincipalId), (recorded.CreatedBy, recorded.UpdatedBy));
        Assert.Equal((Now, Now, Now), (recorded.OccurredAt, recorded.CreatedAt, recorded.UpdatedAt));
        Assert.Equal(CorrelationId, recorded.CorrelationId);
    }

    [Fact]
    public async Task AnEventOfAKnownUserIsTheirs()
    {
        await Trail().RecordAsync(new AuditEntry(AuditEventClass.Authentication, "IdentityAccess.SignInSucceeded", AuditOutcome.Success) { ActorUserId = UserId });

        AuditEvent recorded = Assert.Single(_events.Appended).Event;
        Assert.Equal((UserId, UserId, UserId), (recorded.ActorUserId, recorded.CreatedBy, recorded.UpdatedBy));
    }

    [Fact]
    public async Task TheClientAddressIsRecordedWithTheProducersAttributes()
    {
        await Trail(clientAddress: "203.0.113.7").RecordAsync(new AuditEntry(AuditEventClass.Authentication, "IdentityAccess.SignInFailed", AuditOutcome.Failed)
        {
            Attributes = [AuditAttribute.Of("failure_reason", "CREDENTIALS_REJECTED")],
        });

        AuditRecord record = Assert.Single(_events.Appended);
        Assert.Equal(
            ["client_address=>203.0.113.7", "failure_reason=>CREDENTIALS_REJECTED"],
            record.Attributes.Select(a => $"{a.AttributeName}={a.OldValue}>{a.NewValue}").Order(StringComparer.Ordinal));
        Assert.All(record.Attributes, a => Assert.Equal(record.Event.Id, a.AuditEventId));
    }

    /// <summary>PTBC-029: the event of a forwarded class is queued in the same unit of work; any other is only stored.</summary>
    [Theory]
    [InlineData(AuditEventClass.Authentication, true)]
    [InlineData(AuditEventClass.PermissionChange, true)]
    [InlineData(AuditEventClass.DataChange, false)]
    public async Task OnlyAForwardedClassIsQueuedForTheSiem(AuditEventClass eventClass, bool queued)
    {
        await Trail().RecordAsync(new AuditEntry(eventClass, "Test.Event", AuditOutcome.Success));

        AuditRecord record = Assert.Single(_events.Appended);
        Assert.Equal(queued, record.Forwarding is { Status: AuditForwardingStatus.Pending });
        Assert.Equal(queued ? record.Event.Id : null, record.Forwarding?.AuditEventId);
    }

    /// <summary>A staged event waits for the producer's save; a recorded one is committed on its own.</summary>
    [Fact]
    public async Task StageAndRecordUseDifferentUnitsOfWork()
    {
        AuditTrail trail = Trail();
        trail.Stage(new AuditEntry(AuditEventClass.PrivilegedAction, "IdentityAccess.UserDisabled", AuditOutcome.Success));
        await trail.RecordAsync(new AuditEntry(AuditEventClass.AuthorizationDenial, "IdentityAccess.AccessDenied", AuditOutcome.Denied));

        Assert.Equal("IdentityAccess.UserDisabled", Assert.Single(_events.Staged).Event.EventType);
        Assert.Equal("IdentityAccess.AccessDenied", Assert.Single(_events.Appended).Event.EventType);
    }

    private AuditTrail Trail(string? clientAddress = null)
    {
        SiemForwardingPolicy policy = new();
        policy.ForwardedClasses.Add("AUTHENTICATION");
        policy.ForwardedClasses.Add("PERMISSION_CHANGE");
        return new AuditTrail(_events, new FixedRequestContext(CorrelationId, clientAddress), policy, new FixedTimeProvider(Now));
    }
}
