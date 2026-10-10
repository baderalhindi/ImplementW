using Microsoft.Extensions.DependencyInjection;
using PMPlatform.Application.Features.Reports;
using PMPlatform.Tests.Integration.Identity;

namespace PMPlatform.Tests.Integration.Reports;

/// <summary>
/// The identity test database and people (IdentityDatabase) with what FG-02 reads: test grants beside the shipped ones — Appendix A grants AHDA's
/// roles no source view and no export (reports.md F-3) — a second department, and a PUBLISHED REPORT_RULES allowlist narrower than the seed's
/// proposal, so a field outside it exists to be refused. Source rows are written as fixtures, as the owning module stores them. Jobs are run by
/// calling the maintenance pass (<see cref="RunJobsAsync"/>); the API's own worker polls once an hour.
/// </summary>
/// <remarks>
/// People: local.r02 (R02, ALL) views every source, exports at ALL and holds CONFIGURATION_MANAGE; with local.r01 and local.r03 a governed version has
/// its three people. local.r03 (R03, DEPT on the test department) views projects and progress of that department and exports at DEPT. local.r06 (R06)
/// holds no data grant at all: the unauthorised user. local.r08 is ADR-013's entity user: external, R08 on the active entity and R04 on its project,
/// with the shipped REPORT_EXPORT at ENTITY.
/// </remarks>
public sealed class ReportTestHost : IAsyncLifetime
{
    public static readonly Guid DepartmentId = new(IdentityDatabase.DepartmentId);
    public static readonly Guid OtherDepartmentId = new("00000000-0710-4000-8000-000000000002");
    public static readonly Guid EntityId = new(IdentityDatabase.ActiveEntityId);
    public static readonly Guid OtherEntityId = new(IdentityDatabase.SuspendedEntityId);
    public static readonly Guid ClassificationId = new("00000000-0131-4000-8000-000000000001");
    public static readonly Guid AllowlistVersionId = new("00000000-0711-4000-8000-000000000002");

    /// <summary>The fields the test allowlist leaves out though the register has them: financial amounts, a field of an allowlisted projection.</summary>
    public static readonly string[] NotAllowlisted = ["FINANCIAL_KPI_FINANCIAL_POSITION.APPROVED_BUDGET"];

    private const string Seed = IdentityDatabase.SeedPrincipalId;

    private static string Fixture => $"""
        INSERT INTO identity_access.department (id, code, name_ar, name_en, directory_reference, created_at, created_by, updated_at, updated_by)
        VALUES ('{OtherDepartmentId}', 'DEPT-REPORT-OTHER', 'إدارة أخرى', 'Other department', 'DEPT-REPORT-OTHER', now(), '{Seed}', now(), '{Seed}');

        INSERT INTO identity_access.permission_profile_grant (id, permission_profile_version_id, permission_id, data_scope, created_at, created_by, updated_at, updated_by)
        SELECT gen_random_uuid(), g.version_id::uuid, p.id, g.scope, now(), '{Seed}', now(), '{Seed}'
        FROM (VALUES {Grants(2, "ALL", "PROJECT_VIEW", "PROGRESS_VIEW", "SCHEDULE_VIEW", "RISK_VIEW", "FINANCIAL_VIEW", "KPI_VIEW", "TASK_VIEW", "MILESTONE_VIEW",
                             "CONCERN_VIEW", "CHANGE_REQUEST_VIEW", "SUSPENSION_VIEW", "REPORT_EXPORT", "CONFIGURATION_VIEW", "CONFIGURATION_MANAGE")},
                     {Grants(3, "DEPT", "PROJECT_VIEW", "PROGRESS_VIEW", "REPORT_EXPORT")},
                     {Grants(3, "ALL", "CONFIGURATION_VIEW", "CONFIGURATION_MANAGE")})
             AS g (version_id, code, scope)
        JOIN identity_access.permission p ON p.code = g.code;

        -- The test allowlist: version 2 of REPORT_RULES, PUBLISHED by SQL (the seed's version 1 stays the DRAFT proposal). Every project-grain field
        -- of the seed's proposal but the live Approved Budget.
        INSERT INTO master_data_config.configuration_version (id, configuration_family_id, version_no, lifecycle_state, created_at, created_by, updated_at, updated_by)
        SELECT '{AllowlistVersionId}', f.id, 2, 'DRAFT', now(), '{Seed}', now(), '{Seed}'
        FROM master_data_config.configuration_family f WHERE f.code = 'REPORT_RULES';
        INSERT INTO master_data_config.report_allowlist_entry (id, configuration_version_id, source_entity_code, field_code, label_ar, label_en, is_filterable, is_sortable,
                                                             created_at, created_by, updated_at, updated_by)
        SELECT gen_random_uuid(), '{AllowlistVersionId}', e.source_entity_code, e.field_code, e.label_ar, e.label_en, e.is_filterable, e.is_sortable,
               now(), '{Seed}', now(), '{Seed}'
        FROM master_data_config.report_allowlist_entry e
        WHERE e.configuration_version_id = md5('configuration_version:REPORT_RULES:1')::uuid
          AND e.source_entity_code || '.' || e.field_code <> ALL (ARRAY['{string.Join("','", NotAllowlisted)}']);
        UPDATE master_data_config.report_allowlist_entry SET is_filterable = false
        WHERE configuration_version_id = '{AllowlistVersionId}' AND source_entity_code = 'PROJECT' AND field_code = 'EXTERNAL_ENTITY';
        UPDATE master_data_config.configuration_version
        SET lifecycle_state = 'PUBLISHED', published_at = now() - interval '1 day', effective_from = now() - interval '1 day'
        WHERE id = '{AllowlistVersionId}';
        """;

    public IdentityTestHost Identity { get; } = new();

    public IdentityDatabase Database => Identity.Database;

    public IdentityApiFactory Api { get; private set; } = null!;

    public Guid StandardProfileId { get; private set; }

    public async Task InitializeAsync()
    {
        await Identity.InitializeAsync();
        await Database.ExecuteAsync(Fixture);
        StandardProfileId = Guid.Parse(Assert.Single(await Database.QueryAsync("""
            SELECT i.id::text FROM master_data_config.master_data_item i
            JOIN master_data_config.master_data_catalogue c ON c.id = i.catalogue_id
            WHERE c.code = 'GOVERNANCE_PROFILE' AND i.code = 'STANDARD'
            """)));
        Api = Identity.CreateApi(new Dictionary<string, string?>
        {
            ["Outbox:PollInterval"] = "01:00:00",
            ["Approval:Maintenance:PollInterval"] = "01:00:00",
            ["DocumentManagement:Scan:PollInterval"] = "01:00:00",
            ["Reports:Jobs:PollInterval"] = "01:00:00",
        });
    }

    /// <summary>One pass of FG-02's job runner, as the worker runs it, with the API's default limits.</summary>
    public async Task<int> RunJobsAsync(int maxExportRows = 10_000)
    {
        await using AsyncServiceScope scope = Api.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IReportMaintenance>().RunAsync(
            new ReportMaintenancePolicy(50, TimeSpan.FromDays(1), maxExportRows, TimeSpan.FromMinutes(15)), CancellationToken.None);
    }

    public async Task DisposeAsync()
    {
        await Api.DisposeAsync();
        await Identity.DisposeAsync();
    }

    private static string Grants(int role, string scope, params string[] permissions) =>
        string.Join(", ", permissions.Select(p => $"('{IdentityDatabase.ProfileVersionId(role)}', '{p}', '{scope}')"));
}

[CollectionDefinition(Name)]
public sealed class ReportSuite : ICollectionFixture<ReportTestHost>
{
    public const string Name = "Reports";
}

/// <summary>The governed-version and allowlist-change tests publish new versions, so they have a database of their own.</summary>
[CollectionDefinition(Name)]
public sealed class ReportDefinitionSuite : ICollectionFixture<ReportTestHost>
{
    public const string Name = "ReportDefinitions";
}
