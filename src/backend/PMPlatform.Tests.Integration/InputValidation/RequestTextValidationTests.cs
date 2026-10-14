using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Nodes;
using PMPlatform.Application.Common.Validation;
using PMPlatform.Tests.Integration.Identity;
using PMPlatform.Tests.Integration.Risk;

namespace PMPlatform.Tests.Integration.InputValidation;

/// <summary>
/// TASK-077 (CTL-11) through the API as it runs: every string an action receives obeys the platform's text rules before the action
/// does anything, on a body, a raw JSON escape and a query; text that obeys them is stored and returned exactly as entered (ADR-012).
/// </summary>
[Collection(RiskSuite.Name)]
public sealed class RequestTextValidationTests(RiskTestHost host)
{
    /// <summary>A NUL was a 500 from PostgreSQL and a bell was stored; both are now a 400 naming the field, and nothing is written.</summary>
    [Fact]
    public async Task ControlCharactersInABodyAreRefusedFieldByFieldAndNothingIsStored()
    {
        using HttpClient client = host.Api.CreateClient();
        RiskSessions sessions = await RiskDriver.SignInAsync(client);
        Guid projectId = await host.ProjectAsync();

        JsonObject body = JsonNode.Parse(System.Text.Json.JsonSerializer.Serialize(RiskDriver.RiskBody(projectId, title: "nul \u0000 here"), SessionApi.Json))!.AsObject();
        body["description"]!["text"] = "bell \u0007 here";
        using HttpResponseMessage refused = await client.PostAsync(RiskDriver.Risks, sessions.EntityManager, body);

        Assert.Equal((HttpStatusCode.BadRequest, "VALIDATION_FAILED"), (refused.StatusCode, await CodeOfAsync(refused)));
        Assert.Equal(["title.text INVALID_CHARACTER", "description.text INVALID_CHARACTER"], await refused.ReadFieldErrorsAsync());
        Assert.DoesNotContain("\u0000", await refused.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        Assert.Equal(["0"], await host.Database.QueryAsync($"SELECT count(*)::text FROM risk.risk WHERE project_id = '{projectId}'"));
    }

    /// <summary>
    /// A lone surrogate arrives as a JSON escape no serializer would write; it is not Unicode and cannot be stored as UTF-8. The JSON
    /// reader refuses it while binding, before the text rules run, so the body as a whole is malformed (R-23).
    /// </summary>
    [Fact]
    public async Task ALoneSurrogateEscapeIsRefused()
    {
        using HttpClient client = host.Api.CreateClient();
        RiskSessions sessions = await RiskDriver.SignInAsync(client);
        Guid projectId = await host.ProjectAsync();
        string json = System.Text.Json.JsonSerializer.Serialize(RiskDriver.RiskBody(projectId, title: "SURROGATE"), SessionApi.Json)
            .Replace("SURROGATE", "lone \\ud800 surrogate", StringComparison.Ordinal);

        using HttpRequestMessage request = new(HttpMethod.Post, RiskDriver.Risks) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", sessions.EntityManager);
        request.Headers.TryAddWithoutValidation("Idempotency-Key", Guid.NewGuid().ToString());
        using HttpResponseMessage refused = await client.SendAsync(request);

        Assert.Equal((HttpStatusCode.BadRequest, "VALIDATION_FAILED"), (refused.StatusCode, await CodeOfAsync(refused)));
        Assert.Equal(["body MALFORMED"], await refused.ReadFieldErrorsAsync());
        Assert.Equal(["0"], await host.Database.QueryAsync($"SELECT count(*)::text FROM risk.risk WHERE project_id = '{projectId}'"));
    }

    /// <summary>A query parameter is named as the request names it, and the ceiling holds for it as for a body.</summary>
    [Fact]
    public async Task AQueryParameterIsCheckedAndNamedByItsParameter()
    {
        using HttpClient client = host.Api.CreateClient();
        RiskSessions sessions = await RiskDriver.SignInAsync(client);

        using HttpResponseMessage control = await client.GetAsync($"{RiskDriver.Risks}?status=%07", sessions.Officer);
        Assert.Equal(HttpStatusCode.BadRequest, control.StatusCode);
        Assert.Equal(["status INVALID_CHARACTER"], await control.ReadFieldErrorsAsync());

        using HttpResponseMessage tooLong = await client.GetAsync($"{RiskDriver.Risks}?status={new string('A', TextInput.MaxLength + 1)}", sessions.Officer);
        Assert.Equal(HttpStatusCode.BadRequest, tooLong.StatusCode);
        Assert.Equal(["status MAX_LENGTH"], await tooLong.ReadFieldErrorsAsync());
    }

    /// <summary>
    /// Markup, SQL, Arabic, emoji, tabs and line breaks are text: stored and returned exactly as entered, in JSON, never escaped into
    /// something else or executed. Making them inert where they are shown is the renderer's job (output-encoding-checklist.md).
    /// </summary>
    [Fact]
    public async Task TextThatObeysTheRulesIsStoredAndReturnedExactlyAsEntered()
    {
        using HttpClient client = host.Api.CreateClient();
        RiskSessions sessions = await RiskDriver.SignInAsync(client);
        Guid projectId = await host.ProjectAsync();
        const string Title = "<img src=x onerror=alert(1)> '); DROP TABLE risk.risk;-- تأخر 🚧\tline\nnext";

        using HttpResponseMessage created = await client.PostAsync(RiskDriver.Risks, sessions.EntityManager, RiskDriver.RiskBody(projectId, title: Title));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal("application/json", created.Content.Headers.ContentType?.MediaType);
        Guid riskId = AdministrationApi.IdOf(await created.ReadObjectAsync());

        using HttpResponseMessage read = await client.GetAsync($"{RiskDriver.Risks}/{riskId}", sessions.EntityManager);
        Assert.Equal(Title, (await read.ReadObjectAsync())["title"]!["text"]!.GetValue<string>());
    }

    /// <summary>The rules run after authentication and authorization: a caller without a session learns nothing about the body.</summary>
    [Fact]
    public async Task AnUnauthenticatedRequestIsRefusedAsSuchBeforeItsTextIsLookedAt()
    {
        using HttpClient client = host.Api.CreateClient();
        using HttpResponseMessage refused = await client.PostAsJsonAsync(RiskDriver.Risks, RiskDriver.RiskBody(Guid.NewGuid(), title: "nul \u0000"));
        Assert.Equal(HttpStatusCode.Unauthorized, refused.StatusCode);
    }

    private static async Task<string?> CodeOfAsync(HttpResponseMessage response) => (await response.ReadObjectAsync())["code"]?.GetValue<string>();
}
