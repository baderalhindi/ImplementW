using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using PMPlatform.Tests.Integration.Notifications;

namespace PMPlatform.Tests.Integration.Identity;

/// <summary>A person changing their own password, and resetting a forgotten one by e-mail.</summary>
[Collection(IdentitySuite.Name)]
public sealed partial class SelfServicePasswordTests(IdentityTestHost host) : IAsyncLifetime
{
    private const string Sessions = "/api/v1/sessions";
    private const string Users = "/api/v1/users";
    private const string First = "First4Password9";
    private const string Second = "Second4Password9";
    private const string Third = "Third4Password9";
    private const string AppBaseUrl = "https://platform.test";

    private TestSmtpServer _smtp = null!;
    private IdentityApiFactory _api = null!;

    public async Task InitializeAsync()
    {
        _smtp = await TestSmtpServer.StartAsync();
        Dictionary<string, string?> settings = new(_smtp.Settings) { ["APP_BASE_URL"] = AppBaseUrl };
        _api = host.CreateApi(settings);
    }

    public async Task DisposeAsync()
    {
        await _api.DisposeAsync();
        await _smtp.DisposeAsync();
    }

    private static string Name() => $"local.{Guid.NewGuid():N}"[..20];

    private static async Task<JsonObject> BodyOf(HttpResponseMessage response) => JsonNode.Parse(await response.Content.ReadAsStringAsync())!.AsObject();

    /// <summary>A local account past its temporary password, with <see cref="First"/> as its password.</summary>
    private static async Task<(string Username, string Id)> CreateAccountAsync(HttpClient client)
    {
        string admin = (await client.SignInOrFailAsync(1)).AccessToken;
        string username = Name();
        JsonObject user = await BodyOf(await client.SendAsync(
            HttpMethod.Post,
            Users,
            admin,
            new { userType = "INTERNAL", username, displayName = "Local account", email = $"{username}@test.local", accountSource = "LOCAL", temporaryPassword = First, mustChangePassword = false }));
        return (username, user["id"]!.GetValue<string>());
    }

    private static Task<HttpResponseMessage> SignInAsync(HttpClient client, string username, string password) =>
        client.PostAsJsonAsync(Sessions, new { username, password });

    private static async Task<(string AccessToken, string RefreshToken)> SessionOfAsync(HttpClient client, string username, string password)
    {
        HttpResponseMessage response = await SignInAsync(client, username, password);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        JsonObject session = await BodyOf(response);
        return (session["accessToken"]!.GetValue<string>(), session["refreshToken"]!.GetValue<string>());
    }

    private static Task<HttpResponseMessage> ChangeAsync(HttpClient client, string token, string current, string next) =>
        client.SendAsync(HttpMethod.Post, $"{Sessions}/current/password-change", token, new { currentPassword = current, newPassword = next });

    private static Task<HttpResponseMessage> RequestResetAsync(HttpClient client, string username) =>
        client.PostAsJsonAsync($"{Sessions}/password-reset-requests", new { username });

    private static Task<HttpResponseMessage> CompleteResetAsync(HttpClient client, string resetToken, string newPassword) =>
        client.PostAsJsonAsync($"{Sessions}/password-reset", new { resetToken, newPassword });

    private static Task<HttpResponseMessage> RefreshAsync(HttpClient client, string refreshToken) =>
        client.PostAsJsonAsync($"{Sessions}/current/refresh", new { refreshToken });

    /// <summary>The token in the newest reset e-mail to this account, waiting for the queue to send it.</summary>
    private async Task<string> ResetTokenSentToAsync(string username, int expectedCount = 1)
    {
        string address = $"{username}@test.local";
        for (int attempt = 0; attempt < 100 && _smtp.To(address).Count < expectedCount; attempt++)
        {
            await Task.Delay(50);
        }

        IReadOnlyList<ReceivedEmail> received = _smtp.To(address);
        Assert.Equal(expectedCount, received.Count);
        return TokenInLink().Match(Decoded(received[^1].Content)).Groups["token"].Value;
    }

    /// <summary>The message's text with its transfer encoding undone: Arabic arrives as base64, long lines as quoted-printable.</summary>
    private static string Decoded(string content)
    {
        int bodyStart = content.IndexOf("\r\n\r\n", StringComparison.Ordinal) + 4;
        string headers = content[..bodyStart];
        string body = content[bodyStart..];
        return headers.Contains("base64", StringComparison.OrdinalIgnoreCase)
            ? System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(body.Replace("\r\n", string.Empty, StringComparison.Ordinal).TrimEnd('.')))
            : body.Replace("=\r\n", string.Empty, StringComparison.Ordinal).Replace("=3D", "=", StringComparison.Ordinal);
    }

    [GeneratedRegex(@"/reset-password#token=(?<token>[A-Za-z0-9_\-.]+)")]
    private static partial Regex TokenInLink();

    [Fact]
    public async Task ASignedInLocalAccountChangesItsPasswordAndEverySessionEnds()
    {
        using HttpClient client = _api.CreateClient();
        (string username, _) = await CreateAccountAsync(client);
        (string access, string refresh) = await SessionOfAsync(client, username, First);
        await Task.Delay(TimeSpan.FromSeconds(1.1)); // Sessions are told apart from the change in whole seconds.

        Assert.Equal(HttpStatusCode.NoContent, (await ChangeAsync(client, access, First, Second)).StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await SignInAsync(client, username, First)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshAsync(client, refresh)).StatusCode);
        (_, string fresh) = await SessionOfAsync(client, username, Second);
        Assert.Equal(HttpStatusCode.OK, (await RefreshAsync(client, fresh)).StatusCode);
    }

    [Fact]
    public async Task ChangingAPasswordNeedsTheCurrentOneAndKeepsTheRules()
    {
        using HttpClient client = _api.CreateClient();
        (string username, _) = await CreateAccountAsync(client);
        (string access, _) = await SessionOfAsync(client, username, First);

        async Task AssertRefusedAsync(string current, string next, string field, string code)
        {
            HttpResponseMessage response = await ChangeAsync(client, access, current, next);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            string body = await response.Content.ReadAsStringAsync();
            Assert.Contains(field, body, StringComparison.Ordinal);
            Assert.Contains(code, body, StringComparison.Ordinal);
        }

        await AssertRefusedAsync("not-the-password1", Second, "currentPassword", "PASSWORD_INCORRECT");
        await AssertRefusedAsync(First, "tooshort1", "newPassword", "PASSWORD_TOO_WEAK");
        await AssertRefusedAsync(First, First, "newPassword", "PASSWORD_REUSED");
        Assert.Equal(HttpStatusCode.Created, (await SignInAsync(client, username, First)).StatusCode);
    }

    [Fact]
    public async Task WrongCurrentPasswordsLockTheAccountAsWrongSignInsDo()
    {
        using HttpClient client = _api.CreateClient();
        (string username, _) = await CreateAccountAsync(client);
        (string access, _) = await SessionOfAsync(client, username, First);

        for (int attempt = 0; attempt < 5; attempt++)
        {
            Assert.Equal(HttpStatusCode.BadRequest, (await ChangeAsync(client, access, "not-the-password1", Second)).StatusCode);
        }

        Assert.Equal(HttpStatusCode.BadRequest, (await ChangeAsync(client, access, First, Second)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await SignInAsync(client, username, First)).StatusCode);
    }

    /// <summary>Against the real test directory: the person changes the password themselves, and the directory then honours only the new one.</summary>
    [Fact]
    public async Task ADirectoryAccountChangesItsOwnPasswordInTheDirectory()
    {
        using HttpClient client = _api.CreateClient();
        string admin = (await client.SignInOrFailAsync(1)).AccessToken;
        string username = TestDirectory.PasswordChangeUsername;
        string start = TestDirectory.PasswordChangeStartingPassword;
        Assert.Equal(HttpStatusCode.Created, (await client.SendAsync(
            HttpMethod.Post,
            Users,
            admin,
            new { userType = "INTERNAL", username, displayName = "Directory account", email = $"{username}@test.local", directorySubjectId = TestDirectory.PasswordChangeSubject })).StatusCode);

        (string access, string refresh) = await SessionOfAsync(client, username, start);
        await Task.Delay(TimeSpan.FromSeconds(1.1));

        await AssertRefusedAsync(await ChangeAsync(client, access, "not-the-password-1", Second), "PASSWORD_INCORRECT");
        await AssertRefusedAsync(await ChangeAsync(client, access, start, "short1"), "PASSWORD_TOO_WEAK");
        await AssertRefusedAsync(await ChangeAsync(client, access, start, start), "PASSWORD_REUSED");
        Assert.Equal(HttpStatusCode.Created, (await SignInAsync(client, username, start)).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await ChangeAsync(client, access, start, Second)).StatusCode);
        try
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await SignInAsync(client, username, start)).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshAsync(client, refresh)).StatusCode);
        }
        finally
        {
            // The directory outlives the test: put the password back for the next run.
            await Task.Delay(TimeSpan.FromSeconds(1.1));
            (string again, string freshRefresh) = await SessionOfAsync(client, username, Second);
            Assert.Equal(HttpStatusCode.OK, (await RefreshAsync(client, freshRefresh)).StatusCode);
            Assert.Equal(HttpStatusCode.NoContent, (await ChangeAsync(client, again, Second, start)).StatusCode);
        }

        Assert.Equal(HttpStatusCode.Created, (await SignInAsync(client, username, start)).StatusCode);
    }

    private static async Task AssertRefusedAsync(HttpResponseMessage response, string code)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(code, await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ChangingAPasswordNeedsASession()
    {
        using HttpClient client = _api.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync($"{Sessions}/current/password-change", new { currentPassword = First, newPassword = Second })).StatusCode);
    }

    [Fact]
    public async Task AResetLinkSetsANewPasswordOnceAndIssuesNoSession()
    {
        using HttpClient client = _api.CreateClient();
        (string username, _) = await CreateAccountAsync(client);
        (_, string refresh) = await SessionOfAsync(client, username, First);
        await Task.Delay(TimeSpan.FromSeconds(1.1));

        Assert.Equal(HttpStatusCode.NoContent, (await RequestResetAsync(client, username)).StatusCode);
        string token = await ResetTokenSentToAsync(username);

        Assert.Equal(HttpStatusCode.BadRequest, (await CompleteResetAsync(client, token, "tooshort1")).StatusCode);
        HttpResponseMessage completed = await CompleteResetAsync(client, token, Second);
        Assert.Equal(HttpStatusCode.NoContent, completed.StatusCode);
        Assert.Empty(await completed.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.Unauthorized, (await CompleteResetAsync(client, token, Third)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await SignInAsync(client, username, First)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshAsync(client, refresh)).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await SignInAsync(client, username, Second)).StatusCode);
    }

    [Fact]
    public async Task AResetLinkStopsWorkingOnceThePasswordChangesAnotherWay()
    {
        using HttpClient client = _api.CreateClient();
        (string username, _) = await CreateAccountAsync(client);
        Assert.Equal(HttpStatusCode.NoContent, (await RequestResetAsync(client, username)).StatusCode);
        string token = await ResetTokenSentToAsync(username);

        (string access, _) = await SessionOfAsync(client, username, First);
        Assert.Equal(HttpStatusCode.NoContent, (await ChangeAsync(client, access, First, Second)).StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await CompleteResetAsync(client, token, Third)).StatusCode);
    }

    [Fact]
    public async Task EveryResetRequestAnswersTheSameAndOnlyALocalAccountGetsMail()
    {
        using HttpClient client = _api.CreateClient();
        (string username, string id) = await CreateAccountAsync(client);
        string disabled = (await CreateAccountAsync(client)).Username;
        string admin = (await client.SignInOrFailAsync(1)).AccessToken;
        string disabledId = (await host.Database.QueryAsync($"SELECT id::text FROM identity_access.\"user\" WHERE username = '{disabled}'")).Single();
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(HttpMethod.Post, $"{Users}/{disabledId}/set-end-date", admin, new { endsOn = "2020-01-01" })).StatusCode);

        foreach (string name in new[] { username, "nobody.here", "local.r02", disabled })
        {
            long startedAt = System.Diagnostics.Stopwatch.GetTimestamp();
            HttpResponseMessage response = await RequestResetAsync(client, name);
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
            Assert.Empty(await response.Content.ReadAsStringAsync());
            Assert.True(System.Diagnostics.Stopwatch.GetElapsedTime(startedAt) >= TimeSpan.FromSeconds(0.99), name);
        }

        await ResetTokenSentToAsync(username);
        Assert.Single(_smtp.Received);
        Assert.Equal("1", (await host.Database.QueryAsync($"SELECT count(*)::text FROM audit_activity.audit_event WHERE event_type = 'IdentityAccess.PasswordResetRequested' AND subject_id = '{id}'")).Single());
    }

    [Fact]
    public async Task RepeatedRequestsSendOneMessageWithinTheCooldown()
    {
        using HttpClient client = _api.CreateClient();
        (string username, _) = await CreateAccountAsync(client);

        await Task.WhenAll(RequestResetAsync(client, username), RequestResetAsync(client, username), RequestResetAsync(client, username));
        await ResetTokenSentToAsync(username);
        Assert.Equal(HttpStatusCode.NoContent, (await RequestResetAsync(client, username)).StatusCode);

        await Task.Delay(500);
        Assert.Single(_smtp.To($"{username}@test.local"));
    }

    [Fact]
    public async Task AResetTokenIsNoOtherTokenAndNoOtherTokenIsAResetToken()
    {
        using HttpClient client = _api.CreateClient();
        (string username, _) = await CreateAccountAsync(client);
        (string access, string refresh) = await SessionOfAsync(client, username, First);
        await RequestResetAsync(client, username);
        string token = await ResetTokenSentToAsync(username);

        Assert.Equal(HttpStatusCode.Unauthorized, (await CompleteResetAsync(client, access, Second)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await CompleteResetAsync(client, refresh, Second)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.SendAsync(HttpMethod.Get, $"{Sessions}/current", token, null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshAsync(client, token)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync($"{Sessions}/password-change", new { passwordChangeToken = token, newPassword = Second })).StatusCode);
    }

    [Fact]
    public async Task AnAdministratorsResetEndsTheAccountsSessions()
    {
        using HttpClient client = _api.CreateClient();
        (string username, string id) = await CreateAccountAsync(client);
        (_, string refresh) = await SessionOfAsync(client, username, First);
        await Task.Delay(TimeSpan.FromSeconds(1.1));

        string admin = (await client.SignInOrFailAsync(1)).AccessToken;
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(HttpMethod.Post, $"{Users}/{id}/reset-password", admin, new { })).StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshAsync(client, refresh)).StatusCode);
    }

    [Fact]
    public async Task TheSignInOptionsOfferResetOnlyWhereALinkCanBeSent()
    {
        using HttpClient withMail = _api.CreateClient();
        using HttpClient withoutMail = host.Api.CreateClient();

        Assert.True((await BodyOf(await withMail.GetAsync($"{Sessions}/sign-in-options")))["passwordResetAvailable"]!.GetValue<bool>());
        Assert.False((await BodyOf(await withoutMail.GetAsync($"{Sessions}/sign-in-options")))["passwordResetAvailable"]!.GetValue<bool>());
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await RequestResetAsync(withoutMail, "local.r02")).StatusCode);
    }
}
