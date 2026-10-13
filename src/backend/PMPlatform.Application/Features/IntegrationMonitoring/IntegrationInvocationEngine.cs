using Microsoft.Extensions.Logging;
using PMPlatform.Application.Common.Auditing;
using PMPlatform.Application.Common.Correlation;
using PMPlatform.Application.Features.IntegrationMonitoring.Contracts;
using PMPlatform.Domain.IntegrationMonitoring;

namespace PMPlatform.Application.Features.IntegrationMonitoring;

/// <summary>
/// The invocation, retry and dead-letter engine for the operations FG-05 runs itself (Blueprint §17.1, FG-05 §7). A lineage is one logical
/// operation: its attempts share the instance, the operation, the idempotency key and the correlation id of the request that began it, and
/// each attempt is a new row — a retry never rewrites the attempt it follows (INT-CC-13).
/// <list type="bullet">
/// <item>Starting a lineage whose key already began one answers that lineage and runs nothing (idempotent; the unique attempt number makes a
/// race lose, not duplicate).</item>
/// <item>An attempt is committed IN_PROGRESS before the operation runs, so a crash leaves evidence; it then ends SUCCEEDED, RETRY_SCHEDULED,
/// FAILED or DEAD_LETTER under the definition's policy (<see cref="IntegrationRetryPolicy"/>).</item>
/// <item>Retries — by the worker when due, or by an operator — run under the lineage's correlation id (<see cref="CorrelationScope"/>), so
/// the audit, the telemetry and the outbound calls of a retry hours later trace back to the request that began it.</item>
/// <item>A lineage that succeeded is never retried (INT-CC-16). Nothing the engine records reaches a business table (BR-INT-002).</item>
/// </list>
/// </summary>
internal sealed partial class IntegrationInvocationEngine(
    IIntegrationMonitoringRepository repository,
    IEnumerable<IIntegrationOperation> operations,
    IntegrationHealthRecorder health,
    OperationalAlerting alerting,
    IntegrationMonitoringPolicy policy,
    IAuditTrail audit,
    IAuditRequestContext request,
    TimeProvider timeProvider,
    ILogger<IntegrationInvocationEngine> logger)
{
    public IIntegrationOperation? OperationFor(string integrationCode, string operationCode) =>
        operations.SingleOrDefault(o => o.IntegrationCode == integrationCode && o.OperationCode == operationCode);

    public IIntegrationOperation? RunOperationFor(string integrationCode, SyncRunType runType) =>
        operations.FirstOrDefault(o => o.IntegrationCode == integrationCode && o.RunType == runType);

    public IReadOnlyList<string> OperationsOf(string integrationCode) =>
        [.. operations.Where(o => o.IntegrationCode == integrationCode).Select(o => o.OperationCode).Order(StringComparer.Ordinal)];

    /// <summary>
    /// Why FG-05 would not retry the lineage of <paramref name="attempt"/> now, or null if it would (FG-05 §20.1: the backend decides).
    /// </summary>
    public AdministrationRefusal? RetryRefusal(IntegrationDefinition definition, Invocation attempt, Invocation latest)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(attempt);
        ArgumentNullException.ThrowIfNull(latest);
        return attempt switch
        {
            _ when attempt.SourceModule != IntegrationMonitoringServicePrincipal.Module || OperationFor(definition.Code, attempt.OperationCode) is null
                => new AdministrationRefusal(IntegrationMonitoringErrorCodes.RetryNotPermitted, Conflict: false),
            _ when latest.Status == InvocationStatus.Succeeded => new AdministrationRefusal(IntegrationMonitoringErrorCodes.InvocationAlreadySucceeded, Conflict: true),
            _ when latest.Id != attempt.Id || latest.Status is not (InvocationStatus.DeadLetter or InvocationStatus.RetryScheduled)
                => new AdministrationRefusal(IntegrationMonitoringErrorCodes.InvocationNotRetryable, Conflict: true),
            _ when definition.Status != IntegrationDefinitionStatus.Active => new AdministrationRefusal(IntegrationMonitoringErrorCodes.IntegrationDisabled, Conflict: false),
            _ => null,
        };
    }

    /// <summary>
    /// Begins the lineage <paramref name="idempotencyKey"/> names and runs its first attempt now; or, if that key already began one,
    /// answers it unchanged. <paramref name="requested"/> is the audit event of the request, staged with the first attempt.
    /// </summary>
    public async Task<Invocation> StartAsync(
        IntegrationDefinition definition, IIntegrationOperation operation, string idempotencyKey, Func<Invocation, AuditEntry> requested,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(requested);

        IntegrationInstance instance = await repository.FindPrimaryInstanceAsync(definition.Id, cancellationToken).ConfigureAwait(false)
                                       ?? throw new InvalidOperationException($"Integration {definition.Code} has no instance.");
        if (await repository.FindLatestAttemptAsync(instance.Id, operation.OperationCode, idempotencyKey, cancellationToken).ConfigureAwait(false) is { } begun)
        {
            return begun;
        }

        (Invocation Attempt, SyncRun? Run)? opened;
        await using (IIntegrationMonitoringWork work = await repository.BeginAsync(cancellationToken).ConfigureAwait(false))
        {
            opened = Open(instance, operation, idempotencyKey, 1, request.CorrelationId);
            audit.Stage(requested(opened.Value.Attempt));
            if (await repository.SaveAsync(cancellationToken).ConfigureAwait(false) != IntegrationMonitoringSaveOutcome.Saved)
            {
                opened = null;
            }
            else
            {
                await work.CommitAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        if (opened is not { } started)
        {
            // Another request with the same key began it first: answer its lineage.
            return await repository.FindLatestAttemptAsync(instance.Id, operation.OperationCode, idempotencyKey, cancellationToken).ConfigureAwait(false)
                   ?? throw new InvalidOperationException($"Lineage {idempotencyKey} of {definition.Code} could not be begun or found.");
        }

        await RunAsync(definition, instance.Id, operation, started.Attempt, started.Run, cancellationToken).ConfigureAwait(false);
        return started.Attempt;
    }

    /// <summary>
    /// An operator's retry (FG-05 §13.3): the lineage's next attempt, now, under its own correlation id and key. Refused unless
    /// <see cref="RetryRefusal"/> allows it once the attempt is locked, so two operators' retries make one attempt.
    /// </summary>
    public async Task<(Invocation? Attempt, AdministrationRefusal? Refusal)> RetryNowAsync(Guid actorId, Guid invocationId, CancellationToken cancellationToken)
    {
        IntegrationDefinition definition;
        IIntegrationOperation operation;
        (Invocation Attempt, SyncRun? Run) opened;
        await using (IIntegrationMonitoringWork work = await repository.BeginAsync(cancellationToken).ConfigureAwait(false))
        {
            Invocation? previous = await repository.LockInvocationAsync(invocationId, cancellationToken).ConfigureAwait(false);
            if (previous is null)
            {
                return (null, null);
            }

            IntegrationInstance instance = (await repository.FindInstanceAsync(previous.IntegrationInstanceId, cancellationToken).ConfigureAwait(false))!;
            definition = (await repository.FindDefinitionAsync(instance.IntegrationDefinitionId, null, cancellationToken).ConfigureAwait(false))!;
            Invocation latest = (await repository.FindLatestAttemptAsync(instance.Id, previous.OperationCode, previous.IdempotencyKey ?? string.Empty, cancellationToken).ConfigureAwait(false))
                                ?? previous;
            if (RetryRefusal(definition, previous, latest) is { } refusal)
            {
                return (previous, refusal);
            }

            operation = OperationFor(definition.Code, previous.OperationCode)!;
            opened = Follow(instance, operation, previous);
            audit.Stage(IntegrationMonitoringAudit.InvocationRetried(actorId, previous, opened.Attempt, definition.Code));
            if (await repository.SaveAsync(cancellationToken).ConfigureAwait(false) != IntegrationMonitoringSaveOutcome.Saved)
            {
                return (previous, new AdministrationRefusal(IntegrationMonitoringErrorCodes.InvocationNotRetryable, Conflict: true));
            }

            await work.CommitAsync(cancellationToken).ConfigureAwait(false);
        }

        LogRetried(logger, actorId, definition.Code, opened.Attempt.OperationCode, opened.Attempt.AttemptNo);
        await RunAsync(definition, opened.Attempt.IntegrationInstanceId, operation, opened.Attempt, opened.Run, cancellationToken).ConfigureAwait(false);
        return (opened.Attempt, null);
    }

    /// <summary>The worker's retries: each RETRY_SCHEDULED attempt that is due becomes its lineage's next attempt. Returns how many ran.</summary>
    public async Task<int> RunDueRetriesAsync(int batchSize, CancellationToken cancellationToken)
    {
        int ran = 0;
        foreach (Guid invocationId in await repository.FindDueRetryIdsAsync(timeProvider.GetUtcNow(), batchSize, cancellationToken).ConfigureAwait(false))
        {
            try
            {
                ran += await RetryDueAsync(invocationId, cancellationToken).ConfigureAwait(false) ? 1 : 0;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
#pragma warning disable CA1031 // One lineage that cannot be retried must not stop the pass for the others.
            catch (Exception exception)
#pragma warning restore CA1031
            {
                LogPassItemFailed(logger, "retry", invocationId, exception.GetType().Name);
            }
        }

        return ran;
    }

    /// <summary>
    /// Ends FG-05's attempts that have been IN_PROGRESS longer than the policy allows — their process stopped — as technical failures,
    /// retried or dead-lettered like any other. Returns how many it ended.
    /// </summary>
    public async Task<int> EndAbandonedAttemptsAsync(int batchSize, CancellationToken cancellationToken)
    {
        DateTimeOffset startedBefore = timeProvider.GetUtcNow() - policy.AbandonAfter;
        int ended = 0;
        foreach (Guid invocationId in await repository.FindAbandonedAttemptIdsAsync(IntegrationMonitoringServicePrincipal.Module, startedBefore, batchSize, cancellationToken)
                     .ConfigureAwait(false))
        {
            try
            {
                await using IIntegrationMonitoringWork work = await repository.BeginAsync(cancellationToken).ConfigureAwait(false);
                if (await repository.ClaimAbandonedAttemptAsync(invocationId, startedBefore, cancellationToken).ConfigureAwait(false) is not { } attempt)
                {
                    continue;
                }

                IntegrationInstance instance = (await repository.LockInstanceAsync(attempt.IntegrationInstanceId, cancellationToken).ConfigureAwait(false))!;
                IntegrationDefinition definition = (await repository.FindDefinitionAsync(instance.IntegrationDefinitionId, null, cancellationToken).ConfigureAwait(false))!;
                SyncRun? run = attempt.SyncRunId is { } runId ? await repository.FindSyncRunAsync(runId, cancellationToken).ConfigureAwait(false) : null;
                await ApplyOutcomeAsync(definition, instance, attempt, run, IntegrationOperationResult.Failure(InvocationFailureCategory.Technical, "ABANDONED"), cancellationToken)
                    .ConfigureAwait(false);
                if (await repository.SaveAsync(cancellationToken).ConfigureAwait(false) == IntegrationMonitoringSaveOutcome.Saved)
                {
                    await work.CommitAsync(cancellationToken).ConfigureAwait(false);
                    ended++;
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
#pragma warning disable CA1031 // One attempt that cannot be ended must not stop the pass for the others.
            catch (Exception exception)
#pragma warning restore CA1031
            {
                LogPassItemFailed(logger, "abandoned attempt", invocationId, exception.GetType().Name);
            }
        }

        return ended;
    }

    private async Task<bool> RetryDueAsync(Guid invocationId, CancellationToken cancellationToken)
    {
        IntegrationDefinition definition;
        IIntegrationOperation operation;
        (Invocation Attempt, SyncRun? Run) opened;
        await using (IIntegrationMonitoringWork work = await repository.BeginAsync(cancellationToken).ConfigureAwait(false))
        {
            if (await repository.ClaimDueRetryAsync(invocationId, timeProvider.GetUtcNow(), cancellationToken).ConfigureAwait(false) is not { } previous)
            {
                return false;
            }

            IntegrationInstance instance = (await repository.FindInstanceAsync(previous.IntegrationInstanceId, cancellationToken).ConfigureAwait(false))!;
            definition = (await repository.FindDefinitionAsync(instance.IntegrationDefinitionId, null, cancellationToken).ConfigureAwait(false))!;
            if (OperationFor(definition.Code, previous.OperationCode) is not { } known)
            {
                LogNoOperation(logger, definition.Code, previous.OperationCode, previous.Id);
                return false;
            }

            operation = known;
            opened = Follow(instance, operation, previous);
            if (await repository.SaveAsync(cancellationToken).ConfigureAwait(false) != IntegrationMonitoringSaveOutcome.Saved)
            {
                return false;
            }

            await work.CommitAsync(cancellationToken).ConfigureAwait(false);
        }

        await RunAsync(definition, opened.Attempt.IntegrationInstanceId, operation, opened.Attempt, opened.Run, cancellationToken).ConfigureAwait(false);
        return true;
    }

    /// <summary>The attempt after <paramref name="previous"/>, which stops being its lineage's pending retry or dead letter.</summary>
    private (Invocation Attempt, SyncRun? Run) Follow(IntegrationInstance instance, IIntegrationOperation operation, Invocation previous)
    {
        DateTimeOffset now = timeProvider.GetUtcNow();
        previous.Status = InvocationStatus.Failed;
        previous.NextAttemptAt = null;
        previous.UpdatedAt = now;
        previous.UpdatedBy = IntegrationMonitoringServicePrincipal.Id;
        return Open(instance, operation, previous.IdempotencyKey!, (short)(previous.AttemptNo + 1), previous.CorrelationId);
    }

    /// <summary>A new IN_PROGRESS attempt, and its run when the operation is one, added to the unit of work.</summary>
    private (Invocation Attempt, SyncRun? Run) Open(IntegrationInstance instance, IIntegrationOperation operation, string idempotencyKey, short attemptNo, Guid correlationId)
    {
        DateTimeOffset now = timeProvider.GetUtcNow();
        SyncRun? run = operation.RunType is { } runType
            ? new SyncRun
            {
                Id = Guid.CreateVersion7(now),
                IntegrationInstanceId = instance.Id,
                RunType = runType,
                CorrelationId = correlationId,
                StartedAt = now,
                Status = SyncRunStatus.Running,
                CreatedAt = now,
                CreatedBy = IntegrationMonitoringServicePrincipal.Id,
                UpdatedAt = now,
                UpdatedBy = IntegrationMonitoringServicePrincipal.Id,
            }
            : null;
        Invocation attempt = new()
        {
            Id = Guid.CreateVersion7(now),
            IntegrationInstanceId = instance.Id,
            SyncRunId = run?.Id,
            OperationCode = operation.OperationCode,
            CorrelationId = correlationId,
            IdempotencyKey = idempotencyKey,
            AttemptNo = attemptNo,
            SourceModule = IntegrationMonitoringServicePrincipal.Module,
            StartedAt = now,
            Status = InvocationStatus.InProgress,
            CreatedAt = now,
            CreatedBy = IntegrationMonitoringServicePrincipal.Id,
            UpdatedAt = now,
            UpdatedBy = IntegrationMonitoringServicePrincipal.Id,
        };
        if (run is not null)
        {
            repository.Add(run);
        }

        repository.Add(attempt);
        return (attempt, run);
    }

    /// <summary>Runs the attempt under its lineage's correlation id and records how it ended, with the instance locked.</summary>
    private async Task RunAsync(
        IntegrationDefinition definition, Guid instanceId, IIntegrationOperation operation, Invocation attempt, SyncRun? run, CancellationToken cancellationToken)
    {
        using IDisposable scope = CorrelationScope.Begin(attempt.CorrelationId);
        IntegrationOperationResult result;
        try
        {
            result = await operation.ExecuteAsync(new IntegrationOperationContext(attempt.Id, run?.Id, attempt.CorrelationId, attempt.IdempotencyKey!, attempt.AttemptNo), cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            result = IntegrationOperationResult.Faulted(exception);
        }

        await using IIntegrationMonitoringWork work = await repository.BeginAsync(cancellationToken).ConfigureAwait(false);
        IntegrationInstance instance = (await repository.LockInstanceAsync(instanceId, cancellationToken).ConfigureAwait(false))!;
        await ApplyOutcomeAsync(definition, instance, attempt, run, result, cancellationToken).ConfigureAwait(false);
        if (await repository.SaveAsync(cancellationToken).ConfigureAwait(false) != IntegrationMonitoringSaveOutcome.Saved)
        {
            throw new InvalidOperationException($"The outcome of attempt {attempt.Id} of {definition.Code} {attempt.OperationCode} could not be recorded.");
        }

        await work.CommitAsync(cancellationToken).ConfigureAwait(false);
        LogAttemptEnded(logger, definition.Code, attempt.OperationCode, attempt.AttemptNo, attempt.Status, attempt.ResponseCode);
    }

    /// <summary>Ends the attempt and its run, raises a mismatch alert, and applies the attempt to the instance's health.</summary>
    private async Task ApplyOutcomeAsync(
        IntegrationDefinition definition, IntegrationInstance instance, Invocation attempt, SyncRun? run, IntegrationOperationResult result, CancellationToken cancellationToken)
    {
        DateTimeOffset now = timeProvider.GetUtcNow();
        attempt.CompletedAt = now;
        attempt.ResponseCode = result.ResponseCode;
        attempt.ErrorSummary = result.ErrorSummary;
        attempt.FailureCategory = result.FailureCategory;
        (attempt.Status, attempt.NextAttemptAt) = result.FailureCategory is { } failure
            ? IntegrationRetryPolicy.After(definition, attempt.AttemptNo, failure, now)
            : (InvocationStatus.Succeeded, null);
        attempt.UpdatedAt = now;
        attempt.UpdatedBy = IntegrationMonitoringServicePrincipal.Id;

        if (run is not null)
        {
            IntegrationRunOutcome outcome = result.Run ?? new IntegrationRunOutcome(result.Succeeded ? SyncRunStatus.Succeeded : SyncRunStatus.Failed, 0, 0, 0, 0, null);
            run.Status = outcome.Status;
            run.RecordsRead = outcome.RecordsRead;
            run.RecordsApplied = outcome.RecordsApplied;
            run.RecordsRejected = outcome.RecordsRejected;
            run.MismatchCount = outcome.MismatchCount;
            run.CompletedAt = now;
            run.UpdatedAt = now;
            run.UpdatedBy = IntegrationMonitoringServicePrincipal.Id;
            if (run.RunType == SyncRunType.Reconciliation)
            {
                audit.Stage(IntegrationMonitoringAudit.ReconciliationCompleted(run, attempt, definition.Code));
            }

            if (outcome.MismatchCount > 0)
            {
                await alerting.RaiseAsync(
                    definition, instance, OperationalAlertType.ReconciliationMismatch, outcome.MismatchDetail ?? $"{outcome.MismatchCount} mismatches",
                    attempt.Id, run.Id, cancellationToken).ConfigureAwait(false);
            }
        }

        await health.RecordAsync(definition, instance, attempt, cancellationToken).ConfigureAwait(false);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Integration {Integration} {Operation} attempt {Attempt} ended {Status} ({Response}).")]
    private static partial void LogAttemptEnded(ILogger logger, string integration, string operation, short attempt, InvocationStatus status, string? response);

    [LoggerMessage(Level = LogLevel.Information, Message = "Integration monitoring: {ActorId} retried {Integration} {Operation} as attempt {Attempt}.")]
    private static partial void LogRetried(ILogger logger, Guid actorId, string integration, string operation, short attempt);

    [LoggerMessage(Level = LogLevel.Error, Message = "Integration {Integration} has no operation {Operation}; attempt {InvocationId} is not retried.")]
    private static partial void LogNoOperation(ILogger logger, string integration, string operation, Guid invocationId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Integration monitoring {Item} {InvocationId} failed: {Reason}. It is tried again on the next pass.")]
    private static partial void LogPassItemFailed(ILogger logger, string item, Guid invocationId, string reason);
}

/// <summary>Why the engine refused a command: a module code, answered as 409 when <see cref="Conflict"/> (the state moved on), else 422.</summary>
internal sealed record AdministrationRefusal(string Code, bool Conflict);
