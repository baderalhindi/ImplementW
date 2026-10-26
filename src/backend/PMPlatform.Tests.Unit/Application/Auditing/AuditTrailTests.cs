using PMPlatform.Application.Common.Auditing;
using PMPlatform.Application.Features.AuditActivity;
using PMPlatform.Domain.AuditActivity;
using PMPlatform.Domain.Common;
using PMPlatform.Tests.Unit.Application.IdentityAccess;

namespace PMPlatform.Tests.Unit.Application.Auditing;

/// <summary>TASK-033 and TASK-073: what the trail adds to a producer's entry, what it refuses, and where each capture goes.</summary>
public sealed class AuditTrailTests
{
    private static readonly Guid CorrelationId = Guid.Parse("00000000-0000-4000-8000-0000000c0001");
    private static readonly Guid UserId = Guid.Parse("00000000-0000-4000-8000-0000000c0002");
    private static readonly DateTimeOffset Now = new(2026, 9, 28, 9, 0, 0, TimeSpan.Zero);

    private readonly FakeAuditCaptureStore _store = new();

    [Fact]
    public async Task AnEventWithNoKnownUserHasNoActorAndIsStoredAsTheAuditCapturePrincipals()
    {
        await Trail().RecordAsync(new AuditEntry(AuditEventClass.Authentication, "IdentityAccess.SignInFailed", AuditOutcome.Failed));

        AuditCapture captured = Assert.Single(_store.Recorded);
        Assert.Null(captured.ActorUserId);
        Assert.Equal((Now, CorrelationId, AuditCapture.CurrentSchemaVersion), (captured.OccurredAt, captured.CorrelationId, captured.SchemaVersion));

        AuditEvent stored = Factory().Create(captured).Event;
        Assert.Equal((AuditCapturePrincipal.Id, AuditCapturePrincipal.Id), (stored.CreatedBy, stored.UpdatedBy));
        Assert.Equal((Now, Now, Now), (stored.OccurredAt, stored.CreatedAt, stored.UpdatedAt));
    }

    [Fact]
    public async Task AnEventOfAKnownUserIsTheirs()
    {
        await Trail().RecordAsync(new AuditEntry(AuditEventClass.Authentication, "IdentityAccess.SignInSucceeded", AuditOutcome.Success) { ActorUserId = UserId });

        AuditEvent stored = Factory().Create(Assert.Single(_store.Recorded)).Event;
        Assert.Equal((UserId, UserId, UserId), (stored.ActorUserId, stored.CreatedBy, stored.UpdatedBy));
    }

    [Fact]
    public async Task TheClientAddressIsCapturedWithTheProducersAttributes()
    {
        await Trail(clientAddress: "203.0.113.7").RecordAsync(new AuditEntry(AuditEventClass.Authentication, "IdentityAccess.SignInFailed", AuditOutcome.Failed)
        {
            Attributes = [AuditAttribute.Of("failure_reason", "CREDENTIALS_REJECTED")],
        });

        AuditRecord record = Factory().Create(Assert.Single(_store.Recorded));
        Assert.Equal(
            ["client_address=>203.0.113.7", "failure_reason=>CREDENTIALS_REJECTED"],
            record.Attributes.Select(a => $"{a.AttributeName}={a.OldValue}>{a.NewValue}").Order(StringComparer.Ordinal));
        Assert.All(record.Attributes, a => Assert.Equal(record.Event.Id, a.AuditEventId));
    }

    /// <summary>A staged capture waits for the producer's save; a recorded one is committed on its own.</summary>
    [Fact]
    public async Task StageAndRecordUseDifferentUnitsOfWork()
    {
        AuditTrail trail = Trail();
        trail.Stage(new AuditEntry(AuditEventClass.PrivilegedAction, "IdentityAccess.UserDisabled", AuditOutcome.Success));
        await trail.RecordAsync(new AuditEntry(AuditEventClass.AuthorizationDenial, "IdentityAccess.AccessDenied", AuditOutcome.Denied));

        Assert.Equal("IdentityAccess.UserDisabled", Assert.Single(_store.Staged).EventType);
        Assert.Equal("IdentityAccess.AccessDenied", Assert.Single(_store.Recorded).EventType);
    }

    /// <summary>TASK-073: a malformed event fails its producer's action instead of reaching the outbox (FG-06 §4.2 VALIDATED).</summary>
    [Theory]
    [InlineData("NotDotted", "reason")]
    [InlineData("Risk.RiskRegistered", "Not Snake")]
    [InlineData("risk.registered", "reason")]
    public void AMalformedEventIsRefusedAtCapture(string eventType, string attributeName)
    {
        AuditEntry entry = new(AuditEventClass.DataChange, eventType, AuditOutcome.Success) { Attributes = [AuditAttribute.Of(attributeName, "x")] };

        Assert.Throws<InvalidOperationException>(() => Trail().Stage(entry));
        Assert.Empty(_store.Staged);
    }

    /// <summary>TASK-073 (AUD-CC-14): a secret never reaches the outbox, whatever the producer handed over.</summary>
    [Fact]
    public void ASecretIsRedactedBeforeItIsCaptured()
    {
        Trail().Stage(new AuditEntry(AuditEventClass.PrivilegedAction, "IdentityAccess.UserUpdated", AuditOutcome.Success)
        {
            Attributes = [AuditAttribute.Of("bind_password", "hunter2"), AuditAttribute.Of("note", "Bearer abc.def.ghi"), AuditAttribute.Of("status", "ACTIVE")],
        });

        AuditCapture captured = Assert.Single(_store.Staged);
        Assert.DoesNotContain("hunter2", captured.Serialize(), StringComparison.Ordinal);
        Assert.Equal(
            ["bind_password=>[REDACTED]", "note=>[REDACTED]", "status=>ACTIVE"],
            captured.Attributes.Select(a => $"{a.Name}={a.OldValue}>{a.NewValue}").Order(StringComparer.Ordinal));
    }

    /// <summary>TASK-073: what the outbox holds is read back as the same capture, so ingestion stores what was captured.</summary>
    [Fact]
    public void ACaptureSurvivesTheOutbox()
    {
        Trail().Stage(new AuditEntry(AuditEventClass.LifecycleTransition, "Risk.RiskClosed", AuditOutcome.Success)
        {
            ActorUserId = UserId,
            Subject = new AuditSubject("Risk", "Risk", Guid.Parse("00000000-0000-4000-8000-0000000c0003")),
            ScopeProjectId = Guid.Parse("00000000-0000-4000-8000-0000000c0004"),
            Attributes = [AuditAttribute.Change("status", "MONITORING", "CLOSED")!],
        });

        AuditCapture captured = Assert.Single(_store.Staged);
        AuditCapture read = AuditCapture.Deserialize(captured.Serialize());
        Assert.Equal(captured with { Attributes = [] }, read with { Attributes = [] });
        Assert.Equal(captured.Attributes, read.Attributes);
        Assert.Equal(("Risk", $"Risk.RiskClosed:{captured.EventId:D}"), (captured.SourceModule, captured.MessageKey));
    }

    private AuditTrail Trail(string? clientAddress = null) =>
        new(_store, new FixedRequestContext(CorrelationId, clientAddress), new FixedTimeProvider(Now));

    internal static AuditRecordFactory Factory()
    {
        SiemForwardingPolicy policy = new();
        policy.ForwardedClasses.Add("AUTHENTICATION");
        policy.ForwardedClasses.Add("PERMISSION_CHANGE");
        return new AuditRecordFactory(policy);
    }
}
