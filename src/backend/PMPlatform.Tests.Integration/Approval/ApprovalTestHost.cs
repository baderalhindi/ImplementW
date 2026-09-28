using Microsoft.Extensions.DependencyInjection;
using PMPlatform.Application.Features.Approval.Contracts;
using PMPlatform.Tests.Integration.Identity;

namespace PMPlatform.Tests.Integration.Approval;

/// <summary>
/// The identity test database and people (IdentityDatabase), with the approval permissions granted to test roles —
/// Appendix A grants none (approval-framework.md F-1) — published APPROVAL_AUTHORITY and WORKFLOW_POLICY versions, the
/// <see cref="TestSource"/> module, and an API whose outbox and maintenance workers stay idle so each test dispatches
/// and escalates when it chooses. People: local.r02 decides at ALL (R02); local.r03 decides for their department
/// (R03, DEPT anchor); local.r06 (R06) may only see their own requests; local.r05 is active with no assignment;
/// local.r08 is external and holds R01, R04 and R08.
/// </summary>
public sealed class ApprovalTestHost : IAsyncLifetime
{
    /// <summary>One stage: R02.</summary>
    public const string SingleStage = "TEST_SINGLE_STAGE";

    /// <summary>Stage 1: R03; stage 2: R02.</summary>
    public const string TwoStages = "TEST_TWO_STAGES";

    /// <summary>One stage: R04, which external users hold too (ADR-013).</summary>
    public const string ProjectManagerStage = "TEST_PM_STAGE";

    public const int DueDays = 3;

    private const string Seed = IdentityDatabase.SeedPrincipalId;

    public static string RoleId(int n) => $"00000000-0000-4000-8000-{n:D12}";

    private static string Grants => $"""
        INSERT INTO identity_access.permission_profile_grant (id, permission_profile_version_id, permission_id, data_scope, created_at, created_by, updated_at, updated_by)
        SELECT gen_random_uuid(), g.version_id::uuid, p.id, g.scope, now(), '{Seed}', now(), '{Seed}'
        FROM (VALUES ('{IdentityDatabase.ProfileVersionId(2)}', 'APPROVAL_DECIDE', 'ALL'), ('{IdentityDatabase.ProfileVersionId(2)}', 'APPROVAL_VIEW', 'ALL'),
                     ('{IdentityDatabase.ProfileVersionId(3)}', 'APPROVAL_DECIDE', 'DEPT'), ('{IdentityDatabase.ProfileVersionId(3)}', 'APPROVAL_VIEW', 'DEPT'),
                     ('{IdentityDatabase.ProfileVersionId(4)}', 'APPROVAL_DECIDE', 'ALL'), ('{IdentityDatabase.ProfileVersionId(6)}', 'APPROVAL_VIEW', 'OWN'))
             AS g (version_id, code, scope)
        JOIN identity_access.permission p ON p.code = g.code;
        """;

    private static string Configuration => $"""
        INSERT INTO master_data_config.configuration_version (id, configuration_family_id, version_no, lifecycle_state, created_at, created_by, updated_at, updated_by)
        SELECT v.id, f.id, 1, 'DRAFT', now(), '{Seed}', now(), '{Seed}'
        FROM (VALUES ('00000000-0350-4000-8000-000000000001'::uuid, 'APPROVAL_AUTHORITY'),
                     ('00000000-0350-4000-8000-000000000002'::uuid, 'WORKFLOW_POLICY')) AS v (id, family)
        JOIN master_data_config.configuration_family f ON f.code = v.family;

        INSERT INTO master_data_config.approval_authority_rule (id, configuration_version_id, subject_type_code, sequence_no, approver_role_id, is_mandatory, created_at, created_by, updated_at, updated_by)
        SELECT gen_random_uuid(), '00000000-0350-4000-8000-000000000001', r.subject, r.stage, r.role::uuid, true, now(), '{Seed}', now(), '{Seed}'
        FROM (VALUES ('{SingleStage}', 1, '{RoleId(2)}'), ('{TwoStages}', 1, '{RoleId(3)}'), ('{TwoStages}', 2, '{RoleId(2)}'),
                     ('{ProjectManagerStage}', 1, '{RoleId(4)}')) AS r (subject, stage, role);

        INSERT INTO master_data_config.configuration_value (id, configuration_version_id, value_key, value_text, value_type, created_at, created_by, updated_at, updated_by)
        VALUES (gen_random_uuid(), '00000000-0350-4000-8000-000000000002', 'APPROVAL_TASK_DUE_DAYS', '{DueDays}', 'DURATION_DAYS', now(), '{Seed}', now(), '{Seed}'),
               (gen_random_uuid(), '00000000-0350-4000-8000-000000000002', 'APPROVAL_ESCALATION_ROLE', 'R02', 'TEXT', now(), '{Seed}', now(), '{Seed}');

        UPDATE master_data_config.configuration_version
        SET lifecycle_state = 'PUBLISHED', published_at = now() - interval '1 hour', effective_from = now() - interval '1 hour'
        WHERE id IN ('00000000-0350-4000-8000-000000000001', '00000000-0350-4000-8000-000000000002');
        """;

    public IdentityTestHost Identity { get; } = new();

    public IdentityDatabase Database => Identity.Database;

    public AdjustableTimeProvider Clock => Identity.Clock;

    public TestSourceFailures Failures { get; } = new();

    public IdentityApiFactory Api { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await Identity.InitializeAsync();
        await Database.ExecuteAsync(Grants + Configuration + TestSource.Schema);
        Api = Identity.CreateApi(
            new Dictionary<string, string?>
            {
                ["Outbox:PollInterval"] = "01:00:00",
                ["Approval:Maintenance:PollInterval"] = "01:00:00",
            },
            services =>
            {
                services.AddSingleton(Failures);
                services.AddScoped<IApprovalOutcomeHandler, TestSourceOutcomeHandler>();
            });
    }

    public async Task DisposeAsync()
    {
        await Api.DisposeAsync();
        await Identity.DisposeAsync();
    }
}

[CollectionDefinition(Name)]
public sealed class ApprovalSuite : ICollectionFixture<ApprovalTestHost>
{
    public const string Name = "Approval";
}
