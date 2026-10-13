using PMPlatform.Domain.IntegrationMonitoring;

namespace PMPlatform.Application.Features.IntegrationMonitoring;

/// <summary>
/// What follows a failed attempt of an operation FG-05 runs, under its definition's policy (<c>max_attempts</c>, <c>retry_backoff_seconds</c>):
/// a technical failure is retried after an exponential back-off until the attempts are spent, then dead-lettered; a security refusal needs
/// a person and is dead-lettered at once; a business refusal is the source's to correct and is simply FAILED (FG-05 §7, DC-INT-04).
/// </summary>
public static class IntegrationRetryPolicy
{
    public static (InvocationStatus Status, DateTimeOffset? NextAttemptAt) After(
        IntegrationDefinition definition, short attemptNo, InvocationFailureCategory failure, DateTimeOffset completedAt)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return failure switch
        {
            InvocationFailureCategory.Technical when attemptNo < definition.MaxAttempts =>
                (InvocationStatus.RetryScheduled, completedAt + BackoffAfter(definition, attemptNo)),
            InvocationFailureCategory.Technical => (InvocationStatus.DeadLetter, null),
            InvocationFailureCategory.Security => (InvocationStatus.DeadLetter, null),
            InvocationFailureCategory.BusinessValidation => (InvocationStatus.Failed, null),
            _ => throw new ArgumentOutOfRangeException(nameof(failure), failure, "Unknown failure category."),
        };
    }

    /// <summary>The wait after attempt <paramref name="attemptNo"/>: the back-off, doubled for each attempt before it.</summary>
    public static TimeSpan BackoffAfter(IntegrationDefinition definition, short attemptNo)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return TimeSpan.FromSeconds(definition.RetryBackoffSeconds * Math.Pow(2, Math.Max(0, attemptNo - 1)));
    }
}
