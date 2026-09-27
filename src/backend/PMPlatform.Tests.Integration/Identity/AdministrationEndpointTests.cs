using System.Net;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using PMPlatform.Api.Authorization;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;

namespace PMPlatform.Tests.Integration.Identity;

/// <summary>
/// TASK-031's acceptance criteria and validation check through the API: deactivation preserves attribution, an assignment
/// change reaches the user's next request without a new sign-in, and ADM-002–013 answer R01 only.
/// </summary>
[Collection(IdentitySuite.Name)]
public sealed class AdministrationEndpointTests(IdentityTestHost host)
{
    private const string ViewerAssignmentId = "00000000-0111-4000-8000-000000000006";

    /// <summary>Every FG-03 administration endpoint is protected server-side by the permission it names (start-up enforces a declaration).</summary>
    [Fact]
    public void EveryAdministrationEndpointNamesItsPermission()
    {
        Dictionary<string, string?> declarations = host.Api.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(e => e.Metadata.GetMetadata<IEndpointNameMetadata>() is not null)
            .ToDictionary(e => e.Metadata.GetMetadata<IEndpointNameMetadata>()!.EndpointName, EndpointAuthorization.DeclarationOf);

        Dictionary<string, string> expected = new()
        {
            ["IdentityAccess_ListUsers"] = "permission:USER_VIEW",
            ["IdentityAccess_GetUser"] = "permission:USER_VIEW",
            ["IdentityAccess_CreateUser"] = "permission:USER_MANAGE",
            ["IdentityAccess_UpdateUser"] = "permission:USER_MANAGE",
            ["IdentityAccess_ActivateUser"] = "permission:USER_MANAGE",
            ["IdentityAccess_DisableUser"] = "permission:USER_MANAGE",
            ["IdentityAccess_CreateMobileVerificationChallenge"] = "session",
            ["IdentityAccess_VerifyMobileNumber"] = "session",
            ["IdentityAccess_ListAccessRelationships"] = "permission:USER_VIEW",
            ["IdentityAccess_GetAccessRelationship"] = "permission:USER_VIEW",
            ["IdentityAccess_CreateAccessRelationship"] = "permission:ROLE_ASSIGN",
            ["IdentityAccess_EndAccessRelationship"] = "permission:ROLE_ASSIGN",
            ["IdentityAccess_ListRoles"] = "permission:ROLE_VIEW",
            ["IdentityAccess_GetRole"] = "permission:ROLE_VIEW",
            ["IdentityAccess_UpdateRole"] = "permission:ROLE_MANAGE",
            ["IdentityAccess_ListPermissions"] = "permission:ROLE_VIEW",
            ["IdentityAccess_ListPermissionProfiles"] = "permission:ROLE_VIEW",
            ["IdentityAccess_GetPermissionProfile"] = "permission:ROLE_VIEW",
            ["IdentityAccess_ListDepartments"] = "permission:ORGANIZATION_VIEW",
            ["IdentityAccess_GetDepartment"] = "permission:ORGANIZATION_VIEW",
            ["IdentityAccess_CreateDepartment"] = "permission:ORGANIZATION_MANAGE",
            ["IdentityAccess_UpdateDepartment"] = "permission:ORGANIZATION_MANAGE",
            ["IdentityAccess_ActivateDepartment"] = "permission:ORGANIZATION_MANAGE",
            ["IdentityAccess_DeactivateDepartment"] = "permission:ORGANIZATION_MANAGE",
            ["IdentityAccess_ListExternalEntities"] = "permission:ORGANIZATION_VIEW",
            ["IdentityAccess_GetExternalEntity"] = "permission:ORGANIZATION_VIEW",
            ["IdentityAccess_CreateExternalEntity"] = "permission:ORGANIZATION_MANAGE",
            ["IdentityAccess_UpdateExternalEntity"] = "permission:ORGANIZATION_MANAGE",
            ["IdentityAccess_SuspendExternalEntity"] = "permission:ORGANIZATION_MANAGE",
            ["IdentityAccess_ActivateExternalEntity"] = "permission:ORGANIZATION_MANAGE",
            ["IdentityAccess_RetireExternalEntity"] = "permission:ORGANIZATION_MANAGE",
        };

        Assert.All(expected, e => Assert.Equal(e.Value, declarations[e.Key]));
    }

    /// <summary>TASK-032: ADM-002–013 are reachable only for R01. Called directly, as a client bypassing the SPA would.</summary>
    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    public async Task EveryRoleButR01IsRefusedAdministration(int role)
    {
        using HttpClient client = host.Api.CreateClient();
        Session session = await client.SignInOrFailAsync(6);
        try
        {
            await RebindViewerAsync(role);

            HttpStatusCode[] statuses =
            [
                await StatusAsync(client.GetAsync(AdministrationApi.Users, session.AccessToken)),
                await StatusAsync(client.GetAsync($"{AdministrationApi.Users}/{IdentityDatabase.UserId(8)}", session.AccessToken)),
                await StatusAsync(client.PostAsync($"{AdministrationApi.Users}/{IdentityDatabase.UserId(8)}/disable", session.AccessToken)),
                await StatusAsync(client.PostAsync(AdministrationApi.AccessRelationships, session.AccessToken, new { userId = IdentityDatabase.UserId(2), permissionProfileVersionId = IdentityDatabase.ProfileVersionId(1) })),
                await StatusAsync(client.GetAsync(AdministrationApi.Roles, session.AccessToken)),
                await StatusAsync(client.GetAsync(AdministrationApi.Departments, session.AccessToken)),
                await StatusAsync(client.GetAsync(AdministrationApi.ExternalEntities, session.AccessToken)),
            ];

            Assert.All(statuses, status => Assert.Equal(HttpStatusCode.Forbidden, status));
        }
        finally
        {
            await RebindViewerAsync(6);
        }
    }

    /// <summary>
    /// The workbook's validation check: disable a user who manages a project, and the project still names them, as does
    /// every assignment they hold; they are still readable by id; and from then on they cannot sign in.
    /// </summary>
    [Fact]
    public async Task DisablingAProjectManagerKeepsEveryRecordThatNamesThem()
    {
        string projectManager = IdentityDatabase.UserId(8);
        const string attribution = $"""
            SELECT concat_ws('|', p.project_manager_user_id, (SELECT string_agg(a.id || ':' || a.status || ':' || coalesce(a.end_reason, '-'), ',' ORDER BY a.id)
                                                              FROM identity_access.access_relationship a WHERE a.user_id = p.project_manager_user_id))
            FROM project.project p WHERE p.id = '{IdentityDatabase.EntityProjectId}'
            """;
        using HttpClient client = host.Api.CreateClient();
        Session administrator = await client.SignInOrFailAsync(1);
        IReadOnlyList<string> before = await host.Database.QueryAsync(attribution);
        try
        {
            using HttpResponseMessage disable = await client.PostAsync($"{AdministrationApi.Users}/{projectManager}/disable", administrator.AccessToken);
            IReadOnlyList<string> after = await host.Database.QueryAsync(attribution);
            using HttpResponseMessage read = await client.GetAsync($"{AdministrationApi.Users}/{projectManager}", administrator.AccessToken);
            using HttpResponseMessage signIn = await client.SignInAsync("local.r08", TestDirectory.PersonPassword);

            Assert.Equal(HttpStatusCode.OK, disable.StatusCode);
            Assert.Equal("DISABLED", (await disable.ReadObjectAsync())["status"]!.GetValue<string>());
            Assert.StartsWith(projectManager, after[0], StringComparison.Ordinal);
            Assert.True(before.SequenceEqual(after), $"before: {before[0]}\nafter:  {after[0]}");
            JsonObject user = await read.ReadObjectAsync();
            Assert.Equal(("Local R08", "DISABLED"), (user["displayName"]!.GetValue<string>(), user["status"]!.GetValue<string>()));
            Assert.Equal(HttpStatusCode.Unauthorized, signIn.StatusCode);

            using HttpResponseMessage activate = await client.PostAsync($"{AdministrationApi.Users}/{projectManager}/activate", administrator.AccessToken);
            Assert.Equal(HttpStatusCode.OK, activate.StatusCode);
            Assert.Equal(before, await host.Database.QueryAsync(attribution));
        }
        finally
        {
            await host.Database.ExecuteAsync($"UPDATE identity_access.\"user\" SET status = 'ACTIVE', disabled_at = NULL WHERE id = '{projectManager}'");
        }
    }

    /// <summary>
    /// TASK-031's criterion: an assignment made or ended through the API is decided on the user's very next request, with the
    /// token they already hold. The user signs in holding R01 for a moment, so their session passes MFA and may hold R01
    /// later (CTL-07); by the first request they hold only R06 again.
    /// </summary>
    [Fact]
    public async Task AnAssignmentMadeOrEndedTakesEffectOnTheUsersNextRequest()
    {
        string viewer = IdentityDatabase.UserId(6);
        using HttpClient client = host.Api.CreateClient();
        Session administrator = await client.SignInOrFailAsync(1);
        Guid? assignmentId = null;
        try
        {
            Session session = await SignInViewerWithMfaAsync(client);
            await RebindViewerAsync(6);
            using HttpResponseMessage before = await client.GetAsync(AdministrationApi.Users, session.AccessToken);

            using HttpResponseMessage assign = await client.PostAsync(
                AdministrationApi.AccessRelationships, administrator.AccessToken, new { userId = viewer, permissionProfileVersionId = IdentityDatabase.ProfileVersionId(1) });
            assignmentId = AdministrationApi.IdOf(await assign.ReadObjectAsync());
            using HttpResponseMessage whileAssigned = await client.GetAsync(AdministrationApi.Users, session.AccessToken);

            using HttpResponseMessage end = await client.PostAsync($"{AdministrationApi.AccessRelationships}/{assignmentId}/end", administrator.AccessToken);
            using HttpResponseMessage afterEnd = await client.GetAsync(AdministrationApi.Users, session.AccessToken);

            Assert.Equal(HttpStatusCode.Forbidden, before.StatusCode);
            Assert.Equal(HttpStatusCode.Created, assign.StatusCode);
            Assert.Equal(HttpStatusCode.OK, whileAssigned.StatusCode);
            Assert.Equal(HttpStatusCode.OK, end.StatusCode);
            Assert.Equal(("ENDED", "MANUAL"), ((await end.ReadObjectAsync())["status"]!.GetValue<string>(), (await end.ReadObjectAsync())["endReason"]!.GetValue<string>()));
            Assert.Equal(HttpStatusCode.Forbidden, afterEnd.StatusCode);
        }
        finally
        {
            await RebindViewerAsync(6);
            await host.Database.ExecuteAsync($"UPDATE identity_access.\"user\" SET mfa_enrolled_at = NULL WHERE id = '{viewer}'");
            if (assignmentId is { } id)
            {
                await host.Database.ExecuteAsync($"DELETE FROM identity_access.access_relationship WHERE id = '{id}'");
            }
        }
    }

    /// <summary>A disabled user's token grants nothing from the next request on, though it has not expired.</summary>
    [Fact]
    public async Task ADisabledUsersTokenGrantsNothingFromTheNextRequest()
    {
        string viewer = IdentityDatabase.UserId(6);
        using HttpClient client = host.Api.CreateClient();
        Session administrator = await client.SignInOrFailAsync(1);
        try
        {
            Session session = await SignInViewerWithMfaAsync(client);
            using HttpResponseMessage before = await client.GetAsync(AdministrationApi.Roles, session.AccessToken);

            using HttpResponseMessage disable = await client.PostAsync($"{AdministrationApi.Users}/{viewer}/disable", administrator.AccessToken);
            using HttpResponseMessage after = await client.GetAsync(AdministrationApi.Roles, session.AccessToken);

            Assert.Equal(HttpStatusCode.OK, before.StatusCode);
            Assert.Equal(HttpStatusCode.OK, disable.StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, after.StatusCode);
        }
        finally
        {
            await RebindViewerAsync(6);
            await host.Database.ExecuteAsync($"UPDATE identity_access.\"user\" SET status = 'ACTIVE', disabled_at = NULL, mfa_enrolled_at = NULL WHERE id = '{viewer}'");
        }
    }

    /// <summary>ADR-013: project closure ends every active assignment on the project as PROJECT_CLOSED, and no other.</summary>
    [Fact]
    public async Task ClosingAProjectEndsTheAccessItGave()
    {
        const string projectId = "00000000-0150-4000-8000-000000000031";
        const string onProject = "00000000-0111-4000-8000-000000003101";
        const string elsewhere = "00000000-0111-4000-8000-000000003102";
        await host.Database.ExecuteAsync($"""
            INSERT INTO project.project (id, formal_project_id, title, title_lang, classification_item_id, department_id, external_entity_id, lifecycle_state,
                                         governance_profile_item_id, participation_mode, created_at, created_by, updated_at, updated_by)
            SELECT '{projectId}', 'TASK-031-CLOSURE', 'Project to close', 'en', classification_item_id, department_id, external_entity_id, 'COMPLETED', governance_profile_item_id,
                   participation_mode, now(), '{IdentityDatabase.SeedPrincipalId}', now(), '{IdentityDatabase.SeedPrincipalId}'
            FROM project.project WHERE id = '{IdentityDatabase.EntityProjectId}';
            INSERT INTO identity_access.access_relationship (id, user_id, permission_profile_version_id, project_id, starts_at, status, created_at, created_by, updated_at, updated_by)
            VALUES ('{onProject}', '{IdentityDatabase.UserId(2)}', '{IdentityDatabase.ProfileVersionId(4)}', '{projectId}', now() - interval '1 day', 'ACTIVE',
                    now(), '{IdentityDatabase.SeedPrincipalId}', now(), '{IdentityDatabase.SeedPrincipalId}'),
                   ('{elsewhere}', '{IdentityDatabase.UserId(2)}', '{IdentityDatabase.ProfileVersionId(4)}', '{IdentityDatabase.EntityProjectId}', now() - interval '1 day', 'ACTIVE',
                    now(), '{IdentityDatabase.SeedPrincipalId}', now(), '{IdentityDatabase.SeedPrincipalId}');
            """);
        try
        {
            using IServiceScope scope = host.Api.Services.CreateScope();
            int ended = await scope.ServiceProvider.GetRequiredService<IProjectAccessLifecycle>()
                .EndAccessForClosedProjectAsync(Guid.Parse(projectId), Guid.Parse(IdentityDatabase.UserId(1)), CancellationToken.None);

            Assert.Equal(1, ended);
            Assert.Equal(
                [$"{onProject}|ENDED|PROJECT_CLOSED|{IdentityDatabase.UserId(1)}", $"{elsewhere}|ACTIVE||{IdentityDatabase.SeedPrincipalId}"],
                await host.Database.QueryAsync($"""
                    SELECT concat_ws('|', id, status, coalesce(end_reason, ''), updated_by) FROM identity_access.access_relationship
                    WHERE id IN ('{onProject}', '{elsewhere}') ORDER BY id
                    """));
        }
        finally
        {
            await host.Database.ExecuteAsync($"""
                DELETE FROM identity_access.access_relationship WHERE id IN ('{onProject}', '{elsewhere}');
                DELETE FROM project.project WHERE id = '{projectId}';
                """);
        }
    }

    /// <summary>Signs the viewer in holding R01, which requires MFA, so the session passes (and enrols) a second factor.</summary>
    private async Task<Session> SignInViewerWithMfaAsync(HttpClient client)
    {
        await RebindViewerAsync(1);
        Session session = await client.SignInOrFailAsync(6);
        Assert.True(session.User.MultiFactorAuthenticated);
        return session;
    }

    private static async Task<HttpStatusCode> StatusAsync(Task<HttpResponseMessage> request)
    {
        using HttpResponseMessage response = await request;
        return response.StatusCode;
    }

    private Task RebindViewerAsync(int role) =>
        host.Database.ExecuteAsync($"UPDATE identity_access.access_relationship SET permission_profile_version_id = '{IdentityDatabase.ProfileVersionId(role)}' WHERE id = '{ViewerAssignmentId}'");
}
