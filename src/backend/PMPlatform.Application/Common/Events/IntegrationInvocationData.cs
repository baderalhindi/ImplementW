namespace PMPlatform.Application.Common.Events;

/// <summary>
/// The data of every <c>IntegrationInvocationCompleted</c> (event-conventions §4 rows 3–5; ADR-003 §8.2 edges 31–33): one attempt at
/// one call to an external integration, as the module that made it observed it. It crosses to IntegrationMonitoring through this type
/// in <c>Application/Common</c>, as <see cref="NotificationIntentData"/> crosses to Notifications. It carries codes and times only:
/// never a payload, an address, a credential or a provider's own message (EV-3).
/// </summary>
/// <param name="IntegrationCode">The integration definition's code, e.g. <c>DIRECTORY</c>, <c>EXCHANGE_EMAIL</c>.</param>
/// <param name="OperationCode">What was called, e.g. <c>AUTHENTICATE</c>, <c>SEND</c>; upper snake case.</param>
/// <param name="LineageKey">The same for every attempt of one logical operation (a delivery's id); a fresh key for a call nobody retries.</param>
/// <param name="Outcome">Whether the attempt completed technically.</param>
/// <param name="FailureKind">Why it failed; set exactly when <paramref name="Outcome"/> is FAILED.</param>
/// <param name="RetryDisposition">What the caller does next: nothing, another attempt, or nothing more (dead letter).</param>
/// <param name="StartedAt">When the attempt began.</param>
/// <param name="CompletedAt">When it ended.</param>
/// <param name="ResponseCode">A fixed code, e.g. <c>UNREACHABLE</c>, <c>REJECTED</c>.</param>
/// <param name="ErrorSummary">The kind of fault, e.g. <c>SocketException</c>.</param>
/// <param name="NextAttemptAt">When the caller's next attempt is due; set exactly when the disposition is RETRY_SCHEDULED.</param>
/// <param name="SourceReference">A safe pointer to the business record, e.g. <c>NotificationDelivery/{id}</c>.</param>
public sealed record IntegrationInvocationData(
    string IntegrationCode,
    string OperationCode,
    string LineageKey,
    IntegrationInvocationOutcome Outcome,
    IntegrationFailureKind? FailureKind,
    IntegrationRetryDisposition RetryDisposition,
    DateTimeOffset StartedAt,
    DateTimeOffset CompletedAt,
    string? ResponseCode,
    string? ErrorSummary,
    DateTimeOffset? NextAttemptAt,
    string? SourceReference);

/// <summary>Whether an observed attempt completed technically (FG-05 §6.2).</summary>
public enum IntegrationInvocationOutcome
{
    Succeeded = 1,

    Failed = 2,
}

/// <summary>Why an observed attempt failed (FG-05 §6.3): only a technical failure says the provider is unavailable.</summary>
public enum IntegrationFailureKind
{
    /// <summary>No connection, a timeout, a TLS or provider error.</summary>
    Technical = 1,

    /// <summary>The provider answered and refused the content: a wrong password or code, an unverified identity.</summary>
    BusinessValidation = 2,

    /// <summary>A signature, token or authorisation check failed.</summary>
    Security = 3,
}

/// <summary>What the module that made the call does after this attempt.</summary>
public enum IntegrationRetryDisposition
{
    /// <summary>Nothing: it succeeded, or nobody retries this call (a sign-in is retried by the person, as a new call).</summary>
    None = 1,

    /// <summary>Another attempt of the same lineage is due at <see cref="IntegrationInvocationData.NextAttemptAt"/>.</summary>
    RetryScheduled = 2,

    /// <summary>The last attempt the caller makes; the lineage is dead-lettered at its source.</summary>
    DeadLetter = 3,
}
