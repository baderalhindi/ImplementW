using System.Net;
using PMPlatform.Tests.Integration.Identity;
using PMPlatform.Tests.Integration.MasterDataConfig;
using static PMPlatform.Tests.Integration.ExternalParticipation.ExternalParticipationDriver;

namespace PMPlatform.Tests.Integration.ExternalParticipation;

/// <summary>
/// Review round 7, item 2: a department's manager changes a project's Project Manager with a reason. The new manager must hold R04 over the project; the
/// outgoing manager's open tasks and update requests on it pass to them; both are told; the change is audited. Here because this host has update requests.
/// </summary>
[Collection(ExternalParticipationSuite.Name)]
public sealed class ProjectManagerChangeTests(ExternalParticipationTestHost host)
{
    [Fact]
    public async Task TheNewManagerTakesTheOpenTasksAndUpdateRequestsAndBothAreTold()
    {
        using HttpClient client = host.Api.CreateClient();
        ParticipationSessions sessions = await client.SignInAsync();
        Guid projectId = await host.ProjectAsync();
        Guid taskId = Guid.NewGuid();

        // local.r08 of the delivering entity takes R04 over this project; local.r05 manages it, answers its updates and owns an open task.
        await host.Database.ExecuteAsync($"""
            INSERT INTO identity_access.access_relationship (id, user_id, permission_profile_version_id, external_entity_id, project_id, sponsor_user_id, starts_at, status,
                                                             created_at, created_by, updated_at, updated_by)
            VALUES (gen_random_uuid(), '{Person(8)}', '{IdentityDatabase.ProfileVersionId(4)}', '{ExternalParticipationTestHost.EntityA}', '{projectId}', '{Person(2)}',
                    now() - interval '1 hour', 'ACTIVE', now(), '{IdentityDatabase.SeedPrincipalId}', now(), '{IdentityDatabase.SeedPrincipalId}');
            UPDATE project.project SET entity_contact_user_id = '{Person(5)}' WHERE id = '{projectId}';
            INSERT INTO project_task.project_task (id, project_id, assignee_user_id, status, planned_start_date, planned_finish_date, planned_duration_days, title, title_lang,
                                                   created_at, created_by, updated_at, updated_by)
            VALUES ('{taskId}', '{projectId}', '{Person(5)}', 'NOT_STARTED', current_date, current_date + 9, 10, 'Site survey', 'en',
                    now(), '{IdentityDatabase.SeedPrincipalId}', now(), '{IdentityDatabase.SeedPrincipalId}');
            """);
        await client.CreatedOrFailAsync(sessions.DepartmentManager, "/api/v1/update-request-batches", new
        {
            items = new[] { new { projectId, responsibleUserId = (Guid?)Person(5) } },
            dueDate = Iso(Today.AddDays(5)),
            repeatMonthly = false,
            repeatDay = (int?)null,
            instructions = "Update November",
            reviewerUserId = Person(3),
        });

        string path = $"/api/v1/projects/{projectId}";
        string tag = await ETagAsync(client, sessions.DepartmentManager, path);
        static object Change(int person, string reason = "The contractor's site team takes over") =>
            new { projectManagerUserId = Person(person), reason = new { text = reason, language = "en" } };

        // local.r06 holds no Project Manager role over the project.
        using (HttpResponseMessage ineligible = await client.PostAsync($"{path}/change-manager", sessions.DepartmentManager, Change(6), tag))
        {
            Assert.Equal((HttpStatusCode.UnprocessableEntity, "PROJECT_MANAGER_INVALID"), (ineligible.StatusCode, await ineligible.CodeOfAsync()));
        }

        // The external user holds no PROJECT_MANAGER_CHANGE.
        using (HttpResponseMessage external = await client.PostAsync($"{path}/change-manager", sessions.EntityA, Change(8), tag))
        {
            Assert.Equal(HttpStatusCode.Forbidden, external.StatusCode);
        }

        using (HttpResponseMessage changed = await client.PostAsync($"{path}/change-manager", sessions.DepartmentManager, Change(8), tag))
        {
            Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
            Assert.Equal(Person(8), Guid.Parse((await changed.ReadObjectAsync())["projectManagerUserId"]!.GetValue<string>()));
            tag = AdministrationApi.ETagOf(changed);
        }

        using (HttpResponseMessage again = await client.PostAsync($"{path}/change-manager", sessions.DepartmentManager, Change(8), tag))
        {
            Assert.Equal((HttpStatusCode.Conflict, "PROJECT_MANAGER_UNCHANGED"), (again.StatusCode, await again.CodeOfAsync()));
        }

        Assert.Equal(Person(8).ToString(), Assert.Single(await host.Database.QueryAsync($"SELECT assignee_user_id::text FROM project_task.project_task WHERE id = '{taskId}'")));
        Assert.Equal(Person(8).ToString(), Assert.Single(await host.Database.QueryAsync(
            $"SELECT responsible_user_id::text FROM external_participation.external_update_request WHERE project_id = '{projectId}'")));
        Assert.Equal(Person(8).ToString(), Assert.Single(await host.Database.QueryAsync($"SELECT entity_contact_user_id::text FROM project.project WHERE id = '{projectId}'")));
        Assert.Equal<string>(
            ["moved_tasks=1", "moved_update_requests=1", $"project_manager_user_id={Person(5)}>{Person(8)}"],
            await host.Database.QueryAsync($"""
                SELECT a.attribute_name || '=' || coalesce(a.old_value || '>', '') || a.new_value
                FROM audit_activity.audit_event e JOIN audit_activity.audit_event_attribute a ON a.audit_event_id = e.id
                WHERE e.event_type = 'Project.ManagerChanged' AND e.subject_id = '{projectId}' AND a.attribute_name <> 'reason_text'
                ORDER BY a.attribute_name
                """));
        Assert.Equal(2, (await host.Database.QueryAsync(
            $"SELECT id::text FROM common.outbox_message WHERE message_key LIKE 'Project.ManagerChanged:{projectId}:%'")).Count);
    }

    private static async Task<string> ETagAsync(HttpClient client, string token, string path)
    {
        using HttpResponseMessage read = await client.GetAsync(path, token);
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        return AdministrationApi.ETagOf(read);
    }
}
