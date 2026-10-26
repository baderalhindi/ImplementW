using System.Net;
using System.Text.Json.Nodes;
using PMPlatform.Application.Features.FinancialKpi.Contracts;
using PMPlatform.Tests.Integration.Identity;

namespace PMPlatform.Tests.Integration.FinancialKpi;

/// <summary>
/// KPI Performance through the real API, WF-11 and the outbox (TASK-052). Acceptance criterion 1: a target-version change never
/// rewrites an earlier measurement's recorded target reference — each measurement stays pinned to the version effective when it
/// was recorded, and so does its rating.
/// </summary>
[Collection(FinancialKpiSuite.Name)]
public sealed class KpiPerformanceTests(FinancialKpiTestHost host)
{
    [Fact]
    public async Task ATargetVersionChangeNeverRewritesAnEarlierMeasurement()
    {
        using HttpClient client = host.Api.CreateClient();
        Sessions sessions = await client.SignInAsync();
        Guid assignmentId = await client.AssignmentAsync(sessions, await host.ProjectAsync(), FinancialKpiTestHost.SafetyKpiId);
        DateOnly january = FinancialKpiDriver.Today.AddDays(-90);

        // Version 1: 95% is GREEN, 90% AMBER. A published measurement and a DRAFT one are recorded against it.
        Guid v1 = await host.ActiveTargetAsync(client, sessions, assignmentId, 95m, 95m, 90m);
        JsonObject published = await client.PublishedMeasurementAsync(sessions, assignmentId, january, 92m);
        Guid publishedId = AdministrationApi.IdOf(published);
        Assert.Equal((v1.ToString(), 1, "AMBER", "PUBLISHED"), (published.Text("kpiTargetVersionId"), published["targetVersionNo"]!.GetValue<int>(), published.Text("ragStatus"), published.Text("status")));
        Guid draftId = AdministrationApi.IdOf(await client.CreatedOrFailAsync(sessions.ProjectManager, FinancialKpiDriver.Measurements,
            FinancialKpiDriver.Measurement(assignmentId, january.AddDays(30), 92m)));
        string publishedRow = await host.RowAsync("kpi_measurement", publishedId);

        // Version 2 lowers the bar: 80% GREEN. It supersedes version 1, which is kept as approved.
        string v1Row = await host.RowAsync("kpi_target_version", v1, "status", "superseded_by_target_version_id", "updated_at", "updated_by");
        Guid v2 = await host.ActiveTargetAsync(client, sessions, assignmentId, 80m, 80m, 70m);
        JsonObject superseded = await client.GetOrFailAsync(sessions.ProjectManager, $"{FinancialKpiDriver.Targets}/{v1}");
        Assert.Equal(("SUPERSEDED", false, v2.ToString()), (superseded.Text("status"), superseded["isCurrent"]!.GetValue<bool>(), superseded.Text("supersededByTargetVersionId")));
        Assert.Equal(v1Row, await host.RowAsync("kpi_target_version", v1, "status", "superseded_by_target_version_id", "updated_at", "updated_by"));

        // The earlier measurements keep version 1 and what they were rated against it; nothing about them changed.
        Assert.Equal(publishedRow, await host.RowAsync("kpi_measurement", publishedId));
        JsonObject earlier = await client.GetOrFailAsync(sessions.ProjectManager, $"{FinancialKpiDriver.Measurements}/{publishedId}");
        Assert.Equal((v1.ToString(), 1, 95m, "AMBER"), (earlier.Text("kpiTargetVersionId"), earlier["targetVersionNo"]!.GetValue<int>(), earlier["targetValue"]!.GetValue<decimal>(), earlier.Text("ragStatus")));

        // Editing the DRAFT recorded under version 1 re-rates it against version 1 still: 92 stays AMBER there, though GREEN under version 2.
        JsonObject edited = await client.PutOrFailAsync(sessions.ProjectManager, $"{FinancialKpiDriver.Measurements}/{draftId}",
            new { measuredValue = 92.5m, valueStatus = "MEASURED", asOfDate = FinancialKpiDriver.Iso(FinancialKpiDriver.Today) });
        Assert.Equal((v1.ToString(), "AMBER"), (edited.Text("kpiTargetVersionId"), edited.Text("ragStatus")));

        // A measurement recorded now pins version 2.
        JsonObject later = await client.PublishedMeasurementAsync(sessions, assignmentId, january.AddDays(60), 92m);
        Assert.Equal((v2.ToString(), 2, "GREEN"), (later.Text("kpiTargetVersionId"), later["targetVersionNo"]!.GetValue<int>(), later.Text("ragStatus")));

        // For any writer: the pin is never rewritten, an approved target is never edited.
        Assert.NotNull(await host.RefusedAsync($"UPDATE financial_kpi.kpi_measurement SET kpi_target_version_id = '{v2}' WHERE id = '{publishedId}'"));
        Assert.NotNull(await host.RefusedAsync($"UPDATE financial_kpi.kpi_measurement SET kpi_target_version_id = '{v2}' WHERE id = '{draftId}'"));
        Assert.NotNull(await host.RefusedAsync($"UPDATE financial_kpi.kpi_target_version SET target_value = 50 WHERE id = '{v1}'"));
        Assert.NotNull(await host.RefusedAsync($"UPDATE financial_kpi.kpi_target_version SET target_value = 50 WHERE id = '{v2}'"));
        Assert.Equal(publishedRow, await host.RowAsync("kpi_measurement", publishedId));

        Assert.Equal(["FinancialKpi.VersionCreated", "FinancialKpi.VersionSubmitted", "FinancialKpi.VersionActivated", "FinancialKpi.VersionSuperseded"],
            await host.AuditEventsAsync(v1));
    }

    /// <summary>A measurement needs an approved target to be pinned to, and the assignment ACTIVE; one per period.</summary>
    [Fact]
    public async Task AMeasurementIsPinnedOnlyToAnApprovedTarget()
    {
        using HttpClient client = host.Api.CreateClient();
        Sessions sessions = await client.SignInAsync();
        Guid assignmentId = await client.AssignmentAsync(sessions, await host.ProjectAsync(), FinancialKpiTestHost.ReworkKpiId);
        DateOnly period = FinancialKpiDriver.Today.AddDays(-40);

        using (HttpResponseMessage unpinned = await client.PostAsync(FinancialKpiDriver.Measurements, sessions.ProjectManager, FinancialKpiDriver.Measurement(assignmentId, period, 3m)))
        {
            Assert.Equal((HttpStatusCode.UnprocessableEntity, FinancialKpiErrorCodes.TargetNotApproved), await unpinned.RefusalAsync());
        }

        // A target only DRAFT or SUBMITTED pins nothing either.
        Guid draft = AdministrationApi.IdOf(await client.CreatedOrFailAsync(sessions.ProjectManager, FinancialKpiDriver.Targets,
            new { kpiAssignmentId = assignmentId, targetValue = 2m, greenThreshold = 2m, amberThreshold = 5m }));
        await client.CommandOrFailAsync(sessions.ProjectManager, $"{FinancialKpiDriver.Targets}/{draft}/submit");
        using (HttpResponseMessage pending = await client.PostAsync(FinancialKpiDriver.Measurements, sessions.ProjectManager, FinancialKpiDriver.Measurement(assignmentId, period, 3m)))
        {
            Assert.Equal((HttpStatusCode.UnprocessableEntity, FinancialKpiErrorCodes.TargetNotApproved), await pending.RefusalAsync());
        }

        await host.DecideAndDeliverAsync(FinancialKpiApprovalRouting.TargetVersionType, draft, Application.Features.Approval.Contracts.ApprovalTaskDecision.Approve);
        JsonObject measurement = await client.CreatedOrFailAsync(sessions.ProjectManager, FinancialKpiDriver.Measurements, FinancialKpiDriver.Measurement(assignmentId, period, 3m));
        Assert.Equal("AMBER", measurement.Text("ragStatus"));
        using (HttpResponseMessage twice = await client.PostAsync(FinancialKpiDriver.Measurements, sessions.ProjectManager, FinancialKpiDriver.Measurement(assignmentId, period, 1m)))
        {
            Assert.Equal((HttpStatusCode.Conflict, FinancialKpiErrorCodes.MeasurementExists), await twice.RefusalAsync());
        }

        // Thresholds that do not order as the KPI's direction requires are refused: lower is better, so GREEN may not exceed AMBER.
        using (HttpResponseMessage incoherent = await client.PostAsync(FinancialKpiDriver.Targets, sessions.ProjectManager,
                   new { kpiAssignmentId = assignmentId, targetValue = 2m, greenThreshold = 6m, amberThreshold = 5m }))
        {
            Assert.Equal((HttpStatusCode.UnprocessableEntity, FinancialKpiErrorCodes.ThresholdsInvalid), await incoherent.RefusalAsync());
        }

        // A suspended assignment takes no measurement.
        await client.CommandOrFailAsync(sessions.ProjectManager, $"{FinancialKpiDriver.Assignments}/{assignmentId}/suspend");
        using HttpResponseMessage suspended = await client.PostAsync(FinancialKpiDriver.Measurements, sessions.ProjectManager, FinancialKpiDriver.Measurement(assignmentId, period.AddDays(30), 1m));
        Assert.Equal((HttpStatusCode.UnprocessableEntity, FinancialKpiErrorCodes.AssignmentNotActive), await suspended.RefusalAsync());
    }

    /// <summary>
    /// ADR-013: a published KPI value is AHDA's to publish. The entity Project Manager who recorded it is refused — as an external
    /// user — even holding KPI_REVIEW, and the refusal is audited; only a PUBLISHED catalogue KPI is assigned, once per project.
    /// </summary>
    [Fact]
    public async Task AMeasurementIsPublishedByAhdaNeverByWhoRecordedIt()
    {
        using HttpClient client = host.Api.CreateClient();
        Sessions sessions = await client.SignInAsync();
        Guid projectId = await host.ProjectAsync();
        Guid assignmentId = await client.AssignmentAsync(sessions, projectId, FinancialKpiTestHost.DelayKpiId);
        await host.ActiveTargetAsync(client, sessions, assignmentId, 0m, 5m, 15m);
        Guid id = AdministrationApi.IdOf(await client.CreatedOrFailAsync(sessions.ProjectManager, FinancialKpiDriver.Measurements,
            FinancialKpiDriver.Measurement(assignmentId, FinancialKpiDriver.Today.AddDays(-30), 12m)));
        await client.CommandOrFailAsync(sessions.ProjectManager, $"{FinancialKpiDriver.Measurements}/{id}/submit");

        using (HttpResponseMessage own = await client.PostAsync($"{FinancialKpiDriver.Measurements}/{id}/publish", sessions.ProjectManager))
        {
            Assert.Equal(HttpStatusCode.Forbidden, own.StatusCode);
        }

        Assert.Equal("SUBMITTED", (await client.GetOrFailAsync(sessions.Portfolio, $"{FinancialKpiDriver.Measurements}/{id}")).Text("status"));
        Assert.Contains("FinancialKpi.ReviewRefused", await host.AuditEventsAsync(id));
        Assert.Equal("PUBLISHED", (await client.CommandOrFailAsync(sessions.Portfolio, $"{FinancialKpiDriver.Measurements}/{id}/publish")).Text("status"));

        using (HttpResponseMessage again = await client.PostAsync(FinancialKpiDriver.Assignments, sessions.ProjectManager,
                   new { projectId, kpiDefinitionId = FinancialKpiTestHost.DelayKpiId, measurementFrequencyItemId = FinancialKpiTestHost.MonthlyId }))
        {
            Assert.Equal((HttpStatusCode.Conflict, FinancialKpiErrorCodes.KpiAlreadyAssigned), await again.RefusalAsync());
        }

        using HttpResponseMessage draft = await client.PostAsync(FinancialKpiDriver.Assignments, sessions.ProjectManager,
            new { projectId, kpiDefinitionId = FinancialKpiTestHost.DraftKpiId, measurementFrequencyItemId = FinancialKpiTestHost.MonthlyId });
        Assert.Equal((HttpStatusCode.UnprocessableEntity, FinancialKpiErrorCodes.KpiReferenceInvalid), await draft.RefusalAsync());
    }
}
