using System.Net;
using System.Text.Json.Nodes;
using PMPlatform.Application.Features.Approval.Contracts;
using PMPlatform.Tests.Integration.Identity;
using PMPlatform.Tests.Integration.MasterDataConfig;

namespace PMPlatform.Tests.Integration.Project;

/// <summary>
/// ADR-013 as TASK-041 applies it: an entity user registers and sees projects of their own entity only; AHDA keeps the
/// review and activation gates, refused to an external user whatever they hold; and the Project Manager is an R04 holder,
/// internal or of the delivering entity — not resolved from the AHDA directory alone.
/// </summary>
[Collection(ProjectSuite.Name)]
public sealed class ProjectAuthorizationTests(ProjectTestHost host)
{
    [Fact]
    public async Task AnEntityUserRegistersAndSeesOnlyTheirOwnEntitysProjects()
    {
        using HttpClient client = host.Api.CreateClient();
        Sessions sessions = await client.SignInAsync();

        // Another entity's project, and one with no entity, registered by AHDA (local.r03, DEPT).
        Guid otherEntity = AdministrationApi.IdOf(await client.CreateOrFailAsync(
            sessions.Reviewer, host.Registration(change: r => r["externalEntityId"] = ProjectTestHost.OtherEntityId.ToString())));
        Guid ahdaManaged = AdministrationApi.IdOf(await client.CreateOrFailAsync(sessions.Reviewer, host.Registration(change: r =>
        {
            r["externalEntityId"] = null;
            r["participationMode"] = "AHDA_MANAGED";
        })));
        Guid own = AdministrationApi.IdOf(await client.CreateOrFailAsync(sessions.Entity, host.Registration()));

        foreach (Guid hidden in new[] { otherEntity, ahdaManaged })
        {
            using HttpResponseMessage read = await client.GetAsync($"{ProjectDriver.Projects}/{hidden}", sessions.Entity);
            Assert.Equal(HttpStatusCode.NotFound, read.StatusCode);
        }

        using HttpResponseMessage page = await client.GetAsync($"{ProjectDriver.Projects}?pageSize=200", sessions.Entity);
        HashSet<string> listed = [.. (await page.ReadObjectAsync())["items"]!.AsArray().Select(p => p!["externalEntityId"]?.GetValue<string>() ?? "none")];
        Assert.Equal([ProjectTestHost.EntityId.ToString()], listed);
        Assert.Contains(own.ToString(), await ListIdsAsync(client, sessions.Entity));

        // Registering for another entity, or for none, is refused; nothing is written.
        foreach (JsonObject outside in new[]
                 {
                     host.Registration(change: r => r["externalEntityId"] = ProjectTestHost.OtherEntityId.ToString()),
                     host.Registration(change: r => { r["externalEntityId"] = null; r["participationMode"] = "AHDA_MANAGED"; }),
                 })
        {
            using HttpResponseMessage refused = await client.PostAsync(ProjectDriver.Projects, sessions.Entity, outside);
            Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
        }

        // Nor may they move their own draft to another entity.
        using HttpResponseMessage current = await client.GetAsync($"{ProjectDriver.Projects}/{own}", sessions.Entity);
        using HttpResponseMessage moved = await client.PutAsync(
            $"{ProjectDriver.Projects}/{own}", sessions.Entity,
            host.Registration(change: r => r["externalEntityId"] = ProjectTestHost.OtherEntityId.ToString()), AdministrationApi.ETagOf(current));
        Assert.Equal(HttpStatusCode.Forbidden, moved.StatusCode);
        Assert.Equal(ProjectTestHost.EntityId.ToString(), (await host.RowAsync(own))["external_entity_id"]!.GetValue<string>());
    }

    /// <summary>
    /// ADR-013: AHDA retains every approval and lifecycle gate. local.r08's test R04 profile holds review and activation at
    /// ENTITY, so the engine allows them on their own project; the person check refuses them, and the refusal is audited.
    /// </summary>
    [Fact]
    public async Task AnExternalUserNeverStartsAReviewNorActivatesEvenWithTheGrant()
    {
        using HttpClient client = host.Api.CreateClient();
        Sessions sessions = await client.SignInAsync();
        Guid projectId = AdministrationApi.IdOf(await client.CreateOrFailAsync(sessions.Entity, host.Registration()));
        await client.CommandOrFailAsync(sessions.Entity, projectId, "submit", new { projectManagerUserId = ProjectDriver.Person(8) });

        using (HttpResponseMessage review = await client.PostAsync($"{ProjectDriver.Projects}/{projectId}/start-review", sessions.Entity))
        {
            Assert.Equal(HttpStatusCode.Forbidden, review.StatusCode);
        }

        await client.CommandOrFailAsync(sessions.Reviewer, projectId, "start-review");
        await host.DecideAndDeliverAsync(projectId, ApprovalTaskDecision.Approve);

        using (HttpResponseMessage activate = await client.PostAsync($"{ProjectDriver.Projects}/{projectId}/activate", sessions.Entity))
        {
            Assert.Equal(HttpStatusCode.Forbidden, activate.StatusCode);
        }

        Assert.Equal("APPROVED_PLANNED", (await host.RowAsync(projectId))["lifecycle_state"]!.GetValue<string>());
        Assert.Equal(
            ["PROJECT_ACTIVATE|EXTERNAL_USER", "PROJECT_REVIEW|EXTERNAL_USER"],
            (await host.Database.QueryAsync($"""
                SELECT g.new_value || '|' || r.new_value
                FROM audit_activity.audit_event e
                JOIN audit_activity.audit_event_attribute g ON g.audit_event_id = e.id AND g.attribute_name = 'gate'
                JOIN audit_activity.audit_event_attribute r ON r.audit_event_id = e.id AND r.attribute_name = 'reason'
                WHERE e.event_type = 'Project.LifecycleGateRefused' AND e.subject_id = '{projectId}' AND e.actor_user_id = '{ProjectDriver.Person(8)}'
                  AND e.outcome = 'DENIED' AND e.event_class = 'AUTHORIZATION_DENIAL'
                """)).Order());
    }

    /// <summary>
    /// ADR-013: the manager is an R04 holder over the project now — local.r05 (internal) or local.r08 (of the delivering
    /// entity) — and not merely someone in the directory: local.r06 holds no R04, local.r07's entity is suspended, and
    /// local.r08 belongs to another entity than this project's.
    /// </summary>
    [Fact]
    public async Task TheProjectManagerIsAnR04HolderOverTheProjectInternalOrOfItsEntity()
    {
        using HttpClient client = host.Api.CreateClient();
        Sessions sessions = await client.SignInAsync();
        Guid ownEntity = AdministrationApi.IdOf(await client.CreateOrFailAsync(sessions.Reviewer, host.Registration()));
        Guid otherEntity = AdministrationApi.IdOf(await client.CreateOrFailAsync(
            sessions.Reviewer, host.Registration(change: r => r["externalEntityId"] = ProjectTestHost.OtherEntityId.ToString())));

        foreach ((Guid project, int manager) in new[] { (ownEntity, 6), (ownEntity, 7), (otherEntity, 8), (ownEntity, 4) })
        {
            using HttpResponseMessage refused = await client.PostAsync(
                $"{ProjectDriver.Projects}/{project}/submit", sessions.Reviewer, new { projectManagerUserId = ProjectDriver.Person(manager) });
            Assert.Equal((HttpStatusCode.UnprocessableEntity, "PROJECT_MANAGER_INVALID"), (refused.StatusCode, await refused.CodeOfAsync()));
            Assert.Equal(["projectManagerUserId NOT_ALLOWED"], await refused.ReadFieldErrorsAsync());
        }

        Assert.Equal("SUBMITTED", (await client.CommandOrFailAsync(sessions.Reviewer, ownEntity, "submit", new { projectManagerUserId = ProjectDriver.Person(8) })).Status());
        Assert.Equal("SUBMITTED", (await client.CommandOrFailAsync(sessions.Reviewer, otherEntity, "submit", new { projectManagerUserId = ProjectDriver.Person(5) })).Status());
    }

    [Fact]
    public async Task ARoleWithoutAProjectPermissionIsRefusedAtTheGate()
    {
        using HttpClient client = host.Api.CreateClient();
        string viewer = (await client.SignInOrFailAsync(6)).AccessToken;

        using HttpResponseMessage list = await client.GetAsync(ProjectDriver.Projects, viewer);
        using HttpResponseMessage create = await client.PostAsync(ProjectDriver.Projects, viewer, host.Registration());

        Assert.Equal((HttpStatusCode.Forbidden, HttpStatusCode.Forbidden), (list.StatusCode, create.StatusCode));
    }

    private static async Task<IReadOnlyList<string>> ListIdsAsync(HttpClient client, string token)
    {
        using HttpResponseMessage page = await client.GetAsync($"{ProjectDriver.Projects}?pageSize=200", token);
        return [.. (await page.ReadObjectAsync())["items"]!.AsArray().Select(p => p!["id"]!.GetValue<string>())];
    }
}
