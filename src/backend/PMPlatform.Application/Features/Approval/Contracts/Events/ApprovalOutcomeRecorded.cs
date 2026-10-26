using PMPlatform.Application.Common.Events;

namespace PMPlatform.Application.Features.Approval.Contracts.Events;

/// <summary>
/// <c>Approval.ApprovalOutcomeRecorded</c> (event-conventions §4 row 2; ADR-003 §8.2 edge 28): a run has ended. The
/// idempotency key is the run's <c>outcome_idempotency_key</c>; <c>subject</c> names the source record and revision.
/// </summary>
public sealed record ApprovalOutcomeRecorded : EventEnvelope<ApprovalOutcomeData>
{
    public const string Type = "Approval.ApprovalOutcomeRecorded";

    public const int CurrentSchemaVersion = 1;
}
