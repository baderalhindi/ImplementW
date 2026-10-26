using PMPlatform.Application.Common.Events;

namespace PMPlatform.Application.Features.ManagementConcern.Contracts.Events;

/// <summary>
/// <c>ManagementConcern.ConcernEscalated</c> (event-conventions §3 row 7): the NOTIFICATION_INTENT an escalation publishes to WF-15
/// (edge E-U4), staged in the transaction that records the escalation. Its idempotency key is the escalation's id, so one escalation
/// publishes one intent: the outbox's unique message key refuses a second, and WF-15 records one intent per source reference.
/// </summary>
public sealed record ConcernEscalated : EventEnvelope<NotificationIntentData>
{
    public const string Type = "ManagementConcern.ConcernEscalated";

    public const int CurrentSchemaVersion = 1;

    /// <summary>The FG-04 NOTIFICATION_ROUTING event family that decides its channels and recipient roles (ADR-004).</summary>
    public const string EventFamilyCode = "CONCERN_ESCALATION";
}
