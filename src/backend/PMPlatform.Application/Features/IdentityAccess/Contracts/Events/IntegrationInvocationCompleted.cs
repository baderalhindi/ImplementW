using PMPlatform.Application.Common.Events;

namespace PMPlatform.Application.Features.IdentityAccess.Contracts.Events;

/// <summary>
/// <c>IdentityAccess.IntegrationInvocationCompleted</c> (event-conventions §4 row 3; ADR-003 §8.2 edges 31 and 33): one call IdentityAccess
/// made to the directory, the identity provider, the MFA provider or Nafath, and how it ended. The idempotency key and the subject are the
/// invocation's own id. Consumed by IntegrationMonitoring (TASK-075).
/// </summary>
public sealed record IntegrationInvocationCompleted : EventEnvelope<IntegrationInvocationData>
{
    public const string Type = "IdentityAccess.IntegrationInvocationCompleted";

    public const int CurrentSchemaVersion = 1;
}
