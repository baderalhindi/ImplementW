using System.Net;
using PMPlatform.Tests.Integration.Identity;
using PMPlatform.Tests.Integration.Risk;

namespace PMPlatform.Tests.Integration.GlobalSearch;

/// <summary>The endpoint itself, projects and risks. The other kinds have a test in their own suite's host.</summary>
[Collection(RiskSuite.Name)]
public sealed class GlobalSearchRiskTests(RiskTestHost host)
{
    [Fact]
    public async Task ARiskAndItsProjectAreFoundExactlyByWhoeverMayOpenThem()
    {
        using HttpClient client = host.Api.CreateClient();
        RiskSessions sessions = await client.SignInAsync();
        Guid projectId = await host.ProjectAsync(projectManager: 8);
        Guid riskId = await client.RiskAsync(sessions.EntityManager, projectId);
        string text = GlobalSearchDriver.UniqueText();
        await host.Database.ExecuteAsync($"UPDATE risk.risk SET title = 'Delay {text}' WHERE id = '{riskId}'");
        await host.Database.ExecuteAsync($"UPDATE project.project SET title = 'Road {text}' WHERE id = '{projectId}'");

        foreach ((string token, bool findable) in new[] { (sessions.EntityManager, true), (sessions.Officer, true), (sessions.InternalManager, false) })
        {
            await client.AssertFindableExactlyWhenReadableAsync(token, text, "RISK", riskId, $"{RiskDriver.Risks}/{riskId}", findable);
            await client.AssertFindableExactlyWhenReadableAsync(token, text, "PROJECT", projectId, $"/api/v1/projects/{projectId}", findable);
        }
    }

    [Fact]
    public async Task AHitNamesTheProjectItBelongsTo()
    {
        using HttpClient client = host.Api.CreateClient();
        RiskSessions sessions = await client.SignInAsync();
        Guid projectId = await host.ProjectAsync(projectManager: 8);
        Guid riskId = await client.RiskAsync(sessions.EntityManager, projectId);
        string text = GlobalSearchDriver.UniqueText();
        await host.Database.ExecuteAsync($"UPDATE risk.risk SET title = 'Delay {text}' WHERE id = '{riskId}'");

        using HttpResponseMessage response = await client.GetWithTokenAsync($"{GlobalSearchDriver.Search}?q={text}", sessions.EntityManager);

        System.Text.Json.Nodes.JsonNode hit = Assert.Single((await response.ReadObjectAsync())["items"]!.AsArray())!;
        Assert.Equal(projectId.ToString(), hit["projectId"]!.GetValue<string>());
        Assert.Equal("Risk test project", hit["projectTitle"]!["text"]!.GetValue<string>());
    }

    [Fact]
    public async Task ASearchWildcardIsLiteral()
    {
        using HttpClient client = host.Api.CreateClient();
        RiskSessions sessions = await client.SignInAsync();
        Guid projectId = await host.ProjectAsync(projectManager: 8);
        await client.RiskAsync(sessions.EntityManager, projectId);

        Assert.Empty(await client.HitsAsync(sessions.Officer, "%_%" + GlobalSearchDriver.UniqueText()));
        Assert.Empty(await client.HitsAsync(sessions.Officer, "%%"));
    }

    [Fact]
    public async Task UnderTwoCharactersFindsNothingAndTooLongIsRefused()
    {
        using HttpClient client = host.Api.CreateClient();
        RiskSessions sessions = await client.SignInAsync();

        Assert.Empty(await client.HitsAsync(sessions.Officer, "r"));
        Assert.Empty(await client.HitsAsync(sessions.Officer, "  r  "));
        using HttpResponseMessage tooLong = await client.GetWithTokenAsync($"{GlobalSearchDriver.Search}?q={new string('r', 201)}", sessions.Officer);
        Assert.Equal(HttpStatusCode.BadRequest, tooLong.StatusCode);
    }

    [Fact]
    public async Task ASearchNeedsASession()
    {
        using HttpClient client = host.Api.CreateClient();

        using HttpResponseMessage response = await client.GetAsync($"{GlobalSearchDriver.Search}?q=road");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ASearchLeavesNoRefusalInTheAuditTrail()
    {
        using HttpClient client = host.Api.CreateClient();
        RiskSessions sessions = await client.SignInAsync();
        Guid projectId = await host.ProjectAsync(projectManager: 8);
        Guid riskId = await client.RiskAsync(sessions.EntityManager, projectId);
        string text = GlobalSearchDriver.UniqueText();
        await host.Database.ExecuteAsync($"UPDATE risk.risk SET title = 'Delay {text}' WHERE id = '{riskId}'");
        int before = (await host.AuditEventsAsync(riskId)).Count;

        Assert.Empty(await client.HitsAsync(sessions.InternalManager, text));

        Assert.Equal(before, (await host.AuditEventsAsync(riskId)).Count);
    }

    [Fact]
    public async Task EveryRoleFindsARiskExactlyWhenItsPageOpens()
    {
        using HttpClient client = host.Api.CreateClient();
        RiskSessions sessions = await client.SignInAsync();
        Guid projectId = await host.ProjectAsync(projectManager: 8);
        Guid riskId = await client.RiskAsync(sessions.EntityManager, projectId);
        string text = GlobalSearchDriver.UniqueText();
        await host.Database.ExecuteAsync($"UPDATE risk.risk SET title = 'Delay {text}' WHERE id = '{riskId}'");

        await client.AssertSearchAgreesWithThePageForEveryRoleAsync(text, "RISK", riskId, $"{RiskDriver.Risks}/{riskId}");
    }
}
