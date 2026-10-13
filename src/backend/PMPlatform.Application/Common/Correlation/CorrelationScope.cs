namespace PMPlatform.Application.Common.Correlation;

/// <summary>
/// The correlation id of the work in progress (api-conventions R-41, R-42), carried with the asynchronous flow. The API sets it for
/// each request from <c>X-Correlation-Id</c>; a worker that continues a lineage sets the lineage's own, so an integration retry made
/// hours later by a background pass is traced to the request that began it (TASK-075). Everything that records a correlation id —
/// audit events, outbox envelopes, invocations, outbound calls — reads it through <c>IAuditRequestContext</c> or <see cref="Current"/>.
/// </summary>
public static class CorrelationScope
{
    private static readonly AsyncLocal<Guid?> Ambient = new();

    /// <summary>The id of the innermost scope this flow runs in; null outside any.</summary>
    public static Guid? Current => Ambient.Value;

    /// <summary>Runs the rest of the flow under <paramref name="correlationId"/> until the returned scope is disposed; scopes nest.</summary>
    public static IDisposable Begin(Guid correlationId)
    {
        if (correlationId == Guid.Empty)
        {
            throw new ArgumentException("A correlation id is never empty.", nameof(correlationId));
        }

        Guid? outer = Ambient.Value;
        Ambient.Value = correlationId;
        return new Restore(outer);
    }

    private sealed class Restore(Guid? outer) : IDisposable
    {
        public void Dispose() => Ambient.Value = outer;
    }
}
