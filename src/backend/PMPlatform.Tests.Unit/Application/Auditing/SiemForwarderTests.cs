using Microsoft.Extensions.Logging.Abstractions;
using PMPlatform.Application.Features.AuditActivity;
using PMPlatform.Domain.AuditActivity;
using PMPlatform.Domain.Common;
using PMPlatform.Tests.Unit.Application.IdentityAccess;

namespace PMPlatform.Tests.Unit.Application.Auditing;

/// <summary>TASK-033: each queued event is sent once, its result recorded on its forwarding record, never on the event.</summary>
public sealed class SiemForwarderTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 28, 9, 0, 0, TimeSpan.Zero);

    private readonly FakeForwardingRepository _queue = new();
    private readonly FakeSiemClient _siem = new();

    [Fact]
    public async Task NothingIsSentWhileTheSiemIsNotConfigured()
    {
        _siem.IsConfigured = false;
        _queue.Pending.Add(Pending(1));

        Assert.Equal(0, await Forwarder().ForwardPendingAsync(100, CancellationToken.None));
        Assert.Empty(_siem.Sent);
        Assert.Equal(AuditForwardingStatus.Pending, _queue.Pending[0].Forwarding.Status);
    }

    [Fact]
    public async Task AnAcceptedEventIsForwardedAndSavedBeforeTheNextIsSent()
    {
        _queue.Pending.AddRange([Pending(1), Pending(2)]);

        Assert.Equal(2, await Forwarder().ForwardPendingAsync(100, CancellationToken.None));
        Assert.All(_queue.Pending, p => Assert.Equal((AuditForwardingStatus.Forwarded, Now), (p.Forwarding.Status, p.Forwarding.ForwardedAt)));
        Assert.Equal(2, _queue.Saves);
    }

    /// <summary>A refusal marks the event FAILED for the next pass and stops the batch: the rest would fail the same way.</summary>
    [Fact]
    public async Task ARefusedEventFailsAndStopsTheBatch()
    {
        _queue.Pending.AddRange([Pending(1), Pending(2)]);
        _siem.Accepting = false;

        Assert.Equal(0, await Forwarder().ForwardPendingAsync(100, CancellationToken.None));
        Assert.Equal((AuditForwardingStatus.Failed, null), (_queue.Pending[0].Forwarding.Status, _queue.Pending[0].Forwarding.ForwardedAt));
        Assert.Equal(AuditForwardingStatus.Pending, _queue.Pending[1].Forwarding.Status);
        Assert.Single(_siem.Sent);
    }

    [Fact]
    public void TheSiemReceivesTheEventAsStored()
    {
        PendingForwarding pending = Pending(1);

        SiemEvent sent = SiemForwarder.SiemEventOf(pending);

        Assert.Equal(("AUTHENTICATION", "IdentityAccess.SignInFailed", "FAILED", "USER"), (sent.EventClass, sent.EventType, sent.Outcome, sent.Actor.ActorType));
        Assert.Equal((pending.Event.Id, pending.Event.EventHash), (sent.EventId, sent.EventHash));
        Assert.Null(sent.Subject);
        Assert.Equal(["client_address", "failure_reason"], sent.Attributes.Select(a => a.Name));
    }

    private SiemForwarder Forwarder() => new(_queue, _siem, new FixedTimeProvider(Now), NullLogger<SiemForwarder>.Instance);

    private static PendingForwarding Pending(int n)
    {
        Guid eventId = Guid.Parse($"00000000-0000-4000-8000-{n:D12}");
        AuditEvent auditEvent = new()
        {
            Id = eventId,
            EventClass = AuditEventClass.Authentication,
            EventType = "IdentityAccess.SignInFailed",
            Outcome = AuditOutcome.Failed,
            ActorType = AuditActorType.User,
            EventHash = new string('a', 64),
        };
        return new PendingForwarding(
            new AuditForwardingRecord { AuditEventId = eventId, Status = AuditForwardingStatus.Pending },
            auditEvent,
            [
                new AuditEventAttribute { AuditEventId = eventId, AttributeName = "failure_reason", NewValue = "CREDENTIALS_REJECTED" },
                new AuditEventAttribute { AuditEventId = eventId, AttributeName = "client_address", NewValue = "203.0.113.7" },
            ]);
    }

    private sealed class FakeForwardingRepository : IAuditForwardingRepository
    {
        public List<PendingForwarding> Pending { get; } = [];

        public int Saves { get; private set; }

        public Task<IReadOnlyList<PendingForwarding>> FindUnforwardedAsync(int count, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<PendingForwarding>>([.. Pending.Where(p => p.Forwarding.Status != AuditForwardingStatus.Forwarded).Take(count)]);

        public Task SaveAsync(CancellationToken cancellationToken)
        {
            Saves++;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeSiemClient : ISiemClient
    {
        public bool IsConfigured { get; set; } = true;

        public bool Accepting { get; set; } = true;

        public List<SiemEvent> Sent { get; } = [];

        public Task<bool> SendAsync(SiemEvent siemEvent, CancellationToken cancellationToken)
        {
            Sent.Add(siemEvent);
            return Task.FromResult(Accepting);
        }
    }
}
