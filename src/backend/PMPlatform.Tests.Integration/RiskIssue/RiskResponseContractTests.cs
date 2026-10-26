using System.Text.Json.Nodes;
using PMPlatform.Tests.Integration.Identity;
using PMPlatform.Tests.Integration.Project;
using PMPlatform.Tests.Integration.Risk;

namespace PMPlatform.Tests.Integration.RiskIssue;

/// <summary>
/// The WF-06 document describes the wire (api-conventions R-55's replay, in process; TASK-054 F-5 for this domain): every Risk
/// operation is called on one risk's way through its state machine, and each answer has exactly the properties, JSON types and enum
/// values its documented schema gives.
/// </summary>
[Collection(RiskSuite.Name)]
public sealed class RiskResponseContractTests(RiskTestHost host)
{
    [Fact]
    public async Task EveryRiskResponseIsWhatItsOperationDocuments()
    {
        using HttpClient client = host.Api.CreateClient();
        DocumentedResponses responses = new(await OpenApiDocument.FetchAsync(client));
        RiskSessions sessions = await RiskDriver.SignInAsync(client);
        Guid[] dimensions = await host.DimensionsAsync();
        Guid projectId = await host.ProjectAsync();
        const string risk = $"{RiskDriver.Risks}/{{riskId}}";
        const string assessment = $"{RiskDriver.Assessments}/{{assessmentId}}";
        const string action = $"{RiskDriver.Actions}/{{actionId}}";

        // The register: raise, read, edit, list.
        using HttpResponseMessage created = await client.PostAsync(RiskDriver.Risks, sessions.EntityManager, RiskDriver.RiskBody(projectId));
        Guid riskId = AdministrationApi.IdOf((await responses.CheckAsync("POST", RiskDriver.Risks, created))!);
        string path = $"{RiskDriver.Risks}/{riskId}";
        using HttpResponseMessage read = await client.GetAsync(path, sessions.EntityManager);
        await responses.CheckAsync("GET", risk, read);
        using HttpResponseMessage edited = await client.PutAsync(path, sessions.EntityManager, RiskDriver.RiskBody(projectId, "Contractor mobilisation delayed"), AdministrationApi.ETagOf(read));
        await responses.CheckAsync("PUT", risk, edited);

        // The assessment and its history.
        await CommandAsync("assess", sessions.Officer, RiskDriver.Assessment(dimensions, probability: 3, impact: 2, peak: 4));
        using HttpResponseMessage listed = await client.GetAsync($"{RiskDriver.Risks}?projectId={projectId}", sessions.EntityManager);
        Assert.Single((await responses.CheckAsync("GET", RiskDriver.Risks, listed))!["items"]!.AsArray());
        using HttpResponseMessage history = await client.GetAsync($"{RiskDriver.Assessments}?riskId={riskId}", sessions.EntityManager);
        string assessmentId = Assert.Single((await responses.CheckAsync("GET", RiskDriver.Assessments, history))!["items"]!.AsArray())!.Text("id");
        using HttpResponseMessage version = await client.GetAsync($"{RiskDriver.Assessments}/{assessmentId}", sessions.EntityManager);
        await responses.CheckAsync("GET", assessment, version);

        // Treatment: an action planned, read, edited and started; treatment; the action completed, a second one cancelled; monitoring.
        Guid actionId = await ActionAsync();
        string actionPath = $"{RiskDriver.Actions}/{actionId}";
        using HttpResponseMessage actions = await client.GetAsync($"{RiskDriver.Actions}?riskId={riskId}", sessions.EntityManager);
        Assert.Single((await responses.CheckAsync("GET", RiskDriver.Actions, actions))!["items"]!.AsArray());
        using HttpResponseMessage actionRead = await client.GetAsync(actionPath, sessions.EntityManager);
        await responses.CheckAsync("GET", action, actionRead);
        using HttpResponseMessage actionEdited = await client.PutAsync(actionPath, sessions.EntityManager, ActionBody(riskId, "Agree a revised mobilisation plan"), AdministrationApi.ETagOf(actionRead));
        await responses.CheckAsync("PUT", action, actionEdited);
        await ActionCommandAsync(actionId, "start");
        await CommandAsync("start-treatment", sessions.EntityManager);
        await ActionCommandAsync(actionId, "complete");
        await ActionCommandAsync(await ActionAsync(), "cancel");
        await CommandAsync("monitor", sessions.EntityManager);

        // Acceptance, its history and its revocation.
        await CommandAsync("accept", sessions.Officer, new { expiresOn = RiskDriver.Iso(RiskDriver.Today.AddDays(30)), rationale = new { text = "Within contingency.", language = "en" } });
        using HttpResponseMessage acceptances = await client.GetAsync($"{RiskDriver.Acceptances}?riskId={riskId}", sessions.EntityManager);
        Assert.Single((await responses.CheckAsync("GET", RiskDriver.Acceptances, acceptances))!["items"]!.AsArray());
        await CommandAsync("revoke-acceptance", sessions.Officer);

        // Materialisation into an issue (WF-07 played by TestIssueRegister), closure and the controlled reopen.
        JsonObject materialised = await CommandAsync("materialise", sessions.EntityManager, new { categoryItemId = RiskTestHost.ConcernCategoryId, priorityItemId = RiskTestHost.PriorityId });
        Assert.Single(materialised["materialisedIssueIds"]!.AsArray());
        await CommandAsync("close", sessions.EntityManager, RiskDriver.Rationale("Mobilisation completed."));
        Assert.Equal("ASSESSED", (await CommandAsync("reopen", sessions.Reopener)).Text("status"));

        Assert.Equal((await OpenApiDocument.FetchAsync(client)).Operations(RiskIssueApi.RiskTag).Select(o => o.Name).Order(), responses.Operations.Order());
        Assert.Empty(responses.Violations);

        async Task<JsonObject> CommandAsync(string command, string token, object? body = null)
        {
            using HttpResponseMessage response = await client.CommandAsync(token, riskId, command, body);
            Assert.True(response.IsSuccessStatusCode, $"{command}: {(int)response.StatusCode} {await response.Content.ReadAsStringAsync()}");
            return (await responses.CheckAsync("POST", $"{risk}/{command}", response))!;
        }

        async Task<Guid> ActionAsync()
        {
            using HttpResponseMessage response = await client.PostAsync(RiskDriver.Actions, sessions.EntityManager, ActionBody(riskId, "Agree a mobilisation plan"));
            return AdministrationApi.IdOf((await responses.CheckAsync("POST", RiskDriver.Actions, response))!);
        }

        async Task ActionCommandAsync(Guid id, string command)
        {
            using HttpResponseMessage response = await client.PostAsync($"{RiskDriver.Actions}/{id}/{command}", sessions.EntityManager);
            Assert.True(response.IsSuccessStatusCode, $"{command}: {(int)response.StatusCode} {await response.Content.ReadAsStringAsync()}");
            await responses.CheckAsync("POST", $"{action}/{command}", response);
        }
    }

    private static object ActionBody(Guid riskId, string title) => new
    {
        riskId,
        title = new { text = title, language = "en" },
        actionType = "MITIGATE",
        dueDate = RiskDriver.Iso(RiskDriver.Today.AddDays(14)),
    };
}
