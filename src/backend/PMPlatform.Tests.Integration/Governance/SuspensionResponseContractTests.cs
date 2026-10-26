using System.Text.Json.Nodes;
using PMPlatform.Application.Features.Approval.Contracts;
using PMPlatform.Tests.Integration.Identity;
using PMPlatform.Tests.Integration.Suspension;
using DocumentedResponses = PMPlatform.Tests.Integration.Project.DocumentedResponses;
using OpenApiDocument = PMPlatform.Tests.Integration.Project.OpenApiDocument;

namespace PMPlatform.Tests.Integration.Governance;

/// <summary>
/// The WF-09 document describes the wire (api-conventions R-55's replay, in process): every Suspension operation is called — one
/// suspension request deleted as a draft, one withdrawn, one taken from raising through review, approval and activation, and the
/// project's suspension period read — and each answer has exactly the properties, JSON types and enum values its documented schema gives.
/// </summary>
[Collection(SuspensionSuite.Name)]
public sealed class SuspensionResponseContractTests(SuspensionTestHost host)
{
    private const string Request = $"{SuspensionDriver.Requests}/{{suspensionRequestId}}";

    [Fact]
    public async Task EverySuspensionResponseIsWhatItsOperationDocuments()
    {
        using HttpClient client = host.Api.CreateClient();
        DocumentedResponses responses = new(await OpenApiDocument.FetchAsync(client));
        SuspensionSessions sessions = await client.SignInAsync();
        Guid projectId = await host.ActiveProjectAsync();

        // The register: raise, read, edit, list; the draft deleted.
        Guid draft = await RaiseAsync();
        string path = $"{SuspensionDriver.Requests}/{draft}";
        using HttpResponseMessage read = await client.GetAsync(path, sessions.EntityManager);
        await responses.CheckAsync("GET", Request, read);
        using HttpResponseMessage edited = await client.PutAsync(path, sessions.EntityManager, new
        {
            reason = new { text = "Funding withheld until the board meets.", language = "en" },
            requestedEffectiveDate = SuspensionDriver.Iso(SuspensionDriver.Today),
            plannedResumptionDate = SuspensionDriver.Iso(SuspensionDriver.Today.AddMonths(2)),
        }, AdministrationApi.ETagOf(read));
        await responses.CheckAsync("PUT", Request, edited);
        using HttpResponseMessage listed = await client.GetAsync($"{SuspensionDriver.Requests}?projectId={projectId}", sessions.EntityManager);
        Assert.Single((await responses.CheckAsync("GET", SuspensionDriver.Requests, listed))!["items"]!.AsArray());
        using HttpResponseMessage deleted = await client.SendAsync(HttpMethod.Delete, path, sessions.EntityManager);
        Assert.Null(await responses.CheckAsync("DELETE", Request, deleted));

        // A submitted request withdrawn.
        Guid withdrawn = await RaiseAsync();
        await CommandAsync(withdrawn, "submit", sessions.EntityManager);
        Assert.Equal("WITHDRAWN", (await CommandAsync(withdrawn, "withdraw", sessions.EntityManager)).Text("status"));

        // Review through WF-11, approval, activation by AHDA, and the period it opened.
        Guid requestId = await RaiseAsync();
        await CommandAsync(requestId, "submit", sessions.EntityManager);
        await CommandAsync(requestId, "start-review", sessions.DepartmentManager);
        await host.DecideAndDeliverAsync(requestId, ApprovalTaskDecision.Approve);
        Assert.Equal("EFFECTED", (await CommandAsync(requestId, "activate", sessions.Officer)).Text("status"));
        using HttpResponseMessage periods = await client.GetAsync($"{SuspensionDriver.Suspensions}?projectId={projectId}", sessions.EntityManager);
        Assert.Single((await responses.CheckAsync("GET", SuspensionDriver.Suspensions, periods))!["items"]!.AsArray());

        Assert.Equal((await OpenApiDocument.FetchAsync(client)).Operations(GovernanceApi.SuspensionTag).Select(o => o.Name).Order(), responses.Operations.Order());
        Assert.Empty(responses.Violations);

        async Task<Guid> RaiseAsync()
        {
            using HttpResponseMessage raised = await client.PostAsync(SuspensionDriver.Requests, sessions.EntityManager,
                SuspensionDriver.RequestBody(projectId, "SUSPEND", SuspensionDriver.Today));
            return AdministrationApi.IdOf((await responses.CheckAsync("POST", SuspensionDriver.Requests, raised))!);
        }

        async Task<JsonObject> CommandAsync(Guid id, string command, string token)
        {
            using HttpResponseMessage response = await client.CommandAsync(token, id, command);
            Assert.True(response.IsSuccessStatusCode, $"{command}: {(int)response.StatusCode} {await response.Content.ReadAsStringAsync()}");
            return (await responses.CheckAsync("POST", $"{Request}/{command}", response))!;
        }
    }
}
