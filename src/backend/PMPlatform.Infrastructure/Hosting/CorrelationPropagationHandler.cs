using PMPlatform.Application.Common.Correlation;

namespace PMPlatform.Infrastructure.Hosting;

/// <summary>
/// api-conventions R-42: every outbound integration call that accepts a header carries the correlation id of the work it is part
/// of, so a provider's own logs can be joined to the request, and to an FG-05 invocation, that caused the call (TASK-075). A call
/// made outside any <see cref="CorrelationScope"/> is sent as it is.
/// </summary>
internal sealed class CorrelationPropagationHandler : DelegatingHandler
{
    public const string HeaderName = "X-Correlation-Id";

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (CorrelationScope.Current is { } correlationId && !request.Headers.Contains(HeaderName))
        {
            request.Headers.Add(HeaderName, correlationId.ToString());
        }

        return base.SendAsync(request, cancellationToken);
    }
}
