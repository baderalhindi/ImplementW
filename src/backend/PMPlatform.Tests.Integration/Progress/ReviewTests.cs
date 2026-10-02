using System.Net;
using System.Text.Json.Nodes;
using PMPlatform.Tests.Integration.Identity;

namespace PMPlatform.Tests.Integration.Progress;

/// <summary>
/// The review and publication workflow, and ADR-013's participation amendment: an assigned entity Project Manager submits
/// progress on their own project; published progress remains governed by AHDA, and never by the person who submitted it.
/// </summary>
[Collection(ProgressSuite.Name)]
public sealed class ReviewTests(ProgressTestHost host)
{
    [Fact]
    public async Task AReturnedSubmissionContinuesAsTheNextRevision()
    {
        using HttpClient client = host.Api.CreateClient();
        ProgressSessions sessions = await client.SignInAsync();
        Guid projectId = await host.ProjectAsync();
        host.Inputs.Set(projectId, actualPercent: 40, baselineStart: ProgressDriver.Today.AddDays(-49));
        Guid first = AdministrationApi.IdOf(await client.StartOrFailAsync(sessions.ProjectManager, projectId));
        using (HttpResponseMessage edited = await client.EditAsync(sessions.ProjectManager, first, new { narrative = new { text = "First account", language = "en" } }))
        {
            Assert.Equal(HttpStatusCode.OK, edited.StatusCode);
        }

        await client.CommandOrFailAsync(sessions.ProjectManager, first, "submit");
        await client.CommandOrFailAsync(sessions.Reviewer, first, "start-review");
        using (HttpResponseMessage reasonless = await client.CommandAsync(sessions.Reviewer, first, "return", new { }))
        {
            Assert.Equal(["reason REQUIRED"], await reasonless.ReadFieldErrorsAsync());
        }

        JsonObject returned = await client.CommandOrFailAsync(sessions.Reviewer, first, "return", new { reason = new { text = "Explain the slippage", language = "en" } });
        Assert.Equal(("RETURNED", "Explain the slippage", ProgressDriver.Person(3).ToString()),
            (returned.Status(), returned["returnReason"]!["text"]!.GetValue<string>(), returned["reviewedByUserId"]!.GetValue<string>()));

        JsonArray revisions = await client.ItemsAsync(sessions.ProjectManager, ProgressDriver.Submissions, projectId);
        JsonNode next = revisions[0]!;
        Assert.Equal((2, "DRAFT", "First account", returned["reportingCycleId"]!.GetValue<string>()),
            (next["revisionNo"]!.GetValue<int>(), next.Status(), next["narrative"]!["text"]!.GetValue<string>(), next["reportingCycleId"]!.GetValue<string>()));

        Guid second = Guid.Parse(next["id"]!.GetValue<string>());
        await client.CommandOrFailAsync(sessions.ProjectManager, second, "submit");
        await client.CommandOrFailAsync(sessions.Reviewer, second, "start-review");
        Assert.Equal("PUBLISHED", (await client.CommandOrFailAsync(sessions.Reviewer, second, "publish")).Status());

        Assert.Equal(
            ["Progress.ProgressUpdateStarted", "Progress.ProgressUpdateChanged", "Progress.ProgressSubmitted", "Progress.ReviewStarted", "Progress.ProgressReturned"],
            await host.AuditEventsAsync(first));
        Assert.Equal(["Progress.ProgressUpdateStarted", "Progress.ProgressSubmitted", "Progress.ReviewStarted", "Progress.ProgressPublished"], await host.AuditEventsAsync(second));
    }

    /// <summary>
    /// "Published progress remains governed" (ADR-013): the entity Project Manager holds the review permission here, as an
    /// Appendix A mistake could grant it, and is still refused, because the gate is AHDA's. An AHDA user who submitted a
    /// revision may neither review nor publish it. Neither refusal changes anything, and each is audited.
    /// </summary>
    [Fact]
    public async Task PublishedProgressIsGovernedByAhdaAndNeverByItsSubmitter()
    {
        using HttpClient client = host.Api.CreateClient();
        ProgressSessions sessions = await client.SignInAsync();
        Guid entityProject = await host.ProjectAsync();
        host.Inputs.Set(entityProject, actualPercent: 40, baselineStart: ProgressDriver.Today.AddDays(-49));
        Guid entitySubmission = AdministrationApi.IdOf(await client.StartOrFailAsync(sessions.ProjectManager, entityProject));
        await client.CommandOrFailAsync(sessions.ProjectManager, entitySubmission, "submit");

        using (HttpResponseMessage external = await client.CommandAsync(sessions.ProjectManager, entitySubmission, "start-review"))
        {
            Assert.Equal(HttpStatusCode.Forbidden, external.StatusCode);
        }

        // An AHDA project, submitted by local.r02, who also holds review.
        Guid ahdaProject = await host.ProjectAsync(projectManager: 5, entity: false);
        host.Inputs.Set(ahdaProject, actualPercent: 40, baselineStart: ProgressDriver.Today.AddDays(-49));
        Guid ahdaSubmission = AdministrationApi.IdOf(await client.StartOrFailAsync(sessions.Portfolio, ahdaProject));
        await client.CommandOrFailAsync(sessions.Portfolio, ahdaSubmission, "submit");
        using (HttpResponseMessage ownReview = await client.CommandAsync(sessions.Portfolio, ahdaSubmission, "start-review"))
        {
            Assert.Equal(HttpStatusCode.Forbidden, ownReview.StatusCode);
        }

        // Publication skips no review.
        using (HttpResponseMessage unreviewed = await client.CommandAsync(sessions.Reviewer, ahdaSubmission, "publish"))
        {
            Assert.Equal((HttpStatusCode.Conflict, "INVALID_TRANSITION"), await unreviewed.RefusalAsync());
        }

        await client.CommandOrFailAsync(sessions.Reviewer, ahdaSubmission, "start-review");
        using (HttpResponseMessage ownPublication = await client.CommandAsync(sessions.Portfolio, ahdaSubmission, "publish"))
        {
            Assert.Equal(HttpStatusCode.Forbidden, ownPublication.StatusCode);
        }

        Assert.Equal("SUBMITTED", (await client.ItemsAsync(sessions.Reviewer, ProgressDriver.Submissions, entityProject))[0]!.Status());
        Assert.Equal("UNDER_REVIEW", (await client.ItemsAsync(sessions.Reviewer, ProgressDriver.Submissions, ahdaProject))[0]!.Status());
        Assert.Empty(await client.ItemsAsync(sessions.Reviewer, ProgressDriver.Snapshots, ahdaProject));
        Assert.Equal(
            ["EXTERNAL_USER", "SUBMITTER", "SUBMITTER"],
            await host.Database.QueryAsync($"""
                SELECT a.new_value FROM audit_activity.audit_event e JOIN audit_activity.audit_event_attribute a ON a.audit_event_id = e.id
                WHERE e.event_type = 'Progress.ReviewRefused' AND e.subject_id IN ('{entitySubmission}', '{ahdaSubmission}') AND a.attribute_name = 'reason'
                ORDER BY e.occurred_at
                """));
    }

    /// <summary>
    /// ADR-013: the entity Project Manager submits on the project they manage and on no other — not another project of their
    /// own entity, which they may still see as R08. The internal Project Manager of that project submits it.
    /// </summary>
    [Fact]
    public async Task AnEntityProjectManagerSubmitsOnlyOnTheProjectTheyManage()
    {
        using HttpClient client = host.Api.CreateClient();
        ProgressSessions sessions = await client.SignInAsync();
        string internalManager = (await client.SignInOrFailAsync(5)).AccessToken;
        string viewer = (await client.SignInOrFailAsync(6)).AccessToken;
        Guid managed = await host.ProjectAsync(projectManager: 8);
        Guid notManaged = await host.ProjectAsync(projectManager: 5);
        Guid ahdaProject = await host.ProjectAsync(projectManager: 5, entity: false);
        foreach (Guid project in new[] { managed, notManaged, ahdaProject })
        {
            host.Inputs.Set(project, actualPercent: 10, baselineStart: ProgressDriver.Today);
        }

        await client.StartOrFailAsync(sessions.ProjectManager, managed);
        // They see that project as R08, so the refusal is 403, not 404 (R-47).
        using (HttpResponseMessage another = await client.StartAsync(sessions.ProjectManager, notManaged))
        {
            Assert.Equal(HttpStatusCode.Forbidden, another.StatusCode);
        }

        await client.StartOrFailAsync(internalManager, notManaged);
        Assert.Single(await client.ItemsAsync(sessions.ProjectManager, ProgressDriver.Submissions, notManaged));
        Assert.Empty(await client.ItemsAsync(sessions.ProjectManager, ProgressDriver.Submissions, ahdaProject));

        using HttpResponseMessage noPermission = await client.GetAsync($"{ProgressDriver.Submissions}?projectId={managed}", viewer);
        Assert.Equal(HttpStatusCode.Forbidden, noPermission.StatusCode);
    }
}
