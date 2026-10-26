using PMPlatform.Application.Features.Approval.Contracts;
using PMPlatform.Application.Features.FinancialKpi.Contracts;
using PMPlatform.Tests.Integration.Identity;

namespace PMPlatform.Tests.Integration.FinancialKpi;

/// <summary>
/// Migration <c>TASK-052_GuardFinancialKpiHistory</c>: the history holds for any writer, not only for the services. Each statement
/// is one a careless script could run; each is refused and changes nothing.
/// </summary>
[Collection(FinancialKpiSuite.Name)]
public sealed class FinancialKpiGuardTests(FinancialKpiTestHost host)
{
    [Fact]
    public async Task TheDatabaseHoldsTheVersionsUpdatesAndMeasurements()
    {
        using HttpClient client = host.Api.CreateClient();
        Sessions sessions = await client.SignInAsync();
        Guid projectId = await host.ProjectAsync();
        await host.PeriodsAsync(projectId, 1);
        Guid budget = await host.ActiveBudgetAsync(client, sessions, projectId, "750000.00");
        Guid draftBudget = AdministrationApi.IdOf(await client.CreatedOrFailAsync(sessions.ProjectManager, FinancialKpiDriver.Commitments, FinancialKpiDriver.Budget(projectId, "800000.00")));
        Guid update = await client.PublishedUpdateAsync(sessions, projectId, FinancialKpiDriver.Update("1000.00", null, "MEASURED"));
        Guid assignmentId = await client.AssignmentAsync(sessions, projectId, FinancialKpiTestHost.SafetyKpiId);
        Guid target = await host.ActiveTargetAsync(client, sessions, assignmentId, 95m, 95m, 90m);
        Guid measurement = AdministrationApi.IdOf(await client.PublishedMeasurementAsync(sessions, assignmentId, FinancialKpiDriver.Today.AddDays(-30), 97m));
        string before = await StateAsync(projectId);

        string?[] refusals =
        [
            // A version is born DRAFT and numbered after every other; an ACTIVE one only moves to SUPERSEDED; the end states are final.
            await host.RefusedAsync($"INSERT INTO financial_kpi.financial_commitment SELECT (jsonb_populate_record(NULL::financial_kpi.financial_commitment, to_jsonb(c) || jsonb_build_object('id', gen_random_uuid(), 'version_no', 9))).* FROM financial_kpi.financial_commitment c WHERE id = '{budget}'"),
            await host.RefusedAsync($"UPDATE financial_kpi.financial_commitment SET status = 'DRAFT' WHERE id = '{budget}'"),
            await host.RefusedAsync($"UPDATE financial_kpi.financial_commitment SET status = 'ACTIVE', activated_at = now() WHERE id = '{draftBudget}'"),
            await host.RefusedAsync($"DELETE FROM financial_kpi.financial_commitment WHERE id = '{budget}'"),
            await host.RefusedAsync($"UPDATE financial_kpi.financial_commitment SET version_no = 7 WHERE id = '{draftBudget}'"),
            // A published update and its period are final; only a DRAFT is deleted.
            await host.RefusedAsync($"UPDATE financial_kpi.financial_progress_update SET status = 'DRAFT' WHERE id = '{update}'"),
            await host.RefusedAsync($"DELETE FROM financial_kpi.financial_progress_update WHERE id = '{update}'"),
            // An assignment is never deleted nor moved to another KPI.
            await host.RefusedAsync($"DELETE FROM financial_kpi.kpi_assignment WHERE id = '{assignmentId}'"),
            await host.RefusedAsync($"UPDATE financial_kpi.kpi_assignment SET kpi_definition_id = '{FinancialKpiTestHost.ReworkKpiId}' WHERE id = '{assignmentId}'"),
            // An ACTIVE target is never edited and never deleted; a published measurement changes no more.
            await host.RefusedAsync($"UPDATE financial_kpi.kpi_target_version SET green_threshold = 1, amber_threshold = 0 WHERE id = '{target}'"),
            await host.RefusedAsync($"DELETE FROM financial_kpi.kpi_target_version WHERE id = '{target}'"),
            await host.RefusedAsync($"UPDATE financial_kpi.kpi_measurement SET measured_value = 50 WHERE id = '{measurement}'"),
            await host.RefusedAsync($"UPDATE financial_kpi.kpi_measurement SET period_start = period_start - 1 WHERE id = '{measurement}'"),
            await host.RefusedAsync($"DELETE FROM financial_kpi.kpi_measurement WHERE id = '{measurement}'"),
            await host.RefusedAsync("TRUNCATE financial_kpi.kpi_measurement"),
        ];

        Assert.All(refusals, Assert.NotNull);
        Assert.Equal(before, await StateAsync(projectId));
    }

    /// <summary>A measurement is born pinned to the ACTIVE target version of its own assignment, whatever writes it.</summary>
    [Fact]
    public async Task AMeasurementIsBornPinnedToItsAssignmentsActiveTarget()
    {
        using HttpClient client = host.Api.CreateClient();
        Sessions sessions = await client.SignInAsync();
        Guid projectId = await host.ProjectAsync();
        Guid first = await client.AssignmentAsync(sessions, projectId, FinancialKpiTestHost.SafetyKpiId);
        Guid second = await client.AssignmentAsync(sessions, projectId, FinancialKpiTestHost.ReworkKpiId);
        Guid target = await host.ActiveTargetAsync(client, sessions, first, 95m, 95m, 90m);
        Guid draftTarget = AdministrationApi.IdOf(await client.CreatedOrFailAsync(sessions.ProjectManager, FinancialKpiDriver.Targets,
            new { kpiAssignmentId = second, targetValue = 2m, greenThreshold = 2m, amberThreshold = 5m }));

        static string Insert(Guid assignment, Guid pinned) => $"""
            INSERT INTO financial_kpi.kpi_measurement (id, kpi_assignment_id, kpi_target_version_id, period_start, period_end, measured_value, value_status, rag_status,
                                                       as_of_date, recorded_by_user_id, status, created_at, created_by, updated_at, updated_by)
            VALUES (gen_random_uuid(), '{assignment}', '{pinned}', current_date, current_date, 1, 'MEASURED', 'RED', current_date, '{FinancialKpiDriver.Person(8)}', 'DRAFT',
                    now(), '{FinancialKpiDriver.Person(8)}', now(), '{FinancialKpiDriver.Person(8)}')
            """;

        Assert.Equal("ck_kpi_measurement_pinned_target", await host.RefusedAsync(Insert(second, target)));
        Assert.Equal("ck_kpi_measurement_pinned_target", await host.RefusedAsync(Insert(second, draftTarget)));
        Assert.Null(await host.RefusedAsync(Insert(first, target)));

        // A rejected target never becomes one a measurement can pin.
        await client.CommandOrFailAsync(sessions.ProjectManager, $"{FinancialKpiDriver.Targets}/{draftTarget}/submit");
        await host.DecideAndDeliverAsync(FinancialKpiApprovalRouting.TargetVersionType, draftTarget, ApprovalTaskDecision.Reject, "Not agreed");
        Assert.Equal("REJECTED", (await client.GetOrFailAsync(sessions.ProjectManager, $"{FinancialKpiDriver.Targets}/{draftTarget}")).Text("status"));
        Assert.Equal("ck_kpi_measurement_pinned_target", await host.RefusedAsync(Insert(second, draftTarget)));
    }

    private async Task<string> StateAsync(Guid projectId) => string.Join('\n', await host.Database.QueryAsync($"""
        SELECT to_jsonb(c)::text FROM financial_kpi.financial_commitment c WHERE project_id = '{projectId}'
        UNION ALL SELECT to_jsonb(u)::text FROM financial_kpi.financial_progress_update u WHERE project_id = '{projectId}'
        UNION ALL SELECT to_jsonb(a)::text FROM financial_kpi.kpi_assignment a WHERE project_id = '{projectId}'
        UNION ALL SELECT to_jsonb(t)::text FROM financial_kpi.kpi_target_version t JOIN financial_kpi.kpi_assignment a ON a.id = t.kpi_assignment_id WHERE a.project_id = '{projectId}'
        UNION ALL SELECT to_jsonb(m)::text FROM financial_kpi.kpi_measurement m JOIN financial_kpi.kpi_assignment a ON a.id = m.kpi_assignment_id WHERE a.project_id = '{projectId}'
        ORDER BY 1
        """));
}
