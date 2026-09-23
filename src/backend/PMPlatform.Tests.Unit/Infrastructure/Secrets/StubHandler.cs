using System.Net;

namespace PMPlatform.Tests.Unit.Infrastructure.Secrets;

/// <summary>Answers one canned response and records the request the store made.</summary>
internal sealed class StubHandler(HttpStatusCode status, string body) : HttpMessageHandler
{
    public HttpRequestMessage? LastRequest { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        LastRequest = request;
        return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body) });
    }
}
