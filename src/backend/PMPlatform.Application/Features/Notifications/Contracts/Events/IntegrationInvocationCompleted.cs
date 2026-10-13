using PMPlatform.Application.Common.Events;

namespace PMPlatform.Application.Features.Notifications.Contracts.Events;

/// <summary>
/// <c>Notifications.IntegrationInvocationCompleted</c> (event-conventions §4 row 4; ADR-003 §8.2 edge 32): one e-mail or SMS attempt and how
/// it ended — what WF-15 does next included: another attempt after its back-off, or nothing more (dead letter). Staged with the attempt's
/// own record, keyed by the delivery, the attempt and its time, so a redriven delivery's attempts are new occurrences. Its correlation
/// id is the notification intent's, which is its source request's. Consumed by IntegrationMonitoring (TASK-075).
/// </summary>
public sealed record IntegrationInvocationCompleted : EventEnvelope<IntegrationInvocationData>
{
    public const string Type = "Notifications.IntegrationInvocationCompleted";

    public const int CurrentSchemaVersion = 1;
}
