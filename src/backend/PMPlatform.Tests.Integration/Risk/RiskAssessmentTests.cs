using System.Net;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using PMPlatform.Application.Features.MasterDataConfig.Contracts.Resolution;
using PMPlatform.Tests.Integration.Identity;
using PMPlatform.Tests.Integration.MasterDataConfig;

namespace PMPlatform.Tests.Integration.Risk;

/// <summary>
/// A risk's assessments through the API: rated by the RISK_MATRIX version in force when made, pinned to it, never rewritten by a
/// later publication (the acceptance criterion), fitted to the version's dimensions (ADR-011), and AHDA's authority (ADR-013).
/// </summary>
[Collection(RiskSuite.Name)]
public sealed class RiskAssessmentTests(RiskTestHost host)
{
    /// <summary>
    /// TASK-055's acceptance criterion and validation check: re-publish the probability/impact matrix and the risk assessed under the
    /// earlier version keeps the rating it recorded — on the risk, in its history and in the database — while a new assessment of the
    /// same figures takes the new version's rating and pins the new version.
    /// </summary>
    [Fact]
    public async Task ARatingIsPinnedToTheMatrixInForceAndARepublicationNeverChangesIt()
    {
        using HttpClient client = host.Api.CreateClient();
        RiskSessions sessions = await client.SignInAsync();
        Guid[] dimensions = await host.DimensionsAsync();
        Guid riskId = await client.RiskAsync(sessions.EntityManager, await host.ProjectAsync());

        // Probability 3, the highest dimension at 4: cell 3×4.
        ResolvedConfiguration inForce = await MatrixInForceAsync();
        string before = inForce.RequireRiskRating(3, 4).Code;
        JsonObject assessed = await client.CommandOrFailAsync(sessions.Officer, riskId, "assess", RiskDriver.Assessment(dimensions, probability: 3, impact: 2, peak: 4));
        JsonObject first = assessed["currentAssessment"]!.AsObject();
        Assert.Equal(("ASSESSED", 1, 4, before, inForce.VersionId.ToString()),
            (assessed.Text("status"), first["versionNo"]!.GetValue<int>(), first["overallImpactLevel"]!.GetValue<int>(), first["rating"]!.Text("code"),
             first.Text("matrixConfigurationVersionId")));

        // FG-04 publishes a version that rates the same cell the other way.
        Guid republished = await host.PublishMatrixAsync(client, await client.SignInCrewAsync(), highFrom: before == "HIGH" ? 13 : 12);
        string after = before == "HIGH" ? "LOW" : "HIGH";
        Assert.Equal((republished, after), ((await MatrixInForceAsync()).VersionId, (await MatrixInForceAsync()).RequireRiskRating(3, 4).Code));

        JsonObject reread = await client.GetOrFailAsync(sessions.EntityManager, $"{RiskDriver.Risks}/{riskId}");
        Assert.Equal((before, inForce.VersionId.ToString()), (reread["currentAssessment"]!["rating"]!.Text("code"), reread["currentAssessment"]!.Text("matrixConfigurationVersionId")));
        JsonObject history = await client.GetOrFailAsync(sessions.EntityManager, $"{RiskDriver.Assessments}/{first.Text("id")}");
        Assert.Equal((before, 4), (history["rating"]!.Text("code"), history["impacts"]!.AsArray().Count));
        Assert.Equal(
            [$"1 {inForce.VersionId} {before}"],
            await host.Database.QueryAsync($"""
                SELECT v.version_no || ' ' || v.matrix_configuration_version_id || ' ' || d.code
                FROM risk.risk_assessment_version v JOIN master_data_config.risk_rating_definition d ON d.id = v.risk_rating_definition_id
                WHERE v.risk_id = '{riskId}'
                """));

        // A reassessment of the same figures is rated by the version in force now, and pins it; the first keeps its own.
        JsonObject reassessed = await client.CommandOrFailAsync(sessions.Officer, riskId, "assess", RiskDriver.Assessment(dimensions, probability: 3, impact: 2, peak: 4));
        Assert.Equal((2, after, republished.ToString()),
            (reassessed["currentAssessment"]!["versionNo"]!.GetValue<int>(), reassessed["currentAssessment"]!["rating"]!.Text("code"),
             reassessed["currentAssessment"]!.Text("matrixConfigurationVersionId")));
        JsonArray versions = (await client.GetOrFailAsync(sessions.EntityManager, $"{RiskDriver.Assessments}?riskId={riskId}"))["items"]!.AsArray();
        Assert.Equal([$"2 {after}", $"1 {before}"], versions.Select(v => $"{v!["versionNo"]} {v["rating"]!.Text("code")}"));
        Assert.Contains("Risk.RiskAssessed", await host.AuditEventsAsync(riskId));
    }

    /// <summary>ADR-011: an assessment gives one level for each dimension of the matrix in force, at a level it defines, and nothing more.</summary>
    [Fact]
    public async Task AnAssessmentFitsTheDimensionsOfTheMatrixInForce()
    {
        using HttpClient client = host.Api.CreateClient();
        RiskSessions sessions = await client.SignInAsync();
        Guid[] dimensions = await host.DimensionsAsync();
        Guid riskId = await client.RiskAsync(sessions.EntityManager, await host.ProjectAsync());

        using (HttpResponseMessage missing = await client.CommandAsync(sessions.Officer, riskId, "assess", RiskDriver.Assessment(dimensions[..3], probability: 2, impact: 2)))
        {
            Assert.Equal((HttpStatusCode.UnprocessableEntity, "RISK_IMPACT_INVALID"), await missing.RefusalAsync());
            Assert.Equal(["impacts REQUIRED"], await missing.ReadFieldErrorsAsync());
        }

        using (HttpResponseMessage unknown = await client.CommandAsync(sessions.Officer, riskId, "assess", RiskDriver.Assessment([.. dimensions, RiskTestHost.CategoryId], probability: 2, impact: 2)))
        {
            Assert.Equal(["impacts[4].impactDimensionItemId NOT_ALLOWED"], await unknown.ReadFieldErrorsAsync());
        }

        using (HttpResponseMessage outOfScale = await client.CommandAsync(sessions.Officer, riskId, "assess", RiskDriver.Assessment(dimensions, probability: 6, impact: 2)))
        {
            Assert.Equal((HttpStatusCode.BadRequest, "VALIDATION_FAILED"), await outOfScale.RefusalAsync());
        }

        Assert.Empty(await host.Database.QueryAsync($"SELECT id::text FROM risk.risk_assessment_version WHERE risk_id = '{riskId}'"));
        Assert.Equal("IDENTIFIED", (await client.GetOrFailAsync(sessions.EntityManager, $"{RiskDriver.Risks}/{riskId}")).Text("status"));
    }

    /// <summary>
    /// ADR-013's amendment: rating authority is unchanged. The entity Project Manager holds RISK_ASSESS on their project here and is
    /// still refused, audited as an external user; the internal Project Manager with the same grant rates their own project's risk.
    /// </summary>
    [Fact]
    public async Task RatingAndAcceptanceStayWithAhdaWhateverAnEntityUserHolds()
    {
        using HttpClient client = host.Api.CreateClient();
        RiskSessions sessions = await client.SignInAsync();
        Guid[] dimensions = await host.DimensionsAsync();
        Guid entityRisk = await client.RiskAsync(sessions.EntityManager, await host.ProjectAsync(projectManager: 8));
        Guid internalRisk = await client.RiskAsync(sessions.InternalManager, await host.ProjectAsync(projectManager: 5));

        using (HttpResponseMessage refused = await client.CommandAsync(sessions.EntityManager, entityRisk, "assess", RiskDriver.Assessment(dimensions, 2, 2)))
        {
            Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
        }

        await client.CommandOrFailAsync(sessions.Officer, entityRisk, "assess", RiskDriver.Assessment(dimensions, 2, 2));
        using (HttpResponseMessage refused = await client.CommandAsync(sessions.EntityManager, entityRisk, "accept",
                   new { expiresOn = RiskDriver.Iso(RiskDriver.Today.AddDays(30)), rationale = new { text = "Tolerable.", language = "en" } }))
        {
            Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
        }

        Assert.Equal(
            ["RISK_ASSESS EXTERNAL_USER", "RISK_ACCEPT EXTERNAL_USER"],
            await host.Database.QueryAsync($"""
                SELECT p.new_value || ' ' || r.new_value FROM audit_activity.audit_event e
                JOIN audit_activity.audit_event_attribute p ON p.audit_event_id = e.id AND p.attribute_name = 'permission'
                JOIN audit_activity.audit_event_attribute r ON r.audit_event_id = e.id AND r.attribute_name = 'reason'
                WHERE e.event_type = 'Risk.AuthorityRefused' AND e.subject_id = '{entityRisk}' AND e.actor_user_id = '{RiskDriver.Person(8)}'
                ORDER BY e.occurred_at
                """));

        JsonObject rated = await client.CommandOrFailAsync(sessions.InternalManager, internalRisk, "assess", RiskDriver.Assessment(dimensions, 2, 2));
        Assert.Equal("ASSESSED", rated.Text("status"));
    }

    private async Task<ResolvedConfiguration> MatrixInForceAsync()
    {
        await using AsyncServiceScope scope = host.Api.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IConfigurationResolver>().ResolveAsync("RISK_MATRIX", host.Clock.GetUtcNow(), CancellationToken.None);
    }
}
