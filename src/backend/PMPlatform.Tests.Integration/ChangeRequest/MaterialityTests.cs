using System.Net;
using System.Text.Json.Nodes;
using PMPlatform.Application.Features.Approval.Contracts;
using PMPlatform.Tests.Integration.Identity;

namespace PMPlatform.Tests.Integration.ChangeRequest;

/// <summary>
/// The materiality rule of ADR-016 through the real API (TASK-060; TASK-106's shape): computed by the server — previewed for the
/// requester before submitting, recorded and pinned when AHDA starts the review — from the bands of the project's governance profile in
/// the MATERIALITY_BAND version in force; the highest band any dimension triggers, cumulative against the active baseline; routing the
/// WF-11 run; and failing closed, never reading a missing figure as zero.
/// </summary>
[Collection(ChangeRequestSuite.Name)]
public sealed class MaterialityTests(ChangeRequestTestHost host)
{
    /// <summary>
    /// A first cost change of 3% of the budget is band 1; a second of the same size takes the cumulative position to 6%, band 2 — the
    /// approved change counts against the same baseline. The evaluation is recorded at review with the version, baseline and budget it
    /// was against, and a later publication changes nothing recorded: a preview now resolves the new version.
    /// </summary>
    [Fact]
    public async Task MaterialityIsRecordedAtReviewPinnedAndCumulativeAgainstTheActiveBaseline()
    {
        using HttpClient client = host.Api.CreateClient();
        ChangeSessions sessions = await client.SignInAsync();
        Guid projectId = await host.ProjectAsync("LIGHT");
        Guid baseline = await host.BaselinedAsync(client, sessions, projectId, days: 40);
        Guid budget = await host.BudgetedAsync(client, sessions, projectId, "1000000.00");

        Guid firstId = await client.RaiseAsync(sessions.EntityManager, ChangeRequestDriver.RequestBody(projectId, "COST", costImpactSar: "30000.00"));
        JsonObject first = (await host.ApprovedAsync(client, sessions, firstId))["materiality"]!.AsObject();
        Assert.Equal(("30000.00", 1, 1), (first.Text("cumulativeCostImpactSar"), first["costBandNo"]!.GetValue<int>(), first["resultingBandNo"]!.GetValue<int>()));

        Guid secondId = await client.RaiseAsync(sessions.EntityManager, ChangeRequestDriver.RequestBody(projectId, "COST", costImpactSar: "30000.00"));
        JsonObject second = (await client.UnderReviewAsync(sessions, secondId))["materiality"]!.AsObject();
        Assert.Equal(
            ("60000.00", 2, null, null, 2, ChangeRequestTestHost.MaterialityVersionId.ToString(), baseline.ToString(), budget.ToString()),
            (second.Text("cumulativeCostImpactSar"), second["costBandNo"]!.GetValue<int>(), second["scheduleBandNo"], second["scopeBandNo"],
             second["resultingBandNo"]!.GetValue<int>(), second.Text("materialityConfigurationVersionId"), second.Text("projectBaselineId"), second.Text("financialCommitmentId")));
        Assert.NotNull(second["evaluationId"]);
        string recorded = Assert.Single(await host.Database.QueryAsync($"""
            SELECT revision_no || '|' || materiality_configuration_version_id || '|' || project_baseline_id || '|' || project_baseline_version_no || '|'
                   || financial_commitment_id || '|' || financial_commitment_version_no || '|' || baseline_budget_sar || '|' || cumulative_cost_impact_sar || '|'
                   || cost_band_no || '|' || resulting_band_no
            FROM change_request.materiality_evaluation WHERE change_request_id = '{secondId}'
            """));
        Assert.Equal($"1|{ChangeRequestTestHost.MaterialityVersionId}|{baseline}|1|{budget}|1|1000000.00|60000.00|2|2", recorded);

        // Band 2 routes as the matrix says: one stage, R02's.
        ApprovalInstanceDetail run = Assert.Single(await host.RunsAsync("ChangeRequest", "ChangeRequest", secondId));
        Assert.Equal([(short)1], run.Tasks.Select(t => t.SequenceNo));

        // A re-publication changes no recorded evaluation; a preview evaluates under the version in force now.
        Guid thirdId = await client.RaiseAsync(sessions.EntityManager, ChangeRequestDriver.RequestBody(projectId, "COST", costImpactSar: "5000.00"));
        Guid republished = await host.PublishBandsAsync(costBand2Percent: 1m);
        try
        {
            JsonObject preview = await client.OkOrFailAsync(sessions.EntityManager, $"{ChangeRequestDriver.Requests}/{thirdId}/preview-materiality");
            Assert.Equal((republished.ToString(), "35000.00", 2), (preview.Text("materialityConfigurationVersionId"), preview.Text("cumulativeCostImpactSar"), preview["costBandNo"]!.GetValue<int>()));
            Assert.Equal(recorded, Assert.Single(await host.Database.QueryAsync($"""
                SELECT revision_no || '|' || materiality_configuration_version_id || '|' || project_baseline_id || '|' || project_baseline_version_no || '|'
                       || financial_commitment_id || '|' || financial_commitment_version_no || '|' || baseline_budget_sar || '|' || cumulative_cost_impact_sar || '|'
                       || cost_band_no || '|' || resulting_band_no
                FROM change_request.materiality_evaluation WHERE change_request_id = '{secondId}'
                """)));
        }
        finally
        {
            await host.PublishBandsAsync(ChangeRequestTestHost.CostBand2Percent);
        }
    }

    /// <summary>
    /// TASK-061's need: the requester sees the classification the review will record before submitting, by the same rule, and nothing is
    /// recorded by looking. A classification sent by the client is no input: the server's is recorded. Once the review starts, the
    /// recorded one is read from the request and no preview is offered.
    /// </summary>
    [Fact]
    public async Task TheRequesterSeesTheClassificationBeforeSubmittingAndCannotSetIt()
    {
        using HttpClient client = host.Api.CreateClient();
        ChangeSessions sessions = await client.SignInAsync();
        Guid projectId = await host.ProjectAsync("LIGHT");
        await host.BaselinedAsync(client, sessions, projectId, days: 40);

        JsonObject body = JsonNode.Parse(System.Text.Json.JsonSerializer.Serialize(ChangeRequestDriver.RequestBody(projectId, "SCHEDULE", scheduleImpactDays: 12)))!.AsObject();
        body["resultingBandNo"] = 1;
        body["materiality"] = new JsonObject { ["resultingBandNo"] = 1, ["scheduleBandNo"] = 1 };
        Guid requestId = AdministrationApi.IdOf(await client.CreatedOrFailAsync(sessions.EntityManager, ChangeRequestDriver.Requests, body));

        JsonObject preview = await client.OkOrFailAsync(sessions.EntityManager, $"{ChangeRequestDriver.Requests}/{requestId}/preview-materiality");
        Assert.Equal((null, 2, 2), (preview["evaluationId"], preview["scheduleBandNo"]!.GetValue<int>(), preview["resultingBandNo"]!.GetValue<int>()));
        Assert.Null((await client.RequestAsync(sessions.EntityManager, requestId))["materiality"]);
        Assert.Equal(["0"], await host.Database.QueryAsync($"SELECT count(*)::text FROM change_request.materiality_evaluation WHERE change_request_id = '{requestId}'"));

        // The Department Manager starts the review (R03, DEPT): the recorded classification is the server's, as previewed.
        await client.CommandOrFailAsync(sessions.EntityManager, requestId, "submit");
        JsonObject reviewed = await client.CommandOrFailAsync(sessions.DepartmentManager, requestId, "start-review");
        Assert.Equal(("UNDER_REVIEW", 2, 2), (reviewed.Text("status"), reviewed["materiality"]!["scheduleBandNo"]!.GetValue<int>(), reviewed["materiality"]!["resultingBandNo"]!.GetValue<int>()));
        using HttpResponseMessage late = await client.PostAsync($"{ChangeRequestDriver.Requests}/{requestId}/preview-materiality", sessions.EntityManager);
        Assert.Equal((HttpStatusCode.Conflict, "CHANGE_REQUEST_EVALUATED"), await late.RefusalAsync());
    }

    /// <summary>
    /// A contractual obligation is band 3 whatever its size (TASK-106), and so is a scope change, by the rule band 3 names; band 3 adds
    /// the Department Manager's stage to the WF-11 run. A change with neither a schedule nor a cost impact is issued no authorisation, so it
    /// is implemented without one.
    /// </summary>
    [Fact]
    public async Task AContractualOrScopeChangeIsBandThreeAndTakesTheElevatedRoute()
    {
        using HttpClient client = host.Api.CreateClient();
        ChangeSessions sessions = await client.SignInAsync();
        Guid projectId = await host.ProjectAsync("LIGHT");
        await host.BaselinedAsync(client, sessions, projectId);

        Guid contractual = await client.RaiseAsync(sessions.EntityManager,
            ChangeRequestDriver.RequestBody(projectId, "CONTRACTUAL_OBLIGATION", contractual: true));
        Guid scope = await client.RaiseAsync(sessions.EntityManager,
            ChangeRequestDriver.RequestBody(projectId, "SCOPE", scopeImpact: "Add the second access road to the works."));
        JsonObject contractualReview = (await client.UnderReviewAsync(sessions, contractual))["materiality"]!.AsObject();
        JsonObject scopeReview = (await client.UnderReviewAsync(sessions, scope))["materiality"]!.AsObject();

        Assert.Equal((null, null, null, 3), (contractualReview["costBandNo"], contractualReview["scheduleBandNo"], contractualReview["scopeBandNo"],
                                              contractualReview["resultingBandNo"]!.GetValue<int>()));
        Assert.Equal((3, 3), (scopeReview["scopeBandNo"]!.GetValue<int>(), scopeReview["resultingBandNo"]!.GetValue<int>()));
        foreach (Guid requestId in new[] { contractual, scope })
        {
            ApprovalInstanceDetail run = Assert.Single(await host.RunsAsync("ChangeRequest", "ChangeRequest", requestId));
            Assert.Equal([(1, false), (2, true)], run.Tasks.OrderBy(t => t.SequenceNo).Select(t => ((int)t.SequenceNo, t.AssignedRoleId == ChangeRequestDriver.DepartmentManagerRoleId)));
        }

        await host.DecideAndDeliverAsync("ChangeRequest", "ChangeRequest", contractual, ApprovalTaskDecision.Approve);
        JsonObject approved = await client.RequestAsync(sessions.Officer, contractual);
        Assert.Equal(("APPROVED", 0), (approved.Text("status"), approved["authorizations"]!.AsArray().Count));
        await client.CommandOrFailAsync(sessions.Officer, contractual, "start-implementation");
        Assert.Equal("IMPLEMENTED", (await client.CommandOrFailAsync(sessions.Officer, contractual, "mark-implemented")).Text("status"));
    }

    /// <summary>
    /// Fails closed (WF-08 BR-CHG-010, BR-CHG-031): a profile with no bands in the version in force stops the review with 422
    /// CONFIGURATION_MISSING and leaves the request SUBMITTED, unevaluated and unrouted; a schedule impact without an ACTIVE Approved
    /// Baseline, or a cost impact without an ACTIVE Approved Budget, is not submitted; nor is a request lacking what its type needs; and a
    /// zero impact is refused as no impact at all.
    /// </summary>
    [Fact]
    public async Task MaterialityFailsClosed()
    {
        using HttpClient client = host.Api.CreateClient();
        ChangeSessions sessions = await client.SignInAsync();

        Guid full = await host.ProjectAsync("FULL");
        await host.BaselinedAsync(client, sessions, full);
        Guid unbanded = await client.RaiseAsync(sessions.EntityManager, ChangeRequestDriver.RequestBody(full, "SCHEDULE", scheduleImpactDays: 3));
        await client.CommandOrFailAsync(sessions.EntityManager, unbanded, "submit");
        using (HttpResponseMessage review = await client.CommandAsync(sessions.Officer, unbanded, "start-review"))
        {
            Assert.Equal((HttpStatusCode.UnprocessableEntity, "CONFIGURATION_MISSING"), await review.RefusalAsync());
        }

        Assert.Equal("SUBMITTED", (await client.RequestAsync(sessions.Officer, unbanded)).Text("status"));
        Assert.Equal(["0|0"], await host.Database.QueryAsync($"""
            SELECT (SELECT count(*) FROM change_request.materiality_evaluation WHERE change_request_id = '{unbanded}')::text || '|'
                   || (SELECT count(*) FROM approval.approval_instance WHERE subject_id = '{unbanded}')::text
            """));

        Guid unplanned = await host.ProjectAsync("LIGHT");
        foreach ((object body, string field) in new[]
                 {
                     (ChangeRequestDriver.RequestBody(unplanned, "SCHEDULE", scheduleImpactDays: 3), "scheduleImpactDays"),
                     (ChangeRequestDriver.RequestBody(unplanned, "COST", costImpactSar: "1000.00"), "costImpactSar"),
                 })
        {
            Guid requestId = await client.RaiseAsync(sessions.EntityManager, body);
            using HttpResponseMessage submitted = await client.CommandAsync(sessions.EntityManager, requestId, "submit");
            JsonObject problem = await submitted.ReadObjectAsync();
            Assert.Equal((HttpStatusCode.UnprocessableEntity, "CHANGE_REQUEST_TARGET_UNAVAILABLE", field),
                (submitted.StatusCode, problem.Text("code"), problem["errors"]![0]!.Text("field")));
        }

        // A stated impact is never zero: no impact is left out, so a zero never issues an authorisation.
        using (HttpResponseMessage zero = await client.PostAsync(ChangeRequestDriver.Requests, sessions.EntityManager,
                   ChangeRequestDriver.RequestBody(unplanned, "SCHEDULE", costImpactSar: "0.00", scheduleImpactDays: 0)))
        {
            Assert.Equal(HttpStatusCode.BadRequest, zero.StatusCode);
            Assert.Equal(["costImpactSar:OUT_OF_RANGE", "scheduleImpactDays:OUT_OF_RANGE"],
                (await zero.ReadObjectAsync())["errors"]!.AsArray().Select(e => $"{e!.Text("field")}:{e!.Text("code")}").Order(StringComparer.Ordinal));
        }

        Guid incomplete = await client.RaiseAsync(sessions.EntityManager, ChangeRequestDriver.RequestBody(unplanned, "SCHEDULE"));
        using (HttpResponseMessage submitted = await client.CommandAsync(sessions.EntityManager, incomplete, "submit"))
        {
            Assert.Equal((HttpStatusCode.UnprocessableEntity, "CHANGE_REQUEST_INCOMPLETE"), await submitted.RefusalAsync());
        }

        Guid ownProfile = Guid.Parse(Assert.Single(await host.Database.QueryAsync($"SELECT governance_profile_item_id::text FROM project.project WHERE id = '{unplanned}'")));
        using HttpResponseMessage sameProfile = await client.PostAsync(ChangeRequestDriver.Requests, sessions.EntityManager,
            ChangeRequestDriver.RequestBody(unplanned, "GOVERNANCE_PROFILE", profileItemId: ownProfile));
        Assert.Equal((HttpStatusCode.UnprocessableEntity, "CHANGE_REQUEST_PROFILE_INVALID"), await sameProfile.RefusalAsync());
    }
}
