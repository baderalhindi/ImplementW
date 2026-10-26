using System.Diagnostics;
using System.Net;
using Microsoft.Extensions.DependencyInjection;
using PMPlatform.Application.Features.IdentityAccess.Authentication;
using PMPlatform.Application.Features.IdentityAccess.Contracts;

namespace PMPlatform.Tests.Integration.Identity;

/// <summary>
/// TASK-028 against the test directory: a person the directory authenticates receives a session carrying the platform
/// roles of their active assignments (ADR-007, ADR-018), and every kind of failure looks the same from outside.
/// </summary>
[Collection(IdentitySuite.Name)]
public sealed class DirectorySignInTests(IdentityTestHost host)
{
    [Fact]
    public async Task ADirectorySignInIssuesASessionWithThePlatformRolesOfTheUsersAssignments()
    {
        using HttpClient client = host.Api.CreateClient();

        // R01 requires MFA (TASK-029): the password answers with an MFA token, the second factor with the session.
        using HttpResponseMessage signIn = await client.SignInAsync("local.r01", TestDirectory.PersonPassword);
        Assert.Equal(HttpStatusCode.OK, signIn.StatusCode);
        using HttpResponseMessage response = await client.CompleteSecondFactorAsync((await signIn.ReadAsync<MfaPending>()).MfaToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(SessionApi.Current, response.Headers.Location?.OriginalString);
        Assert.True(response.Headers.CacheControl?.NoStore);
        Session session = await response.ReadAsync<Session>();
        Assert.Equal("Bearer", session.TokenType);
        Assert.Equal(Guid.Parse(IdentityDatabase.UserId(1)), session.User.Id);
        Assert.Equal("INTERNAL", session.User.UserType);
        Assert.Equal("DIRECTORY", session.User.AuthenticationMethod);
        RoleAssignment role = Assert.Single(session.User.RoleAssignments);
        Assert.Equal("R01", role.RoleCode);
        Assert.Equal(Guid.Parse(IdentityDatabase.ProfileVersionId(1)), role.PermissionProfileVersionId);

        // The access token carries the same role to every protected endpoint.
        using HttpResponseMessage current = await client.GetWithTokenAsync(SessionApi.Current, session.AccessToken);
        Assert.Equal(HttpStatusCode.OK, current.StatusCode);
        CurrentSession claims = await current.ReadAsync<CurrentSession>();
        Assert.Equal(session.User.Id, claims.UserId);
        Assert.Equal(["R01"], claims.Roles);
        Assert.Equal("DIRECTORY", claims.AuthenticationMethod);
    }

    /// <summary>
    /// ADR-013, the third user type: an external identity holding an internal-grade project role. R04 comes scoped to
    /// the entity's own project; the R01 assignment an external user may not hold is not honoured.
    /// </summary>
    [Fact]
    public async Task AnEntityProjectManagerSignsInAsAnExternalUserHoldingR04OnTheirProject()
    {
        using HttpClient client = host.Api.CreateClient();

        Session session = await client.SignInOrFailAsync(8);

        Assert.Equal("EXTERNAL", session.User.UserType);
        Assert.Equal(["R04", "R08"], session.User.RoleAssignments.Select(a => a.RoleCode));
        RoleAssignment projectManager = session.User.RoleAssignments.Single(a => a.RoleCode == "R04");
        Assert.Equal(Guid.Parse(IdentityDatabase.EntityProjectId), projectManager.ProjectId);
        Assert.Equal(Guid.Parse(IdentityDatabase.ActiveEntityId), projectManager.ExternalEntityId);
        Assert.Equal(Guid.Parse(IdentityDatabase.ProfileVersionId(4)), projectManager.PermissionProfileVersionId);
        Assert.Equal(Guid.Parse(IdentityDatabase.ActiveEntityId), session.User.RoleAssignments.Single(a => a.RoleCode == "R08").ExternalEntityId);
    }

    /// <summary>
    /// Roles come from assignments in force now, never from the directory: an ended assignment and one not yet started
    /// give a valid session with no role.
    /// </summary>
    [Fact]
    public async Task OnlyAssignmentsInForceBecomeSessionRoles()
    {
        using HttpClient client = host.Api.CreateClient();

        Session session = await client.SignInOrFailAsync(5);

        Assert.Empty(session.User.RoleAssignments);
    }

    /// <summary>ADR-007: the directory is authoritative for department, manager and job title; a sign-in copies them.</summary>
    [Fact]
    public async Task ASignInCopiesTheDirectoryAuthoritativeAttributes()
    {
        using HttpClient client = host.Api.CreateClient();

        await client.SignInOrFailAsync(3);

        IReadOnlyList<string> row = await host.Database.QueryAsync($"""
            SELECT concat_ws('|', job_title, department_id, manager_user_id, updated_by)
            FROM identity_access."user" WHERE id = '{IdentityDatabase.UserId(3)}'
            """);
        Assert.Equal(
            $"Department Manager|{IdentityDatabase.DepartmentId}|{IdentityDatabase.UserId(2)}|{IdentityDatabase.DirectorySyncPrincipalId}",
            Assert.Single(row));
    }

    /// <summary>
    /// No user enumeration: unknown user, wrong password, a directory person with no platform user, a disabled user, an
    /// external user whose entity is suspended and a filter-injection attempt all answer the same 401, no sooner than
    /// the same floor.
    /// </summary>
    [Fact]
    public async Task EveryRejectedSignInIsTheSameGeneric401()
    {
        using HttpClient client = host.Api.CreateClient();
        (string Username, string Password)[] attempts =
        [
            ("no.such.person", TestDirectory.PersonPassword),
            ("local.r01", "wrong-password"),
            ("local.unregistered", TestDirectory.PersonPassword),
            ("local.r04", TestDirectory.PersonPassword),
            ("local.r07", TestDirectory.PersonPassword),
            ("*", TestDirectory.PersonPassword),
            ("local.r01)(uid=*", TestDirectory.PersonPassword),
        ];

        HashSet<string> shapes = [];
        foreach ((string username, string password) in attempts)
        {
            Stopwatch elapsed = Stopwatch.StartNew();
            using HttpResponseMessage response = await client.SignInAsync(username, password);
            elapsed.Stop();

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
            Assert.True(elapsed.Elapsed >= TimeSpan.FromSeconds(1) - TimeSpan.FromMilliseconds(20), $"{username}: answered in {elapsed.Elapsed}");
            Problem problem = await response.ReadAsync<Problem>();
            Assert.Equal("AUTHENTICATION_REQUIRED", problem.Code);
            Assert.NotEqual(Guid.Empty, problem.CorrelationId);
            shapes.Add(await response.ReadProblemShapeAsync());
        }

        Assert.Single(shapes);
    }

    [Fact]
    public async Task AnEmptyPasswordIsAShapeErrorAndNeverReachesTheDirectory()
    {
        using HttpClient client = host.Api.CreateClient();

        using HttpResponseMessage response = await client.SignInAsync("local.r01", string.Empty);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("VALIDATION_FAILED", (await response.ReadAsync<Problem>()).Code);

        // Below the API as well: an empty password is an anonymous bind, which the directory would accept.
        using IServiceScope scope = host.Api.Services.CreateScope();
        DirectoryResult result = await scope.ServiceProvider.GetRequiredService<IDirectoryService>()
            .AuthenticateAsync("local.r01", string.Empty, CancellationToken.None);
        Assert.Equal(AuthenticationFailure.Rejected, result.Failure);
    }
}
