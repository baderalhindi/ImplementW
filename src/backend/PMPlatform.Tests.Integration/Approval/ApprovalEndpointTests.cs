using System.Net;
using System.Text.Json.Nodes;
using PMPlatform.Application.Features.Approval.Contracts;
using PMPlatform.Tests.Integration.Identity;
using PMPlatform.Tests.Integration.MasterDataConfig;

namespace PMPlatform.Tests.Integration.Approval;

/// <summary>
/// The WF-11 endpoints (TASK-035) as the SPA calls them: gates, validation, the R-47 answers, and a retried decision
/// answered 409 without a second outcome. local.r02 decides; local.r06 requests and may only see their own runs.
/// </summary>
[Collection(ApprovalSuite.Name)]
public sealed class ApprovalEndpointTests(ApprovalTestHost host)
{
    private const string Tasks = "/api/v1/approval-tasks";
    private const string Instances = "/api/v1/approval-instances";
    private const string Delegations = "/api/v1/approval-delegations";

    [Fact]
    public async Task AnApproverFindsTheTaskInTheInboxApprovesItOnceAndTheRequesterSeesTheResult()
    {
        using HttpClient client = host.Api.CreateClient();
        string approver = (await client.SignInOrFailAsync(2)).AccessToken;
        string requester = (await client.SignInOrFailAsync(6)).AccessToken;
        ApprovalInstanceDetail run = await host.StartAsync(await host.NewSubjectAsync(), 1, ApprovalTestHost.SingleStage);
        Guid task = run.Tasks.Single().Id;

        using (HttpResponseMessage inbox = await client.GetAsync($"{Tasks}?pageSize=100", approver))
        {
            Assert.Equal(HttpStatusCode.OK, inbox.StatusCode);
            Assert.Contains((await inbox.ReadObjectAsync())["items"]!.AsArray(), item => item!["taskId"]!.GetValue<Guid>() == task);
        }

        string key = Guid.NewGuid().ToString();
        using (HttpResponseMessage approved = await client.SendAsync(HttpMethod.Post, $"{Tasks}/{task}/approve", approver, idempotencyKey: key))
        {
            Assert.Equal(HttpStatusCode.OK, approved.StatusCode);
            JsonObject body = await approved.ReadObjectAsync();
            Assert.Equal(("APPROVED", "APPROVED"), (body["status"]!.GetValue<string>(), body["tasks"]![0]!["status"]!.GetValue<string>()));
        }

        using (HttpResponseMessage retried = await client.SendAsync(HttpMethod.Post, $"{Tasks}/{task}/approve", approver, idempotencyKey: key))
        {
            Assert.Equal((HttpStatusCode.Conflict, "TERMINAL_STATE"), (retried.StatusCode, await retried.CodeOfAsync()));
        }

        await host.OutcomeMessageIdAsync(run.Id);
        using (HttpResponseMessage mine = await client.GetAsync($"{Instances}?requestedBy=me&status=APPROVED&pageSize=100", requester))
        {
            Assert.Contains((await mine.ReadObjectAsync())["items"]!.AsArray(), item => item!["id"]!.GetValue<Guid>() == run.Id);
        }

        using HttpResponseMessage history = await client.GetAsync($"{Instances}/{run.Id}", requester);
        Assert.Equal(HttpStatusCode.OK, history.StatusCode);
        Assert.Equal(IdentityDatabase.UserId(2), (await history.ReadObjectAsync())["tasks"]![0]!["actingUserId"]!.GetValue<string>());
    }

    [Fact]
    public async Task RequestsAreGatedValidatedAndAnsweredByR47()
    {
        using HttpClient client = host.Api.CreateClient();
        string approver = (await client.SignInOrFailAsync(2)).AccessToken;
        string requester = (await client.SignInOrFailAsync(6)).AccessToken;
        string outsider = (await client.SignInOrFailAsync(3)).AccessToken;
        ApprovalInstanceDetail run = await host.StartAsync(await host.NewSubjectAsync(), 1, ApprovalTestHost.SingleStage);
        Guid task = run.Tasks.Single().Id;

        // No APPROVAL_DECIDE: the gate refuses before any record is read.
        await AssertAnswerAsync(HttpStatusCode.Forbidden, "PERMISSION_DENIED", client.GetAsync(Tasks, requester));
        await AssertAnswerAsync(HttpStatusCode.Forbidden, "PERMISSION_DENIED", client.PostAsync($"{Tasks}/{task}/approve", requester));

        // A reason is required to reject or return; a sensitive write needs an Idempotency-Key.
        using (HttpResponseMessage noReason = await client.PostAsync($"{Tasks}/{task}/reject", approver))
        {
            Assert.Equal((HttpStatusCode.BadRequest, "VALIDATION_FAILED"), (noReason.StatusCode, await noReason.CodeOfAsync()));
            Assert.Equal(["reason REQUIRED"], await noReason.ReadFieldErrorsAsync());
        }

        await AssertAnswerAsync(HttpStatusCode.BadRequest, "IDEMPOTENCY_KEY_REQUIRED",
            client.SendAsync(HttpMethod.Post, $"{Tasks}/{task}/approve", approver, idempotencyKey: ""));
        await AssertAnswerAsync(HttpStatusCode.BadRequest, "VALIDATION_FAILED", client.GetAsync(Instances, approver));
        await AssertAnswerAsync(HttpStatusCode.BadRequest, "VALIDATION_FAILED",
            client.GetAsync($"{Instances}?requestedBy=me&subjectModule={TestSource.Module}", approver));

        // local.r03 decides R03 tasks of their department; this run's stage is R02's, so for them it is 403, and for
        // an unknown task, 404.
        await AssertAnswerAsync(HttpStatusCode.Forbidden, "PERMISSION_DENIED", client.PostAsync($"{Tasks}/{task}/approve", outsider));
        await AssertAnswerAsync(HttpStatusCode.NotFound, "NOT_FOUND", client.PostAsync($"{Tasks}/{Guid.NewGuid()}/approve", approver));

        using HttpResponseMessage rejected = await client.PostAsync($"{Tasks}/{task}/reject", approver, new { reason = new { text = "Out of budget.", language = "en" } });
        JsonObject body = await rejected.ReadObjectAsync();
        Assert.Equal(("REJECTED", "Out of budget."), (body["status"]!.GetValue<string>(), body["tasks"]![0]!["decisionReason"]!["text"]!.GetValue<string>()));
    }

    [Fact]
    public async Task ADelegationIsGivenListedAndRevokedThroughTheApi()
    {
        using HttpClient client = host.Api.CreateClient();
        string delegator = (await client.SignInOrFailAsync(2)).AccessToken;

        using HttpResponseMessage created = await client.PostAsync(Delegations, delegator, new
        {
            delegateUserId = IdentityDatabase.UserId(3),
            routingKey = ApprovalTestHost.SingleStage,
            validTo = host.Clock.GetUtcNow().AddDays(3),
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Guid id = AdministrationApi.IdOf(await created.ReadObjectAsync());
        Assert.Equal($"/api/v1/approval-delegations/{id}", created.Headers.Location!.OriginalString);

        using (HttpResponseMessage list = await client.GetAsync(Delegations, delegator))
        {
            Assert.Contains((await list.ReadObjectAsync())["given"]!.AsArray(), d => d!["id"]!.GetValue<Guid>() == id);
        }

        await AssertAnswerAsync(HttpStatusCode.UnprocessableEntity, ApprovalErrorCodes.DelegateInvalid,
            client.PostAsync(Delegations, delegator, new { delegateUserId = IdentityDatabase.UserId(8), validTo = host.Clock.GetUtcNow().AddDays(1) }));
        await AssertAnswerAsync(HttpStatusCode.BadRequest, "VALIDATION_FAILED", client.PostAsync(Delegations, delegator, new { delegateUserId = IdentityDatabase.UserId(3) }));

        using HttpResponseMessage revoked = await client.PostAsync($"{Delegations}/{id}/revoke", delegator);
        Assert.Equal("REVOKED", (await revoked.ReadObjectAsync())["status"]!.GetValue<string>());
        await AssertAnswerAsync(HttpStatusCode.Conflict, "TERMINAL_STATE", client.PostAsync($"{Delegations}/{id}/revoke", delegator));
    }

    private static async Task AssertAnswerAsync(HttpStatusCode status, string code, Task<HttpResponseMessage> call)
    {
        using HttpResponseMessage response = await call;
        Assert.Equal((status, code), (response.StatusCode, await response.CodeOfAsync()));
    }
}
