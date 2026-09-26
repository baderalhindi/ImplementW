using System.Net;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using PMPlatform.Api.Authorization;
using PMPlatform.Application.Common.Authorization;

namespace PMPlatform.Tests.Integration.Identity;

/// <summary>
/// TASK-030 through the API, as a client that bypasses the SPA would call it: every endpoint is protected server-side,
/// and ADM-041 answers each role by the grants of the profile version the user is bound to now, not by the token.
/// </summary>
[Collection(IdentitySuite.Name)]
public sealed class AuthorizationEndpointTests(IdentityTestHost host)
{
    private const string ViewerAssignmentId = "00000000-0111-4000-8000-000000000006";
    private const string SystemAdministratorAssignmentId = "00000000-0111-4000-8000-000000000001";

    /// <summary>The acceptance criterion: no endpoint is protected by the SPA alone. Start-up enforces it; this shows the map.</summary>
    [Fact]
    public void EveryEndpointDeclaresItsServerSideProtection()
    {
        Dictionary<string, string?> declarations = host.Api.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .ToDictionary(e => e.Metadata.GetMetadata<IEndpointNameMetadata>()?.EndpointName ?? e.RoutePattern.RawText!, EndpointAuthorization.DeclarationOf);

        Assert.DoesNotContain(declarations, d => d.Value is null);
        Assert.Equal("permission:IDENTITY_INTEGRATION_MANAGE", declarations["IdentityAccess_GetIdentityIntegration"]);
        Assert.Equal("permission:IDENTITY_INTEGRATION_MANAGE", declarations["IdentityAccess_TestIdentityIntegration"]);
        Assert.Equal("session", declarations["IdentityAccess_GetCurrentSession"]);
        Assert.Equal("anonymous", declarations["IdentityAccess_CreateSession"]);
    }

    /// <summary>
    /// The workbook's validation: a direct call with a token for a role marked "—" for ADM-041 is 403. The viewer's one
    /// assignment is rebound to each role's shipped-default version in turn; the same token then carries that role's grants.
    /// </summary>
    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    public async Task EveryRoleButR01IsRefusedIdentityIntegration(int role)
    {
        using HttpClient client = host.Api.CreateClient();
        Session session = await client.SignInOrFailAsync(6);
        try
        {
            await RebindViewerAsync(role);

            using HttpResponseMessage read = await client.GetWithTokenAsync(SessionApi.IdentityIntegration, session.AccessToken);
            using HttpResponseMessage test = await client.PostWithTokenAsync(SessionApi.IdentityIntegrationTest, session.AccessToken);

            Assert.Equal(HttpStatusCode.Forbidden, read.StatusCode);
            Assert.Equal("PERMISSION_DENIED", (await read.ReadAsync<Problem>()).Code);
            Assert.Equal(HttpStatusCode.Forbidden, test.StatusCode);
        }
        finally
        {
            await RebindViewerAsync(6);
        }
    }

    [Fact]
    public async Task R01IsAllowedIdentityIntegration()
    {
        using HttpClient client = host.Api.CreateClient();
        Session session = await client.SignInOrFailAsync(1);

        using HttpResponseMessage response = await client.GetWithTokenAsync(SessionApi.IdentityIntegration, session.AccessToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>ADR-013: the entity Project Manager's R01 assignment is not honoured, so their real session is refused.</summary>
    [Fact]
    public async Task AnExternalUserHoldingR01IsRefused()
    {
        using HttpClient client = host.Api.CreateClient();
        Session session = await client.SignInOrFailAsync(8);

        using HttpResponseMessage response = await client.GetWithTokenAsync(SessionApi.IdentityIntegration, session.AccessToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>
    /// ADR-013, one layer down: the engine itself holds no grant of an internal-only role for an external user. (The request
    /// above is also refused by the MFA check, so this is what shows the filter on its own.)
    /// </summary>
    [Fact]
    public async Task TheEngineHoldsNoInternalOnlyRoleOfAnExternalUser()
    {
        using IServiceScope scope = host.Api.Services.CreateScope();
        IAuthorizationEngine engine = scope.ServiceProvider.GetRequiredService<IAuthorizationEngine>();

        AuthorizationPrincipal? entityProjectManager = await engine.GetPrincipalAsync(Guid.Parse(IdentityDatabase.UserId(8)), CancellationToken.None);
        AuthorizationPrincipal? systemAdministrator = await engine.GetPrincipalAsync(Guid.Parse(IdentityDatabase.UserId(1)), CancellationToken.None);

        Assert.DoesNotContain(entityProjectManager!.Grants, g => g.RoleCode == "R01");
        Assert.Contains(systemAdministrator!.Grants, g => g.RoleCode == "R01" && g.PermissionCode == PermissionCatalogue.IdentityIntegrationManage);
    }

    /// <summary>
    /// R01 bound mid-session to a user whose token never passed MFA grants nothing until they pass it (CTL-07): the handler
    /// checks MFA against the roles held now, not those the token was issued with.
    /// </summary>
    [Fact]
    public async Task R01GainedMidSessionGrantsNothingWithoutMfa()
    {
        using HttpClient client = host.Api.CreateClient();
        Session session = await client.SignInOrFailAsync(6);
        Assert.False(session.User.MultiFactorAuthenticated);
        try
        {
            await RebindViewerAsync(1);

            using HttpResponseMessage response = await client.GetWithTokenAsync(SessionApi.IdentityIntegration, session.AccessToken);

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
        finally
        {
            await RebindViewerAsync(6);
        }
    }

    /// <summary>TASK-031's criterion, which the engine makes true: an ended assignment stops the very next request, with no new sign-in.</summary>
    [Fact]
    public async Task AnEndedAssignmentTakesEffectOnTheNextRequest()
    {
        using HttpClient client = host.Api.CreateClient();
        Session session = await client.SignInOrFailAsync(1);
        try
        {
            using HttpResponseMessage before = await client.GetWithTokenAsync(SessionApi.IdentityIntegration, session.AccessToken);
            await host.Database.ExecuteAsync($"UPDATE identity_access.access_relationship SET status = 'ENDED', ends_at = now(), end_reason = 'MANUAL' WHERE id = '{SystemAdministratorAssignmentId}'");
            using HttpResponseMessage after = await client.GetWithTokenAsync(SessionApi.IdentityIntegration, session.AccessToken);

            Assert.Equal(HttpStatusCode.OK, before.StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, after.StatusCode);
        }
        finally
        {
            await host.Database.ExecuteAsync($"UPDATE identity_access.access_relationship SET status = 'ACTIVE', ends_at = NULL, end_reason = NULL WHERE id = '{SystemAdministratorAssignmentId}'");
        }
    }

    /// <summary>ADR-018: permissions come from the profile version the assignment is bound to, and only a PUBLISHED one grants.</summary>
    [Fact]
    public async Task GrantsComeFromTheBoundPublishedProfileVersion()
    {
        const string publishedWithout = "00000000-0002-4000-8000-000000001001";
        const string draftWith = "00000000-0002-4000-8000-000000001002";
        using HttpClient client = host.Api.CreateClient();
        Session session = await client.SignInOrFailAsync(1);
        try
        {
            await host.Database.ExecuteAsync($"""
                INSERT INTO identity_access.permission_profile_version (id, permission_profile_id, version_no, lifecycle_state, created_at, created_by, updated_at, updated_by)
                SELECT v.id, p.id, v.version_no, v.lifecycle_state, now(), '{IdentityDatabase.SeedPrincipalId}', now(), '{IdentityDatabase.SeedPrincipalId}'
                FROM identity_access.permission_profile p,
                     (VALUES ('{publishedWithout}'::uuid, 2, 'PUBLISHED'), ('{draftWith}'::uuid, 3, 'DRAFT')) AS v (id, version_no, lifecycle_state)
                WHERE p.code = 'R01-DEFAULT';
                INSERT INTO identity_access.permission_profile_grant (id, permission_profile_version_id, permission_id, data_scope, created_at, created_by, updated_at, updated_by)
                SELECT gen_random_uuid(), '{draftWith}', p.id, 'ALL', now(), '{IdentityDatabase.SeedPrincipalId}', now(), '{IdentityDatabase.SeedPrincipalId}'
                FROM identity_access.permission p WHERE p.code = 'IDENTITY_INTEGRATION_MANAGE';
                """);

            await RebindAsync(SystemAdministratorAssignmentId, publishedWithout);
            using HttpResponseMessage onVersionWithout = await client.GetWithTokenAsync(SessionApi.IdentityIntegration, session.AccessToken);
            await RebindAsync(SystemAdministratorAssignmentId, draftWith);
            using HttpResponseMessage onDraft = await client.GetWithTokenAsync(SessionApi.IdentityIntegration, session.AccessToken);

            Assert.Equal(HttpStatusCode.Forbidden, onVersionWithout.StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, onDraft.StatusCode);
        }
        finally
        {
            await RebindAsync(SystemAdministratorAssignmentId, IdentityDatabase.ProfileVersionId(1));
            await host.Database.ExecuteAsync($"DELETE FROM identity_access.permission_profile_version WHERE id IN ('{publishedWithout}', '{draftWith}')");
        }
    }

    /// <summary>ADR-018: a profile, not a role, carries the permission; granting it to another profile needs no code change or new sign-in.</summary>
    [Fact]
    public async Task APermissionGrantedToAnotherProfileAppliesOnTheNextRequest()
    {
        const string grantId = "00000000-0003-4000-8000-000000001001";
        using HttpClient client = host.Api.CreateClient();
        Session session = await client.SignInOrFailAsync(6);
        try
        {
            using HttpResponseMessage before = await client.GetWithTokenAsync(SessionApi.IdentityIntegration, session.AccessToken);
            await host.Database.ExecuteAsync($"""
                INSERT INTO identity_access.permission_profile_grant (id, permission_profile_version_id, permission_id, data_scope, created_at, created_by, updated_at, updated_by)
                SELECT '{grantId}', '{IdentityDatabase.ProfileVersionId(6)}', p.id, 'ALL', now(), '{IdentityDatabase.SeedPrincipalId}', now(), '{IdentityDatabase.SeedPrincipalId}'
                FROM identity_access.permission p WHERE p.code = 'IDENTITY_INTEGRATION_MANAGE'
                """);
            using HttpResponseMessage after = await client.GetWithTokenAsync(SessionApi.IdentityIntegration, session.AccessToken);

            Assert.Equal(HttpStatusCode.Forbidden, before.StatusCode);
            Assert.Equal(HttpStatusCode.OK, after.StatusCode);
        }
        finally
        {
            await host.Database.ExecuteAsync($"DELETE FROM identity_access.permission_profile_grant WHERE id = '{grantId}'");
        }
    }

    private Task RebindViewerAsync(int role) => RebindAsync(ViewerAssignmentId, IdentityDatabase.ProfileVersionId(role));

    private Task RebindAsync(string assignmentId, string profileVersionId) =>
        host.Database.ExecuteAsync($"UPDATE identity_access.access_relationship SET permission_profile_version_id = '{profileVersionId}' WHERE id = '{assignmentId}'");
}
