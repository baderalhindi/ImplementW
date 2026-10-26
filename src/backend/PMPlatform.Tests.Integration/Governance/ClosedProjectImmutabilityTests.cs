using System.Net;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using PMPlatform.Application.Features.Approval.Contracts;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.Project.Contracts;
using PMPlatform.Domain.Common;
using PMPlatform.Infrastructure.Persistence;
using PMPlatform.Tests.Integration.Closure;
using PMPlatform.Tests.Integration.Identity;
using OpenApiDocument = PMPlatform.Tests.Integration.Project.OpenApiDocument;

namespace PMPlatform.Tests.Integration.Governance;

/// <summary>
/// Invariant 3 (closure.md D-8, D-11): once a project is CLOSED, nothing writes to it. The project is closed the way WF-10 closes one —
/// a completion case and a closure case, each readiness-gated, decided through WF-11 and activated by WF-10's pass — after WF-08 and WF-09
/// have each left a record on it. Then every governance write the API documents is refused with the closed-project error, whatever state
/// its record is in; WF-09's and WF-10's lifecycle commands into Project change nothing; and the database refuses a direct write.
/// </summary>
/// <remarks>
/// <c>ClosedProjectWriteTests</c> sweeps every module's writes over a project closed by a fixture, and accepts any terminal-state error.
/// This test holds the governance modules to the closed-project error itself, on a project their own flows closed, so the refusal is the
/// project being closed and not the record's own state machine.
/// </remarks>
[Collection(ClosureSuite.Name)]
public sealed partial class ClosedProjectImmutabilityTests(ClosureTestHost host)
{
    private const string ChangeRequests = "/api/v1/change-requests";

    /// <summary>Governance operations sent as POST that write nothing, and why.</summary>
    private static readonly Dictionary<string, string> NotAWrite = new(StringComparer.Ordinal)
    {
        [$"POST {ChangeRequests}/{{changeRequestId}}/preview-materiality"] = "a read sent as POST (CHANGE_REQUEST_VIEW): it computes a preview and records nothing",
    };

    [Fact]
    public async Task AProjectClosedThroughWf10TakesNoGovernanceWriteByAnyPath()
    {
        using HttpClient client = host.Api.CreateClient();
        ClosureSessions sessions = await client.SignInAsync();
        Guid projectId = await host.ProjectAsync();

        // WF-08 and WF-09 leave a record each, settled as closure requires; WF-10 completes the project with an obligation, then closes it.
        Guid change = await client.RaiseAsync(sessions.EntityManager, ChangeRequests, ChangeRequestBody(projectId));
        await client.CommandOrFailAsync(sessions.EntityManager, ChangeRequests, change, "submit");
        await client.CommandOrFailAsync(sessions.EntityManager, ChangeRequests, change, "withdraw");
        Guid suspension = await client.RaiseAsync(sessions.EntityManager, ClosureDriver.SuspensionRequests, SuspensionRequestBody(projectId));
        await client.CommandOrFailAsync(sessions.EntityManager, ClosureDriver.SuspensionRequests, suspension, "submit");
        await client.CommandOrFailAsync(sessions.EntityManager, ClosureDriver.SuspensionRequests, suspension, "withdraw");

        Guid completion = await client.RaiseAsync(sessions.EntityManager, ClosureDriver.CompletionCases, ClosureDriver.CompletionBody(projectId));
        Guid obligation = await client.RaiseAsync(sessions.EntityManager, ClosureDriver.Obligations, ObligationBody(completion));
        await client.WaiveFailedAsync(sessions, ClosureDriver.CompletionCases, completion);
        await client.CommandOrFailAsync(sessions.EntityManager, ClosureDriver.CompletionCases, completion, "submit");
        await client.CommandOrFailAsync(sessions.DepartmentManager, ClosureDriver.CompletionCases, completion, "start-review");
        await host.DecideAndDeliverAsync("CompletionCase", completion, ApprovalTaskDecision.Approve);
        await host.ActivateDueAsync();
        await client.CommandOrFailAsync(sessions.EntityManager, ClosureDriver.Obligations, obligation, "satisfy");
        Guid closure = await host.EffectedAsync(client, sessions, ClosureDriver.ClosureCases, ClosureDriver.ClosureBody(projectId));
        Assert.Equal("CLOSED", await host.LifecycleOfAsync(projectId));

        string before = await DigestAsync(projectId);
        DateTimeOffset since = DateTimeOffset.UtcNow;
        Dictionary<string, Guid> records = new(StringComparer.Ordinal)
        {
            ["change-requests"] = change,
            ["suspension-requests"] = suspension,
            ["completion-cases"] = completion,
            ["closure-cases"] = closure,
            ["post-project-obligations"] = obligation,
        };

        // Every governance write the document lists, sent by AHDA's officer, who holds every permission, with a valid body.
        Dictionary<string, object?> bodies = Bodies(projectId, completion);
        OpenApiDocument document = await OpenApiDocument.FetchAsync(client);
        string[] writes = [.. GovernanceApi.Tags.SelectMany(document.Writes).Where(w => !NotAWrite.ContainsKey(w)).Order(StringComparer.Ordinal)];
        Assert.Equal(writes, bodies.Keys.Order(StringComparer.Ordinal));

        List<string> notRefused = [];
        foreach (string write in writes)
        {
            (string method, string template) = (write.Split(' ')[0], write.Split(' ')[1]);
            string path = Parameter().Replace(template, m => records[CollectionOf(template, m.Value)].ToString());
            string? ifMatch = method == "PUT" ? await ETagAsync(client, sessions.Officer, path) : null;
            Answer answer = await GovernanceApi.AnswerAsync(client.SendAsync(new HttpMethod(method), path, sessions.Officer, bodies[write], ifMatch));
            if ((answer.Status, answer.Code) != (HttpStatusCode.Conflict, "PROJECT_CLOSED"))
            {
                notRefused.Add($"{write}: {answer} {answer.Body}");
            }
        }

        Assert.True(notRefused.Count == 0, $"A governance write to a closed project was not refused 409 PROJECT_CLOSED:\n  {string.Join("\n  ", notRefused)}");

        // WF-09's and WF-10's commands into Project's lifecycle (edges 7 and 8) are refused, and stage nothing.
        int written = await host.WithScopeAsync(async services =>
        {
            IProjectSuspensionCommands suspensions = services.GetRequiredService<IProjectSuspensionCommands>();
            IProjectCloseoutCommands closeouts = services.GetRequiredService<IProjectCloseoutCommands>();
            ProjectSuspensionCommand bySuspension = new(projectId, ClosureDriver.Person(2), AuditActorType.User, suspension);
            ProjectCloseoutCommand byCloseout = new(projectId, ClosureDriver.Person(2), AuditActorType.User, closure);
            AdministrationError?[] refusals =
            [
                await suspensions.SuspendAsync(bySuspension, CancellationToken.None), await suspensions.ResumeAsync(bySuspension, CancellationToken.None),
                await closeouts.CompleteAsync(byCloseout, CancellationToken.None), await closeouts.CloseAsync(byCloseout, CancellationToken.None),
            ];
            Assert.All(refusals, Assert.NotNull);
            return await services.GetRequiredService<PMPlatformDbContext>().SaveChangesAsync();
        });
        Assert.Equal(0, written);

        // Nor does a direct write to the database.
        Assert.NotNull(await host.RefusedAsync($"UPDATE project.project SET title = 'Reopened' WHERE id = '{projectId}'"));

        Assert.Equal(before, await DigestAsync(projectId));
        Assert.Equal("CLOSED", await host.LifecycleOfAsync(projectId));
        Assert.Empty(await host.Database.QueryAsync($"""
            SELECT event_type FROM audit_activity.audit_event
            WHERE scope_project_id = '{projectId}' AND outcome = 'SUCCESS' AND event_class IN ('DATA_CHANGE', 'LIFECYCLE_TRANSITION') AND occurred_at >= '{since.UtcDateTime:O}'
            """));
    }

    /// <summary>A valid body for each governance write, by <c>METHOD path</c>; null where the operation takes none.</summary>
    private static Dictionary<string, object?> Bodies(Guid projectId, Guid completionCaseId)
    {
        object narrative = ClosureDriver.Narrative("Written after closure.");
        string today = ClosureDriver.Iso(ClosureDriver.Today);
        Dictionary<string, object?> bodies = new(StringComparer.Ordinal)
        {
            [$"POST {ChangeRequests}"] = ChangeRequestBody(projectId),
            [$"PUT {ChangeRequests}/{{changeRequestId}}"] = new
            {
                title = narrative,
                justification = narrative,
                costImpactSar = (string?)null,
                scheduleImpactDays = (int?)null,
                scopeImpact = narrative,
                isContractualObligation = false,
                requestedGovernanceProfileItemId = (Guid?)null,
            },
            [$"DELETE {ChangeRequests}/{{changeRequestId}}"] = null,
            [$"POST {ClosureDriver.SuspensionRequests}"] = SuspensionRequestBody(projectId),
            [$"PUT {ClosureDriver.SuspensionRequests}/{{suspensionRequestId}}"] = new { reason = narrative, requestedEffectiveDate = today, plannedResumptionDate = (string?)null },
            [$"DELETE {ClosureDriver.SuspensionRequests}/{{suspensionRequestId}}"] = null,
            [$"POST {ClosureDriver.CompletionCases}"] = ClosureDriver.CompletionBody(projectId),
            [$"PUT {ClosureDriver.CompletionCases}/{{caseId}}"] = new { actualProjectCompletionDate = today, completionNarrative = narrative },
            [$"DELETE {ClosureDriver.CompletionCases}/{{caseId}}"] = null,
            [$"POST {ClosureDriver.ClosureCases}"] = ClosureDriver.ClosureBody(projectId),
            [$"PUT {ClosureDriver.ClosureCases}/{{caseId}}"] = new { closureNarrative = narrative },
            [$"DELETE {ClosureDriver.ClosureCases}/{{caseId}}"] = null,
            [$"POST {ClosureDriver.Obligations}"] = ObligationBody(completionCaseId),
            [$"PUT {ClosureDriver.Obligations}/{{obligationId}}"] = new { title = narrative, description = narrative, ownerUserId = ClosureDriver.Person(8), dueDate = today },
        };
        foreach (string command in new[] { "submit", "withdraw", "start-review", "start-implementation", "mark-implemented", "close" })
        {
            bodies[$"POST {ChangeRequests}/{{changeRequestId}}/{command}"] = null;
        }

        foreach (string command in new[] { "submit", "withdraw", "start-review", "activate" })
        {
            bodies[$"POST {ClosureDriver.SuspensionRequests}/{{suspensionRequestId}}/{command}"] = null;
        }

        foreach (string collection in new[] { ClosureDriver.CompletionCases, ClosureDriver.ClosureCases })
        {
            foreach (string command in new[] { "evaluate-readiness", "submit", "withdraw", "start-review", "activate" })
            {
                bodies[$"POST {collection}/{{caseId}}/{command}"] = null;
            }

            bodies[$"POST {collection}/{{caseId}}/waive-check"] = new { checkCode = "PROGRESS_REPORTED", reason = narrative };
        }

        foreach (string command in new[] { "start", "satisfy", "cancel", "waive" })
        {
            bodies[$"POST {ClosureDriver.Obligations}/{{obligationId}}/{command}"] = null;
        }

        return bodies;
    }

    private static object ChangeRequestBody(Guid projectId) => new
    {
        projectId,
        changeType = "SCOPE",
        title = ClosureDriver.Narrative("Add a loading bay"),
        justification = ClosureDriver.Narrative("The operator asked for one."),
        costImpactSar = (string?)null,
        scheduleImpactDays = (int?)null,
        scopeImpact = ClosureDriver.Narrative("One more loading bay."),
        isContractualObligation = false,
        requestedGovernanceProfileItemId = (Guid?)null,
    };

    private static object SuspensionRequestBody(Guid projectId) => new
    {
        projectId,
        requestType = "SUSPEND",
        reason = ClosureDriver.Narrative("Funding withheld pending the board's review."),
        requestedEffectiveDate = ClosureDriver.Iso(ClosureDriver.Today),
        plannedResumptionDate = (string?)null,
    };

    private static object ObligationBody(Guid completionCaseId) => new
    {
        completionCaseId,
        closureCaseId = (Guid?)null,
        title = ClosureDriver.Narrative("Defects liability period"),
        description = ClosureDriver.Narrative("Twelve months of defect rectification by the contractor."),
        ownerUserId = ClosureDriver.Person(8),
        dueDate = ClosureDriver.Iso(ClosureDriver.Today.AddDays(365)),
    };

    /// <summary>The collection a path parameter follows, which names the record it addresses.</summary>
    private static string CollectionOf(string template, string parameter)
    {
        string[] segments = template.Split('/');
        return segments[Array.IndexOf(segments, parameter) - 1];
    }

    private static async Task<string> ETagAsync(HttpClient client, string token, string path)
    {
        using HttpResponseMessage read = await client.GetAsync(path, token);
        Assert.True(read.StatusCode == HttpStatusCode.OK, $"GET {path}: {(int)read.StatusCode}");
        return AdministrationApi.ETagOf(read);
    }

    /// <summary>
    /// A digest of every governance row of the project — its change requests with their evaluations and authorisations, its suspension
    /// requests and periods, its cases with their readiness records, its obligations — and of the project row, row versions included.
    /// </summary>
    private async Task<string> DigestAsync(Guid projectId)
    {
        string cases = $"SELECT id FROM closure.completion_case WHERE project_id = '{projectId}' UNION ALL SELECT id FROM closure.closure_case WHERE project_id = '{projectId}'";
        string changes = $"SELECT id FROM change_request.change_request WHERE project_id = '{projectId}'";
        (string Table, string Where)[] rows =
        [
            ("project.project", $"t.id = '{projectId}'"),
            ("change_request.change_request", $"t.project_id = '{projectId}'"),
            ("change_request.materiality_evaluation", $"t.change_request_id IN ({changes})"),
            ("change_request.change_authorization", $"t.change_request_id IN ({changes})"),
            ("suspension.suspension_request", $"t.project_id = '{projectId}'"),
            ("suspension.active_suspension", $"t.project_id = '{projectId}'"),
            ("closure.completion_case", $"t.project_id = '{projectId}'"),
            ("closure.closure_case", $"t.project_id = '{projectId}'"),
            ("closure.readiness_check", $"t.completion_case_id IN ({cases}) OR t.closure_case_id IN ({cases})"),
            ("closure.post_project_obligation", $"t.project_id = '{projectId}'"),
        ];
        string union = string.Join(" UNION ALL ", rows.Select(r =>
            $"SELECT '{r.Table}:' || t.id AS k, (to_jsonb(t) || jsonb_build_object('xmin', t.xmin::text))::text AS v FROM {r.Table} t WHERE {r.Where}"));
        return Assert.Single(await host.Database.QueryAsync($"SELECT count(*) || ' ' || md5(string_agg(k || '=' || v, '|' ORDER BY k)) FROM ({union}) x"));
    }

    [GeneratedRegex(@"\{[A-Za-z]+\}", RegexOptions.CultureInvariant)]
    private static partial Regex Parameter();
}
