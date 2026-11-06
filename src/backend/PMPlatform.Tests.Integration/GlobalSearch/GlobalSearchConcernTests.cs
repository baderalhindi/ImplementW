using PMPlatform.Tests.Integration.ManagementConcern;

namespace PMPlatform.Tests.Integration.GlobalSearch;

[Collection(ConcernSuite.Name)]
public sealed class GlobalSearchConcernTests(ConcernTestHost host)
{
    [Fact]
    public async Task AConcernIsFoundExactlyByWhoeverMayOpenIt()
    {
        using HttpClient client = host.Api.CreateClient();
        ConcernSessions sessions = await client.SignInAsync();
        Guid projectId = await host.ProjectAsync(projectManager: 8);
        Guid concernId = await client.RaiseAsync(sessions.EntityManager, projectId);
        string text = GlobalSearchDriver.UniqueText();
        await host.Database.ExecuteAsync($"UPDATE management_concern.management_concern SET title = 'Blocker {text}' WHERE id = '{concernId}'");
        string detail = $"{ConcernDriver.Concerns}/{concernId}";

        await client.AssertFindableExactlyWhenReadableAsync(sessions.EntityManager, text, "MANAGEMENT_CONCERN", concernId, detail, findable: true);
        await client.AssertFindableExactlyWhenReadableAsync(sessions.Officer, text, "MANAGEMENT_CONCERN", concernId, detail, findable: true);
        await client.AssertFindableExactlyWhenReadableAsync(sessions.InternalManager, text, "MANAGEMENT_CONCERN", concernId, detail, findable: false);
    }

    /// <summary>Each role holds the concern in turn as its assignee, so an ASSIGNED grant is tried as well as every wider one.</summary>
    [Fact]
    public async Task EveryRoleFindsAnAssignedConcernExactlyWhenItsPageOpens()
    {
        using HttpClient client = host.Api.CreateClient();
        ConcernSessions sessions = await client.SignInAsync();
        Guid projectId = await host.ProjectAsync(projectManager: 8);
        Guid concernId = await client.RaiseAsync(sessions.EntityManager, projectId);
        string text = GlobalSearchDriver.UniqueText();
        await host.Database.ExecuteAsync($"UPDATE management_concern.management_concern SET title = 'Blocker {text}' WHERE id = '{concernId}'");

        await client.AssertSearchAgreesWithThePageForEveryRoleAsync(
            text, "MANAGEMENT_CONCERN", concernId, $"{ConcernDriver.Concerns}/{concernId}",
            role => host.Database.ExecuteAsync($"UPDATE management_concern.management_concern SET assignee_user_id = '{ConcernDriver.Person(role)}' WHERE id = '{concernId}'"));
    }
}
