using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace PMPlatform.Tests.Integration.Identity;

/// <summary>
/// Client review, item 5: delete a user, keeping their history; and adjust one user's permissions on top of their role. Real API and database;
/// the session's own permissions are what shows an adjustment took effect.
/// </summary>
[Collection(IdentitySuite.Name)]
public sealed class UserDeletionAndPermissionTests(IdentityTestHost host)
{
    private const string Users = AdministrationApi.Users;
    private const string CurrentPermissions = "/api/v1/sessions/current/permissions";
    private const int ViewerNumber = 6;

    private static readonly object Reason = new { text = "Covers the risk desk this quarter.", language = "en" };

    [Fact]
    public async Task ADeletedUserIsGoneFromAdministrationCannotSignInAndHistoryKeepsThem()
    {
        using HttpClient client = host.Api.CreateClient();
        string administrator = (await client.SignInOrFailAsync(1)).AccessToken;
        string username = $"local.{Guid.NewGuid():N}"[..20];
        const string password = "Fresh4Password9";
        using HttpResponseMessage created = await client.PostAsync(Users, administrator, new
        {
            userType = "INTERNAL",
            username,
            displayName = "To be deleted",
            email = $"{username}@test.local",
            accountSource = "LOCAL",
            temporaryPassword = password,
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Guid userId = AdministrationApi.IdOf(await created.ReadObjectAsync());
        object assignment = new { userId, permissionProfileVersionId = IdentityDatabase.ProfileVersionId(ViewerNumber), departmentId = IdentityDatabase.DepartmentId };
        using (HttpResponseMessage assigned = await client.PostAsync(AdministrationApi.AccessRelationships, administrator, assignment))
        {
            Assert.Equal(HttpStatusCode.Created, assigned.StatusCode);
        }

        using (HttpResponseMessage deleted = await client.PostAsync($"{Users}/{userId}/delete", administrator))
        {
            Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        }

        using (HttpResponseMessage read = await client.GetAsync($"{Users}/{userId}", administrator))
        {
            Assert.Equal(HttpStatusCode.NotFound, read.StatusCode);
        }

        using (HttpResponseMessage activate = await client.PostAsync($"{Users}/{userId}/activate", administrator))
        {
            Assert.Equal(HttpStatusCode.NotFound, activate.StatusCode);
        }

        using (HttpResponseMessage list = await client.GetAsync($"{Users}?q={username}", administrator))
        {
            Assert.Empty((await list.ReadObjectAsync())["items"]!.AsArray());
        }

        using (HttpResponseMessage signIn = await client.PostAsJsonAsync("/api/v1/sessions", new { username, password }))
        {
            Assert.NotEqual(HttpStatusCode.Created, signIn.StatusCode);
            Assert.False(signIn.IsSuccessStatusCode);
        }

        Assert.Equal(["DELETED|To be deleted"], await host.Database.QueryAsync($"SELECT status || '|' || display_name FROM identity_access.\"user\" WHERE id = '{userId}'"));
        Assert.Equal(["ENDED|USER_DELETED"], await host.Database.QueryAsync($"SELECT status || '|' || end_reason FROM identity_access.access_relationship WHERE user_id = '{userId}'"));
    }

    [Fact]
    public async Task AnAdministratorCannotDeleteThemselvesAndOnlyUserManageDeletes()
    {
        using HttpClient client = host.Api.CreateClient();
        Session administrator = await client.SignInOrFailAsync(1);
        using (HttpResponseMessage self = await client.PostAsync($"{Users}/{IdentityDatabase.UserId(1)}/delete", administrator.AccessToken))
        {
            Assert.Equal((HttpStatusCode.UnprocessableEntity, "IDENTITY_ACCESS_SELF_ADMINISTRATION"), (self.StatusCode, (await self.ReadObjectAsync())["code"]!.GetValue<string>()));
        }

        string viewer = (await client.SignInOrFailAsync(ViewerNumber)).AccessToken;
        using HttpResponseMessage refused = await client.PostAsync($"{Users}/{IdentityDatabase.UserId(8)}/delete", viewer);
        Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
    }

    [Fact]
    public async Task APermissionGrantedOrRemovedForOneUserTakesEffectAtOnceAndIsAudited()
    {
        using HttpClient client = host.Api.CreateClient();
        string administrator = (await client.SignInOrFailAsync(1)).AccessToken;
        string viewer = (await client.SignInOrFailAsync(ViewerNumber)).AccessToken;
        string viewerId = IdentityDatabase.UserId(ViewerNumber);
        string[] before = await HeldAsync(client, viewer);
        const string granted = "RISK_MANAGE";
        string removed = before[0];
        Assert.DoesNotContain(granted, before);
        try
        {
            using (HttpResponseMessage grant = await client.PutAsync($"{Users}/{viewerId}/permissions/{granted}", administrator, new { effect = "GRANT", dataScope = "DEPT", reason = Reason }, ifMatch: null))
            {
                Assert.Equal(HttpStatusCode.OK, grant.StatusCode);
                JsonObject entry = Entry(await grant.ReadObjectAsync(), granted);
                Assert.Equal(("GRANT", "DEPT", true), (entry["overrideEffect"]!.GetValue<string>(), entry["overrideScope"]!.GetValue<string>(), entry["isHeld"]!.GetValue<bool>()));
            }

            using (HttpResponseMessage remove = await client.PutAsync($"{Users}/{viewerId}/permissions/{removed}", administrator, new { effect = "REMOVE", reason = Reason }, ifMatch: null))
            {
                Assert.Equal(HttpStatusCode.OK, remove.StatusCode);
                JsonObject entry = Entry(await remove.ReadObjectAsync(), removed);
                Assert.False(entry["isHeld"]!.GetValue<bool>());
                Assert.NotEmpty(entry["fromRoles"]!.AsArray());
            }

            string[] adjusted = await HeldAsync(client, viewer);
            Assert.Contains(granted, adjusted);
            Assert.DoesNotContain(removed, adjusted);
            Assert.Contains(
                "IdentityAccess.UserPermissionOverrideSet",
                await host.Database.QueryAsync($"SELECT event_type FROM audit_activity.audit_event WHERE subject_id = '{viewerId}'"));
        }
        finally
        {
            foreach (string code in new[] { granted, removed })
            {
                using HttpResponseMessage cleared = await client.PostAsync($"{Users}/{viewerId}/permissions/{code}/clear", administrator, new { reason = Reason });
                Assert.Equal(HttpStatusCode.OK, cleared.StatusCode);
            }
        }

        Assert.Equal(before, await HeldAsync(client, viewer));
    }

    [Fact]
    public async Task AnOverrideIsHeldToTheSafetyRulesAndToAReason()
    {
        using HttpClient client = host.Api.CreateClient();
        string administrator = (await client.SignInOrFailAsync(1)).AccessToken;
        string viewerId = IdentityDatabase.UserId(ViewerNumber);

        // No one changes their own access.
        using (HttpResponseMessage self = await client.PutAsync($"{Users}/{IdentityDatabase.UserId(1)}/permissions/ROLE_VIEW", administrator, new { effect = "REMOVE", reason = Reason }, ifMatch: null))
        {
            Assert.Equal("IDENTITY_ACCESS_SELF_ADMINISTRATION", (await self.ReadObjectAsync())["code"]!.GetValue<string>());
        }

        using (HttpResponseMessage unknown = await client.PutAsync($"{Users}/{viewerId}/permissions/NOT_A_PERMISSION", administrator, new { effect = "REMOVE", reason = Reason }, ifMatch: null))
        {
            Assert.Equal(HttpStatusCode.UnprocessableEntity, unknown.StatusCode);
        }

        // GrantPolicy: R08 never holds approval, whoever asks.
        using (HttpResponseMessage bounded = await client.PutAsync($"{Users}/{IdentityDatabase.UserId(8)}/permissions/APPROVAL_DECIDE", administrator, new { effect = "GRANT", dataScope = "ALL", reason = Reason }, ifMatch: null))
        {
            Assert.Equal(HttpStatusCode.UnprocessableEntity, bounded.StatusCode);
        }

        using (HttpResponseMessage noReason = await client.PutAsync($"{Users}/{viewerId}/permissions/RISK_MANAGE", administrator, new { effect = "GRANT", dataScope = "DEPT" }, ifMatch: null))
        {
            Assert.Equal(HttpStatusCode.BadRequest, noReason.StatusCode);
        }

        using HttpResponseMessage noScope = await client.PutAsync($"{Users}/{viewerId}/permissions/RISK_MANAGE", administrator, new { effect = "GRANT", reason = Reason }, ifMatch: null);
        Assert.Equal(HttpStatusCode.BadRequest, noScope.StatusCode);
    }

    private static JsonObject Entry(JsonObject permissions, string code) =>
        permissions["items"]!.AsArray().Select(i => i!.AsObject()).Single(i => i["permissionCode"]!.GetValue<string>() == code);

    private static async Task<string[]> HeldAsync(HttpClient client, string token)
    {
        using HttpResponseMessage response = await client.GetAsync(CurrentPermissions, token);
        return [.. (await response.ReadObjectAsync())["permissions"]!.AsArray().Select(p => p!["code"]!.GetValue<string>())];
    }
}
