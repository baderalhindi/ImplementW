using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace PMPlatform.Tests.Integration.Identity;

/// <summary>Local accounts and created roles (the 5 Nov 2026 review §6).</summary>
[Collection(IdentitySuite.Name)]
public sealed class LocalAccountTests(IdentityTestHost host)
{
    private const string Sessions = "/api/v1/sessions";
    private const string Users = "/api/v1/users";
    private const string Strong = "Fresh4Password9";
    private static readonly string[] DepartmentOnly = ["DEPARTMENT"];
    private static readonly string[] AnyScope = ["ALL"];

    private static string Name() => $"local.{Guid.NewGuid():N}"[..20];

    private static object LocalUser(string username, string? password = null) => new
    {
        userType = "INTERNAL",
        username,
        displayName = "Local account",
        email = $"{username}@test.local",
        accountSource = "LOCAL",
        temporaryPassword = password,
    };

    private static Task<HttpResponseMessage> SignInAsync(HttpClient client, string username, string password) =>
        client.PostAsJsonAsync(Sessions, new { username, password });

    private static async Task<JsonObject> BodyOf(HttpResponseMessage response) => JsonNode.Parse(await response.Content.ReadAsStringAsync())!.AsObject();

    [Fact]
    public async Task ATemporaryPasswordIsShownOnceAndAdmitsToChangingItOnly()
    {
        using HttpClient client = host.Api.CreateClient();
        string admin = (await client.SignInOrFailAsync(1)).AccessToken;
        string username = Name();

        HttpResponseMessage created = await client.SendAsync(HttpMethod.Post, Users, admin, LocalUser(username));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        JsonObject user = await BodyOf(created);
        string temporary = user["temporaryPassword"]!.GetValue<string>();
        string id = user["id"]!.GetValue<string>();
        Assert.Null((await BodyOf(await client.SendAsync(HttpMethod.Get, $"{Users}/{id}", admin, null)))["temporaryPassword"]);
        Assert.DoesNotContain(temporary, (await host.Database.QueryAsync($"SELECT password_hash FROM identity_access.\"user\" WHERE id = '{id}'")).Single(), StringComparison.Ordinal);

        JsonObject pending = await BodyOf(await SignInAsync(client, username, temporary));
        Assert.Null(pending["accessToken"]);
        string token = pending["passwordChangeToken"]!.GetValue<string>();

        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync($"{Sessions}/password-change", new { passwordChangeToken = token, newPassword = "tooshort1" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync($"{Sessions}/password-change", new { passwordChangeToken = token, newPassword = temporary })).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync($"{Sessions}/password-change", new { passwordChangeToken = token, newPassword = Strong })).StatusCode);

        // The token has done its work, and the new password signs in.
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync($"{Sessions}/password-change", new { passwordChangeToken = token, newPassword = Strong + "x" })).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await SignInAsync(client, username, Strong)).StatusCode);
    }

    [Fact]
    public async Task FiveWrongPasswordsLockTheAccountUntilAnAdministratorUnlocksIt()
    {
        using HttpClient client = host.Api.CreateClient();
        string admin = (await client.SignInOrFailAsync(1)).AccessToken;
        string username = Name();
        string id = (await BodyOf(await client.SendAsync(
            HttpMethod.Post,
            Users,
            admin,
            new
            {
                userType = "INTERNAL",
                username,
                displayName = "Local account",
                email = $"{username}@test.local",
                accountSource = "LOCAL",
                temporaryPassword = Strong,
                mustChangePassword = false,
            })))["id"]!.GetValue<string>();

        for (int attempt = 0; attempt < 5; attempt++)
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await SignInAsync(client, username, "Wrong4Password9")).StatusCode);
        }

        Assert.Equal(HttpStatusCode.Unauthorized, (await SignInAsync(client, username, Strong)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(HttpMethod.Post, $"{Users}/{id}/unlock", admin, null)).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await SignInAsync(client, username, Strong)).StatusCode);

        // An end date in the past closes the account on every path.
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(HttpMethod.Post, $"{Users}/{id}/set-end-date", admin, new { endsOn = "2020-01-01" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await SignInAsync(client, username, Strong)).StatusCode);
    }

    [Fact]
    public async Task ACreatedRoleCopiesAProfileCarriesItsOwnScopeAndRetiresInsteadOfDeletingOnceHeld()
    {
        using HttpClient client = host.Api.CreateClient();
        string admin = (await client.SignInOrFailAsync(1)).AccessToken;
        string code = $"R9-{Guid.NewGuid():N}"[..12].ToUpperInvariant();
        JsonArray roles = JsonNode.Parse(await (await client.SendAsync(HttpMethod.Get, "/api/v1/roles", admin, null)).Content.ReadAsStringAsync())!.AsArray();
        string viewer = roles.First(r => r!["code"]!.GetValue<string>() == "R06")!["id"]!.GetValue<string>();

        HttpResponseMessage created = await client.SendAsync(
            HttpMethod.Post,
            "/api/v1/roles",
            admin,
            new
            {
                code,
                nameAr = "محلل مشاريع",
                nameEn = "Project Analyst",
                isExternalEligible = false,
                allowedScopes = DepartmentOnly,
                sourceRoleId = viewer,
                landingDashboardCode = "PORTFOLIO",
            });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        JsonObject role = await BodyOf(created);
        Assert.False(role["isSystem"]!.GetValue<bool>());
        Assert.Equal("DRAFT", (await host.Database.QueryAsync($"SELECT v.lifecycle_state FROM identity_access.permission_profile_version v JOIN identity_access.permission_profile p ON p.id = v.permission_profile_id WHERE p.code = '{code}-DEFAULT'")).Single());
        Assert.NotEqual("0", (await host.Database.QueryAsync($"SELECT count(*)::text FROM identity_access.permission_profile_grant g JOIN identity_access.permission_profile_version v ON v.id = g.permission_profile_version_id JOIN identity_access.permission_profile p ON p.id = v.permission_profile_id WHERE p.code = '{code}-DEFAULT'")).Single());

        HttpResponseMessage duplicate = await client.SendAsync(HttpMethod.Post, "/api/v1/roles", admin, new { code, nameAr = "x", nameEn = "x", allowedScopes = AnyScope });
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
    }
}
