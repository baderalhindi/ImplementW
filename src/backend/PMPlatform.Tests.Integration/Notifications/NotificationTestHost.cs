using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using PMPlatform.Application.Common.Events;
using PMPlatform.Application.Features.Notifications;
using PMPlatform.Infrastructure.Notifications;
using PMPlatform.Infrastructure.Persistence.Messaging;
using PMPlatform.Tests.Integration.Identity;

namespace PMPlatform.Tests.Integration.Notifications;

/// <summary>
/// The identity test database and people (IdentityDatabase) with what WF-15 needs and no environment has yet: a published
/// NOTIFICATION_ROUTING version (ADR-004's three matrices), PUBLISHED templates, the Risk and ManagementConcern modules
/// played by <see cref="TestNotificationSource"/>, a real SMTP relay in the process (<see cref="TestSmtpServer"/>) and a
/// test SMS provider. The outbox and notification workers are left out, so each test dispatches and processes when it
/// chooses (a worker's first pass at start-up would otherwise race the test's); <c>WorkersDeliverWithoutADriver</c> runs
/// them. The clock is the identity host's, which a test that moves it puts back.
/// </summary>
/// <remarks>
/// Families: <see cref="EscalationFamily"/> is mandatory, on in-app, e-mail and SMS, to R02, R03, R04 and R08.
/// <see cref="ReminderFamily"/> is configurable, in-app and e-mail, to R02. <see cref="ReportFamily"/> is configurable,
/// in-app, e-mail and SMS (both on by default), to R02 and R06. <see cref="UntemplatedFamily"/> routes <see cref="UntemplatedEvent"/>,
/// which has no template. People: local.r02 reads English and has a verified mobile number; local.r03 (R03, DEPT anchor) and
/// local.r08 (external, R04 on <see cref="IdentityDatabase.EntityProjectId"/>, R08 on its entity) read Arabic and have none.
/// local.r05 holds R03 bound to <see cref="OtherEntityProjectId"/> only. local.r07 is external, R08 on the suspended entity.
/// local.r04 is disabled.
/// local.r02 and local.r03 may also author templates, so three people can author, validate and publish one.
/// </remarks>
public sealed class NotificationTestHost : IAsyncLifetime
{
    public const string RiskModule = "Risk";
    public const string ConcernModule = "ManagementConcern";
    public const string ReportsModule = "Reports";

    public const string EscalationFamily = "TEST_CONCERN_ESCALATION";
    public const string ReminderFamily = "TEST_RISK_REVIEW_DUE";
    public const string ReportFamily = "TEST_REPORT_READY";
    public const string UntemplatedFamily = "TEST_UNTEMPLATED";

    public const string EscalatedEvent = "ManagementConcern.ConcernEscalated";
    public const string ReviewDueEvent = "Risk.ReviewDue";
    public const string ReportEvent = "Reports.ReportJobCompleted";
    public const string UntemplatedEvent = "Reports.ReportJobFailed";

    /// <summary>Of <see cref="ReminderFamily"/>, which routes in-app and e-mail; it has an in-app template only.</summary>
    public const string HalfTemplatedEvent = "Risk.ReviewOverdue";

    public const string AppBaseUrl = "https://pmplatform.test";
    public const string VerifiedMobileNumber = "+966500000002";

    public static readonly Guid DepartmentId = new(IdentityDatabase.DepartmentId);
    public static readonly Guid EntityId = new(IdentityDatabase.ActiveEntityId);
    public static readonly Guid EntityProjectId = new(IdentityDatabase.EntityProjectId);

    /// <summary>A project of the same department delivered by another entity (the suspended one).</summary>
    public static readonly Guid OtherEntityProjectId = new("00000000-0390-4000-8000-000000000041");
    public static readonly Guid SuspendedEntityId = new(IdentityDatabase.SuspendedEntityId);

    public static readonly Guid RoutingVersionId = new("00000000-0390-4000-8000-000000000001");

    private const string Seed = IdentityDatabase.SeedPrincipalId;

    public static Guid UserId(int n) => new(IdentityDatabase.UserId(n));

    public static string EmailOf(int n) => $"r0{n}@identity.test";

    private static string RoleId(int n) => $"00000000-0000-4000-8000-{n:D12}";

    private static string Families => $"""
        INSERT INTO master_data_config.configuration_version (id, configuration_family_id, version_no, lifecycle_state, created_at, created_by, updated_at, updated_by)
        SELECT '{RoutingVersionId}', f.id, 1, 'DRAFT', now(), '{Seed}', now(), '{Seed}'
        FROM master_data_config.configuration_family f WHERE f.code = 'NOTIFICATION_ROUTING';

        INSERT INTO master_data_config.notification_event_family (id, configuration_version_id, code, label_ar, label_en, is_mandatory, created_at, created_by, updated_at, updated_by)
        SELECT v.id::uuid, '{RoutingVersionId}', v.code, 'اختبار', v.label_en, v.mandatory, now(), '{Seed}', now(), '{Seed}'
        FROM (VALUES ('00000000-0390-4000-8000-000000000011', '{EscalationFamily}', 'Concern escalated', true),
                     ('00000000-0390-4000-8000-000000000012', '{ReminderFamily}', 'Risk review due', false),
                     ('00000000-0390-4000-8000-000000000013', '{ReportFamily}', 'Report ready', false),
                     ('00000000-0390-4000-8000-000000000014', '{UntemplatedFamily}', 'Report failed', false)) AS v (id, code, label_en, mandatory);

        INSERT INTO master_data_config.notification_channel_rule (id, notification_event_family_id, channel, enabled_by_default, created_at, created_by, updated_at, updated_by)
        SELECT gen_random_uuid(), c.family::uuid, c.channel, c.enabled, now(), '{Seed}', now(), '{Seed}'
        FROM (VALUES ('00000000-0390-4000-8000-000000000011', 'IN_APP', true), ('00000000-0390-4000-8000-000000000011', 'EMAIL', true),
                     ('00000000-0390-4000-8000-000000000011', 'SMS', true),
                     ('00000000-0390-4000-8000-000000000012', 'IN_APP', true), ('00000000-0390-4000-8000-000000000012', 'EMAIL', true),
                     ('00000000-0390-4000-8000-000000000013', 'IN_APP', true), ('00000000-0390-4000-8000-000000000013', 'EMAIL', true),
                     ('00000000-0390-4000-8000-000000000013', 'SMS', true),
                     ('00000000-0390-4000-8000-000000000014', 'IN_APP', true)) AS c (family, channel, enabled);

        INSERT INTO master_data_config.notification_recipient_rule (id, notification_event_family_id, role_id, created_at, created_by, updated_at, updated_by)
        SELECT gen_random_uuid(), r.family::uuid, r.role::uuid, now(), '{Seed}', now(), '{Seed}'
        FROM (VALUES ('00000000-0390-4000-8000-000000000011', '{RoleId(2)}'), ('00000000-0390-4000-8000-000000000011', '{RoleId(3)}'),
                     ('00000000-0390-4000-8000-000000000011', '{RoleId(4)}'), ('00000000-0390-4000-8000-000000000011', '{RoleId(8)}'),
                     ('00000000-0390-4000-8000-000000000012', '{RoleId(2)}'),
                     ('00000000-0390-4000-8000-000000000013', '{RoleId(2)}'), ('00000000-0390-4000-8000-000000000013', '{RoleId(6)}'),
                     ('00000000-0390-4000-8000-000000000014', '{RoleId(2)}')) AS r (family, role);

        UPDATE master_data_config.configuration_version
        SET lifecycle_state = 'PUBLISHED', published_at = now() - interval '1 hour', effective_from = now() - interval '1 hour'
        WHERE id = '{RoutingVersionId}';
        """;

    /// <summary>Born DRAFT, as the database insists, then validated and published.</summary>
    private static string Templates => $$$"""
        INSERT INTO notifications.notification_template (id, event_family_code, event_type, channel, version_no, subject_ar, subject_en, body_ar, body_en,
                                                         lifecycle_state, created_at, created_by, updated_at, updated_by)
        VALUES
            (gen_random_uuid(), '{{{EscalationFamily}}}', '{{{EscalatedEvent}}}', 'IN_APP', 1, 'تصعيد {{concernReference}}', 'Escalated: {{concernReference}}',
             'تم تصعيد {{concernReference}} بخطورة {{severity}}.', 'Concern {{concernReference}} was escalated with severity {{severity}}.', 'DRAFT', now(), '{{{UserId(1)}}}', now(), '{{{UserId(1)}}}'),
            (gen_random_uuid(), '{{{EscalationFamily}}}', '{{{EscalatedEvent}}}', 'EMAIL', 1, 'تصعيد {{concernReference}}', 'Escalated: {{concernReference}}',
             'تم تصعيد {{concernReference}}. {{deepLink}}', 'Concern {{concernReference}} was escalated. Open it: {{deepLink}}', 'DRAFT', now(), '{{{UserId(1)}}}', now(), '{{{UserId(1)}}}'),
            (gen_random_uuid(), '{{{EscalationFamily}}}', '{{{EscalatedEvent}}}', 'SMS', 1, NULL, NULL,
             'تصعيد مسألة في مشروعك: {{deepLink}}', 'A concern on your project was escalated: {{deepLink}}', 'DRAFT', now(), '{{{UserId(1)}}}', now(), '{{{UserId(1)}}}'),
            (gen_random_uuid(), '{{{ReminderFamily}}}', '{{{ReviewDueEvent}}}', 'IN_APP', 1, 'مراجعة الخطر مستحقة', 'Risk review due',
             'حان موعد مراجعة الخطر {{riskReference}}.', 'Risk {{riskReference}} is due for review.', 'DRAFT', now(), '{{{UserId(1)}}}', now(), '{{{UserId(1)}}}'),
            (gen_random_uuid(), '{{{ReminderFamily}}}', '{{{ReviewDueEvent}}}', 'EMAIL', 1, 'مراجعة الخطر مستحقة', 'Risk review due',
             'حان موعد مراجعة الخطر {{riskReference}}: {{deepLink}}', 'Risk {{riskReference}} is due for review: {{deepLink}}', 'DRAFT', now(), '{{{UserId(1)}}}', now(), '{{{UserId(1)}}}'),
            (gen_random_uuid(), '{{{ReminderFamily}}}', '{{{HalfTemplatedEvent}}}', 'IN_APP', 1, 'مراجعة متأخرة', 'Review overdue',
             'مراجعة الخطر {{riskReference}} متأخرة.', 'Risk {{riskReference}} review is overdue.', 'DRAFT', now(), '{{{UserId(1)}}}', now(), '{{{UserId(1)}}}'),
            (gen_random_uuid(), '{{{ReportFamily}}}', '{{{ReportEvent}}}', 'IN_APP', 1, 'التقرير جاهز', 'Report ready',
             'التقرير {{reportName}} جاهز.', 'Report {{reportName}} is ready.', 'DRAFT', now(), '{{{UserId(1)}}}', now(), '{{{UserId(1)}}}'),
            (gen_random_uuid(), '{{{ReportFamily}}}', '{{{ReportEvent}}}', 'EMAIL', 1, 'التقرير جاهز', 'Report ready',
             'التقرير {{reportName}} جاهز: {{deepLink}}', 'Report {{reportName}} is ready: {{deepLink}}', 'DRAFT', now(), '{{{UserId(1)}}}', now(), '{{{UserId(1)}}}'),
            (gen_random_uuid(), '{{{ReportFamily}}}', '{{{ReportEvent}}}', 'SMS', 1, NULL, NULL,
             'تقرير جاهز: {{deepLink}}', 'A report is ready: {{deepLink}}', 'DRAFT', now(), '{{{UserId(1)}}}', now(), '{{{UserId(1)}}}');

        UPDATE notifications.notification_template SET lifecycle_state = 'VALIDATED', validated_by_user_id = '{{{UserId(2)}}}', validated_at = now();
        UPDATE notifications.notification_template SET lifecycle_state = 'PUBLISHED', published_by_user_id = '{{{UserId(3)}}}', published_at = now();
        """;

    private static string People => $"""
        UPDATE identity_access."user" SET preferred_language = 'en', mobile_number = '{VerifiedMobileNumber}', mobile_verified_at = now() WHERE id = '{UserId(2)}';

        INSERT INTO identity_access.permission_profile_grant (id, permission_profile_version_id, permission_id, data_scope, created_at, created_by, updated_at, updated_by)
        SELECT gen_random_uuid(), g.version_id::uuid, p.id, 'ALL', now(), '{Seed}', now(), '{Seed}'
        FROM (VALUES ('{IdentityDatabase.ProfileVersionId(2)}', 'NOTIFICATION_TEMPLATE_VIEW'), ('{IdentityDatabase.ProfileVersionId(2)}', 'NOTIFICATION_TEMPLATE_MANAGE'),
                     ('{IdentityDatabase.ProfileVersionId(3)}', 'NOTIFICATION_TEMPLATE_VIEW'), ('{IdentityDatabase.ProfileVersionId(3)}', 'NOTIFICATION_TEMPLATE_MANAGE'))
             AS g (version_id, code)
        JOIN identity_access.permission p ON p.code = g.code;

        INSERT INTO project.project (id, title, title_lang, classification_item_id, department_id, external_entity_id, lifecycle_state,
                                     governance_profile_item_id, participation_mode, created_at, created_by, updated_at, updated_by)
        SELECT '{OtherEntityProjectId}', 'Other entity project', 'en', '00000000-0131-4000-8000-000000000001', '{DepartmentId}', '{SuspendedEntityId}', 'SUBMITTED',
               i.id, 'ENTITY_MANAGED', now(), '{Seed}', now(), '{Seed}'
        FROM master_data_config.master_data_item i JOIN master_data_config.master_data_catalogue c ON c.id = i.catalogue_id
        WHERE c.code = 'GOVERNANCE_PROFILE' AND i.code = 'STANDARD';

        -- local.r05: R03 bound to the other entity's project and nothing else in force.
        INSERT INTO identity_access.access_relationship (id, user_id, permission_profile_version_id, project_id, starts_at, status, created_at, created_by, updated_at, updated_by)
        VALUES ('00000000-0390-4000-8000-000000000051', '{UserId(5)}', '{IdentityDatabase.ProfileVersionId(3)}', '{OtherEntityProjectId}', now() - interval '1 day', 'ACTIVE',
                now(), '{Seed}', now(), '{Seed}');
        """;

    public IdentityTestHost Identity { get; } = new();

    public IdentityDatabase Database => Identity.Database;

    public AdjustableTimeProvider Clock => Identity.Clock;

    public TestSmtpServer Smtp { get; private set; } = null!;

    public TestSmsGateway Sms { get; } = new();

    public IdentityApiFactory Api { get; private set; } = null!;

    /// <summary>The settings every API of this host shares; a test that needs another channel setup overrides them.</summary>
    public Dictionary<string, string?> Settings
    {
        get
        {
            Dictionary<string, string?> settings = new(Smtp.Settings)
            {
                ["APP_BASE_URL"] = AppBaseUrl,
                ["Outbox:PollInterval"] = "01:00:00",
                ["Notifications:Worker:PollInterval"] = "01:00:00",
                ["Notifications:Delivery:RetryBaseDelay"] = "00:01:00",
                ["Approval:Maintenance:PollInterval"] = "01:00:00",
                ["DocumentManagement:Scan:PollInterval"] = "01:00:00",
            };
            return settings;
        }
    }

    public async Task InitializeAsync()
    {
        await Identity.InitializeAsync();
        Smtp = await TestSmtpServer.StartAsync();
        await Database.ExecuteAsync(Families + Templates + People + TestNotificationSource.Schema);
        Api = CreateApi();
    }

    /// <summary>
    /// An API over this database with the host's settings, <paramref name="overrides"/> on top; the test SMS provider unless
    /// <paramref name="withSms"/> is false; the outbox and notification workers only if <paramref name="withWorkers"/>.
    /// </summary>
    public IdentityApiFactory CreateApi(IReadOnlyDictionary<string, string?>? overrides = null, bool withSms = true, bool withWorkers = false)
    {
        Dictionary<string, string?> settings = Settings;
        foreach ((string key, string? value) in overrides ?? new Dictionary<string, string?>())
        {
            settings[key] = value;
        }

        return Identity.CreateApi(settings, services =>
        {
            if (withSms)
            {
                services.AddSingleton<ISmsGateway>(Sms);
            }

            // The tests play every source module, Risk included: its own condition source gives way to theirs.
            services.RemoveAll<INotificationConditionSource>();
            services.AddScoped<INotificationConditionSource, TestRiskSource>();
            if (!withWorkers)
            {
                foreach (ServiceDescriptor worker in services.Where(d => d.ServiceType == typeof(IHostedService)
                                                                        && (d.ImplementationType == typeof(NotificationWorker) || d.ImplementationType == typeof(OutboxDispatchWorker))).ToList())
                {
                    services.Remove(worker);
                }
            }
        });
    }

    public async Task DisposeAsync()
    {
        await Api.DisposeAsync();
        await Smtp.DisposeAsync();
        await Identity.DisposeAsync();
    }
}

[CollectionDefinition(Name)]
public sealed class NotificationSuite : ICollectionFixture<NotificationTestHost>
{
    public const string Name = "Notifications";
}
