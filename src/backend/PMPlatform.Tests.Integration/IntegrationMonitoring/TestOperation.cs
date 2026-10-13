using System.Collections.Concurrent;
using PMPlatform.Application.Common.Correlation;
using PMPlatform.Application.Features.IntegrationMonitoring;
using PMPlatform.Domain.IntegrationMonitoring;

namespace PMPlatform.Tests.Integration.IntegrationMonitoring;

/// <summary>
/// An operation FG-05 runs against the test fixture's internal system (<see cref="Code"/>), whose outcome the test sets. It records every
/// attempt with the key and correlation id it ran under, and counts each key's successful effects, so a test can tell a retry from a second
/// operation.
/// </summary>
internal sealed class TestOperation : IIntegrationOperation
{
    public const string Code = "TEST_SYSTEM";
    public const string Operation = "TEST_PROBE";

    private readonly ConcurrentQueue<TestOperationCall> _calls = new();
    private readonly ConcurrentDictionary<string, int> _effects = new(StringComparer.Ordinal);

    public string IntegrationCode => Code;

    public string OperationCode => Operation;

    public SyncRunType? RunType => null;

    public bool IsAvailable => true;

    /// <summary>What the next attempts return; a success by default.</summary>
    public Func<IntegrationOperationContext, IntegrationOperationResult> Outcome { get; set; } = _ => IntegrationOperationResult.Success();

    public IReadOnlyList<TestOperationCall> CallsFor(string key) => [.. _calls.Where(c => c.IdempotencyKey == key)];

    /// <summary>How many times the operation took effect for <paramref name="key"/>: what a provider would have done.</summary>
    public int EffectsFor(string key) => _effects.TryGetValue(key, out int count) ? count : 0;

    public Task<IntegrationOperationResult> ExecuteAsync(IntegrationOperationContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        _calls.Enqueue(new TestOperationCall(context.IdempotencyKey, context.AttemptNo, context.CorrelationId, CorrelationScope.Current));
        IntegrationOperationResult result = Outcome(context);
        if (result.Succeeded)
        {
            _effects.AddOrUpdate(context.IdempotencyKey, 1, (_, count) => count + 1);
        }

        return Task.FromResult(result);
    }

    public static IntegrationOperationResult Unreachable() => IntegrationOperationResult.Failure(InvocationFailureCategory.Technical, "UNREACHABLE");
}

/// <summary>
/// One attempt as the operation saw it: its lineage key and number, the lineage's correlation id, and the correlation id the flow ran under
/// (<see cref="CorrelationScope"/>), as an outbound call would send it.
/// </summary>
internal sealed record TestOperationCall(string IdempotencyKey, short AttemptNo, Guid CorrelationId, Guid? AmbientCorrelationId);
