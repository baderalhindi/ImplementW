using System.Net;
using PMPlatform.Tests.Integration.Identity;
using static PMPlatform.Tests.Integration.ExternalParticipation.ExternalParticipationDriver;

namespace PMPlatform.Tests.Integration.ExternalParticipation;

/// <summary>
/// WF-13 Path A, as the gate decision limits it: "direct source action limited to assigned tasks". An entity user works the WF-04 tasks it
/// owns directly in WF-04 — WF-13 duplicates no task state (BR-EXT-042) — and no other task, and never plans, cancels or reopens one.
/// </summary>
[Collection(ExternalParticipationSuite.Name)]
public sealed class DirectSourceActionTests(ExternalParticipationTestHost host)
{
    [Fact]
    public async Task AnEntityUserUpdatesTheTasksItOwnsDirectlyAndNoOther()
    {
        using HttpClient client = host.Api.CreateClient();
        ParticipationSessions sessions = await client.SignInAsync();
        Guid projectId = await host.ProjectAsync();
        await host.GrantProjectAsync(8, projectId);
        Guid owned = await client.StartedTaskAsync(sessions, projectId, "Formwork", owner: 8);
        Guid notOwned = await client.StartedTaskAsync(sessions, projectId, "Rebar");

        Assert.Equal([owned.ToString()], await client.IdsAsync(sessions.EntityA, $"{Tasks}?projectId={projectId}"));
        await client.OkOrFailAsync(sessions.EntityA, $"{Tasks}/{owned}/report-progress", new { actualPercentComplete = 35 });

        using (HttpResponseMessage other = await client.PostAsync($"{Tasks}/{notOwned}/report-progress", sessions.EntityA, new { actualPercentComplete = 35 }))
        {
            Assert.Equal(HttpStatusCode.NotFound, other.StatusCode);
        }

        foreach (string planning in new[] { "cancel", "reopen" })
        {
            using HttpResponseMessage refused = await client.PostAsync($"{Tasks}/{owned}/{planning}", sessions.EntityA);
            Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
        }

        await client.OkOrFailAsync(sessions.EntityA, $"{Tasks}/{owned}/complete");
        Assert.Equal("COMPLETED", (await client.GetOrFailAsync(sessions.ProjectManager, $"{Tasks}/{owned}")).Text("status"));
        Assert.Null((await client.GetOrFailAsync(sessions.ProjectManager, $"{Tasks}/{notOwned}"))["actualPercentComplete"]);

        // The other entity's user owns nothing here and reaches nothing.
        await host.GrantProjectAsync(7, projectId);
        using HttpResponseMessage crossEntity = await client.PostAsync($"{Tasks}/{owned}/report-progress", sessions.EntityB, new { actualPercentComplete = 90 });
        Assert.Equal(HttpStatusCode.NotFound, crossEntity.StatusCode);
    }
}
