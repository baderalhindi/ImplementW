using PMPlatform.Tests.Integration.Identity;
using PMPlatform.Tests.Integration.Persistence;

namespace PMPlatform.Tests.Integration.Closure;

/// <summary>
/// A CLOSED project with one record of every kind a write endpoint addresses — its schedule, activities, dependency, milestone and baseline
/// candidate; tasks and their dependency; a milestone achievement claim; a risk and its treatment action; an issue and its escalation; a
/// change request; a suspension request; a reporting cycle and progress submission; the financial and KPI records; a document and its
/// version; its effected completion and closure cases, an open obligation; and the closure's decided WF-11 run, requested by local.r02 so
/// that its requester's own commands reach the run's state. Written straight to the
/// database with triggers off, as fixtures may: each record is in a state its own module would still take writes in, so that what refuses
/// a write is the project being closed.
/// </summary>
internal sealed class ClosedProjectFixture
{
    private const string Seed = IdentityDatabase.SeedPrincipalId;

    private readonly Dictionary<string, Guid> _ids = new(StringComparer.Ordinal);

    public Guid ProjectId => this["project"];

    /// <summary>The id of the fixture record named <paramref name="name"/>.</summary>
    public Guid this[string name] => _ids.TryGetValue(name, out Guid id) ? id : _ids[name] = Guid.NewGuid();

    /// <summary>Every fixture row, as (table, id): what a write must leave unchanged.</summary>
    public IReadOnlyList<(string Table, Guid Id)> Rows =>
    [
        ("project.project", this["project"]), ("schedule.project_schedule", this["schedule"]), ("schedule.schedule_activity", this["activity"]),
        ("schedule.schedule_activity", this["activity2"]), ("schedule.schedule_dependency", this["scheduleDependency"]), ("schedule.project_milestone", this["milestone"]),
        ("schedule.project_baseline", this["baseline"]), ("project_task.project_task", this["task"]), ("project_task.project_task", this["task2"]),
        ("project_task.task_dependency", this["taskDependency"]), ("milestone.milestone_achievement", this["achievement"]), ("risk.risk", this["risk"]),
        ("risk.risk_treatment_action", this["action"]), ("management_concern.management_concern", this["concern"]),
        ("management_concern.concern_escalation", this["escalation"]), ("change_request.change_request", this["changeRequest"]),
        ("suspension.suspension_request", this["suspensionRequest"]), ("progress.reporting_cycle", this["cycle"]), ("progress.progress_submission", this["submission"]),
        ("financial_kpi.financial_source_mode", this["sourceMode"]), ("financial_kpi.financial_commitment", this["commitment"]),
        ("financial_kpi.financial_progress_update", this["update"]), ("financial_kpi.kpi_assignment", this["assignment"]),
        ("financial_kpi.kpi_target_version", this["target"]), ("financial_kpi.kpi_measurement", this["measurement"]),
        ("document_management.document", this["document"]), ("document_management.document_version", this["version"]),
        ("closure.completion_case", this["completionCase"]), ("closure.closure_case", this["closureCase"]), ("closure.post_project_obligation", this["obligation"]),
        ("approval.approval_instance", this["approvalInstance"]), ("approval.approval_task", this["approvalTask"]),
    ];

    public static async Task<ClosedProjectFixture> CreateAsync(TestDatabase database, Guid documentTypeId, Guid classificationId)
    {
        ArgumentNullException.ThrowIfNull(database);
        ClosedProjectFixture f = new();
        string manager = IdentityDatabase.UserId(8), officer = IdentityDatabase.UserId(2), r02 = "00000000-0000-4000-8000-000000000002";
        string audit = $"now(), '{Seed}', now(), '{Seed}'";
        await database.ExecuteAsync($"""
            BEGIN;
            SET LOCAL session_replication_role = replica;

            INSERT INTO project.project (id, formal_project_id, title, title_lang, classification_item_id, department_id, external_entity_id, project_manager_user_id,
                                         lifecycle_state, governance_profile_item_id, participation_mode, activated_at, closed_at, created_at, created_by, updated_at, updated_by)
            SELECT '{f["project"]}', 'PRJ-X{f["project"].ToString("N")[..12]}', 'Closed project', 'en', '{ClosureTestHost.ClassificationId}', '{ClosureTestHost.DepartmentId}',
                   '{ClosureTestHost.EntityId}', '{manager}', 'CLOSED', i.id, 'ENTITY_MANAGED', now() - interval '60 days', now(), {audit}
            FROM master_data_config.master_data_item i JOIN master_data_config.master_data_catalogue c ON c.id = i.catalogue_id
            WHERE c.code = 'GOVERNANCE_PROFILE' AND i.code = 'LIGHT';

            INSERT INTO schedule.project_schedule (id, project_id, created_at, created_by, updated_at, updated_by) VALUES ('{f["schedule"]}', '{f.ProjectId}', {audit});
            INSERT INTO schedule.schedule_activity (id, project_schedule_id, wbs_code, name, name_lang, activity_kind, requested_start_date, planned_start_date,
                                                    planned_finish_date, planned_duration_days, forecast_start_date, forecast_finish_date, status, sort_order,
                                                    created_at, created_by, updated_at, updated_by)
            VALUES ('{f["activity"]}', '{f["schedule"]}', '1', 'Works', 'en', 'ACTIVITY', current_date, current_date, current_date + 9, 10, current_date, current_date + 9, 'PLANNED', 0, {audit}),
                   ('{f["activity2"]}', '{f["schedule"]}', '2', 'Handover', 'en', 'ACTIVITY', current_date, current_date, current_date + 9, 10, current_date, current_date + 9, 'PLANNED', 1, {audit});
            INSERT INTO schedule.schedule_dependency (id, predecessor_activity_id, successor_activity_id, dependency_type, created_at, created_by, updated_at, updated_by)
            VALUES ('{f["scheduleDependency"]}', '{f["activity"]}', '{f["activity2"]}', 'FS', {audit});
            INSERT INTO schedule.project_milestone (id, project_id, project_schedule_id, milestone_category_item_id, forecast_date, status, title, title_lang, created_at, created_by, updated_at, updated_by)
            VALUES ('{f["milestone"]}', '{f.ProjectId}', '{f["schedule"]}', gen_random_uuid(), current_date + 9, 'PLANNED', 'Handover', 'en', {audit});
            INSERT INTO schedule.project_baseline (id, project_id, baseline_type, version_no, revision_no, status, baseline_finish_date, created_at, created_by, updated_at, updated_by)
            VALUES ('{f["baseline"]}', '{f.ProjectId}', 'APPROVED', 1, 1, 'DRAFT', current_date + 9, {audit});

            INSERT INTO project_task.project_task (id, project_id, status, planned_start_date, planned_finish_date, planned_duration_days, title, title_lang, created_at, created_by, updated_at, updated_by)
            VALUES ('{f["task"]}', '{f.ProjectId}', 'NOT_STARTED', current_date, current_date + 4, 5, 'Install', 'en', {audit}),
                   ('{f["task2"]}', '{f.ProjectId}', 'NOT_STARTED', current_date, current_date + 4, 5, 'Commission', 'en', {audit});
            INSERT INTO project_task.task_dependency (id, predecessor_task_id, successor_task_id, dependency_type, created_at, created_by, updated_at, updated_by)
            VALUES ('{f["taskDependency"]}', '{f["task"]}', '{f["task2"]}', 'FS', {audit});

            INSERT INTO milestone.milestone_achievement (id, project_milestone_id, project_id, status, claimed_achievement_date, created_at, created_by, updated_at, updated_by)
            VALUES ('{f["achievement"]}', '{f["milestone"]}', '{f.ProjectId}', 'DRAFT', current_date, {audit});

            INSERT INTO risk.risk (id, project_id, risk_category_item_id, status, identified_date, description, description_lang, title, title_lang, created_at, created_by, updated_at, updated_by)
            VALUES ('{f["risk"]}', '{f.ProjectId}', gen_random_uuid(), 'IDENTIFIED', current_date, 'Ground conditions.', 'en', 'Ground', 'en', {audit});
            INSERT INTO risk.risk_treatment_action (id, risk_id, action_type, status, title, title_lang, created_at, created_by, updated_at, updated_by)
            VALUES ('{f["action"]}', '{f["risk"]}', 'MITIGATE', 'PLANNED', 'Survey', 'en', {audit});

            INSERT INTO management_concern.management_concern (id, project_id, concern_type, category_item_id, priority_item_id, status, raised_by_user_id, raised_at,
                                                               next_review_date, description, description_lang, title, title_lang, created_at, created_by, updated_at, updated_by)
            VALUES ('{f["concern"]}', '{f.ProjectId}', 'ISSUE', gen_random_uuid(), gen_random_uuid(), 'OPEN', '{manager}', now(), current_date + 7, 'Access road closed.', 'en', 'Access', 'en', {audit});
            INSERT INTO management_concern.concern_escalation (id, management_concern_id, escalation_no, escalated_by_user_id, escalated_at, escalated_to_role_id, status,
                                                               request_key, reason, reason_lang, created_at, created_by, updated_at, updated_by)
            VALUES ('{f["escalation"]}', '{f["concern"]}', 1, '{officer}', now(), '{r02}', 'OPEN', gen_random_uuid(), 'Blocked.', 'en', {audit});

            INSERT INTO change_request.change_request (id, project_id, change_type, status, requested_by_user_id, justification, justification_lang, title, title_lang,
                                                       created_at, created_by, updated_at, updated_by)
            VALUES ('{f["changeRequest"]}', '{f.ProjectId}', 'SCOPE', 'DRAFT', '{manager}', 'Client asked.', 'en', 'Extra bay', 'en', {audit});

            INSERT INTO suspension.suspension_request (id, project_id, request_type, status, requested_by_user_id, reason, reason_lang, created_at, created_by, updated_at, updated_by)
            VALUES ('{f["suspensionRequest"]}', '{f.ProjectId}', 'SUSPEND', 'DRAFT', '{manager}', 'Funding.', 'en', {audit});

            INSERT INTO progress.reporting_cycle (id, project_id, period_start, period_end, due_date, status, created_at, created_by, updated_at, updated_by)
            VALUES ('{f["cycle"]}', '{f.ProjectId}', current_date - 6, current_date, current_date + 3, 'OPEN', {audit});
            INSERT INTO progress.progress_submission (id, project_id, reporting_cycle_id, status, actual_percent_calculated, created_at, created_by, updated_at, updated_by)
            VALUES ('{f["submission"]}', '{f.ProjectId}', '{f["cycle"]}', 'DRAFT', 0, {audit});

            INSERT INTO financial_kpi.financial_source_mode (id, project_id, field_code, source_mode, configured_at, created_at, created_by, updated_at, updated_by)
            VALUES ('{f["sourceMode"]}', '{f.ProjectId}', 'APPROVED_BUDGET', 'MANUAL', now(), {audit});
            INSERT INTO financial_kpi.financial_commitment (id, project_id, commitment_type, version_no, status, amount_sar, source_type, as_of_date, entered_by_user_id,
                                                            created_at, created_by, updated_at, updated_by)
            VALUES ('{f["commitment"]}', '{f.ProjectId}', 'APPROVED_BUDGET', 1, 'DRAFT', 1000, 'MANUAL', current_date, '{manager}', {audit});
            INSERT INTO financial_kpi.financial_progress_update (id, project_id, reporting_cycle_id, status, value_status, source_type, as_of_date, entered_by_user_id,
                                                                 created_at, created_by, updated_at, updated_by)
            VALUES ('{f["update"]}', '{f.ProjectId}', '{f["cycle"]}', 'DRAFT', 'MISSING', 'MANUAL', current_date, '{manager}', {audit});
            INSERT INTO financial_kpi.kpi_assignment (id, project_id, kpi_definition_id, measurement_frequency_item_id, status, assigned_at, created_at, created_by, updated_at, updated_by)
            VALUES ('{f["assignment"]}', '{f.ProjectId}', gen_random_uuid(), gen_random_uuid(), 'ACTIVE', now(), {audit});
            INSERT INTO financial_kpi.kpi_target_version (id, kpi_assignment_id, version_no, status, target_value, created_at, created_by, updated_at, updated_by)
            VALUES ('{f["target"]}', '{f["assignment"]}', 1, 'DRAFT', 10, {audit});
            INSERT INTO financial_kpi.kpi_measurement (id, kpi_assignment_id, kpi_target_version_id, period_start, period_end, value_status, rag_status, as_of_date,
                                                       recorded_by_user_id, status, created_at, created_by, updated_at, updated_by)
            VALUES ('{f["measurement"]}', '{f["assignment"]}', '{f["target"]}', current_date - 6, current_date, 'MISSING', 'UNKNOWN', current_date, '{manager}', 'DRAFT', {audit});

            INSERT INTO document_management.document (id, project_id, document_type_item_id, data_classification_item_id, owner_user_id, status, title, title_lang,
                                                      created_at, created_by, updated_at, updated_by)
            VALUES ('{f["document"]}', '{f.ProjectId}', '{documentTypeId}', '{classificationId}', '{officer}', 'ACTIVE', 'Handover certificate', 'en', {audit});
            INSERT INTO document_management.document_version (id, document_id, version_no, storage_object_key, file_name, content_type, size_bytes, checksum_sha256,
                                                              uploaded_by_user_id, uploaded_at, scan_state, created_at, created_by, updated_at, updated_by)
            VALUES ('{f["version"]}', '{f["document"]}', 1, 'closed/handover.pdf', 'handover.pdf', 'application/pdf', 8, repeat('0', 64), '{officer}', now(), 'SCAN_PENDING', {audit});

            INSERT INTO closure.completion_case (id, project_id, status, revision_no, requested_by_user_id, submitted_at, actual_project_completion_date, completion_narrative,
                                                 completion_narrative_lang, effected_at, created_at, created_by, updated_at, updated_by)
            VALUES ('{f["completionCase"]}', '{f.ProjectId}', 'EFFECTED', 1, '{manager}', now(), current_date - 1, 'Delivered.', 'en', now(), {audit});
            INSERT INTO closure.closure_case (id, project_id, completion_case_id, status, revision_no, requested_by_user_id, submitted_at, closure_narrative, closure_narrative_lang,
                                              effected_at, created_at, created_by, updated_at, updated_by)
            VALUES ('{f["closureCase"]}', '{f.ProjectId}', '{f["completionCase"]}', 'EFFECTED', 1, '{manager}', now(), 'Closed.', 'en', now(), {audit});
            INSERT INTO closure.post_project_obligation (id, project_id, completion_case_id, title, title_lang, status, created_at, created_by, updated_at, updated_by)
            VALUES ('{f["obligation"]}', '{f.ProjectId}', '{f["completionCase"]}', 'Warranty', 'en', 'OPEN', {audit});

            INSERT INTO approval.approval_instance (id, subject_module, subject_type, subject_id, subject_revision_no, routing_key, authority_configuration_version_id,
                                                    scope_project_id, scope_department_id, requested_by_user_id, requested_at, status, completed_at,
                                                    outcome_idempotency_key, outcome_delivered_at, created_at, created_by, updated_at, updated_by)
            VALUES ('{f["approvalInstance"]}', 'Closure', 'ClosureCase', '{f["closureCase"]}', 1, 'CLOSURE', '00000000-0630-4000-8000-000000000103',
                    '{f.ProjectId}', '{ClosureTestHost.DepartmentId}', '{officer}', now(), 'APPROVED', now(), 'apr-{f["approvalInstance"]}-outcome', now(), {audit});
            INSERT INTO approval.approval_task (id, approval_instance_id, sequence_no, assigned_role_id, acting_user_id, status, decided_at, created_at, created_by, updated_at, updated_by)
            VALUES ('{f["approvalTask"]}', '{f["approvalInstance"]}', 1, '{r02}', '{officer}', 'APPROVED', now(), {audit});
            COMMIT;
            """);
        return f;
    }

    /// <summary>A digest of every fixture row, its row version included, and of how many rows each table holds of the project.</summary>
    public async Task<string> DigestAsync(TestDatabase database)
    {
        ArgumentNullException.ThrowIfNull(database);
        string rows = string.Join(" UNION ALL ", Rows.Select(r =>
            $"SELECT '{r.Table}:{r.Id}' AS k, (to_jsonb(t) || jsonb_build_object('xmin', t.xmin::text))::text AS v FROM {r.Table} t WHERE t.id = '{r.Id}'"));
        string counts = string.Join(" UNION ALL ", Rows.Select(r => r.Table).Distinct().Where(HasProjectId).Select(table =>
            $"SELECT '{table}#' AS k, count(*)::text AS v FROM {table} t WHERE t.project_id = '{ProjectId}'"));
        return Assert.Single(await database.QueryAsync($"SELECT md5(string_agg(k || '=' || v, '|' ORDER BY k)) FROM ({rows} UNION ALL {counts}) x"));
    }

    private static bool HasProjectId(string table) =>
        table is not ("project.project" or "schedule.schedule_activity" or "schedule.schedule_dependency" or "project_task.task_dependency" or "risk.risk_treatment_action"
            or "management_concern.concern_escalation" or "financial_kpi.kpi_target_version" or "financial_kpi.kpi_measurement" or "document_management.document_version"
            or "approval.approval_instance" or "approval.approval_task");
}
