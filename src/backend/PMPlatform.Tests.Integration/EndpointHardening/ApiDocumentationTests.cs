using System.Net;
using PMPlatform.Api.ApiDocumentation;
using PMPlatform.Api.Middleware;
using PMPlatform.Tests.Integration.Identity;

namespace PMPlatform.Tests.Integration.EndpointHardening;

/// <summary>
/// TASK-094: the hosted docs UI. Where <c>ApiDocumentation:Enabled</c> (DEV, SIT) it is served anonymously from the API's own assets
/// under its own Content-Security-Policy, reads the generated document and sends nothing else; where it is not (the default), the
/// path does not exist and keeps the API's policy.
/// </summary>
[Collection(IdentitySuite.Name)]
public sealed class ApiDocumentationTests(IdentityTestHost host)
{
    private static readonly string[] UiAssets = ["index.html", "index.js", "index.css", "swagger-ui.css", "swagger-ui-bundle.js", "swagger-ui-standalone-preset.js"];

    [Fact]
    public async Task WhereEnabledTheUiIsServedAnonymouslyUnderItsOwnPolicy()
    {
        await using IdentityApiFactory api = host.CreateApi(HardeningApi.Settings(($"{ApiDocumentationOptions.Section}:Enabled", "true")));
        using HttpClient client = api.CreateClient();

        foreach (string asset in UiAssets)
        {
            using HttpResponseMessage response = await client.GetAsync($"{ApiDocumentationEndpoints.UiPath}/{asset}");

            Assert.True(response.StatusCode == HttpStatusCode.OK, $"{asset}: {response.StatusCode}");
            Assert.Equal(ApiDocumentationEndpoints.UiContentSecurityPolicy, response.Headers.GetValues("Content-Security-Policy").Single());
            Assert.All(SecurityHeaders.Headers.Where(header => header.Key != "Content-Security-Policy"), header =>
                Assert.Equal(header.Value.ToString(), response.Headers.GetValues(header.Key).Single()));
        }

        using HttpResponseMessage document = await client.GetAsync(ApiDocumentationEndpoints.DocumentPath);
        Assert.Equal(HttpStatusCode.OK, document.StatusCode);
        Assert.Equal(SecurityHeaders.Headers[0].Value.ToString(), document.Headers.GetValues("Content-Security-Policy").Single());
    }

    /// <summary>The UI reads the API's own document, validates against nothing outside it, offers no "Try it out", and opens with each module closed.</summary>
    [Fact]
    public async Task TheUiReadsTheGeneratedDocumentAndSendsNoRequest()
    {
        await using IdentityApiFactory api = host.CreateApi(HardeningApi.Settings(($"{ApiDocumentationOptions.Section}:Enabled", "true")));
        using HttpClient client = api.CreateClient();

        string configuration = await client.GetStringAsync($"{ApiDocumentationEndpoints.UiPath}/index.js");

        Assert.Contains($"\"url\":\"{ApiDocumentationEndpoints.DocumentPath}\"", configuration, StringComparison.Ordinal);
        Assert.Contains("\"supportedSubmitMethods\":[]", configuration, StringComparison.Ordinal);
        Assert.Contains("\"docExpansion\":\"none\"", configuration, StringComparison.Ordinal);
        Assert.DoesNotContain("validatorUrl\":\"http", configuration, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ByDefaultThereIsNoUi()
    {
        await using IdentityApiFactory api = host.CreateApi(HardeningApi.Settings());
        using HttpClient client = api.CreateClient();

        using HttpResponseMessage response = await client.GetAsync($"{ApiDocumentationEndpoints.UiPath}/index.html");

        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(SecurityHeaders.Headers[0].Value.ToString(), response.Headers.GetValues("Content-Security-Policy").Single());
    }
}
