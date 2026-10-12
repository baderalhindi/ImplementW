using System.Net.Http.Headers;
using Microsoft.Extensions.DependencyInjection;
using PMPlatform.Application.Common.Auditing;
using PMPlatform.Domain.Common;
using PMPlatform.Infrastructure.Persistence;
using PMPlatform.Tests.Integration.Identity;

namespace PMPlatform.Tests.Integration.AuditActivity;

/// <summary>
/// The identity test database and people with what FG-06 (TASK-073) needs: a second department and its project, two
/// DATA_CLASSIFICATION levels, and Audit Reviewer grants beside the shipped ones — AHDA has named no reviewer role (TBC-AUD-05 to
/// -07), so the reviewers here are the test's own. The API's ingestion and projection workers poll once an hour, so an event found
/// right after a request was stored by the commit path, not by a worker.
/// </summary>
/// <remarks>
/// Projects: A is the identity database's entity-delivered project (department 1, entity 1, managed by local.r08); B is internal, in
/// department 2, managed by local.r02. People: local.r01 (R01) holds only the shipped grants: administration audit and operations.
/// local.r02 (R02) reviews security, business and request context at ALL, business with a LOW clearance, and reads Activity at ALL.
/// local.r03 (R03, DEPT on department 1) reviews business audit and reads Activity at DEPT, and security audit at ALL without request
/// context. local.r06 (R06) holds nothing. local.r08
/// is ADR-013's entity Project Manager: external, R08 on entity 1 and R04 on project A, with the shipped Activity grants.
/// </remarks>
public sealed class AuditActivityTestHost : IAsyncLifetime
{
    public static readonly Guid ProjectA = new(IdentityDatabase.EntityProjectId);
    public static readonly Guid ProjectB = new("00000000-0730-4000-8000-000000000002");
    public static readonly Guid DepartmentB = new("00000000-0730-4000-8000-000000000001");
    public static readonly Guid EntityA = new(IdentityDatabase.ActiveEntityId);
    public static readonly Guid LowClassification = new("00000000-0730-4000-8000-00000000c001");
    public static readonly Guid HighClassification = new("00000000-0730-4000-8000-00000000c002");

    private const string Seed = IdentityDatabase.SeedPrincipalId;

    private static string Fixture => $"""
        INSERT INTO identity_access.department (id, code, name_ar, name_en, directory_reference, created_at, created_by, updated_at, updated_by)
        VALUES ('{DepartmentB}', 'DEPT-AUDIT-B', 'إدارة ب', 'Department B', 'DEPT-AUDIT-B', now(), '{Seed}', now(), '{Seed}');

        INSERT INTO project.project (id, title, title_lang, classification_item_id, department_id, external_entity_id, project_manager_user_id, lifecycle_state,
                                     governance_profile_item_id, participation_mode, created_at, created_by, updated_at, updated_by)
        SELECT '{ProjectB}', 'Internal project', 'en', '00000000-0131-4000-8000-000000000001', '{DepartmentB}', NULL, '{IdentityDatabase.UserId(2)}', 'SUBMITTED',
               i.id, 'AHDA_MANAGED', now(), '{Seed}', now(), '{Seed}'
        FROM master_data_config.master_data_item i JOIN master_data_config.master_data_catalogue c ON c.id = i.catalogue_id
        WHERE c.code = 'GOVERNANCE_PROFILE' AND i.code = 'STANDARD';

        INSERT INTO master_data_config.master_data_item (id, catalogue_id, code, label_ar, label_en, sort_order, lifecycle_state, created_at, created_by, updated_at, updated_by)
        SELECT v.id::uuid, c.id, v.code, 'مستوى', v.code, v.sort_order, 'PUBLISHED', now(), '{Seed}', now(), '{Seed}'
        FROM master_data_config.master_data_catalogue c,
             (VALUES ('{LowClassification}', 'AUDIT_TEST_LOW', 1), ('{HighClassification}', 'AUDIT_TEST_HIGH', 2)) AS v (id, code, sort_order)
        WHERE c.code = 'DATA_CLASSIFICATION';
        UPDATE identity_access.permission SET data_classification_item_id = '{LowClassification}' WHERE code = 'AUDIT_BUSINESS_VIEW';

        INSERT INTO identity_access.permission_profile_grant (id, permission_profile_version_id, permission_id, data_scope, created_at, created_by, updated_at, updated_by)
        SELECT gen_random_uuid(), g.version_id::uuid, p.id, g.scope, now(), '{Seed}', now(), '{Seed}'
        FROM (VALUES {Grants(2, "ALL", "AUDIT_SECURITY_VIEW", "AUDIT_BUSINESS_VIEW", "AUDIT_CONTEXT_VIEW", "ACTIVITY_VIEW")},
                     {Grants(3, "DEPT", "AUDIT_BUSINESS_VIEW", "ACTIVITY_VIEW")},
                     {Grants(3, "ALL", "AUDIT_SECURITY_VIEW")})
             AS g (version_id, code, scope)
        JOIN identity_access.permission p ON p.code = g.code;
        """;

    public IdentityTestHost Identity { get; } = new();

    public IdentityDatabase Database => Identity.Database;

    public IdentityApiFactory Api { get; private set; } = null!;

    public static Guid Person(int n) => Guid.Parse(IdentityDatabase.UserId(n));

    public async Task InitializeAsync()
    {
        await Identity.InitializeAsync();
        await Database.ExecuteAsync(Fixture);
        Api = Identity.CreateApi(new Dictionary<string, string?>
        {
            ["Audit:Ingestion:PollInterval"] = "01:00:00",
            ["Audit:Activity:PollInterval"] = "01:00:00",
            ["Outbox:PollInterval"] = "01:00:00",
            ["Approval:Maintenance:PollInterval"] = "01:00:00",
        });
    }

    /// <summary>A client signed in as local.r0<paramref name="person"/>, sending <paramref name="correlationId"/> on every request when given.</summary>
    public async Task<HttpClient> ClientAsync(int person, Guid? correlationId = null)
    {
        HttpClient client = Api.CreateClient();
        Session session = await client.SignInOrFailAsync(person);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
        if (correlationId is { } id)
        {
            client.WithCorrelationId(id);
        }

        return client;
    }

    /// <summary>
    /// Captures the entries as a producer does — staged in a unit of work whose save commits them — through the API's own trail,
    /// outbox, ingestion and projection. Returns once the save has committed, and the commit path has stored and projected them.
    /// </summary>
    public async Task StageAsync(params AuditEntry[] entries)
    {
        await using AsyncServiceScope scope = Api.Services.CreateAsyncScope();
        IAuditTrail trail = scope.ServiceProvider.GetRequiredService<IAuditTrail>();
        foreach (AuditEntry entry in entries)
        {
            trail.Stage(entry);
        }

        await scope.ServiceProvider.GetRequiredService<PMPlatformDbContext>().SaveChangesAsync();
    }

    /// <summary>A business event on a project, as the module that owns the record would stage it.</summary>
    public static AuditEntry Business(string eventType, Guid projectId, Guid subjectId, int actor, AuditEventClass eventClass = AuditEventClass.LifecycleTransition,
        Guid? classification = null, Guid? entityId = null) =>
        new(eventClass, eventType, AuditOutcome.Success)
        {
            ActorUserId = Person(actor),
            Subject = new AuditSubject(eventType[..eventType.IndexOf('.', StringComparison.Ordinal)], "TestRecord", subjectId),
            ScopeProjectId = projectId,
            ScopeExternalEntityId = entityId,
            DataClassificationItemId = classification,
            Attributes = [AuditAttribute.Of("status", "CHANGED")],
        };

    public async Task<T> WithScopeAsync<T>(Func<IServiceProvider, Task<T>> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        await using AsyncServiceScope scope = Api.Services.CreateAsyncScope();
        return await action(scope.ServiceProvider);
    }

    /// <summary>The id of the one stored event about <paramref name="subjectId"/>.</summary>
    public async Task<Guid> EventIdAsync(Guid subjectId) =>
        Guid.Parse(Assert.Single(await Database.QueryAsync($"SELECT id::text FROM audit_activity.audit_event WHERE subject_id = '{subjectId}'")));

    public async Task DisposeAsync()
    {
        await Api.DisposeAsync();
        await Identity.DisposeAsync();
    }

    /// <summary>The context the API uses, for a test that tries the data layer directly.</summary>
    public static PMPlatformDbContext Context(IServiceProvider services) => services.GetRequiredService<PMPlatformDbContext>();

    private static string Grants(int role, string scope, params string[] permissions) =>
        string.Join(", ", permissions.Select(p => $"('{IdentityDatabase.ProfileVersionId(role)}', '{p}', '{scope}')"));
}

[CollectionDefinition(Name)]
public sealed class AuditActivitySuite : ICollectionFixture<AuditActivityTestHost>
{
    public const string Name = "AuditActivity";
}

internal static class AuditActivityApi
{
    public const string Events = "/api/v1/audit-events";
    public const string Access = "/api/v1/audit-access";
    public const string Activity = "/api/v1/activity-entries";
    public const string Status = "/api/v1/audit-ingestion-status";
    public const string Verifications = "/api/v1/audit-integrity-verifications";
    public const string ProjectionChecks = "/api/v1/activity-projection-checks";
    public const string ProjectionRebuilds = "/api/v1/activity-projection-rebuilds";

    /// <summary>A sensitive write: a fresh <c>Idempotency-Key</c> (R-36).</summary>
    public static async Task<HttpResponseMessage> PostSensitiveAsync(this HttpClient client, string path)
    {
        using HttpRequestMessage request = new(HttpMethod.Post, path);
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        return await client.SendAsync(request);
    }

    /// <summary>The body of a 2xx, as JSON.</summary>
    public static async Task<System.Text.Json.Nodes.JsonObject> OkAsync(this HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return await response.ReadObjectAsync();
    }

    /// <summary>The ids of a page's items.</summary>
    public static IReadOnlyList<Guid> Ids(this System.Text.Json.Nodes.JsonObject page, string idField = "id") =>
        [.. page["items"]!.AsArray().Select(i => Guid.Parse(i![idField]!.GetValue<string>()))];
}
