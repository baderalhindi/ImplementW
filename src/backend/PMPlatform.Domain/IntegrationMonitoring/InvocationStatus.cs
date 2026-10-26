namespace PMPlatform.Domain.IntegrationMonitoring;

/// <summary>
/// One attempt's outcome (ERD §6). An attempt is IN_PROGRESS until it ends, then fixed: a retry is a new attempt of the same
/// lineage, never a rewrite of this one (INT-CC-13). The lineage's state is its latest attempt's.
/// </summary>
public enum InvocationStatus
{
    /// <summary>Started and not yet ended.</summary>
    InProgress = 1,

    /// <summary>The operation completed technically. A business answer may still have been negative (FG-05 §6.2).</summary>
    Succeeded = 2,

    /// <summary>Failed, and no retry follows: a business or security refusal, or an attempt its source does not retry.</summary>
    Failed = 3,

    /// <summary>Failed technically; another attempt of the lineage is due at <c>next_attempt_at</c>.</summary>
    RetryScheduled = 4,

    /// <summary>Failed and the lineage's retries are exhausted or the failure is permanent; an operator decides what happens next (DC-INT-06).</summary>
    DeadLetter = 5,
}
