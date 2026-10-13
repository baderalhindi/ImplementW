using PMPlatform.Application.Common.Events;
using IntegrationInvocationCompleted = PMPlatform.Application.Features.Notifications.Contracts.Events.IntegrationInvocationCompleted;

namespace PMPlatform.Application.Features.IntegrationMonitoring.EventHandlers;

/// <summary>The consumer of <c>Notifications.IntegrationInvocationCompleted</c> (event-conventions §4 row 4; ADR-003 §8.2 edge 32).</summary>
internal sealed class NotificationInvocationConsumer(InvocationTelemetryIntake intake) : IDomainEventConsumer
{
    public string EventType => IntegrationInvocationCompleted.Type;

    public Task HandleAsync(string payload, CancellationToken cancellationToken) =>
        intake.RecordAsync(EventSerialization.Deserialize<IntegrationInvocationCompleted>(payload), cancellationToken);
}
