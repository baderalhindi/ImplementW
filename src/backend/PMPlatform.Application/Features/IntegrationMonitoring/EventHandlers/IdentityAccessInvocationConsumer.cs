using PMPlatform.Application.Common.Events;
using IntegrationInvocationCompleted = PMPlatform.Application.Features.IdentityAccess.Contracts.Events.IntegrationInvocationCompleted;

namespace PMPlatform.Application.Features.IntegrationMonitoring.EventHandlers;

/// <summary>The consumer of <c>IdentityAccess.IntegrationInvocationCompleted</c> (event-conventions §4 row 3; ADR-003 §8.2 edges 31, 33).</summary>
internal sealed class IdentityAccessInvocationConsumer(InvocationTelemetryIntake intake) : IDomainEventConsumer
{
    public string EventType => IntegrationInvocationCompleted.Type;

    public Task HandleAsync(string payload, CancellationToken cancellationToken) =>
        intake.RecordAsync(EventSerialization.Deserialize<IntegrationInvocationCompleted>(payload), cancellationToken);
}
