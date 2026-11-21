using System.Net;
using System.Text.Json.Nodes;
using PMPlatform.Application.Features.Approval.Contracts;
using PMPlatform.Tests.Integration.Identity;
using PMPlatform.Tests.Integration.MasterDataConfig;

namespace PMPlatform.Tests.Integration.Project;

/// <summary>
/// Review round 7, item 4: R02 edits the registration of an approved project with a reason, audited old and new; the budget, the planned dates and the
/// governance profile still change only through a change request, and a draft is edited as a draft.
/// </summary>
[Collection(ProjectSuite.Name)]
public sealed class ProjectRegistrationEditTests(ProjectTestHost host)
{
    [Fact]
    public async Task R02EditsAnApprovedRegistrationButNotItsCommitments()
    {
        using HttpClient client = host.Api.CreateClient();
        Sessions sessions = await client.SignInAsync();
        Guid projectId = await host.UnderReviewAsync(client, sessions);
        await host.DecideAndDeliverAsync(projectId, ApprovalTaskDecision.Approve);
        string path = $"{ProjectDriver.Projects}/{projectId}";
        JsonObject registration = host.Registration();
        object Edit(Action<JsonObject> change)
        {
            JsonObject body = JsonNode.Parse(registration.ToJsonString())!.AsObject();
            change(body);
            return new { registration = body, reason = new { text = "Corrected after the kickoff meeting", language = "en" } };
        }

        // The approved project's own dates, as registered.
        JsonObject row = await host.RowAsync(projectId);
        registration["plannedStartDate"] = row["planned_start_date"]!.GetValue<string>();
        registration["plannedEndDate"] = row["planned_end_date"]!.GetValue<string>();

        string tag = await ETagAsync(client, sessions.Approver, path);
        using (HttpResponseMessage budget = await client.PostAsync($"{path}/edit-registration", sessions.Approver, Edit(r => r["registrationBudgetSar"] = "13000000.00"), tag))
        {
            Assert.Equal((HttpStatusCode.UnprocessableEntity, "PROJECT_CHANGE_REQUEST_REQUIRED"), (budget.StatusCode, await budget.CodeOfAsync()));
            Assert.Equal(["registrationBudgetSar NOT_ALLOWED"], await budget.ReadFieldErrorsAsync());
        }

        using (HttpResponseMessage reviewer = await client.PostAsync($"{path}/edit-registration", sessions.Reviewer, Edit(_ => { }), tag))
        {
            Assert.Equal(HttpStatusCode.Forbidden, reviewer.StatusCode);
        }

        using (HttpResponseMessage edited = await client.PostAsync(
                   $"{path}/edit-registration", sessions.Approver, Edit(r => r["title"] = new JsonObject { ["text"] = "Regional road upgrade, phase 1", ["language"] = "en" }), tag))
        {
            Assert.Equal(HttpStatusCode.OK, edited.StatusCode);
            Assert.Equal("Regional road upgrade, phase 1", (await edited.ReadObjectAsync())["title"]!["text"]!.GetValue<string>());
        }

        Assert.Equal<string>(
            ["reason_text", "title"],
            await host.Database.QueryAsync($"""
                SELECT a.attribute_name FROM audit_activity.audit_event e JOIN audit_activity.audit_event_attribute a ON a.audit_event_id = e.id
                WHERE e.event_type = 'Project.RegistrationEdited' AND e.subject_id = '{projectId}' ORDER BY a.attribute_name
                """));

        // A draft is edited as a draft, by its registrant.
        Guid draft = AdministrationApi.IdOf(await client.CreateOrFailAsync(sessions.Entity, host.Registration()));
        using HttpResponseMessage asDraft = await client.PostAsync(
            $"{ProjectDriver.Projects}/{draft}/edit-registration", sessions.Approver, Edit(_ => { }), await ETagAsync(client, sessions.Approver, $"{ProjectDriver.Projects}/{draft}"));
        Assert.Equal((HttpStatusCode.Conflict, "PROJECT_EDIT_AS_DRAFT"), (asDraft.StatusCode, await asDraft.CodeOfAsync()));
    }

    private static async Task<string> ETagAsync(HttpClient client, string token, string path)
    {
        using HttpResponseMessage read = await client.GetAsync(path, token);
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        return AdministrationApi.ETagOf(read);
    }
}
