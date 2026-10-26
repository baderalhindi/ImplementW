using System.Net;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using PMPlatform.Application.Features.Approval.Contracts;
using PMPlatform.Application.Features.Schedule;
using PMPlatform.Domain.Common;
using PMPlatform.Tests.Integration.Identity;

namespace PMPlatform.Tests.Integration.Schedule;

/// <summary>
/// The gate decision: an approved ACTIVE baseline is required before a project goes Active, because without it there is no
/// planned percentage (ADR-009); ADR-014's Declared Baseline meets it for a legacy-intake project; ADR-015 makes the approval,
/// not the baseline, depend on the profile.
/// </summary>
[Collection(ScheduleSuite.Name)]
public sealed class ActivationGateTests(ScheduleTestHost host)
{
    private const string Projects = "/api/v1/projects";

    [Fact]
    public async Task AProjectGoesActiveOnlyOnceItHasAnApprovedActiveBaseline()
    {
        using HttpClient client = host.Api.CreateClient();
        ScheduleSessions sessions = await client.SignInAsync();
        Guid projectId = await host.ScheduledProjectAsync(client, sessions.ProjectManager);
        await client.ActivityAsync(sessions.ProjectManager, projectId, "1", 0, 10);

        using (HttpResponseMessage noBaseline = await client.PostAsync($"{Projects}/{projectId}/activate", sessions.Portfolio))
        {
            Assert.Equal((HttpStatusCode.UnprocessableEntity, "SCHEDULE_ACTIVE_BASELINE_REQUIRED"), await noBaseline.RefusalAsync());
        }

        // A candidate under review is no baseline yet.
        Guid baseline = AdministrationApi.IdOf(await client.SubmittedBaselineAsync(sessions.ProjectManager, projectId));
        using (HttpResponseMessage underReview = await client.PostAsync($"{Projects}/{projectId}/activate", sessions.Portfolio))
        {
            Assert.Equal((HttpStatusCode.UnprocessableEntity, "SCHEDULE_ACTIVE_BASELINE_REQUIRED"), await underReview.RefusalAsync());
        }

        Assert.Equal(["APPROVED_PLANNED|"], await host.Database.QueryAsync($"SELECT lifecycle_state || '|' || coalesce(activated_at::text, '') FROM project.project WHERE id = '{projectId}'"));

        await host.DecideAndDeliverAsync(baseline, ApprovalTaskDecision.Approve);
        Assert.Equal("ACTIVE", (await client.CommandOrFailAsync(sessions.Portfolio, $"{Projects}/{projectId}/activate")).Text("status"));
    }

    /// <summary>ADR-015: under Light the baseline needs no approval, but the project still needs one ACTIVE before it goes Active.</summary>
    [Fact]
    public async Task UnderALightProfileTheBaselineIsStillRequiredButNotApproved()
    {
        using HttpClient client = host.Api.CreateClient();
        ScheduleSessions sessions = await client.SignInAsync();
        Guid projectId = await host.ScheduledProjectAsync(client, sessions.ProjectManager, profileId: host.LightProfileId);

        using (HttpResponseMessage noBaseline = await client.PostAsync($"{Projects}/{projectId}/activate", sessions.Portfolio))
        {
            Assert.Equal((HttpStatusCode.UnprocessableEntity, "SCHEDULE_ACTIVE_BASELINE_REQUIRED"), await noBaseline.RefusalAsync());
        }

        await client.ActivityAsync(sessions.ProjectManager, projectId, "1", 0, 10);
        Assert.Equal("ACTIVE", (await client.SubmittedBaselineAsync(sessions.ProjectManager, projectId)).Text("status"));
        Assert.Equal("ACTIVE", (await client.CommandOrFailAsync(sessions.Portfolio, $"{Projects}/{projectId}/activate")).Text("status"));
    }

    /// <summary>
    /// ADR-014: a legacy-intake project enters with a Declared Baseline — recorded once, ACTIVE, distinguishable from an Approved
    /// one — which meets the gate. Its first Approved Baseline then supersedes it with no change authorisation: it is the
    /// project's first approved plan, not a rebaseline.
    /// </summary>
    [Fact]
    public async Task ADeclaredBaselineMeetsTheGateForALegacyIntakeProject()
    {
        using HttpClient client = host.Api.CreateClient();
        ScheduleSessions sessions = await client.SignInAsync();
        DateOnly intakeDate = ScheduleDriver.Day1.AddDays(-200);
        Guid projectId = await host.ProjectAsync(legacyIntakeDate: intakeDate);
        Guid intakeId = Guid.NewGuid();
        await host.Database.ExecuteAsync($"""
            INSERT INTO project.project_intake (id, project_id, intake_date, declared_scope, declared_scope_lang, declared_budget_sar, declared_end_date, opening_percent_complete,
                                                opening_spend_to_date_sar, recorded_by_user_id, created_at, created_by, updated_at, updated_by)
            VALUES ('{intakeId}', '{projectId}', '{ScheduleDriver.Iso(intakeDate)}', 'Road works already under way', 'en', 1000000, '{ScheduleDriver.Iso(ScheduleDriver.Day1.AddDays(90))}',
                    35, 400000, '{ScheduleDriver.Person(2)}', now(), '{ScheduleDriver.Person(2)}', now(), '{ScheduleDriver.Person(2)}')
            """);
        DeclaredBaselineIntake intake = new(projectId, intakeId, intakeDate, ScheduleDriver.Day1.AddDays(90), new NarrativeText("Road works already under way", Language.En), ScheduleDriver.Person(2));

        Assert.True(await host.WithScopeAsync(services => services.GetRequiredService<ScheduleDeclaredBaseline>().RecordAsync(intake, CancellationToken.None)));
        Assert.False(await host.WithScopeAsync(services => services.GetRequiredService<ScheduleDeclaredBaseline>().RecordAsync(intake, CancellationToken.None)));

        JsonObject declared = (await client.ItemsAsync(sessions.ProjectManager, ScheduleDriver.Baselines, projectId)).Single()!.AsObject();
        Assert.Equal(("DECLARED", "ACTIVE", ScheduleDriver.Iso(ScheduleDriver.Day1.AddDays(90)), intakeId.ToString()),
            (declared.Text("baselineType"), declared.Text("status"), declared.Text("declaredEndDate"), declared.Text("projectIntakeId")));
        Assert.Equal("ACTIVE", (await client.CommandOrFailAsync(sessions.Portfolio, $"{Projects}/{projectId}/activate")).Text("status"));

        await client.CreatedOrFailAsync(sessions.ProjectManager, ScheduleDriver.Schedules, new { projectId });
        await client.ActivityAsync(sessions.ProjectManager, projectId, "1", 0, 30);
        Guid approved = AdministrationApi.IdOf(await client.SubmittedBaselineAsync(sessions.ProjectManager, projectId));
        await host.DecideAndDeliverAsync(approved, ApprovalTaskDecision.Approve);

        Assert.Equal(["1 SUPERSEDED", "2 ACTIVE"], await host.BaselineStatesAsync(projectId));
    }
}
