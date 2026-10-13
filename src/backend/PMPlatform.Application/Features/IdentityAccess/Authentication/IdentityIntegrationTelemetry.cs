using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PMPlatform.Application.Common.Correlation;
using PMPlatform.Application.Common.Events;
using PMPlatform.Domain.Common;
using IntegrationInvocationCompleted = PMPlatform.Application.Features.IdentityAccess.Contracts.Events.IntegrationInvocationCompleted;

namespace PMPlatform.Application.Features.IdentityAccess.Authentication;

/// <summary>
/// Publishes each call IdentityAccess makes to an identity provider as <c>IdentityAccess.IntegrationInvocationCompleted</c> (ADR-003 §8.2
/// edges 31, 33), committed in a transaction of its own because the call changes nothing of IdentityAccess's. Telemetry is best effort:
/// if it cannot be committed, the failure is logged and the sign-in goes on — monitoring never decides whether someone may sign in
/// (FG-05 §26: ingestion decoupled from the business transaction).
/// </summary>
internal sealed partial class IdentityIntegrationTelemetry(IServiceScopeFactory scopes, TimeProvider timeProvider, ILogger<IdentityIntegrationTelemetry> logger)
{
    public const string SourceModule = "IdentityAccess";

    /// <summary>Makes the call, records how it ended, and returns its result or rethrows its fault.</summary>
    public async Task<TResult> ObserveAsync<TResult>(
        string integrationCode, string operationCode, Func<Task<TResult>> call, Func<TResult, InvocationObservation?> classify)
    {
        ArgumentNullException.ThrowIfNull(call);
        ArgumentNullException.ThrowIfNull(classify);

        DateTimeOffset startedAt = timeProvider.GetUtcNow();
        TResult result;
        try
        {
            result = await call().ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            await RecordAsync(integrationCode, operationCode, startedAt, InvocationObservation.Faulted(exception)).ConfigureAwait(false);
            throw;
        }

        if (classify(result) is { } observation)
        {
            await RecordAsync(integrationCode, operationCode, startedAt, observation).ConfigureAwait(false);
        }

        return result;
    }

    private async Task RecordAsync(string integrationCode, string operationCode, DateTimeOffset startedAt, InvocationObservation observation)
    {
        DateTimeOffset completedAt = timeProvider.GetUtcNow();
        Guid invocationId = Guid.CreateVersion7(completedAt);
        string key = invocationId.ToString();
        IntegrationInvocationCompleted envelope = new()
        {
            EventId = invocationId,
            EventType = IntegrationInvocationCompleted.Type,
            SchemaVersion = IntegrationInvocationCompleted.CurrentSchemaVersion,
            Kind = EventKind.DomainEvent,
            MessageKey = EventMessageKey.Of(IntegrationInvocationCompleted.Type, key),
            IdempotencyKey = key,
            OccurredAt = completedAt,
            SourceModule = SourceModule,
            CorrelationId = CorrelationScope.Current ?? Guid.NewGuid(),
            Actor = new EventActor(AuditActorType.Service, IdentityIntegrationServicePrincipal.Id),
            Subject = new EventSubject(SourceModule, "IntegrationInvocation", invocationId, null),
            Scope = new EventScope(null, null, null),
            Data = new IntegrationInvocationData(
                integrationCode, operationCode, key, observation.Outcome, observation.FailureKind, IntegrationRetryDisposition.None,
                startedAt, completedAt, observation.ResponseCode, observation.ErrorSummary, null, null),
        };

        try
        {
            await using AsyncServiceScope scope = scopes.CreateAsyncScope();

            // Not cancellable: an aborted request's provider call is still telemetry.
            await scope.ServiceProvider.GetRequiredService<IOutbox>().RecordAsync(envelope, CancellationToken.None).ConfigureAwait(false);
        }
#pragma warning disable CA1031 // Telemetry is best effort: losing one observation must never fail the sign-in it observed.
        catch (Exception exception)
#pragma warning restore CA1031
        {
            LogNotRecorded(logger, integrationCode, operationCode, exception.GetType().Name);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Integration telemetry for {Integration} {Operation} was not recorded: {Reason}.")]
    private static partial void LogNotRecorded(ILogger logger, string integration, string operation, string reason);
}
