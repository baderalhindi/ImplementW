using System.Net;
using System.Text.Json.Nodes;
using PMPlatform.Tests.Integration.Identity;

namespace PMPlatform.Tests.Integration.Project;

/// <summary>
/// Stage QA D1-01: an approver's inbox row names the project it decides. The name comes with the task the approver's authority was proved
/// over (APPROVAL_DECIDE covering the run's project), so it reaches no one the task does not; the answer is what the operation documents.
/// </summary>
[Collection(ProjectSuite.Name)]
public sealed class ProjectApprovalInboxTests(ProjectTestHost host)
{
    private const string ApprovalTasks = "/api/v1/approval-tasks";
    private const string ApprovalInstances = "/api/v1/approval-instances";

    [Fact]
    public async Task AnInboxRowNamesTheProjectUnderReview()
    {
        using HttpClient client = host.Api.CreateClient();
        DocumentedResponses responses = new(await OpenApiDocument.FetchAsync(client));
        Sessions sessions = await client.SignInAsync();
        Guid projectId = await host.UnderReviewAsync(client, sessions);
        JsonObject project;
        using (HttpResponseMessage read = await client.GetAsync($"{ProjectDriver.Projects}/{projectId}", sessions.Approver))
        {
            Assert.Equal(HttpStatusCode.OK, read.StatusCode);
            project = await read.ReadObjectAsync();
        }

        using HttpResponseMessage inbox = await client.GetAsync($"{ApprovalTasks}?pageSize=200", sessions.Approver);
        Assert.Equal(HttpStatusCode.OK, inbox.StatusCode);
        // The answer, the new property with it, is what the operation documents.
        JsonObject page = (await responses.CheckAsync("GET", ApprovalTasks, inbox))!;
        Assert.Empty(responses.Violations);
        JsonNode item = Assert.Single(
            page["items"]!.AsArray(),
            i => i!["instance"]!["subject"]!["id"]!.GetValue<Guid>() == projectId)!;

        JsonObject named = item["project"]!.AsObject();
        Assert.Equal(projectId, named["id"]!.GetValue<Guid>());
        Assert.Null(named["formalProjectId"]?.GetValue<string>());
        Assert.Equal(project["title"]!["text"]!.GetValue<string>(), named["title"]!["text"]!.GetValue<string>());
        Assert.Equal(project["title"]!["language"]!.GetValue<string>(), named["title"]!["language"]!.GetValue<string>());
    }

    /// <summary>Stage QA DEV-D1-03: a requester's My Requests row names the project, as the approver's inbox row does.</summary>
    [Fact]
    public async Task AMyRequestsRowNamesTheProjectItWasRequestedFor()
    {
        using HttpClient client = host.Api.CreateClient();
        DocumentedResponses responses = new(await OpenApiDocument.FetchAsync(client));
        Sessions sessions = await client.SignInAsync();
        // local.r03 registers and submits, so the run is theirs; local.r02 starts the review.
        Guid projectId = AdministrationApi.IdOf(await client.CreateOrFailAsync(sessions.Reviewer, host.Registration()));
        await client.CommandOrFailAsync(sessions.Reviewer, projectId, "submit", new { projectManagerUserId = ProjectDriver.Person(8) });
        await client.CommandOrFailAsync(sessions.Approver, projectId, "start-review");
        JsonObject project;
        using (HttpResponseMessage read = await client.GetAsync($"{ProjectDriver.Projects}/{projectId}", sessions.Reviewer))
        {
            Assert.Equal(HttpStatusCode.OK, read.StatusCode);
            project = await read.ReadObjectAsync();
        }

        using HttpResponseMessage mine = await client.GetAsync($"{ApprovalInstances}?requestedBy=me&pageSize=200", sessions.Reviewer);
        Assert.Equal(HttpStatusCode.OK, mine.StatusCode);
        JsonObject page = (await responses.CheckAsync("GET", ApprovalInstances, mine))!;
        Assert.Empty(responses.Violations);
        JsonNode row = Assert.Single(page["items"]!.AsArray(), i => i!["subject"]!["id"]!.GetValue<Guid>() == projectId)!;

        JsonObject named = row["project"]!.AsObject();
        Assert.Equal(projectId, named["id"]!.GetValue<Guid>());
        Assert.Equal(project["title"]!["text"]!.GetValue<string>(), named["title"]!["text"]!.GetValue<string>());
    }
}
