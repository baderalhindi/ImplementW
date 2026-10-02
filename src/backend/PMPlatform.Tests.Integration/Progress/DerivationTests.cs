using System.Net;
using System.Text.Json.Nodes;
using Npgsql;
using PMPlatform.Tests.Integration.Identity;

namespace PMPlatform.Tests.Integration.Progress;

/// <summary>
/// ADR-009 as applied to TASK-044: progress is derived, never typed in at project level; planned is calculated from the
/// baseline and never editable; an override needs a recorded reason, keeps the calculated value and is flagged. ADR-017:
/// the next period is pre-filled from the last, so confirming is the default action.
/// </summary>
[Collection(ProgressSuite.Name)]
public sealed class DerivationTests(ProgressTestHost host)
{
    [Fact]
    public async Task ProgressIsDerivedFromTheWorkBreakdownAndNeverTypedIn()
    {
        using HttpClient client = host.Api.CreateClient();
        ProgressSessions sessions = await client.SignInAsync();
        Guid projectId = await host.ProjectAsync();

        // No work breakdown to derive from: there is no figure, and none is taken from a person instead.
        using (HttpResponseMessage underived = await client.StartAsync(sessions.ProjectManager, projectId))
        {
            Assert.Equal((HttpStatusCode.UnprocessableEntity, "PROGRESS_ROLLUP_UNAVAILABLE"), await underived.RefusalAsync());
        }

        host.Inputs.Set(projectId, actualPercent: 30, baselineStart: ProgressDriver.Today.AddDays(-49));
        JsonObject draft = await client.StartOrFailAsync(sessions.ProjectManager, projectId);
        Guid id = AdministrationApi.IdOf(draft);
        Assert.Equal(
            ("DRAFT", 30m, 30m, 50m, FakeProgressInputs.BaselineId.ToString(), false),
            (draft.Status(), draft.Percent("actualPercent"), draft.Percent("actualPercentCalculated"), draft.Percent("plannedPercent"),
             draft["baselineId"]!.GetValue<string>(), draft["isOverridden"]!.GetValue<bool>()));

        // Figures sent by a client are not fields of the request: they are ignored.
        using (HttpResponseMessage edited = await client.EditAsync(sessions.ProjectManager, id, new
        {
            narrative = new { text = "Earthworks on schedule", language = "en" },
            actualPercent = 99,
            actualPercentCalculated = 99,
            plannedPercent = 99,
        }))
        {
            JsonObject body = await edited.ReadObjectAsync();
            Assert.Equal((HttpStatusCode.OK, 30m, 50m, false), (edited.StatusCode, body.Percent("actualPercent"), body.Percent("plannedPercent"), body["isOverridden"]!.GetValue<bool>()));
        }

        // The work moved on between starting and submitting: the figures are derived again and fixed at submission.
        host.Inputs.Set(projectId, actualPercent: 35, baselineStart: ProgressDriver.Today.AddDays(-49));
        JsonObject submitted = await client.CommandOrFailAsync(sessions.ProjectManager, id, "submit");
        Assert.Equal(("SUBMITTED", 35m, 50m), (submitted.Status(), submitted.Percent("actualPercentCalculated"), submitted.Percent("plannedPercent")));

        PostgresException fixedOnSubmission = await Assert.ThrowsAsync<PostgresException>(() =>
            host.Database.ExecuteRolledBackAsync($"UPDATE progress.progress_submission SET planned_percent = 10 WHERE id = '{id}'"));
        Assert.Equal(PostgresErrorCodes.RestrictViolation, fixedOnSubmission.SqlState);
    }

    [Fact]
    public async Task AnOverrideNeedsItsReasonKeepsTheCalculatedValueAndIsFlagged()
    {
        using HttpClient client = host.Api.CreateClient();
        ProgressSessions sessions = await client.SignInAsync();
        Guid projectId = await host.ProjectAsync();
        host.Inputs.Set(projectId, actualPercent: 40, baselineStart: ProgressDriver.Today.AddDays(-49));
        Guid id = AdministrationApi.IdOf(await client.StartOrFailAsync(sessions.ProjectManager, projectId));

        using (HttpResponseMessage withoutReason = await client.EditAsync(sessions.ProjectManager, id, new { @override = new { actualPercent = 48 } }))
        {
            Assert.Equal(HttpStatusCode.BadRequest, withoutReason.StatusCode);
            Assert.Equal(["override.reason REQUIRED"], await withoutReason.ReadFieldErrorsAsync());
        }

        using (HttpResponseMessage outOfRange = await client.EditAsync(sessions.ProjectManager, id, new
        {
            @override = new { actualPercent = 100.5, reason = new { text = "x", language = "en" } },
        }))
        {
            Assert.Equal(["override.actualPercent OUT_OF_RANGE"], await outOfRange.ReadFieldErrorsAsync());
        }

        using (HttpResponseMessage overridden = await client.EditAsync(sessions.ProjectManager, id, new
        {
            @override = new { actualPercent = 48, reason = new { text = "Site survey shows the earthworks further along than the task log", language = "en" } },
        }))
        {
            JsonObject body = await overridden.ReadObjectAsync();
            Assert.Equal(
                (HttpStatusCode.OK, 48m, 40m, 48m, true),
                (overridden.StatusCode, body.Percent("actualPercent"), body.Percent("actualPercentCalculated"), body.Percent("actualPercentOverride"), body["isOverridden"]!.GetValue<bool>()));
        }

        await client.CommandOrFailAsync(sessions.ProjectManager, id, "submit");
        await client.CommandOrFailAsync(sessions.Reviewer, id, "start-review");
        JsonObject published = await client.CommandOrFailAsync(sessions.Reviewer, id, "publish");
        Assert.Equal((40m, 48m), (published.Percent("actualPercentCalculated"), published.Percent("actualPercentOverride")));

        // Published: the overridden figure, flagged, rated on what was published (2 points behind: GREEN). Live: the derived
        // figure, never an override (10 points behind: AMBER).
        JsonNode snapshot = Assert.Single(await client.ItemsAsync(sessions.Reviewer, ProgressDriver.Snapshots, projectId))!;
        JsonNode live = Assert.Single(await client.ItemsAsync(sessions.Reviewer, ProgressDriver.HealthStatuses, projectId))!;
        Assert.Equal((48m, true, "GREEN"), (snapshot.Percent("actualPercent"), snapshot["isOverridden"]!.GetValue<bool>(), snapshot["overallHealth"]!.GetValue<string>()));
        Assert.Equal((40m, "AMBER"), (live.Percent("actualPercent"), live["overallHealth"]!.GetValue<string>()));

        PostgresException reasonless = await Assert.ThrowsAsync<PostgresException>(() => host.Database.ExecuteRolledBackAsync($"""
            UPDATE progress.progress_submission SET override_reason = NULL, override_reason_lang = NULL
            WHERE project_id = '{projectId}' AND status = 'PUBLISHED'
            """));
        Assert.Equal(PostgresErrorCodes.RestrictViolation, reasonless.SqlState);
    }

    [Fact]
    public async Task TheNextPeriodIsPrefilledFromTheLastPublishedSoConfirmingIsTheDefault()
    {
        using HttpClient client = host.Api.CreateClient();
        ProgressSessions sessions = await client.SignInAsync();
        Guid projectId = await host.ProjectAsync(activatedDaysAgo: 10);
        host.Inputs.Set(projectId, actualPercent: 40, baselineStart: ProgressDriver.Today.AddDays(-49));
        var edit = new
        {
            narrative = new { text = "سير العمل وفق الخطة", language = "ar" },
            @override = new { actualPercent = 45, reason = new { text = "Verified on site", language = "en" } },
        };
        await client.PublishedPeriodAsync(sessions.ProjectManager, sessions.Reviewer, projectId, edit);

        host.Inputs.Set(projectId, actualPercent: 47, baselineStart: ProgressDriver.Today.AddDays(-49));
        JsonObject next = await client.StartOrFailAsync(sessions.ProjectManager, projectId);

        Assert.Equal(
            ("سير العمل وفق الخطة", "AR", 45m, "Verified on site", 47m),
            (next["narrative"]!["text"]!.GetValue<string>(), next["narrative"]!["language"]!.GetValue<string>(), next.Percent("actualPercentOverride"),
             next["overrideReason"]!["text"]!.GetValue<string>(), next.Percent("actualPercentCalculated")));
        Assert.Equal(2, (await client.ItemsAsync(sessions.ProjectManager, ProgressDriver.Cycles, projectId)).Count);

        // Confirming is submitting what was pre-filled, unchanged.
        Assert.Equal("SUBMITTED", (await client.CommandOrFailAsync(sessions.ProjectManager, AdministrationApi.IdOf(next), "submit")).Status());
    }
}
