using PMPlatform.Tests.Integration.ChangeRequest;

namespace PMPlatform.Tests.Integration.GlobalSearch;

[Collection(ChangeRequestSuite.Name)]
public sealed class GlobalSearchChangeRequestTests(ChangeRequestTestHost host)
{
    [Fact]
    public async Task AChangeRequestIsFoundExactlyByWhoeverMayOpenIt()
    {
        using HttpClient client = host.Api.CreateClient();
        ChangeSessions sessions = await client.SignInAsync();
        Guid projectId = await host.ProjectAsync();
        Guid requestId = await client.RaiseAsync(sessions.EntityManager, ChangeRequestDriver.RequestBody(projectId, "SCOPE", scopeImpact: "One more culvert."));
        string text = GlobalSearchDriver.UniqueText();
        await host.Database.ExecuteAsync($"UPDATE change_request.change_request SET title = 'Change {text}' WHERE id = '{requestId}'");
        string detail = $"{ChangeRequestDriver.Requests}/{requestId}";

        await client.AssertFindableExactlyWhenReadableAsync(sessions.EntityManager, text, "CHANGE_REQUEST", requestId, detail, findable: true);
        await client.AssertFindableExactlyWhenReadableAsync(sessions.Officer, text, "CHANGE_REQUEST", requestId, detail, findable: true);
        await client.AssertFindableExactlyWhenReadableAsync(sessions.InternalManager, text, "CHANGE_REQUEST", requestId, detail, findable: false);
    }

    [Fact]
    public async Task EveryRoleFindsAChangeRequestExactlyWhenItsPageOpens()
    {
        using HttpClient client = host.Api.CreateClient();
        ChangeSessions sessions = await client.SignInAsync();
        Guid projectId = await host.ProjectAsync();
        Guid requestId = await client.RaiseAsync(sessions.EntityManager, ChangeRequestDriver.RequestBody(projectId, "SCOPE", scopeImpact: "One more culvert."));
        string text = GlobalSearchDriver.UniqueText();
        await host.Database.ExecuteAsync($"UPDATE change_request.change_request SET title = 'Change {text}' WHERE id = '{requestId}'");

        await client.AssertSearchAgreesWithThePageForEveryRoleAsync(text, "CHANGE_REQUEST", requestId, $"{ChangeRequestDriver.Requests}/{requestId}");
    }
}
