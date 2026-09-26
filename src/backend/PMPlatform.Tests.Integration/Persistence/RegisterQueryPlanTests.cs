using System.Globalization;
using System.Text.Json;
using Xunit.Abstractions;
using static PMPlatform.Tests.Integration.Persistence.RepresentativeVolume;

namespace PMPlatform.Tests.Integration.Persistence;

/// <summary>
/// TASK-026 validation (<c>indexing-strategy.md</c> §5): every register screen's default query, and its
/// <c>totalCount</c> where the screen is offset-paged (R-29), is planned with <c>EXPLAIN ANALYZE</c> at representative
/// volume. The plan must use an index and contain no sequential scan except of a table small enough that reading it
/// whole is the right plan (<see cref="SmallTablePages"/>, P-8), and the execution time must stay under
/// <see cref="CeilingMilliseconds"/>. Each query's time is written to the output beside its recorded baseline in
/// <c>register-query-baseline.csv</c>, which is how a regression is tracked.
/// </summary>
public sealed class RegisterQueryPlanTests(RepresentativeVolume volume, ITestOutputHelper output) : IClassFixture<RepresentativeVolume>
{
    /// <summary>
    /// Every baseline but the one known full scan is under 5 ms. A query over 50 ms on any runner has lost its index
    /// path, not met a slow machine; 50 ms is also half the threshold at which P-7 revisits free-text search.
    /// </summary>
    private const double CeilingMilliseconds = 50;

    /// <summary>P-8: 16 pages is 128 KB. A table that small is read whole faster than through any index.</summary>
    private const int SmallTablePages = 16;

    /// <summary>
    /// Queries whose full scan no index can remove, each with the finding that records why and what removes it. The test
    /// fails if one of them stops scanning, so the list cannot outlive the reason.
    /// </summary>
    private static readonly Dictionary<string, string> KnownFullScans = new()
    {
        ["SCR-081 critical count"] = "indexing-strategy.md F-3: risk does not store its current rating",
    };

    private const string InDepartment = $"project_id IN (SELECT id FROM project.project WHERE department_id = '{DepartmentId}')";

    private const string OpenTask = "status IN ('NOT_STARTED', 'IN_PROGRESS', 'BLOCKED')";

    private const string CurrentCriticalRating = $"""
        FROM plan_review.risk_assessment_version v JOIN plan_review.risk r ON r.id = v.risk_id
        WHERE v.risk_rating_definition_id = '{CriticalRatingId}' AND r.status <> 'CLOSED'
          AND NOT EXISTS (SELECT 1 FROM plan_review.risk_assessment_version later WHERE later.risk_id = v.risk_id AND later.version_no > v.version_no)
        """;

    private static readonly Lazy<Dictionary<string, double>> Baseline = new(() => File.ReadLines(RepositoryFile.Path("docs/architecture/register-query-baseline.csv"))
        .Skip(1)
        .Select(line => line.Split(','))
        .ToDictionary(fields => fields[0], fields => double.Parse(fields[^1], CultureInfo.InvariantCulture)));

    public static TheoryData<string, string> DefaultQueries() => new()
    {
        // WF-01 projects, FG-03 identity and FG-04 configuration: the real core tables.
        { "SCR-025 ALL page", "SELECT id, formal_project_id, title, lifecycle_state, updated_at FROM project.project ORDER BY updated_at DESC, id DESC LIMIT 25" },
        { "SCR-025 ALL page 11", "SELECT id, formal_project_id, title, lifecycle_state, updated_at FROM project.project ORDER BY updated_at DESC, id DESC LIMIT 25 OFFSET 250" },
        { "SCR-025 ALL count", "SELECT count(*) FROM project.project" },
        { "SCR-025 DEPT page", $"SELECT id, title FROM project.project WHERE department_id = '{DepartmentId}' ORDER BY updated_at DESC, id DESC LIMIT 25" },
        { "SCR-025 DEPT count", $"SELECT count(*) FROM project.project WHERE department_id = '{DepartmentId}'" },
        { "SCR-025 ENTITY page", $"SELECT id, title FROM project.project WHERE external_entity_id = '{EntityId}' ORDER BY updated_at DESC, id DESC LIMIT 25" },
        { "SCR-025 ENTITY count", $"SELECT count(*) FROM project.project WHERE external_entity_id = '{EntityId}'" },
        { "SCR-025 state filter page", "SELECT id, title FROM project.project WHERE lifecycle_state = 'ACTIVE' ORDER BY updated_at DESC, id DESC LIMIT 25" },
        { "SCR-025 state filter count", "SELECT count(*) FROM project.project WHERE lifecycle_state = 'ACTIVE'" },
        { "SCR-026 OWN page", $"SELECT id, title FROM project.project WHERE project_manager_user_id = '{UserId}' ORDER BY updated_at DESC, id DESC LIMIT 25" },
        { "SCR-026 OWN count", $"SELECT count(*) FROM project.project WHERE project_manager_user_id = '{UserId}'" },
        { "Data scope: caller's active grants", $"SELECT department_id, external_entity_id, project_id FROM identity_access.access_relationship WHERE user_id = '{UserId}' AND status = 'ACTIVE'" },
        { "ADM-002 user list page", "SELECT id, display_name FROM identity_access.\"user\" WHERE status = 'ACTIVE' ORDER BY display_name, id LIMIT 25" },
        { "ADM-020 catalogue items", $"SELECT id, code, label_en FROM master_data_config.master_data_item WHERE catalogue_id = '{CatalogueId}' AND lifecycle_state = 'PUBLISHED' ORDER BY sort_order, id" },
        { "FG-04 configuration as of now", $"SELECT id FROM master_data_config.configuration_version WHERE configuration_family_id = '{ConfigurationFamilyId}' AND lifecycle_state = 'PUBLISHED' AND effective_from <= now() ORDER BY effective_from DESC LIMIT 1" },

        // WF-06 risk.
        { "SCR-080 ALL page", "SELECT id, title, status, next_review_date, updated_at FROM plan_review.risk ORDER BY updated_at DESC, id DESC LIMIT 25" },
        { "SCR-080 ALL count", "SELECT count(*) FROM plan_review.risk" },
        { "SCR-080 DEPT page", $"SELECT id, title, status FROM plan_review.risk WHERE {InDepartment} ORDER BY updated_at DESC, id DESC LIMIT 25" },
        { "SCR-080 DEPT count", $"SELECT count(*) FROM plan_review.risk WHERE {InDepartment}" },
        { "SCR-080 project tab page", $"SELECT id, title, status FROM plan_review.risk WHERE project_id = '{ProjectId}' ORDER BY updated_at DESC, id DESC LIMIT 25" },
        { "SCR-081 critical page", $"SELECT r.id, r.title {CurrentCriticalRating} ORDER BY r.updated_at DESC, r.id DESC LIMIT 25" },
        { "SCR-081 critical count", $"SELECT count(*) {CurrentCriticalRating}" },

        // WF-07 issues, challenges and escalations.
        { "SCR-083 ALL page", "SELECT id, title, status FROM plan_review.management_concern WHERE concern_type = 'ISSUE' ORDER BY updated_at DESC, id DESC LIMIT 25" },
        { "SCR-083 ALL count", "SELECT count(*) FROM plan_review.management_concern WHERE concern_type = 'ISSUE'" },
        { "SCR-083 DEPT page", $"SELECT id, title, status FROM plan_review.management_concern WHERE concern_type = 'ISSUE' AND {InDepartment} ORDER BY updated_at DESC, id DESC LIMIT 25" },
        { "SCR-083 DEPT count", $"SELECT count(*) FROM plan_review.management_concern WHERE concern_type = 'ISSUE' AND {InDepartment}" },
        { "SCR-085 ALL page", "SELECT id, title, status FROM plan_review.management_concern WHERE concern_type = 'CHALLENGE' ORDER BY updated_at DESC, id DESC LIMIT 25" },
        { "SCR-085 ALL count", "SELECT count(*) FROM plan_review.management_concern WHERE concern_type = 'CHALLENGE'" },
        { "SCR-087 open page", "SELECT id, management_concern_id, escalated_at FROM plan_review.concern_escalation WHERE status = 'OPEN' ORDER BY escalated_at DESC, id DESC LIMIT 25" },
        { "SCR-087 open count", "SELECT count(*) FROM plan_review.concern_escalation WHERE status = 'OPEN'" },

        // WF-11 approvals.
        { "SCR-100 personal page", $"SELECT id, approval_instance_id, due_at FROM plan_review.approval_task WHERE assigned_user_id = '{UserId}' AND status = 'PENDING' ORDER BY due_at, id LIMIT 25" },
        { "SCR-100 personal count", $"SELECT count(*) FROM plan_review.approval_task WHERE assigned_user_id = '{UserId}' AND status = 'PENDING'" },
        { "SCR-100 role queue page", "SELECT id, approval_instance_id, due_at FROM plan_review.approval_task WHERE assigned_role_id = '00000000-0001-4000-8000-000000000003' AND assigned_user_id IS NULL AND status = 'PENDING' ORDER BY due_at, id LIMIT 25" },
        { "SCR-101 page", $"SELECT id, status, requested_at FROM plan_review.approval_instance WHERE requested_by_user_id = '{UserId}' ORDER BY requested_at DESC, id DESC LIMIT 25" },
        { "SCR-101 count", $"SELECT count(*) FROM plan_review.approval_instance WHERE requested_by_user_id = '{UserId}'" },
        { "SCR-114 page", $"SELECT id, delegator_user_id, valid_to FROM plan_review.approval_delegation WHERE delegate_user_id = '{UserId}' AND status = 'ACTIVE' ORDER BY valid_to" },

        // WF-08 change requests, WF-09 suspension.
        { "SCR-105 ALL page", "SELECT id, title, status FROM plan_review.change_request ORDER BY updated_at DESC, id DESC LIMIT 25" },
        { "SCR-105 ALL count", "SELECT count(*) FROM plan_review.change_request" },
        { "SCR-105 project page", $"SELECT id, title, status FROM plan_review.change_request WHERE project_id = '{ProjectId}' ORDER BY updated_at DESC, id DESC LIMIT 25" },
        { "SCR-108 ALL page", "SELECT id, request_type, status FROM plan_review.suspension_request ORDER BY updated_at DESC, id DESC LIMIT 25" },
        { "SCR-108 ALL count", "SELECT count(*) FROM plan_review.suspension_request" },

        // WF-12 documents.
        { "SCR-120 ALL page", "SELECT id, title, status FROM plan_review.document ORDER BY updated_at DESC, id DESC LIMIT 25" },
        { "SCR-120 ALL count", "SELECT count(*) FROM plan_review.document" },
        { "SCR-121 project page", $"SELECT id, title, status FROM plan_review.document WHERE project_id = '{ProjectId}' ORDER BY updated_at DESC, id DESC LIMIT 25" },

        // WF-04 tasks, WF-03/WF-05 milestones.
        { "SCR-047 project tasks page", $"SELECT id, title, status, planned_finish_date FROM plan_review.project_task WHERE project_id = '{ProjectId}' AND {OpenTask} ORDER BY planned_finish_date, id LIMIT 25" },
        { "SCR-063 my tasks page", $"SELECT id, title, status, planned_finish_date FROM plan_review.project_task WHERE assignee_user_id = '{UserId}' AND {OpenTask} ORDER BY planned_finish_date, id LIMIT 25" },
        { "SCR-063 my tasks count", $"SELECT count(*) FROM plan_review.project_task WHERE assignee_user_id = '{UserId}' AND {OpenTask}" },
        { "SCR-065 my overdue page", $"SELECT id, title, planned_finish_date FROM plan_review.project_task WHERE assignee_user_id = '{UserId}' AND {OpenTask} AND planned_finish_date < date '2026-09-26' ORDER BY planned_finish_date, id LIMIT 25" },
        { "SCR-046 project milestones", $"SELECT id, title, status, forecast_date FROM plan_review.project_milestone WHERE project_id = '{ProjectId}' ORDER BY forecast_date, id" },
        { "SCR-062 register page", "SELECT id, title, forecast_date FROM plan_review.project_milestone WHERE status = 'PLANNED' AND forecast_date >= date '2026-09-26' ORDER BY forecast_date, id LIMIT 25" },
        { "SCR-062 register count", "SELECT count(*) FROM plan_review.project_milestone WHERE status = 'PLANNED' AND forecast_date >= date '2026-09-26'" },

        // WF-02 progress, WF-14 financial and KPI, FG-01 projections.
        { "SCR-070 progress history", $"SELECT id, status, submitted_at FROM plan_review.progress_submission WHERE project_id = '{ProjectId}' ORDER BY submitted_at DESC, id DESC LIMIT 25" },
        { "SCR-071 financial progress", $"SELECT id, status, as_of_date FROM plan_review.financial_progress_update WHERE project_id = '{ProjectId}' ORDER BY as_of_date DESC, id DESC LIMIT 25" },
        { "SCR-072 project KPIs", $"SELECT id, kpi_definition_id, status FROM plan_review.kpi_assignment WHERE project_id = '{ProjectId}'" },
        { "SCR-073 KPI history", $"SELECT period_start, value_status, rag_status FROM plan_review.kpi_measurement WHERE kpi_assignment_id = {KpiAssignmentId} ORDER BY period_start DESC" },
        { "FG-01 latest progress snapshot", $"SELECT id, overall_health, actual_percent FROM plan_review.published_progress_snapshot WHERE project_id = '{ProjectId}' ORDER BY published_at DESC LIMIT 1" },
        { "FG-01 latest financial snapshot", $"SELECT id, financial_status FROM plan_review.published_financial_snapshot WHERE project_id = '{ProjectId}' ORDER BY as_of_date DESC LIMIT 1" },

        // WF-13 external participation.
        { "SCR-160 project requests", $"SELECT id, status, issued_at FROM plan_review.external_update_request WHERE project_id = '{ProjectId}' ORDER BY issued_at DESC, id DESC LIMIT 25" },
        { "SCR-163 entity open page", $"SELECT id, project_id, due_date FROM plan_review.external_update_request WHERE external_entity_id = '{EntityId}' AND status IN ('ISSUED', 'IN_PROGRESS') ORDER BY due_date, id LIMIT 25" },
        { "SCR-163 entity open count", $"SELECT count(*) FROM plan_review.external_update_request WHERE external_entity_id = '{EntityId}' AND status IN ('ISSUED', 'IN_PROGRESS')" },
        { "SCR-164 my contributions", $"SELECT id, status, submitted_at FROM plan_review.external_contribution WHERE contributor_user_id = '{UserId}' ORDER BY submitted_at DESC, id DESC LIMIT 25" },
        { "SCR-165 review queue page", "SELECT id, project_id, submitted_at FROM plan_review.external_contribution WHERE status = 'SUBMITTED' ORDER BY submitted_at, id LIMIT 25" },
        { "SCR-165 review queue count", "SELECT count(*) FROM plan_review.external_contribution WHERE status = 'SUBMITTED'" },
        { "SCR-166 conflicts page", "SELECT id, external_contribution_id, attempted_at FROM plan_review.source_application WHERE status = 'CONFLICT' ORDER BY attempted_at DESC LIMIT 25" },

        // WF-15 notifications, cursor-paged (R-30).
        { "SCR-150 first page", $"SELECT id, rendered_subject, read_at, created_at FROM plan_review.notification_delivery WHERE recipient_user_id = '{UserId}' AND channel = 'IN_APP' ORDER BY created_at DESC, id DESC LIMIT 25" },
        { "SCR-150 next page", $"SELECT id, rendered_subject, read_at, created_at FROM plan_review.notification_delivery WHERE recipient_user_id = '{UserId}' AND channel = 'IN_APP' AND (created_at, id) < (timestamptz '2025-12-01', 'ffffffff-ffff-4fff-bfff-ffffffffffff') ORDER BY created_at DESC, id DESC LIMIT 25" },
        { "SCR-150 unread badge", $"SELECT count(*) FROM plan_review.notification_delivery WHERE recipient_user_id = '{UserId}' AND channel = 'IN_APP' AND read_at IS NULL" },

        // FG-06 activity and audit, cursor-paged (R-30).
        { "SCR-057 activity first page", $"SELECT id, activity_kind, summary_en, occurred_at FROM plan_review.business_activity_entry WHERE project_id = '{ProjectId}' ORDER BY occurred_at DESC, id DESC LIMIT 25" },
        { "SCR-058 audit first page", $"SELECT id, event_type, outcome, occurred_at FROM plan_review.audit_event WHERE scope_project_id = '{ProjectId}' ORDER BY occurred_at DESC, id DESC LIMIT 25" },
        { "ADM-050 audit trail first page", "SELECT id, event_type, outcome, occurred_at FROM plan_review.audit_event ORDER BY occurred_at DESC, id DESC LIMIT 25" },
        { "ADM-050 by actor first page", $"SELECT id, event_type, outcome, occurred_at FROM plan_review.audit_event WHERE actor_user_id = '{UserId}' ORDER BY occurred_at DESC LIMIT 25" },

        // FG-02 reports.
        { "SCR-139 my saved views", $"SELECT id, name, view_type FROM plan_review.saved_view WHERE owner_user_id = '{UserId}' ORDER BY view_type" },
        { "SCR-140 export history page", $"SELECT id, status, requested_at FROM plan_review.report_job WHERE requested_by_user_id = '{UserId}' ORDER BY requested_at DESC, id DESC LIMIT 25" },
        { "SCR-140 export history count", $"SELECT count(*) FROM plan_review.report_job WHERE requested_by_user_id = '{UserId}'" },
    };

    [Theory]
    [MemberData(nameof(DefaultQueries))]
    public async Task EveryRegisterQueryIsServedByAnIndex(string query, string sql)
    {
        string plan = (await volume.Database.QueryAsync($"EXPLAIN (ANALYZE, FORMAT JSON) {sql}"))[0];
        JsonElement root = JsonDocument.Parse(plan).RootElement[0];
        List<PlanNode> nodes = QueryPlan.Nodes(root.GetProperty("Plan"));
        double milliseconds = root.GetProperty("Execution Time").GetDouble();
        string indexes = string.Join(" + ", nodes.Where(n => n.IndexName is not null).Select(n => n.IndexName).Distinct());

        // The first line is a row of register-query-baseline.csv, so a new baseline is the output of one run.
        output.WriteLine(string.Create(CultureInfo.InvariantCulture, $"{query},{indexes},{milliseconds:0.000}"));
        output.WriteLine(Baseline.Value.TryGetValue(query, out double baseline)
            ? string.Create(CultureInfo.InvariantCulture, $"baseline {baseline:0.000} ms; this run {milliseconds / baseline:0.0}x")
            : "no baseline");
        output.WriteLine(string.Join("; ", nodes.Select(n => $"{n.NodeType} {n.RelationName} {n.IndexName}".Trim())));

        List<string> largeScans = [];
        foreach (PlanNode scan in nodes.Where(n => n.NodeType == "Seq Scan"))
        {
            int pages = int.Parse((await volume.Database.QueryAsync(
                $"SELECT max(relpages)::text FROM pg_class WHERE relkind = 'r' AND relname = '{scan.RelationName}'"))[0], CultureInfo.InvariantCulture);
            output.WriteLine($"Seq Scan {scan.RelationName}: {pages} pages");
            if (pages > SmallTablePages)
            {
                largeScans.Add(scan.RelationName!);
            }
        }

        if (KnownFullScans.TryGetValue(query, out string? reason))
        {
            Assert.True(largeScans.Count > 0, $"{query} no longer scans a table in full; remove it from KnownFullScans ({reason}).");
        }
        else
        {
            Assert.Empty(largeScans);
            Assert.True(indexes.Length > 0 || nodes.Any(n => n.NodeType == "Seq Scan"), $"{query} reads no index and no small table.");
        }

        Assert.True(milliseconds < CeilingMilliseconds, $"{query}: {milliseconds:0.000} ms, ceiling {CeilingMilliseconds} ms");
    }

    /// <summary>Every query has a recorded baseline and every baseline row is a query, so the record stays complete.</summary>
    [Fact]
    public void EveryQueryHasABaseline()
    {
        List<string> queries = [.. DefaultQueries().Select(row => (string)row[0])];

        Assert.Empty(queries.Except(Baseline.Value.Keys));
        Assert.Empty(Baseline.Value.Keys.Except(queries));
    }
}
