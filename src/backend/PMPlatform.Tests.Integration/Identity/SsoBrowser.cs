using System.Net;
using Microsoft.AspNetCore.WebUtilities;

namespace PMPlatform.Tests.Integration.Identity;

/// <summary>What a browser does in an SSO sign-in, shared by the TASK-028 and TASK-029 tests.</summary>
internal static class SsoBrowser
{
    /// <summary>Asks the API where to go and follows the identity provider's redirect back to the callback URL.</summary>
    public static async Task<Callback> AuthorizeAsync(HttpClient client, TestIdentityProvider identityProvider, string subject)
    {
        identityProvider.NextSubject = subject;
        using HttpResponseMessage response = await client.GetAsync(SessionApi.SsoAuthorization);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        SsoAuthorization authorization = await response.ReadAsync<SsoAuthorization>();

        using HttpClientHandler handler = new() { AllowAutoRedirect = false };
        using HttpClient browser = new(handler);
        using HttpResponseMessage redirect = await browser.GetAsync(authorization.AuthorizationUrl);
        Assert.Equal(HttpStatusCode.Redirect, redirect.StatusCode);
        Uri location = redirect.Headers.Location!;
        Assert.StartsWith(TestIdentityProvider.CallbackUrl, location.AbsoluteUri, StringComparison.Ordinal);

        Dictionary<string, Microsoft.Extensions.Primitives.StringValues> query = QueryHelpers.ParseQuery(location.Query);
        return new Callback(query["code"]!, query["state"]!, authorization.Transaction);
    }
}

internal sealed record SsoAuthorization(Uri AuthorizationUrl, string Transaction);

internal sealed record Callback(string Code, string State, string Transaction);
