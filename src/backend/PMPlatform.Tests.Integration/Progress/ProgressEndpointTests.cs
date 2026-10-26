using System.Net;
using System.Text.Json.Nodes;
using PMPlatform.Tests.Integration.Identity;

namespace PMPlatform.Tests.Integration.Progress;

/// <summary>The WF-02 API's shape: required filters, R-21 concurrency, the state an edit needs, and the gate.</summary>
[Collection(ProgressSuite.Name)]
public sealed class ProgressEndpointTests(ProgressTestHost host)
{
    [Theory]
    [InlineData(ProgressDriver.Submissions)]
    [InlineData(ProgressDriver.Cycles)]
    [InlineData(ProgressDriver.Snapshots)]
    [InlineData(ProgressDriver.HealthStatuses)]
    public async Task ACollectionIsOneProjectsAndEmptyForAProjectTheCallerCannotSee(string collection)
    {
        using HttpClient client = host.Api.CreateClient();
        ProgressSessions sessions = await client.SignInAsync();

        using (HttpResponseMessage unfiltered = await client.GetAsync(collection, sessions.Reviewer))
        {
            Assert.Equal(["projectId REQUIRED"], await unfiltered.ReadFieldErrorsAsync());
        }

        using (HttpResponseMessage badPage = await client.GetAsync($"{collection}?projectId={Guid.NewGuid()}&pageSize=500", sessions.Reviewer))
        {
            Assert.Equal(["pageSize OUT_OF_RANGE"], await badPage.ReadFieldErrorsAsync());
        }

        using HttpResponseMessage unknown = await client.GetAsync($"{collection}?projectId={Guid.NewGuid()}", sessions.Reviewer);
        JsonObject page = await unknown.ReadObjectAsync();
        Assert.Equal((HttpStatusCode.OK, 0, 0), (unknown.StatusCode, page["items"]!.AsArray().Count, page["totalCount"]!.GetValue<int>()));
    }

    [Fact]
    public async Task AnEditNeedsTheCurrentVersionAndADraft()
    {
        using HttpClient client = host.Api.CreateClient();
        ProgressSessions sessions = await client.SignInAsync();
        Guid projectId = await host.ProjectAsync();
        host.Inputs.Set(projectId, actualPercent: 10, baselineStart: ProgressDriver.Today);
        Guid id = AdministrationApi.IdOf(await client.StartOrFailAsync(sessions.ProjectManager, projectId));
        string path = $"{ProgressDriver.Submissions}/{id}";
        var body = new { narrative = new { text = "On plan", language = "en" } };

        using (HttpResponseMessage noVersion = await client.PutAsync(path, sessions.ProjectManager, body, ifMatch: null))
        {
            Assert.Equal(HttpStatusCode.PreconditionRequired, noVersion.StatusCode);
        }

        using (HttpResponseMessage stale = await client.PutAsync(path, sessions.ProjectManager, body, "\"1\""))
        {
            Assert.Equal(HttpStatusCode.PreconditionFailed, stale.StatusCode);
        }

        await client.CommandOrFailAsync(sessions.ProjectManager, id, "submit");
        using (HttpResponseMessage submitted = await client.EditAsync(sessions.ProjectManager, id, body))
        {
            Assert.Equal((HttpStatusCode.Conflict, "PROGRESS_NOT_EDITABLE"), await submitted.RefusalAsync());
        }

        using (HttpResponseMessage resubmitted = await client.CommandAsync(sessions.ProjectManager, id, "submit"))
        {
            Assert.Equal((HttpStatusCode.Conflict, "INVALID_TRANSITION"), await resubmitted.RefusalAsync());
        }

        using HttpResponseMessage missing = await client.GetAsync($"{ProgressDriver.Submissions}/{Guid.NewGuid()}", sessions.ProjectManager);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Fact]
    public async Task StartingNeedsAProjectAndAnIdempotencyKey()
    {
        using HttpClient client = host.Api.CreateClient();
        ProgressSessions sessions = await client.SignInAsync();

        using (HttpResponseMessage noProject = await client.PostAsync(ProgressDriver.Submissions, sessions.ProjectManager, new { }))
        {
            Assert.Equal(["projectId REQUIRED"], await noProject.ReadFieldErrorsAsync());
        }

        using (HttpResponseMessage unknown = await client.StartAsync(sessions.ProjectManager, Guid.NewGuid()))
        {
            Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        }

        using HttpResponseMessage noKey = await client.SendAsync(HttpMethod.Post, ProgressDriver.Submissions, sessions.ProjectManager, new { projectId = Guid.NewGuid() }, idempotencyKey: "");
        Assert.Equal((HttpStatusCode.BadRequest, "IDEMPOTENCY_KEY_REQUIRED"), await noKey.RefusalAsync());
    }
}
