using System.Text.Json.Nodes;
using PMPlatform.Application.Features.Approval.Contracts;
using PMPlatform.Application.Features.ChangeRequest.Contracts;
using PMPlatform.Tests.Integration.ChangeRequest;
using PMPlatform.Tests.Integration.Identity;
using DocumentedResponses = PMPlatform.Tests.Integration.Project.DocumentedResponses;
using OpenApiDocument = PMPlatform.Tests.Integration.Project.OpenApiDocument;

namespace PMPlatform.Tests.Integration.Governance;

/// <summary>
/// The WF-08 document describes the wire (api-conventions R-55's replay, in process): every ChangeRequest operation is called — one
/// change request from raising through review, approval, its authorisation applied by WF-03, implementation and closure; one deleted as a
/// draft; one withdrawn — and each answer has exactly the properties, JSON types and enum values its documented schema gives.
/// </summary>
[Collection(ChangeRequestSuite.Name)]
public sealed class ChangeRequestResponseContractTests(ChangeRequestTestHost host)
{
    private const string Request = $"{ChangeRequestDriver.Requests}/{{changeRequestId}}";

    [Fact]
    public async Task EveryChangeRequestResponseIsWhatItsOperationDocuments()
    {
        using HttpClient client = host.Api.CreateClient();
        DocumentedResponses responses = new(await OpenApiDocument.FetchAsync(client));
        ChangeSessions sessions = await client.SignInAsync();
        Guid projectId = await host.ProjectAsync("LIGHT");
        await host.BaselinedAsync(client, sessions, projectId);

        // The register: raise, read, edit, list, and the classification previewed before submission.
        Guid requestId = await RaiseAsync();
        string path = $"{ChangeRequestDriver.Requests}/{requestId}";
        using HttpResponseMessage read = await client.GetAsync(path, sessions.EntityManager);
        await responses.CheckAsync("GET", Request, read);
        using HttpResponseMessage edited = await client.PutAsync(path, sessions.EntityManager, new
        {
            title = new { text = "Extend the works by four days", language = "en" },
            justification = new { text = "The ground survey found rock where the design assumed sand.", language = "en" },
            costImpactSar = (string?)null,
            scheduleImpactDays = 4,
            scopeImpact = (object?)null,
            isContractualObligation = false,
            requestedGovernanceProfileItemId = (Guid?)null,
        }, AdministrationApi.ETagOf(read));
        await responses.CheckAsync("PUT", Request, edited);
        using HttpResponseMessage listed = await client.GetAsync($"{ChangeRequestDriver.Requests}?projectId={projectId}", sessions.EntityManager);
        Assert.Single((await responses.CheckAsync("GET", ChangeRequestDriver.Requests, listed))!["items"]!.AsArray());
        await CommandAsync(requestId, "preview-materiality", sessions.EntityManager);

        // Review through WF-11, implementation opened, the authorisation read and applied by WF-03's rebaseline, implemented, closed.
        await CommandAsync(requestId, "submit", sessions.EntityManager);
        await CommandAsync(requestId, "start-review", sessions.Officer);
        await host.DecideAndDeliverAsync(ChangeRequestApprovalRouting.SubjectModule, ChangeRequestApprovalRouting.SubjectType, requestId, ApprovalTaskDecision.Approve);
        await CommandAsync(requestId, "start-implementation", sessions.Officer);
        using HttpResponseMessage authorizations = await client.GetAsync($"{ChangeRequestDriver.Authorizations}?projectId={projectId}&changeRequestId={requestId}", sessions.Officer);
        Guid authorizationId = AdministrationApi.IdOf(Assert.Single((await responses.CheckAsync("GET", ChangeRequestDriver.Authorizations, authorizations))!["items"]!.AsArray())!.AsObject());
        await client.SubmittedBaselineAsync(sessions.EntityManager, projectId, authorizationId);
        using HttpResponseMessage authorization = await client.GetAsync($"{ChangeRequestDriver.Authorizations}/{authorizationId}", sessions.Officer);
        Assert.Equal("APPLIED", (await responses.CheckAsync("GET", $"{ChangeRequestDriver.Authorizations}/{{changeAuthorizationId}}", authorization))!.Text("status"));
        await CommandAsync(requestId, "mark-implemented", sessions.Officer);
        Assert.Equal("CLOSED", (await CommandAsync(requestId, "close", sessions.Officer)).Text("status"));

        // A draft deleted; a submitted request withdrawn.
        using HttpResponseMessage deleted = await client.SendAsync(HttpMethod.Delete, $"{ChangeRequestDriver.Requests}/{await RaiseAsync()}", sessions.EntityManager);
        Assert.Null(await responses.CheckAsync("DELETE", Request, deleted));
        Guid withdrawn = await RaiseAsync();
        await client.CommandOrFailAsync(sessions.EntityManager, withdrawn, "submit");
        Assert.Equal("WITHDRAWN", (await CommandAsync(withdrawn, "withdraw", sessions.EntityManager)).Text("status"));

        Assert.Equal((await OpenApiDocument.FetchAsync(client)).Operations(GovernanceApi.ChangeRequestTag).Select(o => o.Name).Order(), responses.Operations.Order());
        Assert.Empty(responses.Violations);

        async Task<Guid> RaiseAsync()
        {
            using HttpResponseMessage raised = await client.PostAsync(ChangeRequestDriver.Requests, sessions.EntityManager,
                ChangeRequestDriver.RequestBody(projectId, "SCHEDULE", scheduleImpactDays: 3));
            return AdministrationApi.IdOf((await responses.CheckAsync("POST", ChangeRequestDriver.Requests, raised))!);
        }

        async Task<JsonObject> CommandAsync(Guid id, string command, string token)
        {
            using HttpResponseMessage response = await client.CommandAsync(token, id, command);
            Assert.True(response.IsSuccessStatusCode, $"{command}: {(int)response.StatusCode} {await response.Content.ReadAsStringAsync()}");
            return (await responses.CheckAsync("POST", $"{Request}/{command}", response))!;
        }
    }
}
