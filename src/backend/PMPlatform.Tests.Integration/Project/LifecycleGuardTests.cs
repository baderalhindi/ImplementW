using System.Net;
using System.Text.Json.Nodes;
using Npgsql;
using PMPlatform.Application.Features.Approval.Contracts;
using PMPlatform.Tests.Integration.Identity;
using PMPlatform.Tests.Integration.MasterDataConfig;

namespace PMPlatform.Tests.Integration.Project;

/// <summary>
/// Acceptance criteria 1 and 3, in the API and in the database: no project skips UNDER_REVIEW (no fast-track rule is
/// authorised, PTBC-002), ACTIVE has one way in, and every project has exactly one Formal Project ID, unique and fixed once
/// issued. Migration TASK-041_GuardProjectLifecycle refuses a forbidden change whoever writes it.
/// </summary>
[Collection(ProjectSuite.Name)]
public sealed class LifecycleGuardTests(ProjectTestHost host)
{
    private static readonly string[] States = ["DRAFT", "SUBMITTED", "UNDER_REVIEW", "RETURNED", "APPROVED_PLANNED", "ACTIVE", "SUSPENDED", "COMPLETED", "CLOSED"];

    /// <summary>
    /// TASK-041's seven edges, as the specification states them, and TASK-062's two; every other pair of states is refused. That a project
    /// takes TASK-062's edges only with its active suspension is held too, and tested with WF-09 (SuspensionGuardTests).
    /// </summary>
    private static readonly HashSet<(string From, string To)> Edges =
    [
        ("DRAFT", "SUBMITTED"), ("SUBMITTED", "DRAFT"), ("SUBMITTED", "UNDER_REVIEW"), ("UNDER_REVIEW", "RETURNED"),
        ("UNDER_REVIEW", "APPROVED_PLANNED"), ("RETURNED", "SUBMITTED"), ("APPROVED_PLANNED", "ACTIVE"),
        ("ACTIVE", "SUSPENDED"), ("SUSPENDED", "ACTIVE"),
    ];

    public static TheoryData<string, string> ForbiddenChanges()
    {
        TheoryData<string, string> data = [];
        foreach (string from in States)
        {
            foreach (string to in States.Where(to => to != from && !Edges.Contains((from, to))))
            {
                data.Add(from, to);
            }
        }

        return data;
    }

    /// <summary>Acceptance criterion 3 through the API: from DRAFT, SUBMITTED and RETURNED no command reaches APPROVED_PLANNED or ACTIVE.</summary>
    [Fact]
    public async Task NoCommandTakesAProjectPastReview()
    {
        using HttpClient client = host.Api.CreateClient();
        Sessions sessions = await client.SignInAsync();

        Guid projectId = AdministrationApi.IdOf(await client.CreateOrFailAsync(sessions.Entity, host.Registration()));
        await AssertRefusedAsync(client, sessions, projectId, "activate", "start-review", "withdraw");

        await client.CommandOrFailAsync(sessions.Entity, projectId, "submit", new { projectManagerUserId = ProjectDriver.Person(8) });
        await AssertRefusedAsync(client, sessions, projectId, "activate", "submit");

        await client.CommandOrFailAsync(sessions.Reviewer, projectId, "start-review");
        await AssertRefusedAsync(client, sessions, projectId, "activate", "start-review", "withdraw", "submit");
        using (HttpResponseMessage edit = await client.PutAsync($"{ProjectDriver.Projects}/{projectId}", sessions.Entity, host.Registration(), "\"1\""))
        {
            Assert.Equal((HttpStatusCode.Conflict, "PROJECT_NOT_EDITABLE"), (edit.StatusCode, await edit.CodeOfAsync()));
        }

        await host.DecideAndDeliverAsync(projectId, ApprovalTaskDecision.Return, "Scope unclear");
        await AssertRefusedAsync(client, sessions, projectId, "activate", "start-review", "withdraw");

        JsonObject row = await host.RowAsync(projectId);
        Assert.Equal(("RETURNED", null), (row["lifecycle_state"]!.GetValue<string>(), row["formal_project_id"]?.GetValue<string>()));
    }

    /// <summary>
    /// The workbook's validation check: forcing a DRAFT straight to ACTIVE through the API is refused every way the API
    /// offers. The activation command, sent by someone who holds PROJECT_ACTIVATE, is 409; an edit that carries
    /// <c>status</c>, <c>activatedAt</c> and <c>formalProjectId</c> changes none of them. The row ends as it began.
    /// </summary>
    [Fact]
    public async Task ADraftCannotBeForcedToActiveThroughTheApi()
    {
        using HttpClient client = host.Api.CreateClient();
        Sessions sessions = await client.SignInAsync();
        Guid projectId = AdministrationApi.IdOf(await client.CreateOrFailAsync(sessions.Entity, host.Registration()));
        JsonObject before = await host.RowAsync(projectId);

        using (HttpResponseMessage activate = await client.PostAsync($"{ProjectDriver.Projects}/{projectId}/activate", sessions.Approver))
        {
            Assert.Equal((HttpStatusCode.Conflict, "INVALID_TRANSITION"), (activate.StatusCode, await activate.CodeOfAsync()));
        }

        using HttpResponseMessage read = await client.GetAsync($"{ProjectDriver.Projects}/{projectId}", sessions.Entity);
        JsonObject forced = host.Registration(change: r =>
        {
            r["status"] = "ACTIVE";
            r["activatedAt"] = "2026-01-01T00:00:00Z";
            r["formalProjectId"] = "PRJ-FORCED";
        });
        using (HttpResponseMessage edited = await client.PutAsync($"{ProjectDriver.Projects}/{projectId}", sessions.Entity, forced, AdministrationApi.ETagOf(read)))
        {
            JsonObject body = await edited.ReadObjectAsync();
            Assert.Equal((HttpStatusCode.OK, "DRAFT", null), (edited.StatusCode, body.Status(), body.FormalProjectId()));
            Assert.Null(body["activatedAt"]);
        }

        JsonObject after = await host.RowAsync(projectId);
        Assert.Equal(
            (before["lifecycle_state"]!.ToJsonString(), before["activated_at"]?.ToJsonString(), before["formal_project_id"]?.ToJsonString()),
            (after["lifecycle_state"]!.ToJsonString(), after["activated_at"]?.ToJsonString(), after["formal_project_id"]?.ToJsonString()));
        Assert.DoesNotContain("Project.ProjectActivated", await host.AuditEventsAsync(projectId));
    }

    /// <summary>The database refuses every change of state that is not one of TASK-041's or TASK-062's edges, whoever writes it.</summary>
    [Theory]
    [MemberData(nameof(ForbiddenChanges))]
    public async Task TheDatabaseRefusesAChangeOfStateThatIsNoEdge(string from, string to)
    {
        Guid projectId = await InsertAsync(from);

        PostgresException refused = await Assert.ThrowsAsync<PostgresException>(() =>
            host.Database.ExecuteAsync($"UPDATE project.project SET lifecycle_state = '{to}' WHERE id = '{projectId}'"));

        Assert.Equal((PostgresErrorCodes.RestrictViolation, true), (refused.SqlState, refused.MessageText.Contains("is not a lifecycle transition", StringComparison.Ordinal)));
    }

    /// <summary>
    /// Acceptance criterion 1: one authoritative identifier per project. The unique key refuses a second project with the same
    /// one; an issued identifier is never changed or cleared; and none is issued but by approval.
    /// </summary>
    [Fact]
    public async Task AFormalProjectIdIsUniqueIssuedOnlyByApprovalAndNeverChanged()
    {
        using HttpClient client = host.Api.CreateClient();
        Sessions sessions = await client.SignInAsync();
        Guid first = await host.UnderReviewAsync(client, sessions);
        Guid second = await host.UnderReviewAsync(client, sessions);
        await host.DecideAndDeliverAsync(first, ApprovalTaskDecision.Approve);
        await host.DecideAndDeliverAsync(second, ApprovalTaskDecision.Approve);
        string firstId = (await host.RowAsync(first))["formal_project_id"]!.GetValue<string>();
        string secondId = (await host.RowAsync(second))["formal_project_id"]!.GetValue<string>();
        Assert.NotEqual(firstId, secondId);

        PostgresException duplicate = await Assert.ThrowsAsync<PostgresException>(() => InsertAsync("APPROVED_PLANNED", firstId));
        Assert.Equal((PostgresErrorCodes.UniqueViolation, "ix_project_formal_project_id"), (duplicate.SqlState, duplicate.ConstraintName));

        await AssertGuardAsync($"UPDATE project.project SET formal_project_id = 'PRJ-REISSUED' WHERE id = '{first}'", "issued once");
        await AssertGuardAsync($"UPDATE project.project SET formal_project_id = NULL WHERE id = '{first}'", "issued once");

        Guid submitted = await InsertAsync("SUBMITTED");
        await AssertGuardAsync($"UPDATE project.project SET formal_project_id = 'PRJ-EARLY' WHERE id = '{submitted}'", "issued once");

        // Without an identifier, no project is APPROVED_PLANNED or later (TASK-025's check).
        PostgresException missing = await Assert.ThrowsAsync<PostgresException>(() =>
            host.Database.ExecuteAsync($"UPDATE project.project SET lifecycle_state = 'UNDER_REVIEW' WHERE id = '{submitted}'; UPDATE project.project SET lifecycle_state = 'APPROVED_PLANNED' WHERE id = '{submitted}'"));
        Assert.Equal((PostgresErrorCodes.CheckViolation, "ck_project_formal_project_id"), (missing.SqlState, missing.ConstraintName));

        Assert.Equal(firstId, (await host.RowAsync(first))["formal_project_id"]!.GetValue<string>());
    }

    /// <summary>ACTIVE is stamped once by its command; the ADR-014 intake marker is set once; the revision moves only on resubmission; only a DRAFT is deleted.</summary>
    [Fact]
    public async Task TheDatabaseHoldsActivationIntakeRevisionAndDeletionToTheirRules()
    {
        Guid planned = await InsertAsync("APPROVED_PLANNED", "PRJ-GUARD-1");
        await AssertGuardAsync($"UPDATE project.project SET lifecycle_state = 'ACTIVE' WHERE id = '{planned}'", "activated_at");
        await AssertGuardAsync($"UPDATE project.project SET activated_at = now() WHERE id = '{planned}'", "activated_at");
        await host.Database.ExecuteAsync($"UPDATE project.project SET lifecycle_state = 'ACTIVE', activated_at = now() WHERE id = '{planned}'");
        await AssertGuardAsync($"UPDATE project.project SET activated_at = now() + interval '1 day' WHERE id = '{planned}'", "activated_at");

        await host.Database.ExecuteAsync($"UPDATE project.project SET legacy_intake_date = current_date WHERE id = '{planned}'");
        await AssertGuardAsync($"UPDATE project.project SET legacy_intake_date = NULL WHERE id = '{planned}'", "intake marker");

        Guid draft = await InsertAsync("DRAFT");
        await AssertGuardAsync($"UPDATE project.project SET revision_no = 2 WHERE id = '{draft}'", "revision");
        Guid returned = await InsertAsync("RETURNED");
        await AssertGuardAsync($"UPDATE project.project SET lifecycle_state = 'SUBMITTED' WHERE id = '{returned}'", "revision");
        await AssertGuardAsync($"UPDATE project.project SET lifecycle_state = 'SUBMITTED', revision_no = 3 WHERE id = '{returned}'", "revision");
        await host.Database.ExecuteAsync($"UPDATE project.project SET lifecycle_state = 'SUBMITTED', revision_no = 2 WHERE id = '{returned}'");

        await AssertGuardAsync($"DELETE FROM project.project WHERE id = '{returned}'", "only a DRAFT is deleted");
        await host.Database.ExecuteAsync($"DELETE FROM project.project WHERE id = '{draft}'");
    }

    /// <summary>Each command sent by someone allowed to send it, so that only the project's state refuses it.</summary>
    private static async Task AssertRefusedAsync(HttpClient client, Sessions sessions, Guid projectId, params string[] commands)
    {
        foreach (string command in commands)
        {
            string token = command switch
            {
                "activate" => sessions.Approver,
                "start-review" => sessions.Reviewer,
                _ => sessions.Entity,
            };
            object? body = command == "submit" ? new { projectManagerUserId = ProjectDriver.Person(8) } : null;
            using HttpResponseMessage response = await client.PostAsync($"{ProjectDriver.Projects}/{projectId}/{command}", token, body);
            Assert.True(
                (response.StatusCode, await response.CodeOfAsync()) == (HttpStatusCode.Conflict, "INVALID_TRANSITION"),
                $"{command}: {(int)response.StatusCode} {await response.Content.ReadAsStringAsync()}");
        }
    }

    private async Task AssertGuardAsync(string sql, string reason)
    {
        PostgresException refused = await Assert.ThrowsAsync<PostgresException>(() => host.Database.ExecuteAsync(sql));
        Assert.Equal(PostgresErrorCodes.RestrictViolation, refused.SqlState);
        Assert.Contains(reason, refused.MessageText, StringComparison.Ordinal);
    }

    /// <summary>A project written straight into <paramref name="state"/>: an INSERT is not a transition, so the guard lets it in.</summary>
    private async Task<Guid> InsertAsync(string state, string? formalProjectId = null)
    {
        Guid id = Guid.NewGuid();
        string formal = formalProjectId is null
            ? state is "DRAFT" or "SUBMITTED" or "UNDER_REVIEW" or "RETURNED" ? "NULL" : $"'PRJ-GUARD-{id:N}'"
            : $"'{formalProjectId}'";
        string activated = state is "ACTIVE" or "SUSPENDED" or "COMPLETED" or "CLOSED" ? "now()" : "NULL";
        await host.Database.ExecuteAsync($"""
            INSERT INTO project.project (id, formal_project_id, title, title_lang, classification_item_id, department_id, lifecycle_state,
                                         governance_profile_item_id, participation_mode, activated_at, created_at, created_by, updated_at, updated_by)
            VALUES ('{id}', {formal}, 'Guarded project', 'en', '{ProjectTestHost.ClassificationId}', '{ProjectTestHost.DepartmentId}', '{state}',
                    '{host.GovernanceProfileId}', 'AHDA_MANAGED', {activated}, now(), '{IdentityDatabase.SeedPrincipalId}', now(), '{IdentityDatabase.SeedPrincipalId}')
            """);
        return id;
    }
}
