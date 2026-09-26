using System.Globalization;
using System.Text.Json;
using Xunit.Abstractions;
using static PMPlatform.Tests.Integration.Persistence.RepresentativeVolume;

namespace PMPlatform.Tests.Integration.Persistence;

/// <summary>
/// TASK-026 acceptance: the query-plan review of the five busiest register screens (<c>indexing-strategy.md</c> §5).
/// Each screen's default page query and its <c>totalCount</c>, under the data scopes that reach it, is planned with
/// <c>EXPLAIN ANALYZE</c> at representative volume and must contain no sequential scan. The execution time is written
/// to the test output as the baseline for regression tracking.
/// </summary>
public sealed class RegisterQueryPlanTests(RepresentativeVolume volume, ITestOutputHelper output) : IClassFixture<RepresentativeVolume>
{
    private const string InDepartment = $"project_id IN (SELECT id FROM project.project WHERE department_id = '{DepartmentId}')";

    public static TheoryData<string, string> DefaultQueries() => new()
    {
        { "SCR-025 ALL page", "SELECT id, formal_project_id, title, lifecycle_state, updated_at FROM project.project ORDER BY updated_at DESC, id DESC LIMIT 25" },
        { "SCR-025 ALL page 11", "SELECT id, formal_project_id, title, lifecycle_state, updated_at FROM project.project ORDER BY updated_at DESC, id DESC LIMIT 25 OFFSET 250" },
        { "SCR-025 ALL count", "SELECT count(*) FROM project.project" },
        { "SCR-025 DEPT page", $"SELECT id, title FROM project.project WHERE department_id = '{DepartmentId}' ORDER BY updated_at DESC, id DESC LIMIT 25" },
        { "SCR-025 DEPT count", $"SELECT count(*) FROM project.project WHERE department_id = '{DepartmentId}'" },
        { "SCR-025 state filter page", "SELECT id, title FROM project.project WHERE lifecycle_state = 'ACTIVE' ORDER BY updated_at DESC, id DESC LIMIT 25" },
        { "SCR-025 state filter count", "SELECT count(*) FROM project.project WHERE lifecycle_state = 'ACTIVE'" },
        { "SCR-026 OWN page", $"SELECT id, title FROM project.project WHERE project_manager_user_id = '{UserId}' ORDER BY updated_at DESC, id DESC LIMIT 25" },
        { "SCR-080 ALL page", "SELECT id, title, status, next_review_date, updated_at FROM plan_review.risk ORDER BY updated_at DESC, id DESC LIMIT 25" },
        { "SCR-080 ALL count", "SELECT count(*) FROM plan_review.risk" },
        { "SCR-080 DEPT page", $"SELECT id, title, status FROM plan_review.risk WHERE {InDepartment} ORDER BY updated_at DESC, id DESC LIMIT 25" },
        { "SCR-080 DEPT count", $"SELECT count(*) FROM plan_review.risk WHERE {InDepartment}" },
        { "SCR-083 ALL page", "SELECT id, title, status FROM plan_review.management_concern WHERE concern_type = 'ISSUE' ORDER BY updated_at DESC, id DESC LIMIT 25" },
        { "SCR-083 ALL count", "SELECT count(*) FROM plan_review.management_concern WHERE concern_type = 'ISSUE'" },
        { "SCR-083 DEPT page", $"SELECT id, title, status FROM plan_review.management_concern WHERE concern_type = 'ISSUE' AND {InDepartment} ORDER BY updated_at DESC, id DESC LIMIT 25" },
        { "SCR-083 DEPT count", $"SELECT count(*) FROM plan_review.management_concern WHERE concern_type = 'ISSUE' AND {InDepartment}" },
        { "SCR-100 personal page", $"SELECT id, approval_instance_id, due_at FROM plan_review.approval_task WHERE assigned_user_id = '{UserId}' AND status = 'PENDING' ORDER BY due_at, id LIMIT 25" },
        { "SCR-100 personal count", $"SELECT count(*) FROM plan_review.approval_task WHERE assigned_user_id = '{UserId}' AND status = 'PENDING'" },
        { "SCR-100 role queue page", "SELECT id, approval_instance_id, due_at FROM plan_review.approval_task WHERE assigned_role_id = '00000000-0001-4000-8000-000000000003' AND assigned_user_id IS NULL AND status = 'PENDING' ORDER BY due_at, id LIMIT 25" },
        { "SCR-150 first page", $"SELECT id, rendered_subject, read_at, created_at FROM plan_review.notification_delivery WHERE recipient_user_id = '{UserId}' AND channel = 'IN_APP' ORDER BY created_at DESC, id DESC LIMIT 25" },
        { "SCR-150 next page", $"SELECT id, rendered_subject, read_at, created_at FROM plan_review.notification_delivery WHERE recipient_user_id = '{UserId}' AND channel = 'IN_APP' AND (created_at, id) < (timestamptz '2025-12-01', 'ffffffff-ffff-4fff-bfff-ffffffffffff') ORDER BY created_at DESC, id DESC LIMIT 25" },
        { "SCR-150 unread badge", $"SELECT count(*) FROM plan_review.notification_delivery WHERE recipient_user_id = '{UserId}' AND channel = 'IN_APP' AND read_at IS NULL" },
    };

    [Theory]
    [MemberData(nameof(DefaultQueries))]
    public async Task TheBusiestRegistersReadNoTableInFull(string query, string sql)
    {
        string plan = (await volume.Database.QueryAsync($"EXPLAIN (ANALYZE, FORMAT JSON) {sql}"))[0];
        JsonElement root = JsonDocument.Parse(plan).RootElement[0];

        List<PlanNode> nodes = QueryPlan.Nodes(root.GetProperty("Plan"));
        output.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"{query}: {root.GetProperty("Execution Time").GetDouble():0.000} ms; {string.Join("; ", nodes.Select(n => $"{n.NodeType} {n.RelationName} {n.IndexName}".Trim()))}"));

        Assert.DoesNotContain(nodes, n => n.NodeType == "Seq Scan");
        Assert.Contains(nodes, n => n.IndexName is not null);
    }
}
