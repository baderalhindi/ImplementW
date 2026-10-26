using PMPlatform.Tests.Integration.Identity;
using PMPlatform.Tests.Integration.Persistence;

namespace PMPlatform.Tests.Integration.Schedule;

/// <summary>Rows written straight to the database, as fixtures may, for tests that need a schedule state no API call of theirs creates.</summary>
internal static class ScheduleFixture
{
    private const string Seed = IdentityDatabase.SeedPrincipalId;

    /// <summary>
    /// ADR-009's activation precondition met: a one-activity schedule and an APPROVED baseline of it, ACTIVE, written along the
    /// guarded edges — born DRAFT, its copy written, then activated — as WF-03 leaves them. Returns the baseline.
    /// </summary>
    public static async Task<Guid> ActiveBaselineAsync(TestDatabase database, Guid projectId, Guid? baselineId = null)
    {
        Guid schedule = Guid.NewGuid();
        Guid activity = Guid.NewGuid();
        Guid baseline = baselineId ?? Guid.NewGuid();
        await database.ExecuteAsync($"""
            INSERT INTO schedule.project_schedule (id, project_id, created_at, created_by, updated_at, updated_by)
            VALUES ('{schedule}', '{projectId}', now(), '{Seed}', now(), '{Seed}');
            INSERT INTO schedule.schedule_activity (id, project_schedule_id, wbs_code, name, name_lang, activity_kind, requested_start_date, planned_start_date,
                                                    planned_finish_date, planned_duration_days, forecast_start_date, forecast_finish_date, status, sort_order,
                                                    created_at, created_by, updated_at, updated_by)
            VALUES ('{activity}', '{schedule}', '1', 'Works', 'en', 'ACTIVITY', current_date, current_date, current_date + 9, 10, current_date, current_date + 9,
                    'PLANNED', 0, now(), '{Seed}', now(), '{Seed}');
            INSERT INTO schedule.project_baseline (id, project_id, baseline_type, version_no, revision_no, status, baseline_finish_date, created_at, created_by, updated_at, updated_by)
            VALUES ('{baseline}', '{projectId}', 'APPROVED', 1, 1, 'DRAFT', current_date + 9, now(), '{Seed}', now(), '{Seed}');
            INSERT INTO schedule.baseline_activity (id, project_baseline_id, schedule_activity_id, activity_kind, planned_start_date, planned_finish_date,
                                                    planned_duration_days, created_at, created_by, updated_at, updated_by)
            VALUES (gen_random_uuid(), '{baseline}', '{activity}', 'ACTIVITY', current_date, current_date + 9, 10, now(), '{Seed}', now(), '{Seed}');
            UPDATE schedule.project_baseline SET status = 'ACTIVE', activated_at = now() WHERE id = '{baseline}';
            """);
        return baseline;
    }
}
