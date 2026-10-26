using PMPlatform.Api.Correlation;
using PMPlatform.Application.Common.Correlation;
using PMPlatform.Application.Common.Observability;
using Sentry.Extensibility;

namespace PMPlatform.Api.Observability;

/// <summary>
/// Runs on every event and transaction before it is queued. It tags each with the correlation id of the work it belongs to
/// (api-conventions R-42) — the request's, which the 500 envelope also carries, or for a background pass the lineage's — so an
/// error in the dashboard leads straight to the request, its log lines and its audit events. It also keeps out of the event what
/// the APM has no use for: the query string, cookies, the request body, the client address, and every request header but the
/// few named here — and masks, in a logged error's formatted message, the values of properties with a registered name. What
/// remains is redacted on the way out (<see cref="ApmPayloadRedaction"/>).
/// </summary>
internal sealed class ApmEventEnrichment(IHttpContextAccessor httpContextAccessor) : ISentryEventProcessor, ISentryTransactionProcessor
{
    public const string CorrelationTag = "correlation_id";

    private static readonly HashSet<string> KeptHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "Accept", "Content-Length", "Content-Type", "User-Agent", CorrelationId.HeaderName,
    };

    public SentryEvent Process(SentryEvent @event)
    {
        ArgumentNullException.ThrowIfNull(@event);
        Enrich(@event);
        MaskLoggedValues(@event);
        return @event;
    }

    public SentryTransaction Process(SentryTransaction transaction)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        Enrich(transaction);
        return transaction;
    }

    private void Enrich<T>(T item)
        where T : IEventLike, IHasTags
    {
        if (!item.Tags.ContainsKey(CorrelationTag) && CurrentCorrelationId() is { } correlationId)
        {
            item.SetTag(CorrelationTag, correlationId.ToString());
        }

        SentryRequest request = item.Request;
        request.QueryString = null;
        request.Cookies = null;
        request.Data = null;
        request.Env.Remove("REMOTE_ADDR");
        foreach (string header in request.Headers.Keys.Where(header => !KeptHeaders.Contains(header)).ToList())
        {
            request.Headers.Remove(header);
        }
    }

    // A logged error carries its message both as a template with its properties as tags, and formatted, where a value appears
    // without its name: "Sign-in failed for {Password}" becomes "Sign-in failed for hunter2". The value of a property with a
    // registered name is masked in the formatted text here; the tag itself is masked on the way out.
    private static void MaskLoggedValues(SentryEvent @event)
    {
        if (@event.Message?.Formatted is not { } formatted)
        {
            return;
        }

        foreach (string value in @event.Tags.Where(tag => TelemetryRedactor.IsSensitiveName(tag.Key) && tag.Value.Length > 0).Select(tag => tag.Value))
        {
            formatted = formatted.Replace(value, TelemetryRedactor.Mask, StringComparison.Ordinal);
        }

        @event.Message.Formatted = formatted;
    }

    private Guid? CurrentCorrelationId() =>
        httpContextAccessor.HttpContext is { } context && CorrelationId.Of(context) is var id && id != Guid.Empty
            ? id
            : CorrelationScope.Current;
}
