using Microsoft.Extensions.DependencyInjection;
using PMPlatform.Application.Features.Dashboards;
using PMPlatform.Application.Features.Dashboards.Projections;
using PMPlatform.Application.Features.FinancialKpi.Contracts;
using PMPlatform.Tests.Integration.Identity;

namespace PMPlatform.Tests.Integration.Dashboards;

/// <summary>
/// The identity test database and people (IdentityDatabase) with what FG-01 reads and no environment grants yet: test view grants for the
/// sources beside the shipped R08 ones — Appendix A grants AHDA's roles none (dashboards.md F-3) — and a second department, so DEPT scope has
/// something to leave out. Source rows are written as fixtures, without their workflows, because FG-01 reads what the sources stored and
/// nothing else. <see cref="KpiFailure"/> makes the KPI projection's source fail on demand (BR-DSH-040).
/// </summary>
/// <remarks>
/// People: local.r02 (R02, ALL) views every source and holds CONFIGURATION_MANAGE, so with local.r01 (R01's shipped grants) and local.r03 a
/// governed version has its three people. local.r03 (R03, DEPT on the test department) views projects and progress of that department.
/// local.r06 (R06) holds no grant at all. local.r08 is ADR-013's entity Project Manager: external, R08 on the active entity and R04 on its
/// entity-delivered project.
/// </remarks>
public sealed class DashboardTestHost : IAsyncLifetime
{
    public static readonly Guid DepartmentId = new(IdentityDatabase.DepartmentId);
    public static readonly Guid OtherDepartmentId = new("00000000-0690-4000-8000-000000000002");
    public static readonly Guid EntityId = new(IdentityDatabase.ActiveEntityId);
    public static readonly Guid OtherEntityId = new(IdentityDatabase.SuspendedEntityId);
    public static readonly Guid ClassificationId = new("00000000-0131-4000-8000-000000000001");

    private const string Seed = IdentityDatabase.SeedPrincipalId;

    private static string Fixture => $"""
        INSERT INTO identity_access.department (id, code, name_ar, name_en, directory_reference, created_at, created_by, updated_at, updated_by)
        VALUES ('{OtherDepartmentId}', 'DEPT-DASHBOARD-OTHER', 'إدارة أخرى', 'Other department', 'DEPT-DASHBOARD-OTHER', now(), '{Seed}', now(), '{Seed}');

        INSERT INTO identity_access.permission_profile_grant (id, permission_profile_version_id, permission_id, data_scope, created_at, created_by, updated_at, updated_by)
        SELECT gen_random_uuid(), g.version_id::uuid, p.id, g.scope, now(), '{Seed}', now(), '{Seed}'
        FROM (VALUES ('{IdentityDatabase.ProfileVersionId(2)}', 'PROJECT_VIEW', 'ALL'), ('{IdentityDatabase.ProfileVersionId(2)}', 'PROGRESS_VIEW', 'ALL'),
                     ('{IdentityDatabase.ProfileVersionId(2)}', 'SCHEDULE_VIEW', 'ALL'), ('{IdentityDatabase.ProfileVersionId(2)}', 'RISK_VIEW', 'ALL'),
                     ('{IdentityDatabase.ProfileVersionId(2)}', 'FINANCIAL_VIEW', 'ALL'), ('{IdentityDatabase.ProfileVersionId(2)}', 'KPI_VIEW', 'ALL'),
                     ('{IdentityDatabase.ProfileVersionId(2)}', 'CONFIGURATION_VIEW', 'ALL'), ('{IdentityDatabase.ProfileVersionId(2)}', 'CONFIGURATION_MANAGE', 'ALL'),
                     ('{IdentityDatabase.ProfileVersionId(3)}', 'PROJECT_VIEW', 'DEPT'), ('{IdentityDatabase.ProfileVersionId(3)}', 'PROGRESS_VIEW', 'DEPT'),
                     ('{IdentityDatabase.ProfileVersionId(3)}', 'CONFIGURATION_VIEW', 'ALL'), ('{IdentityDatabase.ProfileVersionId(3)}', 'CONFIGURATION_MANAGE', 'ALL'))
             AS g (version_id, code, scope)
        JOIN identity_access.permission p ON p.code = g.code;
        """;

    public IdentityTestHost Identity { get; } = new();

    public IdentityDatabase Database => Identity.Database;

    public IdentityApiFactory Api { get; private set; } = null!;

    public SourceFailure KpiFailure { get; } = new();

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
        Api = Identity.CreateApi(
            new Dictionary<string, string?>
            {
                ["Outbox:PollInterval"] = "01:00:00",
                ["Approval:Maintenance:PollInterval"] = "01:00:00",
                ["DocumentManagement:Scan:PollInterval"] = "01:00:00",
            },
            services => services.AddScoped<IDashboardProjectionSource>(sp => new FailableSource(new KpiConditionSource(sp.GetRequiredService<IKpiConditionReader>()), KpiFailure)));
    }

    public async Task DisposeAsync()
    {
        await Api.DisposeAsync();
        await Identity.DisposeAsync();
    }
}

/// <summary>Whether a projection's source fails: what a database outage or a missing configuration looks like to FG-01.</summary>
public sealed class SourceFailure
{
    public bool IsOn { get; set; }
}

/// <summary>A projection source that throws while <see cref="SourceFailure.IsOn"/>, and otherwise is the real one.</summary>
internal sealed class FailableSource(IDashboardProjectionSource inner, SourceFailure failure) : IDashboardProjectionSource
{
    public string ProjectionCode => inner.ProjectionCode;

    public Task<ProjectionReading> ReadAsync(ProjectionRequest request, CancellationToken cancellationToken) =>
        failure.IsOn ? throw new InvalidOperationException("The source is down.") : inner.ReadAsync(request, cancellationToken);
}

[CollectionDefinition(Name)]
public sealed class DashboardSuite : ICollectionFixture<DashboardTestHost>
{
    public const string Name = "Dashboards";
}

/// <summary>The governed-version tests publish new versions of the seeded dashboards, so they have a database of their own.</summary>
[CollectionDefinition(Name)]
public sealed class DashboardDefinitionSuite : ICollectionFixture<DashboardTestHost>
{
    public const string Name = "DashboardDefinitions";
}
