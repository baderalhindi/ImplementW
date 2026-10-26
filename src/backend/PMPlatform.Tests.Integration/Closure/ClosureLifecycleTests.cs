using System.Net;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using PMPlatform.Application.Common.Events;
using PMPlatform.Application.Features.Approval;
using PMPlatform.Application.Features.Approval.Contracts;
using PMPlatform.Application.Features.Approval.Contracts.Events;
using PMPlatform.Application.Features.Closure;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.Suspension;
using PMPlatform.Domain.Common;
using PMPlatform.Tests.Integration.Identity;

namespace PMPlatform.Tests.Integration.Closure;

/// <summary>
/// TASK-063's description and first acceptance criterion over HTTP: Completion and Closure are two distinct, sequential cases, each
/// readiness-gated and decided through WF-11 apart from the lifecycle activation that moves the project; no progress figure or completed
/// task completes a project; the actual completion date is captured and checked; post-project obligations stay open after Completion
/// until the closure policy is satisfied; and a SUSPENDED project is closed without being completed.
/// </summary>
[Collection(ClosureSuite.Name)]
public sealed class ClosureLifecycleTests(ClosureTestHost host)
{
    /// <summary>
    /// Acceptance criterion 1 and validation check 1: the project's every task complete, its schedule activity executed to 100%, and every
    /// pass that could act run — and it stays ACTIVE, with no completion case and no completion event; the database refuses to complete it by
    /// a write. Only the explicit flow completes it: a case raised, readiness-gated, approved through WF-11 (still ACTIVE), then activated.
    /// </summary>
    [Fact]
    public async Task NoCompletedTaskOrFullProgressCompletesAProjectWithoutTheApprovedCase()
    {
        using HttpClient client = host.Api.CreateClient();
        ClosureSessions sessions = await client.SignInAsync();
        Guid projectId = await host.ProjectAsync();
        Guid activityId = await client.BaselinedActivityAsync(sessions, projectId);
        Guid taskId = await client.TaskAsync(sessions, projectId, activityId);
        await client.CommandOrFailAsync(sessions.EntityManager, ClosureDriver.Tasks, taskId, "start");
        await client.CommandOrFailAsync(sessions.EntityManager, ClosureDriver.Tasks, taskId, "report-progress", new { actualPercentComplete = 100 });
        Assert.Equal("COMPLETED", (await client.CommandOrFailAsync(sessions.EntityManager, ClosureDriver.Tasks, taskId, "complete")).Text("status"));
        Assert.Equal(["true"], await host.Database.QueryAsync($"SELECT (actual_percent_complete = 100)::text FROM project_task.activity_execution_progress WHERE schedule_activity_id = '{activityId}'"));

        await host.ActivateDueAsync();
        await host.WithScopeAsync(services => services.GetRequiredService<ISuspensionMaintenance>().RunAsync(100, CancellationToken.None));
        await host.WithScopeAsync(services => services.GetRequiredService<IApprovalMaintenance>().RunAsync(100, CancellationToken.None));

        Assert.Equal("ACTIVE", await host.LifecycleOfAsync(projectId));
        Assert.Empty(await host.Database.QueryAsync($"SELECT id::text FROM closure.completion_case WHERE project_id = '{projectId}'"));
        Assert.DoesNotContain(await host.AuditTrailAsync("Project", projectId), e => e.StartsWith("Project.ProjectCompleted", StringComparison.Ordinal));
        Assert.Contains("COMPLETED only by its effected completion case",
            await host.RefusedAsync($"UPDATE project.project SET lifecycle_state = 'COMPLETED' WHERE id = '{projectId}'"));

        Guid caseId = await host.ApprovedAsync(client, sessions, ClosureDriver.CompletionCases, ClosureDriver.CompletionBody(projectId));
        Assert.Equal("ACTIVE", await host.LifecycleOfAsync(projectId));
        await host.ActivateDueAsync();

        JsonObject effected = await client.GetOrFailAsync(sessions.EntityManager, $"{ClosureDriver.CompletionCases}/{caseId}");
        Assert.Equal(("EFFECTED", ClosureDriver.Iso(ClosureDriver.Today)), (effected.Text("status"), effected.Text("actualProjectCompletionDate")));
        Assert.Equal("COMPLETED", await host.LifecycleOfAsync(projectId));
    }

    /// <summary>
    /// Approval and activation are two transactions with two audit events (WF-10 P3, CLO-CC-10): after WF-11's approval the project row is
    /// identical to the row version and no Project event is written; the run's requester is the originator; the outcome handed to WF-10
    /// again applies nothing. WF-10's pass then effects the case as its service principal, and the project names the case.
    /// </summary>
    [Fact]
    public async Task ApprovalAndActivationAreTwoSeparatelyAuditedEvents()
    {
        using HttpClient client = host.Api.CreateClient();
        ClosureSessions sessions = await client.SignInAsync();
        Guid projectId = await host.ProjectAsync();
        string projectBefore = await host.RowAsync("project.project", projectId);

        Guid caseId = await host.ApprovedAsync(client, sessions, ClosureDriver.CompletionCases, ClosureDriver.CompletionBody(projectId));

        Assert.Equal("APPROVED", (await client.GetOrFailAsync(sessions.EntityManager, $"{ClosureDriver.CompletionCases}/{caseId}")).Text("status"));
        Assert.Equal(projectBefore, await host.RowAsync("project.project", projectId));
        Assert.Empty(await host.AuditTrailAsync("Project", projectId));
        ApprovalInstanceDetail run = Assert.Single(await host.RunsAsync("Closure", "CompletionCase", caseId));
        Assert.Equal((ClosureDriver.Person(8), "COMPLETION"), (run.RequestedByUserId, run.RoutingKey));

        string caseRow = await host.RowAsync("closure.completion_case", caseId);
        string payload = Assert.Single(await host.Database.QueryAsync($"SELECT payload::text FROM common.outbox_message WHERE message_key = 'Approval.ApprovalOutcomeRecorded:apr-{run.Id}-outcome'"));
        await host.WithScopeAsync(async services =>
        {
            await services.GetServices<IApprovalOutcomeHandler>().Single(h => h.SubjectModule == "Closure")
                .HandleAsync(EventSerialization.Deserialize<ApprovalOutcomeRecorded>(payload), CancellationToken.None);
            return true;
        });
        Assert.Equal(caseRow, await host.RowAsync("closure.completion_case", caseId));

        await host.ActivateDueAsync();

        string service = CloseoutServicePrincipal.Id.ToString();
        IReadOnlyList<string> trail = await host.AuditTrailAsync("Closure", caseId);
        Assert.Equal(
            [
                $"Closure.CaseCreated USER {ClosureDriver.Person(8)}", $"Closure.ReadinessEvaluated USER {ClosureDriver.Person(8)}",
                $"Closure.CheckWaived USER {ClosureDriver.Person(3)}", $"Closure.CaseSubmitted USER {ClosureDriver.Person(8)}",
                $"Closure.ReviewStarted USER {ClosureDriver.Person(3)}", $"Closure.CaseApproved USER {ClosureDriver.Person(2)}",
                $"Closure.OutcomeIgnored USER {ClosureDriver.Person(2)}", $"Closure.CaseEffected SERVICE {service}",
            ],
            trail);
        Assert.Equal([$"Project.ProjectCompleted SERVICE {service}"], await host.AuditTrailAsync("Project", projectId));
        Assert.Equal(
            [caseId.ToString()],
            await host.Database.QueryAsync($"""
                SELECT a.new_value FROM audit_activity.audit_event e JOIN audit_activity.audit_event_attribute a ON a.audit_event_id = e.id
                WHERE e.subject_module = 'Project' AND e.subject_id = '{projectId}' AND a.attribute_name = 'completion_case_id'
                """));
    }

    /// <summary>
    /// The readiness gate (WF-10 §6, CLO-CC-14): an open task fails TASKS_DISPOSITIONED and submission is refused 422 CLOSURE_BLOCKER_EXISTS,
    /// naming it; the Department Manager's waiver makes the case READY_WITH_CONDITIONS and it is submitted, the snapshot frozen; an entity
    /// Project Manager cannot waive (ADR-013), nor can anyone waive a decision still to land; and a blocker appearing after approval refuses
    /// the activation, leaving the case APPROVED, until it is cleared.
    /// </summary>
    [Fact]
    public async Task CompletionIsReadinessGatedAndRevalidatedAtActivation()
    {
        using HttpClient client = host.Api.CreateClient();
        ClosureSessions sessions = await client.SignInAsync();
        Guid projectId = await host.ProjectAsync();
        Guid activityId = await client.BaselinedActivityAsync(sessions, projectId);
        Guid taskId = await client.TaskAsync(sessions, projectId, activityId);
        Guid caseId = await client.RaiseAsync(sessions.EntityManager, ClosureDriver.CompletionCases, ClosureDriver.CompletionBody(projectId));

        JsonObject evaluated = await client.CommandOrFailAsync(sessions.EntityManager, ClosureDriver.CompletionCases, caseId, "evaluate-readiness");
        Assert.Equal("NOT_READY", evaluated["readiness"]!.Text("status"));
        Assert.Contains("TASKS_DISPOSITIONED FAIL", evaluated.Checks());
        using (HttpResponseMessage blocked = await client.CommandAsync(sessions.EntityManager, ClosureDriver.CompletionCases, caseId, "submit"))
        {
            Assert.Equal((HttpStatusCode.UnprocessableEntity, "CLOSURE_BLOCKER_EXISTS"), await blocked.RefusalAsync());
            Assert.Contains("TASKS_DISPOSITIONED NOT_ALLOWED", await blocked.ReadFieldErrorsAsync());
        }

        object waiver = new { checkCode = "TASKS_DISPOSITIONED", reason = ClosureDriver.Narrative("Snagging transferred to the operator.") };
        using (HttpResponseMessage external = await client.CommandAsync(sessions.EntityManager, ClosureDriver.CompletionCases, caseId, "waive-check", waiver))
        {
            Assert.Equal(HttpStatusCode.Forbidden, external.StatusCode);
        }

        Assert.Contains($"Closure.AuthorityRefused USER {ClosureDriver.Person(8)}", await host.AuditTrailAsync("Closure", caseId));
        using (HttpResponseMessage hard = await client.CommandAsync(sessions.DepartmentManager, ClosureDriver.CompletionCases, caseId, "waive-check",
                   new { checkCode = "DECISIONS_SETTLED", reason = ClosureDriver.Narrative("Not waivable.") }))
        {
            Assert.Equal((HttpStatusCode.UnprocessableEntity, "CLOSURE_CHECK_NOT_WAIVABLE"), await hard.RefusalAsync());
        }

        await client.WaiveFailedAsync(sessions, ClosureDriver.CompletionCases, caseId);
        JsonObject submitted = await client.CommandOrFailAsync(sessions.EntityManager, ClosureDriver.CompletionCases, caseId, "submit");
        Assert.Equal(("SUBMITTED", "READY_WITH_CONDITIONS"), (submitted.Text("status"), submitted["readiness"]!.Text("status")));
        Assert.Contains("TASKS_DISPOSITIONED WAIVED", submitted.Checks());
        Assert.Equal(ClosureDriver.Person(3), Guid.Parse(submitted["readiness"]!["checks"]!.AsArray().Single(c => c!.Text("checkCode") == "TASKS_DISPOSITIONED")!.Text("waivedByUserId")));

        await client.CommandOrFailAsync(sessions.DepartmentManager, ClosureDriver.CompletionCases, caseId, "start-review");
        await host.DecideAndDeliverAsync("CompletionCase", caseId, ApprovalTaskDecision.Approve);

        // A blocker that is not waived appears after the approval: an open suspension request. Activation revalidates and refuses it.
        object suspension = new
        {
            projectId,
            requestType = "SUSPEND",
            reason = ClosureDriver.Narrative("Funding review."),
            requestedEffectiveDate = ClosureDriver.Iso(ClosureDriver.Today.AddDays(30)),
        };
        using (HttpResponseMessage raised = await client.PostAsync(ClosureDriver.SuspensionRequests, sessions.EntityManager, suspension))
        {
            Assert.Equal(HttpStatusCode.Created, raised.StatusCode);
            Guid requestId = AdministrationApi.IdOf(await raised.ReadObjectAsync());
            using (HttpResponseMessage refused = await client.CommandAsync(sessions.Officer, ClosureDriver.CompletionCases, caseId, "activate"))
            {
                Assert.Equal((HttpStatusCode.UnprocessableEntity, "CLOSURE_BLOCKER_EXISTS"), await refused.RefusalAsync());
                Assert.Equal(["SUSPENSION_REQUESTS_SETTLED NOT_ALLOWED"], await refused.ReadFieldErrorsAsync());
            }

            await host.ActivateDueAsync();
            Assert.Equal("APPROVED", (await client.GetOrFailAsync(sessions.Officer, $"{ClosureDriver.CompletionCases}/{caseId}")).Text("status"));
            Assert.Equal("ACTIVE", await host.LifecycleOfAsync(projectId));

            using HttpResponseMessage deleted = await client.SendAsync(HttpMethod.Delete, $"{ClosureDriver.SuspensionRequests}/{requestId}", sessions.EntityManager);
            Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        }

        await host.ActivateDueAsync();
        Assert.Equal("COMPLETED", await host.LifecycleOfAsync(projectId));

        // The task stays as it was: completion completes no task (BR-CLO-011).
        Assert.Equal(["NOT_STARTED"], await host.Database.QueryAsync($"SELECT status FROM project_task.project_task WHERE id = '{taskId}'"));
    }

    /// <summary>
    /// DECISIONS_SETTLED (not waivable): while a WF-11 run of the project is pending — and after it is decided, until its outcome has reached
    /// its source module — no case is submitted, so no decision lands on a record after its project moves.
    /// </summary>
    [Fact]
    public async Task ADecisionStillToLandBlocksTheCase()
    {
        using HttpClient client = host.Api.CreateClient();
        ClosureSessions sessions = await client.SignInAsync();
        Guid projectId = await host.ProjectAsync();
        Guid requestId = await client.RaiseAsync(sessions.EntityManager, ClosureDriver.SuspensionRequests, new
        {
            projectId,
            requestType = "SUSPEND",
            reason = ClosureDriver.Narrative("Funding review."),
            requestedEffectiveDate = ClosureDriver.Iso(ClosureDriver.Today.AddDays(30)),
        });
        await client.OkOrFailAsync(sessions.EntityManager, $"{ClosureDriver.SuspensionRequests}/{requestId}/submit");
        await client.OkOrFailAsync(sessions.DepartmentManager, $"{ClosureDriver.SuspensionRequests}/{requestId}/start-review");
        Guid caseId = await client.RaiseAsync(sessions.EntityManager, ClosureDriver.CompletionCases, ClosureDriver.CompletionBody(projectId));

        JsonObject pending = await client.CommandOrFailAsync(sessions.EntityManager, ClosureDriver.CompletionCases, caseId, "evaluate-readiness");
        Assert.Contains("DECISIONS_SETTLED FAIL", pending.Checks());

        // Decided, and not yet delivered: the run is no longer pending, but its outcome has not reached WF-09, whose request is still under review.
        ApprovalInstanceDetail run = Assert.Single(await host.RunsAsync("Suspension", "SuspensionRequest", requestId));
        AdministrationResult<ApprovalInstanceDetail> decided = await host.WithScopeAsync(services => services.GetRequiredService<IApprovalWorkflowService>().DecideAsync(
            ClosureDriver.Person(2), Assert.Single(run.Tasks).Id, ApprovalTaskDecision.Reject, new NarrativeText("Not now.", Language.En), CancellationToken.None));
        Assert.True(decided.Succeeded);
        await client.WaiveFailedAsync(sessions, ClosureDriver.CompletionCases, caseId);
        using (HttpResponseMessage undelivered = await client.CommandAsync(sessions.EntityManager, ClosureDriver.CompletionCases, caseId, "submit"))
        {
            Assert.Equal((HttpStatusCode.UnprocessableEntity, "CLOSURE_BLOCKER_EXISTS"), await undelivered.RefusalAsync());
            Assert.Equal(["DECISIONS_SETTLED NOT_ALLOWED", "SUSPENSION_REQUESTS_SETTLED NOT_ALLOWED"], await undelivered.ReadFieldErrorsAsync());
        }

        Assert.True(await host.DeliverAsync(run.Id));
        Assert.Equal("REJECTED", (await client.GetOrFailAsync(sessions.Officer, $"{ClosureDriver.SuspensionRequests}/{requestId}")).Text("status"));
        Assert.Equal("SUBMITTED", (await client.CommandOrFailAsync(sessions.EntityManager, ClosureDriver.CompletionCases, caseId, "submit")).Text("status"));
    }

    /// <summary>
    /// One case per unit of work: a case whose activation the database refuses at commit is rolled back and stays APPROVED, its project as it
    /// was, and the case approved after it is still activated in the same pass. Once the cause is gone, the next pass activates it.
    /// </summary>
    [Fact]
    public async Task ACaseThatCannotBeActivatedHoldsUpNoOther()
    {
        using HttpClient client = host.Api.CreateClient();
        ClosureSessions sessions = await client.SignInAsync();
        Guid stuckProject = await host.ProjectAsync();
        Guid otherProject = await host.ProjectAsync();
        Guid stuck = await host.ApprovedAsync(client, sessions, ClosureDriver.CompletionCases, ClosureDriver.CompletionBody(stuckProject));
        Guid other = await host.ApprovedAsync(client, sessions, ClosureDriver.CompletionCases, ClosureDriver.CompletionBody(otherProject));
        string refusal = $"refuse_{stuckProject:N}";
        await host.Database.ExecuteAsync($"""
            CREATE FUNCTION public.{refusal}() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RAISE EXCEPTION 'the database is refusing this project'; END $$;
            CREATE TRIGGER {refusal} BEFORE UPDATE ON project.project FOR EACH ROW WHEN (OLD.id = '{stuckProject}') EXECUTE FUNCTION public.{refusal}();
            """);
        try
        {
            Assert.Equal(1, await host.ActivateDueAsync());
        }
        finally
        {
            await host.Database.ExecuteAsync($"DROP TRIGGER {refusal} ON project.project; DROP FUNCTION public.{refusal}();");
        }

        Assert.Equal(("APPROVED", "ACTIVE"), ((await client.GetOrFailAsync(sessions.Officer, $"{ClosureDriver.CompletionCases}/{stuck}")).Text("status"), await host.LifecycleOfAsync(stuckProject)));
        Assert.Equal(("EFFECTED", "COMPLETED"), ((await client.GetOrFailAsync(sessions.Officer, $"{ClosureDriver.CompletionCases}/{other}")).Text("status"), await host.LifecycleOfAsync(otherProject)));

        Assert.Equal(1, await host.ActivateDueAsync());
        Assert.Equal("COMPLETED", await host.LifecycleOfAsync(stuckProject));
    }

    /// <summary>
    /// ActualProjectCompletionDate (ERD F-044; WF-10 §8): required to submit, never after today and never before the project's activation;
    /// once the case is effected it is the project's official completion date — not inferred from any task or progress date (BR-CLO-006).
    /// </summary>
    [Fact]
    public async Task TheActualCompletionDateIsCapturedAndChecked()
    {
        using HttpClient client = host.Api.CreateClient();
        ClosureSessions sessions = await client.SignInAsync();
        Guid projectId = await host.ProjectAsync();

        using (HttpResponseMessage future = await client.PostAsync(ClosureDriver.CompletionCases, sessions.EntityManager, ClosureDriver.CompletionBody(projectId, ClosureDriver.Today.AddDays(1))))
        {
            Assert.Equal((HttpStatusCode.UnprocessableEntity, "CLOSURE_COMPLETION_DATE_INVALID"), await future.RefusalAsync());
        }

        using (HttpResponseMessage beforeActivation = await client.PostAsync(ClosureDriver.CompletionCases, sessions.EntityManager, ClosureDriver.CompletionBody(projectId, ClosureDriver.Today.AddDays(-30))))
        {
            Assert.Equal((HttpStatusCode.UnprocessableEntity, "CLOSURE_COMPLETION_DATE_INVALID"), await beforeActivation.RefusalAsync());
            Assert.Equal(["actualProjectCompletionDate DATE_BEFORE_START"], await beforeActivation.ReadFieldErrorsAsync());
        }

        Guid caseId = await client.RaiseAsync(sessions.EntityManager, ClosureDriver.CompletionCases, new { projectId });
        using (HttpResponseMessage incomplete = await client.CommandAsync(sessions.EntityManager, ClosureDriver.CompletionCases, caseId, "submit"))
        {
            Assert.Equal((HttpStatusCode.UnprocessableEntity, "CLOSURE_INCOMPLETE"), await incomplete.RefusalAsync());
            Assert.Equal(["actualProjectCompletionDate REQUIRED"], await incomplete.ReadFieldErrorsAsync());
        }

        DateOnly completedOn = ClosureDriver.Today.AddDays(-3);
        object fields = new { actualProjectCompletionDate = ClosureDriver.Iso(completedOn), completionNarrative = ClosureDriver.Narrative("Practical completion certified.") };
        using (HttpResponseMessage read = await client.GetAsync($"{ClosureDriver.CompletionCases}/{caseId}", sessions.EntityManager))
        using (HttpResponseMessage edited = await client.PutAsync($"{ClosureDriver.CompletionCases}/{caseId}", sessions.EntityManager, fields, AdministrationApi.ETagOf(read)))
        {
            Assert.Equal(HttpStatusCode.OK, edited.StatusCode);
        }

        await client.WaiveFailedAsync(sessions, ClosureDriver.CompletionCases, caseId);
        await client.CommandOrFailAsync(sessions.EntityManager, ClosureDriver.CompletionCases, caseId, "submit");
        await client.CommandOrFailAsync(sessions.DepartmentManager, ClosureDriver.CompletionCases, caseId, "start-review");
        await host.DecideAndDeliverAsync("CompletionCase", caseId, ApprovalTaskDecision.Approve);
        JsonObject effected = await client.CommandOrFailAsync(sessions.Officer, ClosureDriver.CompletionCases, caseId, "activate");

        Assert.Equal(("EFFECTED", ClosureDriver.Iso(completedOn)), (effected.Text("status"), effected.Text("actualProjectCompletionDate")));
        Assert.Equal([ClosureDriver.Iso(completedOn)], await host.Database.QueryAsync($"SELECT actual_project_completion_date::text FROM closure.completion_case WHERE id = '{caseId}'"));
    }

    /// <summary>
    /// Completion and Closure are two distinct, sequential cases: a closure case is refused for an ACTIVE project, a second open completion
    /// case for the same project is refused 409, and a completion case is refused once the project is COMPLETED.
    /// </summary>
    [Fact]
    public async Task CompletionAndClosureAreDistinctSequentialCases()
    {
        using HttpClient client = host.Api.CreateClient();
        ClosureSessions sessions = await client.SignInAsync();
        Guid projectId = await host.ProjectAsync();

        using (HttpResponseMessage early = await client.PostAsync(ClosureDriver.ClosureCases, sessions.EntityManager, ClosureDriver.ClosureBody(projectId)))
        {
            Assert.Equal((HttpStatusCode.UnprocessableEntity, "CLOSURE_PROJECT_NOT_ELIGIBLE"), await early.RefusalAsync());
        }

        Guid first = await client.RaiseAsync(sessions.EntityManager, ClosureDriver.CompletionCases, ClosureDriver.CompletionBody(projectId));
        using (HttpResponseMessage second = await client.PostAsync(ClosureDriver.CompletionCases, sessions.Officer, ClosureDriver.CompletionBody(projectId)))
        {
            Assert.Equal((HttpStatusCode.Conflict, "CLOSURE_CASE_ALREADY_OPEN"), await second.RefusalAsync());
        }

        await client.CommandOrFailAsync(sessions.EntityManager, ClosureDriver.CompletionCases, first, "withdraw");
        await host.EffectedAsync(client, sessions, ClosureDriver.CompletionCases, ClosureDriver.CompletionBody(projectId));
        using (HttpResponseMessage again = await client.PostAsync(ClosureDriver.CompletionCases, sessions.EntityManager, ClosureDriver.CompletionBody(projectId)))
        {
            Assert.Equal((HttpStatusCode.UnprocessableEntity, "CLOSURE_PROJECT_NOT_ELIGIBLE"), await again.RefusalAsync());
        }

        // ADR-013: per-project access ends with the project's closure.
        Guid assignment = Guid.NewGuid();
        await host.Database.ExecuteAsync($"""
            INSERT INTO identity_access.access_relationship (id, user_id, permission_profile_version_id, project_id, starts_at, status, created_at, created_by, updated_at, updated_by)
            VALUES ('{assignment}', '{IdentityDatabase.UserId(6)}', '{IdentityDatabase.ProfileVersionId(6)}', '{projectId}', now() - interval '1 day', 'ACTIVE',
                    now(), '{IdentityDatabase.SeedPrincipalId}', now(), '{IdentityDatabase.SeedPrincipalId}')
            """);

        Guid closure = await host.EffectedAsync(client, sessions, ClosureDriver.ClosureCases, ClosureDriver.ClosureBody(projectId));
        JsonObject closed = await client.GetOrFailAsync(sessions.Officer, $"{ClosureDriver.ClosureCases}/{closure}");
        Assert.Equal(("COMPLETED", false), (closed.Text("outcome"), closed["completionCaseId"] is null));
        Assert.Equal("CLOSED", await host.LifecycleOfAsync(projectId));
        Assert.Equal(["ENDED PROJECT_CLOSED"], await host.Database.QueryAsync($"SELECT status || ' ' || end_reason FROM identity_access.access_relationship WHERE id = '{assignment}'"));
    }

    /// <summary>
    /// PostProjectObligation tracking: completion needs each open obligation owned and dated; the obligation outlives the completion —
    /// worked on while the project is COMPLETED — and closure is refused until every obligation is settled, the closure policy; once the
    /// project is CLOSED the obligation takes no write.
    /// </summary>
    [Fact]
    public async Task ObligationsStayOpenAfterCompletionUntilTheClosurePolicyIsMet()
    {
        using HttpClient client = host.Api.CreateClient();
        ClosureSessions sessions = await client.SignInAsync();
        Guid projectId = await host.ProjectAsync();
        Guid completionId = await client.RaiseAsync(sessions.EntityManager, ClosureDriver.CompletionCases, ClosureDriver.CompletionBody(projectId));
        Guid obligationId = await client.RaiseAsync(sessions.EntityManager, ClosureDriver.Obligations, new
        {
            completionCaseId = completionId,
            title = ClosureDriver.Narrative("Defects liability period"),
            description = ClosureDriver.Narrative("Twelve months of defect rectification by the contractor."),
        });

        JsonObject evaluated = await client.CommandOrFailAsync(sessions.EntityManager, ClosureDriver.CompletionCases, completionId, "evaluate-readiness");
        Assert.Contains("OBLIGATIONS_OWNED FAIL", evaluated.Checks());
        using (HttpResponseMessage hard = await client.CommandAsync(sessions.DepartmentManager, ClosureDriver.CompletionCases, completionId, "waive-check",
                   new { checkCode = "OBLIGATIONS_OWNED", reason = ClosureDriver.Narrative("Not waivable.") }))
        {
            Assert.Equal((HttpStatusCode.UnprocessableEntity, "CLOSURE_CHECK_NOT_WAIVABLE"), await hard.RefusalAsync());
        }

        await EditObligationAsync(client, sessions.EntityManager, obligationId, owner: ClosureDriver.Person(8), due: ClosureDriver.Today.AddDays(365));
        await client.WaiveFailedAsync(sessions, ClosureDriver.CompletionCases, completionId);
        await client.CommandOrFailAsync(sessions.EntityManager, ClosureDriver.CompletionCases, completionId, "submit");
        await client.CommandOrFailAsync(sessions.DepartmentManager, ClosureDriver.CompletionCases, completionId, "start-review");
        await host.DecideAndDeliverAsync("CompletionCase", completionId, ApprovalTaskDecision.Approve);
        await host.ActivateDueAsync();
        Assert.Equal("COMPLETED", await host.LifecycleOfAsync(projectId));

        // COMPLETED: the obligation remains, and is worked on.
        Assert.Equal("IN_PROGRESS", (await client.CommandOrFailAsync(sessions.EntityManager, ClosureDriver.Obligations, obligationId, "start")).Text("status"));
        Guid closureId = await client.RaiseAsync(sessions.EntityManager, ClosureDriver.ClosureCases, ClosureDriver.ClosureBody(projectId));
        using (HttpResponseMessage blocked = await client.CommandAsync(sessions.EntityManager, ClosureDriver.ClosureCases, closureId, "submit"))
        {
            Assert.Equal((HttpStatusCode.UnprocessableEntity, "CLOSURE_BLOCKER_EXISTS"), await blocked.RefusalAsync());
            Assert.Equal(["OBLIGATIONS_SATISFIED NOT_ALLOWED"], await blocked.ReadFieldErrorsAsync());
        }

        JsonObject satisfied = await client.CommandOrFailAsync(sessions.EntityManager, ClosureDriver.Obligations, obligationId, "satisfy");
        Assert.Equal("SATISFIED", satisfied.Text("status"));
        Assert.NotNull(satisfied["satisfiedAt"]);
        JsonObject submitted = await client.CommandOrFailAsync(sessions.EntityManager, ClosureDriver.ClosureCases, closureId, "submit");
        Assert.Equal("READY", submitted["readiness"]!.Text("status"));
        await client.CommandOrFailAsync(sessions.DepartmentManager, ClosureDriver.ClosureCases, closureId, "start-review");
        await host.DecideAndDeliverAsync("ClosureCase", closureId, ApprovalTaskDecision.Approve);
        await host.ActivateDueAsync();
        Assert.Equal("CLOSED", await host.LifecycleOfAsync(projectId));

        using HttpResponseMessage afterClosure = await client.CommandAsync(sessions.EntityManager, ClosureDriver.Obligations, obligationId, "cancel");
        Assert.Equal((HttpStatusCode.Conflict, "PROJECT_CLOSED"), await afterClosure.RefusalAsync());
    }

    /// <summary>
    /// WF-10 §9's terminal path: a SUSPENDED project that will not resume is closed without completion — no completion case, no actual
    /// completion date, outcome TERMINATED_WITHOUT_COMPLETION — its open suspension ended as PROJECT_CLOSED by WF-09, its history kept.
    /// </summary>
    [Fact]
    public async Task ASuspendedProjectIsClosedWithoutBeingCompleted()
    {
        using HttpClient client = host.Api.CreateClient();
        ClosureSessions sessions = await client.SignInAsync();
        Guid projectId = await host.ProjectAsync();
        Guid requestId = await client.RaiseAsync(sessions.EntityManager, ClosureDriver.SuspensionRequests, new
        {
            projectId,
            requestType = "SUSPEND",
            reason = ClosureDriver.Narrative("Funding withdrawn."),
            requestedEffectiveDate = ClosureDriver.Iso(ClosureDriver.Today),
        });
        await client.OkOrFailAsync(sessions.EntityManager, $"{ClosureDriver.SuspensionRequests}/{requestId}/submit");
        await client.OkOrFailAsync(sessions.DepartmentManager, $"{ClosureDriver.SuspensionRequests}/{requestId}/start-review");
        await host.DecideAndDeliverAsync("SuspensionRequest", requestId, ApprovalTaskDecision.Approve, "Suspension");
        await client.OkOrFailAsync(sessions.Officer, $"{ClosureDriver.SuspensionRequests}/{requestId}/activate");
        Assert.Equal("SUSPENDED", await host.LifecycleOfAsync(projectId));

        using (HttpResponseMessage completion = await client.PostAsync(ClosureDriver.CompletionCases, sessions.EntityManager, ClosureDriver.CompletionBody(projectId)))
        {
            Assert.Equal((HttpStatusCode.UnprocessableEntity, "CLOSURE_PROJECT_NOT_ELIGIBLE"), await completion.RefusalAsync());
        }

        Guid closureId = await host.EffectedAsync(client, sessions, ClosureDriver.ClosureCases, ClosureDriver.ClosureBody(projectId, "Stopped: the programme was cancelled."));

        JsonObject closure = await client.GetOrFailAsync(sessions.Officer, $"{ClosureDriver.ClosureCases}/{closureId}");
        Assert.Equal(("TERMINATED_WITHOUT_COMPLETION", true), (closure.Text("outcome"), closure["completionCaseId"] is null));
        Assert.Equal("CLOSED", await host.LifecycleOfAsync(projectId));
        Assert.Empty(await host.Database.QueryAsync($"SELECT id::text FROM closure.completion_case WHERE project_id = '{projectId}'"));
        Assert.Equal(["PROJECT_CLOSED"], await host.Database.QueryAsync($"SELECT end_reason FROM suspension.active_suspension WHERE project_id = '{projectId}' AND ended_at IS NOT NULL"));
        Assert.Equal($"Suspension.SuspensionEnded SERVICE {CloseoutServicePrincipal.Id}", (await host.AuditTrailAsync("Suspension", requestId))[^1]);
        Assert.Equal($"Project.ProjectClosed SERVICE {CloseoutServicePrincipal.Id}", (await host.AuditTrailAsync("Project", projectId))[^1]);
    }

    /// <summary>
    /// ADR-013: the entity Project Manager raises, evaluates and submits; review and activation are AHDA's and refused to them even with the
    /// grant, each refusal audited.
    /// </summary>
    [Fact]
    public async Task TheEntityRaisesAndAhdaReviewsAndActivates()
    {
        using HttpClient client = host.Api.CreateClient();
        ClosureSessions sessions = await client.SignInAsync();
        Guid projectId = await host.ProjectAsync();
        Guid caseId = await client.RaiseAsync(sessions.EntityManager, ClosureDriver.CompletionCases, ClosureDriver.CompletionBody(projectId));
        await client.WaiveFailedAsync(sessions, ClosureDriver.CompletionCases, caseId);
        await client.CommandOrFailAsync(sessions.EntityManager, ClosureDriver.CompletionCases, caseId, "submit");

        foreach (string command in new[] { "start-review", "activate" })
        {
            using HttpResponseMessage refused = await client.CommandAsync(sessions.EntityManager, ClosureDriver.CompletionCases, caseId, command);
            Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
        }

        Assert.Equal(2, (await host.AuditTrailAsync("Closure", caseId)).Count(e => e.StartsWith("Closure.AuthorityRefused", StringComparison.Ordinal)));
        Assert.Equal("SUBMITTED", (await client.GetOrFailAsync(sessions.Officer, $"{ClosureDriver.CompletionCases}/{caseId}")).Text("status"));
    }

    private static async Task EditObligationAsync(HttpClient client, string token, Guid obligationId, Guid owner, DateOnly due)
    {
        using HttpResponseMessage read = await client.GetAsync($"{ClosureDriver.Obligations}/{obligationId}", token);
        object fields = new
        {
            title = ClosureDriver.Narrative("Defects liability period"),
            description = ClosureDriver.Narrative("Twelve months of defect rectification by the contractor."),
            ownerUserId = owner,
            dueDate = ClosureDriver.Iso(due),
        };
        using HttpResponseMessage edited = await client.PutAsync($"{ClosureDriver.Obligations}/{obligationId}", token, fields, AdministrationApi.ETagOf(read));
        Assert.True(edited.StatusCode == HttpStatusCode.OK, $"PUT obligation: {(int)edited.StatusCode} {await edited.Content.ReadAsStringAsync()}");
    }
}
