using System.Net;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using PMPlatform.Application.Features.Risk;
using PMPlatform.Tests.Integration.Identity;

namespace PMPlatform.Tests.Integration.Risk;

/// <summary>
/// A risk's way through the API: the state machine enforced by the server, the reopen only RISK_REOPEN allows, treatment that needs
/// an action, and an acceptance that expires and returns the risk for review (TASK-055 gate decision).
/// </summary>
[Collection(RiskSuite.Name)]
public sealed class RiskLifecycleTests(RiskTestHost host)
{
    /// <summary>
    /// TASK-055's acceptance criterion: reopening a closed risk requires the specific reopen permission and is captured in Audit.
    /// local.r02 edits every risk (RISK_MANAGE at ALL) and is refused, as is the Project Manager who closed it; local.r03 holds
    /// RISK_REOPEN for the department and reopens it, back to ASSESSED for review, with the audit event naming them.
    /// </summary>
    [Fact]
    public async Task ReopeningAClosedRiskNeedsTheReopenPermissionAndIsAudited()
    {
        using HttpClient client = host.Api.CreateClient();
        RiskSessions sessions = await client.SignInAsync();
        Guid riskId = await client.RiskAsync(sessions.EntityManager, await host.ProjectAsync());
        await client.CommandOrFailAsync(sessions.Officer, riskId, "assess", RiskDriver.Assessment(await host.DimensionsAsync(), 2, 3));

        JsonObject closed = await client.CommandOrFailAsync(sessions.EntityManager, riskId, "close", RiskDriver.Rationale("The contractor has mobilised."));
        Assert.Equal(("CLOSED", RiskDriver.Person(8).ToString()), (closed.Text("status"), closed.Text("closedByUserId")));

        foreach (string token in new[] { sessions.Officer, sessions.EntityManager })
        {
            using HttpResponseMessage refused = await client.CommandAsync(token, riskId, "reopen");
            Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
        }

        using (HttpResponseMessage edit = await client.PutAsync($"{RiskDriver.Risks}/{riskId}", sessions.Officer, RiskDriver.RiskBody(riskId), await client.ETagAsync(sessions.Officer, riskId)))
        {
            Assert.Equal((HttpStatusCode.Conflict, "RISK_CLOSED"), await edit.RefusalAsync());
        }

        JsonObject reopened = await client.CommandOrFailAsync(sessions.Reopener, riskId, "reopen");
        Assert.Equal(("ASSESSED", 1), (reopened.Text("status"), reopened["reopenedCount"]!.GetValue<int>()));
        Assert.Null(reopened["closureRationale"]);
        Assert.Equal(
            [$"{RiskDriver.Person(3)} CLOSED ASSESSED"],
            await host.Database.QueryAsync($"""
                SELECT e.actor_user_id || ' ' || a.old_value || ' ' || a.new_value FROM audit_activity.audit_event e
                JOIN audit_activity.audit_event_attribute a ON a.audit_event_id = e.id AND a.attribute_name = 'status'
                WHERE e.event_type = 'Risk.RiskReopened' AND e.subject_id = '{riskId}'
                """));
        Assert.Equal(["Risk.RiskRegistered", "Risk.RiskAssessed", "Risk.RiskClosed", "Risk.RiskReopened"], await host.AuditEventsAsync(riskId));
    }

    /// <summary>A risk never assessed reopens to IDENTIFIED; a closed risk changes nothing until reopened; only the reopen leaves CLOSED.</summary>
    [Fact]
    public async Task AClosedRiskIsFrozenUntilItIsReopened()
    {
        using HttpClient client = host.Api.CreateClient();
        RiskSessions sessions = await client.SignInAsync();
        Guid riskId = await client.RiskAsync(sessions.EntityManager, await host.ProjectAsync());

        using (HttpResponseMessage blank = await client.CommandAsync(sessions.EntityManager, riskId, "close", RiskDriver.Rationale(" ")))
        {
            Assert.Equal((HttpStatusCode.BadRequest, "VALIDATION_FAILED"), await blank.RefusalAsync());
        }

        using (HttpResponseMessage absent = await client.CommandAsync(sessions.EntityManager, riskId, "close", new { }))
        {
            Assert.Equal(["rationale REQUIRED"], await absent.ReadFieldErrorsAsync());
        }

        await client.CommandOrFailAsync(sessions.EntityManager, riskId, "close", RiskDriver.Rationale("Raised in error."));
        foreach ((string command, object? body) in new (string, object?)[] { ("monitor", null), ("close", RiskDriver.Rationale("Again.")) })
        {
            using HttpResponseMessage refused = await client.CommandAsync(sessions.EntityManager, riskId, command, body);
            Assert.Equal((HttpStatusCode.Conflict, "RISK_CLOSED"), await refused.RefusalAsync());
        }

        using (HttpResponseMessage action = await client.PostAsync(RiskDriver.Actions, sessions.EntityManager, ActionBody(riskId)))
        {
            Assert.Equal((HttpStatusCode.Conflict, "RISK_CLOSED"), await action.RefusalAsync());
        }

        Assert.Equal("IDENTIFIED", (await client.CommandOrFailAsync(sessions.Reopener, riskId, "reopen")).Text("status"));
    }

    /// <summary>Treatment starts with a live action and no acceptance; the actions move forward only; monitoring follows treatment.</summary>
    [Fact]
    public async Task TreatmentNeedsALiveActionAndMonitoringFollowsIt()
    {
        using HttpClient client = host.Api.CreateClient();
        RiskSessions sessions = await client.SignInAsync();
        Guid riskId = await client.RiskAsync(sessions.EntityManager, await host.ProjectAsync());

        using (HttpResponseMessage unrated = await client.CommandAsync(sessions.EntityManager, riskId, "start-treatment"))
        {
            Assert.Equal((HttpStatusCode.Conflict, "INVALID_TRANSITION"), await unrated.RefusalAsync());
        }

        await client.CommandOrFailAsync(sessions.Officer, riskId, "assess", RiskDriver.Assessment(await host.DimensionsAsync(), 4, 4));
        using (HttpResponseMessage noAction = await client.CommandAsync(sessions.EntityManager, riskId, "start-treatment"))
        {
            Assert.Equal((HttpStatusCode.UnprocessableEntity, "RISK_TREATMENT_ACTION_REQUIRED"), await noAction.RefusalAsync());
        }

        Guid actionId = AdministrationApi.IdOf(await client.CreatedOrFailAsync(sessions.EntityManager, RiskDriver.Actions, ActionBody(riskId)));
        Assert.Equal("TREATMENT", (await client.CommandOrFailAsync(sessions.EntityManager, riskId, "start-treatment")).Text("status"));

        using (HttpResponseMessage skip = await client.PostAsync($"{RiskDriver.Actions}/{actionId}/complete", sessions.EntityManager))
        {
            Assert.Equal((HttpStatusCode.Conflict, "INVALID_TRANSITION"), await skip.RefusalAsync());
        }

        await client.OkOrFailAsync(sessions.EntityManager, $"{RiskDriver.Actions}/{actionId}/start");
        JsonObject completed = await client.OkOrFailAsync(sessions.EntityManager, $"{RiskDriver.Actions}/{actionId}/complete");
        Assert.Equal("COMPLETED", completed.Text("status"));
        Assert.NotNull(completed["completedAt"]);
        using (HttpResponseMessage final = await client.PostAsync($"{RiskDriver.Actions}/{actionId}/cancel", sessions.EntityManager))
        {
            Assert.Equal((HttpStatusCode.Conflict, "RISK_ACTION_NOT_EDITABLE"), await final.RefusalAsync());
        }

        Assert.Equal("MONITORING", (await client.CommandOrFailAsync(sessions.EntityManager, riskId, "monitor")).Text("status"));
        using (HttpResponseMessage again = await client.CommandAsync(sessions.EntityManager, riskId, "start-treatment"))
        {
            Assert.Equal((HttpStatusCode.UnprocessableEntity, "RISK_TREATMENT_ACTION_REQUIRED"), await again.RefusalAsync());
        }

        Assert.Equal(
            ["Risk.TreatmentActionCreated", "Risk.TreatmentActionStarted", "Risk.TreatmentActionCompleted"],
            await host.AuditEventsAsync(actionId));
    }

    /// <summary>
    /// The gate decision: risk acceptance carries an expiry and returns for review — no permanent acceptance. An acceptance must end
    /// after today, holds the risk in MONITORING meanwhile, and when its day comes the expiry pass marks it EXPIRED, as the service
    /// principal, and returns the risk to ASSESSED with its review due that day.
    /// </summary>
    [Fact]
    public async Task AnAcceptanceExpiresAndReturnsTheRiskForReview()
    {
        using HttpClient client = host.Api.CreateClient();
        RiskSessions sessions = await client.SignInAsync();
        Guid riskId = await client.RiskAsync(sessions.EntityManager, await host.ProjectAsync());
        await client.CommandOrFailAsync(sessions.Officer, riskId, "assess", RiskDriver.Assessment(await host.DimensionsAsync(), 2, 2));
        DateOnly expiry = DateOnly.FromDateTime(host.Clock.GetUtcNow().UtcDateTime).AddDays(1);

        using (HttpResponseMessage permanent = await client.CommandAsync(sessions.Officer, riskId, "accept", new { rationale = new { text = "Forever.", language = "en" } }))
        {
            Assert.Equal(["expiresOn REQUIRED"], await permanent.ReadFieldErrorsAsync());
        }

        using (HttpResponseMessage today = await client.CommandAsync(sessions.Officer, riskId, "accept", Acceptance(expiry.AddDays(-1))))
        {
            Assert.Equal((HttpStatusCode.UnprocessableEntity, "RISK_ACCEPTANCE_EXPIRY_INVALID"), await today.RefusalAsync());
        }

        JsonObject accepted = await client.CommandOrFailAsync(sessions.Officer, riskId, "accept", Acceptance(expiry));
        Assert.Equal(("MONITORING", RiskDriver.Iso(expiry), RiskDriver.Iso(expiry)), (accepted.Text("status"), accepted.Text("acceptedUntil"), accepted.Text("nextReviewDate")));
        foreach ((string command, object? body, string code) in new (string, object?, string)[]
                 {
                     ("accept", Acceptance(expiry.AddDays(5)), "RISK_ACCEPTANCE_ACTIVE"),
                     ("start-treatment", null, "RISK_ACCEPTANCE_ACTIVE"),
                 })
        {
            using HttpResponseMessage refused = await client.CommandAsync(sessions.Officer, riskId, command, body);
            Assert.Equal(code, (await refused.RefusalAsync()).Code);
        }

        // Nothing lapses before its day.
        Assert.Equal(0, await RunExpiryPassAsync());
        host.Clock.Advance(TimeSpan.FromDays(2));
        try
        {
            Assert.True(await RunExpiryPassAsync() >= 1);
        }
        finally
        {
            host.Clock.Reset();
        }

        Assert.Equal(
            [$"ASSESSED {RiskDriver.Iso(expiry)} EXPIRED"],
            await host.Database.QueryAsync($"SELECT r.status || ' ' || r.next_review_date || ' ' || a.status FROM risk.risk r JOIN risk.risk_acceptance a ON a.risk_id = r.id WHERE r.id = '{riskId}'"));
        Assert.Equal(
            [$"{RiskServicePrincipal.Id} SERVICE"],
            await host.Database.QueryAsync($"SELECT actor_user_id || ' ' || actor_type FROM audit_activity.audit_event WHERE event_type = 'Risk.AcceptanceExpired' AND subject_id = '{riskId}'"));
        JsonObject reviewed = await client.GetOrFailAsync(sessions.EntityManager, $"{RiskDriver.Risks}/{riskId}");
        Assert.Null(reviewed["acceptedUntil"]);
        Assert.Equal("EXPIRED", (await client.GetOrFailAsync(sessions.EntityManager, $"{RiskDriver.Acceptances}?riskId={riskId}"))["items"]![0]!.Text("status"));
    }

    /// <summary>A revoked acceptance returns the risk for review at once; closing an accepted risk ends its acceptance.</summary>
    [Fact]
    public async Task RevokingOrClosingEndsAnAcceptance()
    {
        using HttpClient client = host.Api.CreateClient();
        RiskSessions sessions = await client.SignInAsync();
        Guid[] dimensions = await host.DimensionsAsync();
        Guid revokedRisk = await client.RiskAsync(sessions.EntityManager, await host.ProjectAsync());
        Guid closedRisk = await client.RiskAsync(sessions.EntityManager, await host.ProjectAsync());
        foreach (Guid riskId in new[] { revokedRisk, closedRisk })
        {
            await client.CommandOrFailAsync(sessions.Officer, riskId, "assess", RiskDriver.Assessment(dimensions, 1, 2));
            await client.CommandOrFailAsync(sessions.Officer, riskId, "accept", Acceptance(RiskDriver.Today.AddDays(60)));
        }

        Assert.Equal("ASSESSED", (await client.CommandOrFailAsync(sessions.Officer, revokedRisk, "revoke-acceptance")).Text("status"));
        using (HttpResponseMessage twice = await client.CommandAsync(sessions.Officer, revokedRisk, "revoke-acceptance"))
        {
            Assert.Equal((HttpStatusCode.Conflict, "INVALID_TRANSITION"), await twice.RefusalAsync());
        }

        await client.CommandOrFailAsync(sessions.EntityManager, closedRisk, "close", RiskDriver.Rationale("The window has passed."));
        Assert.Equal(
            ["REVOKED", "REVOKED"],
            await host.Database.QueryAsync($"SELECT status FROM risk.risk_acceptance WHERE risk_id IN ('{revokedRisk}', '{closedRisk}') AND revoked_at IS NOT NULL ORDER BY risk_id"));
    }

    private static object Acceptance(DateOnly expiresOn) =>
        new { expiresOn = RiskDriver.Iso(expiresOn), rationale = new { text = "Within the project's contingency.", language = "en" } };

    private static object ActionBody(Guid riskId) => new
    {
        riskId,
        title = new { text = "Agree a mobilisation plan with the contractor", language = "en" },
        actionType = "MITIGATE",
        dueDate = RiskDriver.Iso(RiskDriver.Today.AddDays(14)),
    };

    private async Task<int> RunExpiryPassAsync()
    {
        await using AsyncServiceScope scope = host.Api.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IRiskMaintenance>().RunAsync(100, CancellationToken.None);
    }
}
