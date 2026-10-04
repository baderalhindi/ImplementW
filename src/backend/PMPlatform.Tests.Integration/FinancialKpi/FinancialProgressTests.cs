using System.Net;
using System.Text.Json.Nodes;
using PMPlatform.Application.Features.Approval.Contracts;
using PMPlatform.Application.Features.FinancialKpi.Contracts;
using PMPlatform.Tests.Integration.Identity;

namespace PMPlatform.Tests.Integration.FinancialKpi;

/// <summary>
/// Financial Progress through the real API, WF-11 and the outbox (TASK-052): Approved Budget versions under the ADR-008 gate,
/// periodic figures published by AHDA into immutable snapshots distinct from the live position, and source modes per field.
/// </summary>
[Collection(FinancialKpiSuite.Name)]
public sealed class FinancialProgressTests(FinancialKpiTestHost host)
{
    /// <summary>
    /// ADR-008: any change to the Approved Budget requires a referenced document. Approval makes the version ACTIVE — the budget of
    /// record — and supersedes the previous one, which keeps every figure it had. Provenance is recorded on each version.
    /// </summary>
    [Fact]
    public async Task AnApprovedBudgetChangeNeedsAReferencedDocumentAndSupersedesThePreviousVersion()
    {
        using HttpClient client = host.Api.CreateClient();
        Sessions sessions = await client.SignInAsync();
        Guid projectId = await host.ProjectAsync();
        Guid first = await host.ActiveBudgetAsync(client, sessions, projectId, "1000000.00");
        string firstRow = await host.RowAsync("financial_commitment", first, "status", "superseded_by_commitment_id", "updated_at", "updated_by", "xmin");

        JsonObject change = await client.CreatedOrFailAsync(sessions.ProjectManager, FinancialKpiDriver.Commitments, FinancialKpiDriver.Budget(projectId, "1200000.00"));
        Guid second = AdministrationApi.IdOf(change);
        Assert.Equal((2, "DRAFT", "MANUAL", "Board minute 12/2026", FinancialKpiDriver.Person(8).ToString()),
            (change["versionNo"]!.GetValue<int>(), change.Text("status"), change.Text("sourceType"), change.Text("sourceReference"), change.Text("enteredByUserId")));

        // No document, no submission; one version on its way at a time.
        using (HttpResponseMessage bare = await client.PostAsync($"{FinancialKpiDriver.Commitments}/{second}/submit", sessions.ProjectManager))
        {
            Assert.Equal((HttpStatusCode.UnprocessableEntity, FinancialKpiErrorCodes.BudgetDocumentRequired), await bare.RefusalAsync());
        }

        using (HttpResponseMessage third = await client.PostAsync(FinancialKpiDriver.Commitments, sessions.ProjectManager, FinancialKpiDriver.Budget(projectId, "1.00")))
        {
            Assert.Equal((HttpStatusCode.Conflict, FinancialKpiErrorCodes.CommitmentOpen), await third.RefusalAsync());
        }

        // Returned by WF-11, it is corrected and resubmitted as revision 2 under a new run; the ACTIVE version stays the budget of record.
        await host.AttachDocumentAsync(client, sessions.ProjectManager, projectId, second);
        await client.CommandOrFailAsync(sessions.ProjectManager, $"{FinancialKpiDriver.Commitments}/{second}/submit");
        await host.DecideAndDeliverAsync(FinancialKpiApprovalRouting.CommitmentType, second, ApprovalTaskDecision.Return, "State the variation order");
        Assert.Equal("RETURNED", (await client.GetOrFailAsync(sessions.ProjectManager, $"{FinancialKpiDriver.Commitments}/{second}")).Text("status"));
        Assert.True((await client.GetOrFailAsync(sessions.ProjectManager, $"{FinancialKpiDriver.Commitments}/{first}"))["isCurrent"]!.GetValue<bool>());
        await client.PutOrFailAsync(sessions.ProjectManager, $"{FinancialKpiDriver.Commitments}/{second}",
            new { amountSar = "1150000.00", sourceReference = "Variation order 7", asOfDate = FinancialKpiDriver.Iso(FinancialKpiDriver.Today) });
        JsonObject resubmitted = await client.CommandOrFailAsync(sessions.ProjectManager, $"{FinancialKpiDriver.Commitments}/{second}/submit");
        Assert.Equal(("SUBMITTED", 2), (resubmitted.Text("status"), resubmitted["revisionNo"]!.GetValue<int>()));
        await host.DecideAndDeliverAsync(FinancialKpiApprovalRouting.CommitmentType, second, ApprovalTaskDecision.Approve);

        JsonObject active = await client.GetOrFailAsync(sessions.ProjectManager, $"{FinancialKpiDriver.Commitments}/{second}");
        JsonObject superseded = await client.GetOrFailAsync(sessions.ProjectManager, $"{FinancialKpiDriver.Commitments}/{first}");
        Assert.Equal(("ACTIVE", true, "1150000.00"), (active.Text("status"), active["isCurrent"]!.GetValue<bool>(), active.Text("amountSar")));
        Assert.Equal(("SUPERSEDED", false, second.ToString(), "1000000.00"),
            (superseded.Text("status"), superseded["isCurrent"]!.GetValue<bool>(), superseded.Text("supersededByCommitmentId"), superseded.Text("amountSar")));
        Assert.Equal(firstRow, await host.RowAsync("financial_commitment", first, "status", "superseded_by_commitment_id", "updated_at", "updated_by", "xmin"));

        // The budget of record is never edited, for any writer.
        Assert.NotNull(await host.RefusedAsync($"UPDATE financial_kpi.financial_commitment SET amount_sar = 1 WHERE id = '{second}'"));
        Assert.Equal(["FinancialKpi.VersionCreated", "FinancialKpi.DocumentAttached", "FinancialKpi.VersionSubmitted", "FinancialKpi.VersionReturned",
                      "FinancialKpi.VersionChanged", "FinancialKpi.VersionSubmitted", "FinancialKpi.VersionActivated"], await host.AuditEventsAsync(second));
    }

    /// <summary>
    /// Publishing a period never alters an earlier Published Financial Snapshot, even when the budget and every figure change after
    /// it; the live position moves, the published one does not. Publication is AHDA's: the submitter and an external user are refused.
    /// </summary>
    [Fact]
    public async Task PublishingAPeriodNeverAltersAnEarlierSnapshotAndTheLiveViewStaysApart()
    {
        using HttpClient client = host.Api.CreateClient();
        Sessions sessions = await client.SignInAsync();
        Guid projectId = await host.ProjectAsync();
        await host.PeriodsAsync(projectId, 2);
        await host.ActiveBudgetAsync(client, sessions, projectId, "1000000.00");

        // Period 1: the entity Project Manager submits; they may not review their own figures, nor may any external user.
        Guid firstUpdate = await client.SubmittedUpdateAsync(sessions, projectId, FinancialKpiDriver.Update("300000.00", "1080000.00", "MEASURED", "Invoice register 03"));
        using (HttpResponseMessage own = await client.PostAsync($"{FinancialKpiDriver.Updates}/{firstUpdate}/start-review", sessions.ProjectManager))
        {
            Assert.Equal(HttpStatusCode.Forbidden, own.StatusCode);
        }

        Assert.Contains("FinancialKpi.ReviewRefused", await host.AuditEventsAsync(firstUpdate));
        await client.CommandOrFailAsync(sessions.Portfolio, $"{FinancialKpiDriver.Updates}/{firstUpdate}/start-review");
        await client.CommandOrFailAsync(sessions.Portfolio, $"{FinancialKpiDriver.Updates}/{firstUpdate}/publish");

        JsonObject published = Assert.Single(await client.ItemsAsync(sessions.Portfolio, FinancialKpiDriver.Snapshots, $"projectId={projectId}"))!.AsObject();
        Guid snapshotId = AdministrationApi.IdOf(published);
        Assert.Equal(("1000000.00", "300000.00", "1080000.00", "AMBER", "Invoice register 03"),
            (published.Text("approvedBudgetSar"), published.Text("actualExpenditureToDateSar"), published.Text("forecastAtCompletionSar"), published.Text("financialStatus"), published.Text("sourceReference")));
        string snapshotRow = await host.RowAsync("published_financial_snapshot", snapshotId);

        // Afterwards everything moves: a new budget, a second period with new figures, both published.
        await host.ActiveBudgetAsync(client, sessions, projectId, "1200000.00");
        Guid secondUpdate = await client.PublishedUpdateAsync(sessions, projectId, FinancialKpiDriver.Update("700000.00", "1150000.00", "MEASURED"));

        Assert.Equal(snapshotRow, await host.RowAsync("published_financial_snapshot", snapshotId));
        JsonArray snapshots = await client.ItemsAsync(sessions.Portfolio, FinancialKpiDriver.Snapshots, $"projectId={projectId}");
        Assert.Equal([("1200000.00", "GREEN"), ("1000000.00", "AMBER")], snapshots.Select(s => (s!.Text("approvedBudgetSar"), s!.Text("financialStatus"))));

        // The live position is the newest figures under the budget in force; the published history is not merged into it.
        JsonObject live = Assert.Single(await client.ItemsAsync(sessions.Portfolio, FinancialKpiDriver.Positions, $"projectId={projectId}"))!.AsObject();
        Assert.Equal(("CURRENT_LIVE", secondUpdate.ToString(), "1200000.00", "700000.00"),
            (live.Text("semanticState"), live.Text("financialProgressUpdateId"), live.Text("approvedBudgetSar"), live.Text("actualExpenditureToDateSar")));

        // Every begun period is published: nothing more to report.
        using (HttpResponseMessage nothing = await client.PostAsync(FinancialKpiDriver.Updates, sessions.ProjectManager, new { projectId }))
        {
            Assert.Equal((HttpStatusCode.Conflict, FinancialKpiErrorCodes.NothingToReport), await nothing.RefusalAsync());
        }

        // For any writer: a snapshot is never updated or deleted, and a published update never changes.
        Assert.NotNull(await host.RefusedAsync($"UPDATE financial_kpi.published_financial_snapshot SET actual_expenditure_to_date_sar = 1 WHERE id = '{snapshotId}'"));
        Assert.NotNull(await host.RefusedAsync($"DELETE FROM financial_kpi.published_financial_snapshot WHERE id = '{snapshotId}'"));
        Assert.NotNull(await host.RefusedAsync($"UPDATE financial_kpi.financial_progress_update SET forecast_at_completion_sar = 1 WHERE id = '{firstUpdate}'"));
        Assert.NotNull(await host.RefusedAsync("TRUNCATE financial_kpi.published_financial_snapshot CASCADE"));
        Assert.Equal(snapshotRow, await host.RowAsync("published_financial_snapshot", snapshotId));
    }

    /// <summary>
    /// ADR-013, the person check on its own: an internal reviewer who submitted the figures may not review them — no one publishes
    /// their own — and the refusal is audited with the reason SUBMITTER.
    /// </summary>
    [Fact]
    public async Task NoOneReviewsFiguresTheySubmitted()
    {
        using HttpClient client = host.Api.CreateClient();
        Sessions sessions = await client.SignInAsync();
        Guid projectId = await host.ProjectAsync();
        await host.PeriodsAsync(projectId, 1);
        Guid id = AdministrationApi.IdOf(await client.CreatedOrFailAsync(sessions.Portfolio, FinancialKpiDriver.Updates, new { projectId }));
        await client.PutOrFailAsync(sessions.Portfolio, $"{FinancialKpiDriver.Updates}/{id}", FinancialKpiDriver.Update("2500.00", null, "MEASURED"));
        await client.CommandOrFailAsync(sessions.Portfolio, $"{FinancialKpiDriver.Updates}/{id}/submit");

        using HttpResponseMessage own = await client.PostAsync($"{FinancialKpiDriver.Updates}/{id}/start-review", sessions.Portfolio);
        Assert.Equal(HttpStatusCode.Forbidden, own.StatusCode);
        Assert.Equal("SUBMITTED", (await client.GetOrFailAsync(sessions.Portfolio, $"{FinancialKpiDriver.Updates}/{id}")).Text("status"));
        Assert.Equal(["SUBMITTER"], await host.Database.QueryAsync(
            $"SELECT a.new_value FROM audit_activity.audit_event e JOIN audit_activity.audit_event_attribute a ON a.audit_event_id = e.id WHERE e.subject_id = '{id}' AND e.event_type = 'FinancialKpi.ReviewRefused' AND a.attribute_name = 'reason'"));
    }

    /// <summary>A returned update keeps the reason, and the period continues as the next revision with the figures to correct.</summary>
    [Fact]
    public async Task AReturnedUpdateContinuesAsTheNextRevision()
    {
        using HttpClient client = host.Api.CreateClient();
        Sessions sessions = await client.SignInAsync();
        Guid projectId = await host.ProjectAsync();
        await host.PeriodsAsync(projectId, 1);
        Guid id = await client.SubmittedUpdateAsync(sessions, projectId, FinancialKpiDriver.Update("10000.00", null, "MEASURED"));
        await client.CommandOrFailAsync(sessions.Portfolio, $"{FinancialKpiDriver.Updates}/{id}/start-review");
        JsonObject returned = await client.CommandOrFailAsync(sessions.Portfolio, $"{FinancialKpiDriver.Updates}/{id}/return",
            new { reason = new { text = "Add the forecast", language = "en" } });

        Assert.Equal(("RETURNED", "Add the forecast"), (returned.Text("status"), returned["returnReason"]!.Text("text")));
        JsonObject next = (await client.ItemsAsync(sessions.ProjectManager, FinancialKpiDriver.Updates, $"projectId={projectId}"))[0]!.AsObject();
        Assert.Equal(("DRAFT", 2, "10000.00"), (next.Text("status"), next["revisionNo"]!.GetValue<int>(), next.Text("actualExpenditureToDateSar")));
    }

    /// <summary>
    /// ADR-008: manual at launch, INTEGRATED and HYBRID built and unconnected, set per project and per field. Once a field is
    /// INTEGRATED, no figure is entered for it by hand — an outage leaves it Unknown — and it never returns to manual entry.
    /// </summary>
    [Fact]
    public async Task AnIntegratedFieldTakesNoManualFigureAndIsNeverSwitchedBack()
    {
        using HttpClient client = host.Api.CreateClient();
        Sessions sessions = await client.SignInAsync();
        Guid projectId = await host.ProjectAsync();
        await host.PeriodsAsync(projectId, 1);

        JsonArray launch = await client.ItemsAsync(sessions.Portfolio, FinancialKpiDriver.SourceModes, $"projectId={projectId}");
        Assert.All(launch, m => Assert.Equal("MANUAL", m!.Text("sourceMode")));

        JsonObject integrated = await client.CreatedOrFailAsync(sessions.Portfolio, FinancialKpiDriver.SourceModes,
            new { projectId, fieldCode = "ACTUAL_EXPENDITURE", sourceMode = "INTEGRATED" });
        await client.CreatedOrFailAsync(sessions.Portfolio, FinancialKpiDriver.SourceModes, new { projectId, fieldCode = "APPROVED_BUDGET", sourceMode = "INTEGRATED" });
        await client.CreatedOrFailAsync(sessions.Portfolio, FinancialKpiDriver.SourceModes, new { projectId, fieldCode = "FORECAST_AT_COMPLETION", sourceMode = "HYBRID" });

        Guid updateId = AdministrationApi.IdOf(await client.CreatedOrFailAsync(sessions.ProjectManager, FinancialKpiDriver.Updates, new { projectId }));
        using (HttpResponseMessage manual = await client.PutAsync(sessions.ProjectManager, $"{FinancialKpiDriver.Updates}/{updateId}", FinancialKpiDriver.Update("5000.00", null, "MEASURED")))
        {
            Assert.Equal((HttpStatusCode.UnprocessableEntity, FinancialKpiErrorCodes.FieldIntegrated), await manual.RefusalAsync());
        }

        using (HttpResponseMessage budget = await client.PostAsync(FinancialKpiDriver.Commitments, sessions.ProjectManager, FinancialKpiDriver.Budget(projectId, "100.00")))
        {
            Assert.Equal((HttpStatusCode.UnprocessableEntity, FinancialKpiErrorCodes.FieldIntegrated), await budget.RefusalAsync());
        }

        // The source being unconnected, the figure stays Unknown.
        await client.PutOrFailAsync(sessions.ProjectManager, $"{FinancialKpiDriver.Updates}/{updateId}", FinancialKpiDriver.Update(null, null, "MISSING"));

        using (HttpResponseMessage back = await client.PutAsync(sessions.Portfolio, $"{FinancialKpiDriver.SourceModes}/{AdministrationApi.IdOf(integrated)}", new { sourceMode = "MANUAL" }))
        {
            Assert.Equal((HttpStatusCode.Conflict, FinancialKpiErrorCodes.SourceModeLocked), await back.RefusalAsync());
        }

        Assert.Equal("ck_financial_source_mode_integrated", await host.RefusedAsync(
            $"UPDATE financial_kpi.financial_source_mode SET source_mode = 'HYBRID' WHERE id = '{AdministrationApi.IdOf(integrated)}'"));

        using HttpResponseMessage unused = await client.PostAsync(FinancialKpiDriver.SourceModes, sessions.Portfolio, new { projectId, fieldCode = "OPEN_COMMITMENT", sourceMode = "MANUAL" });
        Assert.Equal((HttpStatusCode.UnprocessableEntity, FinancialKpiErrorCodes.NotInUse), await unused.RefusalAsync());
    }

    /// <summary>
    /// ADR-013's amendment: an entity sees budget, expenditure and KPI status for its own project, and nothing of another entity's.
    /// ADR-010: a field its audience may not see is omitted from the representation and named in <c>maskedFields</c> — not sent as
    /// null, which would read as Unknown — and the aggregate leaves the masked figures out.
    /// </summary>
    [Fact]
    public async Task AnEntitySeesItsOwnProjectsFinancialsMaskedByAudience()
    {
        using HttpClient client = host.Api.CreateClient();
        Sessions sessions = await client.SignInAsync();
        string entityUser = (await client.SignInOrFailAsync(8)).AccessToken;
        (Guid own, Guid other) = (await host.ProjectAsync(), await host.ProjectAsync(FinancialKpiTestHost.OtherEntityId));
        await host.PeriodsAsync(own, 1);
        await host.ActiveBudgetAsync(client, sessions, own, "2000000.00");
        await client.PublishedUpdateAsync(sessions, own, FinancialKpiDriver.Update("100000.00", "1900000.00", "MEASURED"));
        Guid assignmentId = await client.AssignmentAsync(sessions, own, FinancialKpiTestHost.SafetyKpiId);

        Assert.Single(await client.ItemsAsync(entityUser, FinancialKpiDriver.Positions, $"projectId={own}"));
        Assert.Single(await client.ItemsAsync(entityUser, FinancialKpiDriver.Assignments, $"projectId={own}"));
        Assert.Empty(await client.ItemsAsync(entityUser, FinancialKpiDriver.Positions, $"projectId={other}"));
        Assert.Empty(await client.ItemsAsync(entityUser, FinancialKpiDriver.Snapshots, $"projectId={other}"));
        Assert.NotNull(assignmentId.ToString());

        const string version = "00000000-0520-4000-8000-0000000001f1";
        try
        {
            await host.Database.ExecuteAsync($"""
                INSERT INTO master_data_config.configuration_version (id, configuration_family_id, version_no, lifecycle_state, created_at, created_by, updated_at, updated_by)
                SELECT '{version}', f.id, 1, 'DRAFT', now(), '{IdentityDatabase.SeedPrincipalId}', now(), '{IdentityDatabase.SeedPrincipalId}'
                FROM master_data_config.configuration_family f WHERE f.code = 'FIELD_CLASSIFICATION';
                INSERT INTO master_data_config.field_classification_rule (id, configuration_version_id, entity_code, field_code, data_classification_item_id, masking_rule, created_at, created_by, updated_at, updated_by)
                VALUES (gen_random_uuid(), '{version}', 'PublishedFinancialSnapshot', 'actualExpenditureToDateSar', '{FinancialKpiTestHost.Restricted}', 'WITHHOLD', now(), '{IdentityDatabase.SeedPrincipalId}', now(), '{IdentityDatabase.SeedPrincipalId}');
                UPDATE master_data_config.configuration_version SET lifecycle_state = 'PUBLISHED', published_at = now() - interval '1 minute', effective_from = now() - interval '1 minute' WHERE id = '{version}';
                UPDATE identity_access.permission SET data_classification_item_id = '{FinancialKpiTestHost.Internal}' WHERE code = 'FINANCIAL_VIEW';
                """);

            JsonObject masked = Assert.Single(await client.ItemsAsync(entityUser, FinancialKpiDriver.Snapshots, $"projectId={own}"))!.AsObject();
            Assert.False(masked.ContainsKey("actualExpenditureToDateSar"));
            Assert.Equal(["actualExpenditureToDateSar"], masked["maskedFields"]!.AsArray().Select(f => f!.GetValue<string>()));
            Assert.Equal("2000000.00", masked.Text("approvedBudgetSar"));

            JsonObject aggregate = await client.GetOrFailAsync(entityUser, $"{FinancialKpiDriver.FinancialAggregates}?projectId={own}");
            Assert.Equal((true, "NONE"), (aggregate["isPartial"]!.GetValue<bool>(), aggregate.Text("coverage")));
            Assert.Equal("MASKED", aggregate["exclusions"]!.AsArray().Single()!.Text("reason"));
        }
        finally
        {
            await host.Database.ExecuteAsync($"""
                UPDATE identity_access.permission SET data_classification_item_id = NULL WHERE code = 'FINANCIAL_VIEW';
                UPDATE master_data_config.configuration_version SET lifecycle_state = 'RETIRED', retired_at = now(), effective_to = now() WHERE id = '{version}';
                """);
        }
    }
}
