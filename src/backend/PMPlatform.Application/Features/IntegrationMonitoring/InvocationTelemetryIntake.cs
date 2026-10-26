using System.Text.RegularExpressions;
using PMPlatform.Application.Common.Events;
using PMPlatform.Domain.IntegrationMonitoring;

namespace PMPlatform.Application.Features.IntegrationMonitoring;

/// <summary>
/// Records an attempt another module observed (event-conventions §4 rows 3–5) as an invocation of its integration's instance, then applies
/// it to that instance's health. Runs in the outbox dispatcher's transaction, after the producer committed: nothing here can reach the
/// producer's work, and the producer's work never waited for it. The attempt is stored under the event's id, so a repeated delivery
/// stores nothing (EV-4). Codes and summaries that are not plain codes are redacted before they are stored (INT-CC-08): FG-05 keeps no
/// provider message, address or payload.
/// </summary>
internal sealed partial class InvocationTelemetryIntake(IIntegrationMonitoringRepository repository, IntegrationHealthRecorder health)
{
    public const string Redacted = "[REDACTED]";

    public async Task RecordAsync(EventEnvelope<IntegrationInvocationData> envelope, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        if (await repository.InvocationExistsAsync(envelope.EventId, cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        IntegrationInvocationData data = envelope.Data;
        if (Problem(data) is { } problem)
        {
            throw new InvalidOperationException($"Integration telemetry {envelope.MessageKey} is refused: {problem}.");
        }

        IntegrationDefinition definition = await repository.FindDefinitionByCodeAsync(data.IntegrationCode, cancellationToken).ConfigureAwait(false)
                                           ?? throw new InvalidOperationException($"Integration telemetry {envelope.MessageKey} names no integration definition {data.IntegrationCode}.");
        await using IIntegrationMonitoringWork work = await repository.BeginAsync(cancellationToken).ConfigureAwait(false);
        IntegrationInstance instance = await repository.LockTelemetryInstanceAsync(definition.Id, cancellationToken).ConfigureAwait(false)
                                       ?? throw new InvalidOperationException($"Integration {definition.Code} has no instance to attribute telemetry to.");

        // The lineage's previous attempt stops being its pending retry or its dead letter: this attempt follows it.
        Invocation? previous = await repository.FindLatestAttemptAsync(instance.Id, data.OperationCode, data.LineageKey, cancellationToken).ConfigureAwait(false);
        if (previous is { Status: InvocationStatus.RetryScheduled or InvocationStatus.DeadLetter })
        {
            previous.Status = InvocationStatus.Failed;
            previous.NextAttemptAt = null;
            previous.UpdatedAt = data.CompletedAt;
            previous.UpdatedBy = IntegrationMonitoringServicePrincipal.Id;
        }

        Invocation attempt = new()
        {
            Id = envelope.EventId,
            IntegrationInstanceId = instance.Id,
            OperationCode = data.OperationCode,
            CorrelationId = envelope.CorrelationId,
            IdempotencyKey = data.LineageKey,
            AttemptNo = (short)((previous?.AttemptNo ?? 0) + 1),
            SourceModule = envelope.SourceModule,
            SourceReference = SourceReferencePattern().IsMatch(data.SourceReference ?? string.Empty) ? data.SourceReference : null,
            StartedAt = data.StartedAt,
            CompletedAt = data.CompletedAt,
            Status = StatusOf(data),
            FailureCategory = CategoryOf(data.FailureKind),
            ResponseCode = Code(data.ResponseCode, ResponseCodePattern()),
            ErrorSummary = Code(data.ErrorSummary, ErrorSummaryPattern()),
            NextAttemptAt = data.RetryDisposition == IntegrationRetryDisposition.RetryScheduled ? data.NextAttemptAt : null,
            CreatedAt = data.CompletedAt,
            CreatedBy = IntegrationMonitoringServicePrincipal.Id,
            UpdatedAt = data.CompletedAt,
            UpdatedBy = IntegrationMonitoringServicePrincipal.Id,
        };
        repository.Add(attempt);
        await health.RecordAsync(definition, instance, attempt, cancellationToken).ConfigureAwait(false);

        if (await repository.SaveAsync(cancellationToken).ConfigureAwait(false) != IntegrationMonitoringSaveOutcome.Saved)
        {
            throw new InvalidOperationException($"Integration telemetry {envelope.MessageKey} could not be recorded; it is tried again.");
        }

        await work.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>What makes the data unusable, or null. A producer that breaks the contract has its message kept in the outbox with this error.</summary>
    public static string? Problem(IntegrationInvocationData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        bool failed = data.Outcome == IntegrationInvocationOutcome.Failed;
        bool retryScheduled = data.RetryDisposition == IntegrationRetryDisposition.RetryScheduled;
        return data switch
        {
            _ when !CodePattern().IsMatch(data.IntegrationCode ?? string.Empty) => "the integration code is not a code",
            _ when !CodePattern().IsMatch(data.OperationCode ?? string.Empty) => "the operation code is not a code",
            _ when string.IsNullOrWhiteSpace(data.LineageKey) || data.LineageKey.Length > 200 => "the lineage key is empty or longer than 200",
            _ when !Enum.IsDefined(data.Outcome) || !Enum.IsDefined(data.RetryDisposition) => "the outcome or the disposition is unknown",
            _ when failed != data.FailureKind.HasValue => "a failure kind is given exactly when the attempt failed",
            _ when !failed && data.RetryDisposition != IntegrationRetryDisposition.None => "a successful attempt is followed by nothing",
            _ when retryScheduled != data.NextAttemptAt.HasValue => "a next attempt is given exactly when one is scheduled",
            _ when data.CompletedAt < data.StartedAt => "the attempt ends before it starts",
            _ => null,
        };
    }

    private static InvocationStatus StatusOf(IntegrationInvocationData data) => (data.Outcome, data.RetryDisposition) switch
    {
        (IntegrationInvocationOutcome.Succeeded, _) => InvocationStatus.Succeeded,
        (_, IntegrationRetryDisposition.RetryScheduled) => InvocationStatus.RetryScheduled,
        (_, IntegrationRetryDisposition.DeadLetter) => InvocationStatus.DeadLetter,
        _ => InvocationStatus.Failed,
    };

    private static InvocationFailureCategory? CategoryOf(IntegrationFailureKind? kind) => kind switch
    {
        null => null,
        IntegrationFailureKind.Technical => InvocationFailureCategory.Technical,
        IntegrationFailureKind.BusinessValidation => InvocationFailureCategory.BusinessValidation,
        IntegrationFailureKind.Security => InvocationFailureCategory.Security,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown failure kind."),
    };

    /// <summary>A plain code is kept; anything else — text a provider wrote, which can echo an address or a token — is redacted.</summary>
    private static string? Code(string? value, Regex pattern) => value is null ? null : pattern.IsMatch(value) ? value : Redacted;

    /// <summary>An integration or operation code: upper snake case.</summary>
    [GeneratedRegex("^[A-Z][A-Z0-9_]{0,49}$", RegexOptions.CultureInvariant)]
    private static partial Regex CodePattern();

    [GeneratedRegex("^[A-Z0-9_]{1,50}$", RegexOptions.CultureInvariant)]
    private static partial Regex ResponseCodePattern();

    /// <summary>The kind of a fault: a type name, e.g. <c>SocketException</c>, <c>System.TimeoutException</c>.</summary>
    [GeneratedRegex(@"^[A-Za-z][A-Za-z0-9_.]{0,199}$", RegexOptions.CultureInvariant)]
    private static partial Regex ErrorSummaryPattern();

    /// <summary>A safe pointer to a record: its type and its id, e.g. <c>NotificationDelivery/{id}</c>.</summary>
    [GeneratedRegex("^[A-Z][A-Za-z0-9]{0,99}/[A-Za-z0-9-]{1,99}$", RegexOptions.CultureInvariant)]
    private static partial Regex SourceReferencePattern();
}
