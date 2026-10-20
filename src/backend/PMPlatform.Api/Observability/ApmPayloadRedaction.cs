namespace PMPlatform.Api.Observability;

/// <summary>
/// The last step before the APM endpoint (CTL-27): the SDK's transport sends every envelope through this handler, which replaces
/// its body with the redacted one (<see cref="ApmEnvelope"/>). Whatever the SDK or an integration put in an event, nothing leaves
/// the process unredacted. A body it cannot read — compressed, or not an envelope — is not sent at all: the SDK drops the
/// envelope and the request it describes is unaffected.
/// </summary>
internal sealed class ApmPayloadRedaction(HttpMessageHandler innerHandler) : DelegatingHandler(innerHandler)
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.Content is { } content)
        {
            if (content.Headers.ContentEncoding.Count > 0)
            {
                throw new InvalidOperationException("A compressed APM envelope cannot be redacted, so it is not sent.");
            }

            byte[] body = await content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
            ByteArrayContent redacted = new(ApmEnvelope.Redact(body));
            foreach (KeyValuePair<string, IEnumerable<string>> header in content.Headers)
            {
                if (!string.Equals(header.Key, "Content-Length", StringComparison.OrdinalIgnoreCase))
                {
                    redacted.Headers.TryAddWithoutValidation(header.Key, header.Value);
                }
            }

            request.Content = redacted;
            content.Dispose();
        }

        return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }
}
