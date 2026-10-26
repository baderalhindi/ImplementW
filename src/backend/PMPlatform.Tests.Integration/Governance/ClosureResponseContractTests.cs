using System.Text.Json.Nodes;
using PMPlatform.Application.Features.Approval.Contracts;
using PMPlatform.Tests.Integration.Closure;
using PMPlatform.Tests.Integration.Identity;
using DocumentedResponses = PMPlatform.Tests.Integration.Project.DocumentedResponses;
using OpenApiDocument = PMPlatform.Tests.Integration.Project.OpenApiDocument;

namespace PMPlatform.Tests.Integration.Governance;

/// <summary>
/// The WF-10 document describes the wire (api-conventions R-55's replay, in process): every Closure operation is called — one project
/// completed through a completion case with its obligations started, satisfied, cancelled and waived; a suspended one closed on the terminal
/// path through a closure case; for each kind of case one deleted as a draft and one withdrawn — and each answer has exactly the
/// properties, JSON types and enum values its documented schema gives.
/// </summary>
[Collection(ClosureSuite.Name)]
public sealed class ClosureResponseContractTests(ClosureTestHost host)
{
    private const string Obligation = $"{ClosureDriver.Obligations}/{{obligationId}}";

    [Fact]
    public async Task EveryClosureResponseIsWhatItsOperationDocuments()
    {
        using HttpClient client = host.Api.CreateClient();
        DocumentedResponses responses = new(await OpenApiDocument.FetchAsync(client));
        ClosureSessions sessions = await client.SignInAsync();

        // Completion: a draft deleted, one withdrawn, one read, edited, listed, evaluated, waived, submitted, reviewed, approved, activated.
        Guid completed = await host.ProjectAsync();
        Guid completionId = await CaseAsync(ClosureDriver.CompletionCases, ClosureDriver.CompletionBody(completed), completed, new
        {
            actualProjectCompletionDate = ClosureDriver.Iso(ClosureDriver.Today),
            completionNarrative = ClosureDriver.Narrative("Works handed over to the operator after snagging."),
        }, "CompletionCase", beforeSubmission: async caseId =>
        {
            // An obligation recorded against the case, read, edited and listed; owned and dated, as completion needs.
            Guid obligationId = await ObligationAsync(caseId);
            string path = $"{ClosureDriver.Obligations}/{obligationId}";
            using HttpResponseMessage read = await client.GetAsync(path, sessions.EntityManager);
            await responses.CheckAsync("GET", Obligation, read);
            using HttpResponseMessage edited = await client.PutAsync(path, sessions.EntityManager, new
            {
                title = ClosureDriver.Narrative("Defects liability period"),
                description = ClosureDriver.Narrative("Twelve months of defect rectification, extended to the cladding."),
                ownerUserId = ClosureDriver.Person(8),
                dueDate = ClosureDriver.Iso(ClosureDriver.Today.AddDays(400)),
            }, AdministrationApi.ETagOf(read));
            await responses.CheckAsync("PUT", Obligation, edited);
            using HttpResponseMessage listed = await client.GetAsync($"{ClosureDriver.Obligations}?projectId={completed}", sessions.EntityManager);
            Assert.Single((await responses.CheckAsync("GET", ClosureDriver.Obligations, listed))!["items"]!.AsArray());
        });
        Assert.Equal("COMPLETED", await host.LifecycleOfAsync(completed));

        // Obligations past Completion: one started and satisfied, one cancelled, one waived.
        Guid obligation = Guid.Parse(Assert.Single(await host.Database.QueryAsync($"SELECT id::text FROM closure.post_project_obligation WHERE project_id = '{completed}'")));
        await ObligationCommandAsync(obligation, "start", sessions.EntityManager);
        Assert.Equal("SATISFIED", (await ObligationCommandAsync(obligation, "satisfy", sessions.EntityManager)).Text("status"));
        Assert.Equal("CANCELLED", (await ObligationCommandAsync(await ObligationAsync(completionId), "cancel", sessions.EntityManager)).Text("status"));
        Assert.Equal("WAIVED", (await ObligationCommandAsync(await ObligationAsync(completionId), "waive", sessions.DepartmentManager)).Text("status"));

        // Closure, on the terminal path of a suspended project: the same operations of a closure case.
        Guid terminated = await host.ProjectAsync();
        await SuspendAsync(terminated);
        await CaseAsync(ClosureDriver.ClosureCases, ClosureDriver.ClosureBody(terminated, "Stopped: the programme was cancelled."), terminated, new
        {
            closureNarrative = ClosureDriver.Narrative("Stopped: the programme was cancelled; records archived."),
        }, "ClosureCase");
        Assert.Equal("CLOSED", await host.LifecycleOfAsync(terminated));

        Assert.Equal((await OpenApiDocument.FetchAsync(client)).Operations(GovernanceApi.ClosureTag).Select(o => o.Name).Order(), responses.Operations.Order());
        Assert.Empty(responses.Violations);

        // Every operation of one kind of case, in an order its state machine allows; returns the case effected.
        async Task<Guid> CaseAsync(string collection, object body, Guid projectId, object changes, string subjectType, Func<Guid, Task>? beforeSubmission = null)
        {
            string item = $"{collection}/{{caseId}}";
            using HttpResponseMessage deleted = await client.SendAsync(HttpMethod.Delete, $"{collection}/{await RaiseAsync()}", sessions.EntityManager);
            Assert.Null(await responses.CheckAsync("DELETE", item, deleted));
            Assert.Equal("WITHDRAWN", (await CommandAsync(await RaiseAsync(), "withdraw", sessions.EntityManager)).Text("status"));

            Guid caseId = await RaiseAsync();
            string path = $"{collection}/{caseId}";
            using HttpResponseMessage read = await client.GetAsync(path, sessions.EntityManager);
            await responses.CheckAsync("GET", item, read);
            using HttpResponseMessage edited = await client.PutAsync(path, sessions.EntityManager, changes, AdministrationApi.ETagOf(read));
            await responses.CheckAsync("PUT", item, edited);
            using HttpResponseMessage listed = await client.GetAsync($"{collection}?projectId={projectId}", sessions.EntityManager);
            Assert.Equal(2, (await responses.CheckAsync("GET", collection, listed))!["items"]!.AsArray().Count);
            if (beforeSubmission is not null)
            {
                await beforeSubmission(caseId);
            }

            // A fresh project has criteria that fail and may be waived: the Department Manager waives each.
            JsonObject evaluated = await CommandAsync(caseId, "evaluate-readiness", sessions.EntityManager);
            JsonNode[] waivable = [.. evaluated["readiness"]!["checks"]!.AsArray().Where(c => c!.Text("result") == "FAIL" && c!["waivable"]!.GetValue<bool>()).Select(c => c!)];
            Assert.NotEmpty(waivable);
            foreach (JsonNode check in waivable)
            {
                await CommandAsync(caseId, "waive-check", sessions.DepartmentManager, new { checkCode = check.Text("checkCode"), reason = ClosureDriver.Narrative("Accepted by the Department Manager.") });
            }

            using HttpResponseMessage records = await client.GetAsync($"{ClosureDriver.ReadinessChecks}?{(collection == ClosureDriver.CompletionCases ? "completionCaseId" : "closureCaseId")}={caseId}", sessions.EntityManager);
            Assert.NotEmpty((await responses.CheckAsync("GET", ClosureDriver.ReadinessChecks, records))!["items"]!.AsArray());

            await CommandAsync(caseId, "submit", sessions.EntityManager);
            await CommandAsync(caseId, "start-review", sessions.DepartmentManager);
            await host.DecideAndDeliverAsync(subjectType, caseId, ApprovalTaskDecision.Approve);
            Assert.Equal("EFFECTED", (await CommandAsync(caseId, "activate", sessions.Officer)).Text("status"));
            return caseId;

            async Task<Guid> RaiseAsync()
            {
                using HttpResponseMessage raised = await client.PostAsync(collection, sessions.EntityManager, body);
                return AdministrationApi.IdOf((await responses.CheckAsync("POST", collection, raised))!);
            }

            async Task<JsonObject> CommandAsync(Guid id, string command, string token, object? commandBody = null)
            {
                using HttpResponseMessage response = await client.CommandAsync(token, collection, id, command, commandBody);
                Assert.True(response.IsSuccessStatusCode, $"{command}: {(int)response.StatusCode} {await response.Content.ReadAsStringAsync()}");
                return (await responses.CheckAsync("POST", $"{item}/{command}", response))!;
            }
        }

        async Task<Guid> ObligationAsync(Guid completionCaseId)
        {
            using HttpResponseMessage created = await client.PostAsync(ClosureDriver.Obligations, sessions.EntityManager, new
            {
                completionCaseId,
                closureCaseId = (Guid?)null,
                title = ClosureDriver.Narrative("Defects liability period"),
                description = ClosureDriver.Narrative("Twelve months of defect rectification by the contractor."),
                ownerUserId = ClosureDriver.Person(8),
                dueDate = ClosureDriver.Iso(ClosureDriver.Today.AddDays(365)),
            });
            return AdministrationApi.IdOf((await responses.CheckAsync("POST", ClosureDriver.Obligations, created))!);
        }

        async Task<JsonObject> ObligationCommandAsync(Guid id, string command, string token)
        {
            using HttpResponseMessage response = await client.CommandAsync(token, ClosureDriver.Obligations, id, command);
            Assert.True(response.IsSuccessStatusCode, $"{command}: {(int)response.StatusCode} {await response.Content.ReadAsStringAsync()}");
            return (await responses.CheckAsync("POST", $"{Obligation}/{command}", response))!;
        }

        // WF-09 suspends the project, so that WF-10 closes it without completion.
        async Task SuspendAsync(Guid projectId)
        {
            Guid requestId = await client.RaiseAsync(sessions.EntityManager, ClosureDriver.SuspensionRequests, new
            {
                projectId,
                requestType = "SUSPEND",
                reason = ClosureDriver.Narrative("Funding withdrawn."),
                requestedEffectiveDate = ClosureDriver.Iso(ClosureDriver.Today),
            });
            await client.CommandOrFailAsync(sessions.EntityManager, ClosureDriver.SuspensionRequests, requestId, "submit");
            await client.CommandOrFailAsync(sessions.DepartmentManager, ClosureDriver.SuspensionRequests, requestId, "start-review");
            await host.DecideAndDeliverAsync("SuspensionRequest", requestId, ApprovalTaskDecision.Approve, "Suspension");
            await client.CommandOrFailAsync(sessions.Officer, ClosureDriver.SuspensionRequests, requestId, "activate");
            Assert.Equal("SUSPENDED", await host.LifecycleOfAsync(projectId));
        }
    }
}
