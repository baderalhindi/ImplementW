using System.Net;
using System.Text.Json.Nodes;
using PMPlatform.Tests.Integration.Identity;
using PMPlatform.Tests.Integration.ManagementConcern;
using PMPlatform.Tests.Integration.Project;
using PMPlatform.Tests.Integration.Risk;

namespace PMPlatform.Tests.Integration.RiskIssue;

/// <summary>
/// The WF-07 document describes the wire (api-conventions R-55's replay, in process; TASK-054 F-5 for this domain): every
/// ManagementConcern operation is called on one concern's way from OPEN to CLOSED, with an escalation resolved and one withdrawn, and
/// each answer has exactly the properties, JSON types and enum values its documented schema gives.
/// </summary>
[Collection(ConcernSuite.Name)]
public sealed class ConcernResponseContractTests(ConcernTestHost host)
{
    [Fact]
    public async Task EveryManagementConcernResponseIsWhatItsOperationDocuments()
    {
        using HttpClient client = host.Api.CreateClient();
        DocumentedResponses responses = new(await OpenApiDocument.FetchAsync(client));
        ConcernSessions sessions = await ConcernDriver.SignInAsync(client);
        Guid[] dimensions = await host.DimensionsAsync();
        Guid projectId = await host.ProjectAsync();
        const string concern = $"{ConcernDriver.Concerns}/{{concernId}}";
        const string escalation = $"{ConcernDriver.Escalations}/{{escalationId}}";

        // The register: raise, read, edit, list.
        using HttpResponseMessage raised = await client.PostAsync(ConcernDriver.Concerns, sessions.InternalManager, ConcernDriver.ConcernBody(projectId, ConcernDriver.Impacts(dimensions, 2)));
        Guid concernId = AdministrationApi.IdOf((await responses.CheckAsync("POST", ConcernDriver.Concerns, raised))!);
        string path = $"{ConcernDriver.Concerns}/{concernId}";
        using HttpResponseMessage read = await client.GetAsync(path, sessions.InternalManager);
        await responses.CheckAsync("GET", concern, read);
        object changes = new
        {
            title = new { text = "Site access blocked", language = "en" },
            description = new { text = "The access road is closed.", language = "en" },
            categoryItemId = ConcernTestHost.CategoryId,
            priorityItemId = ConcernTestHost.LowPriorityId,
            targetResolutionDate = ConcernDriver.Iso(ConcernDriver.Today.AddDays(10)),
        };
        using HttpResponseMessage edited = await client.PutAsync(path, sessions.InternalManager, changes, AdministrationApi.ETagOf(read));
        await responses.CheckAsync("PUT", concern, edited);
        using HttpResponseMessage listed = await client.GetAsync($"{ConcernDriver.Concerns}?projectId={projectId}", sessions.InternalManager);
        Assert.Single((await responses.CheckAsync("GET", ConcernDriver.Concerns, listed))!["items"]!.AsArray());

        // Assessment and work.
        await CommandAsync("assess", sessions.InternalManager, new { impacts = ConcernDriver.Impacts(dimensions, 3, 1) });
        await CommandAsync("assign", sessions.InternalManager, new { assigneeUserId = ConcernDriver.Person(3) });
        await CommandAsync("start", sessions.InternalManager);

        // An escalation raised, read and resolved by the role it is routed to; a second one withdrawn by its escalator.
        string first = await EscalateAsync();
        using HttpResponseMessage escalations = await client.GetAsync($"{ConcernDriver.Escalations}?managementConcernId={concernId}", sessions.InternalManager);
        Assert.Single((await responses.CheckAsync("GET", ConcernDriver.Escalations, escalations))!["items"]!.AsArray());
        using HttpResponseMessage escalationRead = await client.GetAsync($"{ConcernDriver.Escalations}/{first}", sessions.InternalManager);
        await responses.CheckAsync("GET", escalation, escalationRead);
        await EscalationCommandAsync(first, "resolve", sessions.DepartmentManager, ConcernDriver.Resolution("Utility owner reopens the road on Sunday."));
        await EscalationCommandAsync(await EscalateAsync(), "withdraw", sessions.InternalManager);

        // Review, the resolution validated through WF-11, and closure.
        await CommandAsync("review", sessions.InternalManager);
        await CommandAsync("submit-resolution", sessions.InternalManager, ConcernDriver.Resolution("Temporary access agreed with the utility owner."));
        await host.DecideAsync(client, sessions, concernId, "approve");
        Assert.Equal("CLOSED", (await CommandAsync("close", sessions.InternalManager)).Text("status"));

        Assert.Equal((await OpenApiDocument.FetchAsync(client)).Operations(RiskIssueApi.ConcernTag).Select(o => o.Name).Order(), responses.Operations.Order());
        Assert.Empty(responses.Violations);

        async Task<JsonObject> CommandAsync(string command, string token, object? body = null)
        {
            using HttpResponseMessage response = await ConcernDriver.CommandAsync(client, token, concernId, command, body);
            Assert.True(response.StatusCode == HttpStatusCode.OK, $"{command}: {(int)response.StatusCode} {await response.Content.ReadAsStringAsync()}");
            return (await responses.CheckAsync("POST", $"{concern}/{command}", response))!;
        }

        async Task<string> EscalateAsync()
        {
            using HttpResponseMessage response = await client.EscalateAsync(sessions.InternalManager, concernId, Guid.NewGuid());
            Assert.True(response.StatusCode == HttpStatusCode.Created, $"escalate: {(int)response.StatusCode} {await response.Content.ReadAsStringAsync()}");
            return (await responses.CheckAsync("POST", ConcernDriver.Escalations, response))!.Text("id");
        }

        async Task EscalationCommandAsync(string id, string command, string token, object? body = null)
        {
            using HttpResponseMessage response = await client.PostAsync($"{ConcernDriver.Escalations}/{id}/{command}", token, body);
            Assert.True(response.StatusCode == HttpStatusCode.OK, $"{command}: {(int)response.StatusCode} {await response.Content.ReadAsStringAsync()}");
            await responses.CheckAsync("POST", $"{escalation}/{command}", response);
        }
    }
}
