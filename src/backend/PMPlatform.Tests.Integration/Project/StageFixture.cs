using PMPlatform.Tests.Integration.Identity;

namespace PMPlatform.Tests.Integration.Project;

/// <summary>
/// Review round 7, item 1: the six project stages the seed publishes, with their standard weights, as a PUBLISHED PROJECT_STAGE version in force from
/// now, after the seeded one the test database withdraws. Idempotent. Only a suite with a database of its own publishes it: with stages configured, every
/// registration needs a stage plan and every progress period is stage-based.
/// </summary>
internal static class StageFixture
{
    public static readonly Guid VersionId = new("00000000-0712-4000-8000-000000000001");

    public static readonly (string Code, decimal Weight)[] Stages =
        [("BUSINESS_CASE", 5m), ("DESIGN", 15m), ("TENDERING", 10m), ("EXECUTION", 55m), ("TESTING", 10m), ("HANDOVER", 5m)];

    private const string Seed = IdentityDatabase.SeedPrincipalId;

    public static Task PublishAsync(IdentityDatabase database) => database.ExecuteAsync($"""
        INSERT INTO master_data_config.configuration_version (id, configuration_family_id, version_no, lifecycle_state, created_at, created_by, updated_at, updated_by)
        SELECT '{VersionId}', f.id, 1 + (SELECT count(*) FROM master_data_config.configuration_version v WHERE v.configuration_family_id = f.id), 'DRAFT',
               now(), '{Seed}', now(), '{Seed}'
        FROM master_data_config.configuration_family f WHERE f.code = 'PROJECT_STAGE'
        ON CONFLICT DO NOTHING;

        INSERT INTO master_data_config.project_stage_definition (id, configuration_version_id, code, sort_order, weight_percent, is_active, name_ar, name_en, created_at, created_by, updated_at, updated_by)
        SELECT gen_random_uuid(), v.id, s.code, s.sort_order, s.weight, true, s.code, initcap(replace(s.code, '_', ' ')), now(), '{Seed}', now(), '{Seed}'
        FROM master_data_config.configuration_version v
        CROSS JOIN (VALUES ('BUSINESS_CASE', 1::smallint, 5.00), ('DESIGN', 2::smallint, 15.00), ('TENDERING', 3::smallint, 10.00),
                           ('EXECUTION', 4::smallint, 55.00), ('TESTING', 5::smallint, 10.00), ('HANDOVER', 6::smallint, 5.00)) AS s (code, sort_order, weight)
        WHERE v.id = '{VersionId}' AND v.lifecycle_state = 'DRAFT'
        ON CONFLICT DO NOTHING;

        UPDATE master_data_config.configuration_version
        SET lifecycle_state = 'PUBLISHED', published_at = now(), effective_from = now()
        WHERE id = '{VersionId}' AND lifecycle_state = 'DRAFT';
        """);

    /// <summary>Withdraws the fixture's version from now, so no stage version is in force again; it cannot be published a second time.</summary>
    public static Task WithdrawAsync(IdentityDatabase database) => database.ExecuteAsync($"""
        UPDATE master_data_config.configuration_version SET lifecycle_state = 'RETIRED', effective_to = now()
        WHERE id = '{VersionId}' AND lifecycle_state = 'PUBLISHED';
        """);

    /// <summary>A plan applying every stage but <paramref name="skipped"/>, one after another from <paramref name="start"/>, each <paramref name="days"/> long.</summary>
    public static object Plan(DateOnly start, int days = 30, string? skipped = null, decimal? startingPercent = null, Guid? changeAuthorizationId = null) =>
        new
        {
            stages = Stages.Select((s, i) => s.Code == skipped
                ? new { stageCode = s.Code, applies = false, plannedStartDate = (DateOnly?)null, plannedEndDate = (DateOnly?)null, startingPercent = (decimal?)null }
                : new
                {
                    stageCode = s.Code,
                    applies = true,
                    plannedStartDate = (DateOnly?)start.AddDays(i * days),
                    plannedEndDate = (DateOnly?)start.AddDays(((i + 1) * days) - 1),
                    startingPercent,
                }).ToArray(),
            changeAuthorizationId,
        };
}
