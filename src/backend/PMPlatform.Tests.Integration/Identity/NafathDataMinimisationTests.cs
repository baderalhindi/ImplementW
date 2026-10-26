using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace PMPlatform.Tests.Integration.Identity;

/// <summary>
/// TASK-068 acceptance criterion 2 and the second half of its validation check: "review stored fields against the
/// documented minimization decision" (nafath-data-minimisation.md). The test Nafath hands over a national id as the
/// subject, and a name and a birth date besides; after a verification the platform holds a reference and a time, and
/// none of what Nafath said — in any table, in any log line, or in any response.
/// </summary>
[Collection(IdentitySuite.Name)]
public sealed class NafathDataMinimisationTests(IdentityTestHost host) : IAsyncLifetime
{
    private static readonly string ExternalUser = IdentityDatabase.UserId(8);

    /// <summary>The user row's columns a verification may change (MD-1): the reference, the time, and the row's own audit stamp.</summary>
    private static readonly string[] VerificationColumns = ["nafath_verification_reference", "nafath_verified_at", "updated_at", "updated_by"];

    public Task InitializeAsync() => TestNafath.ResetAsync(host);

    public Task DisposeAsync() => TestNafath.ResetAsync(host);

    [Fact]
    public async Task OnlyAReferenceAndATimeAreKeptOfAVerification()
    {
        JsonObject before = await UserRowAsync();
        await using IdentityApiFactory api = host.CreateApi(TestNafath.Settings(host.Nafath));
        using HttpClient client = api.CreateClient();
        List<string> responses = [];

        using HttpResponseMessage signIn = await client.SignInAsync("local.r08", TestDirectory.PersonPassword);
        responses.Add(await signIn.Content.ReadAsStringAsync());
        string verificationToken = (await signIn.ReadAsync<IdentityVerificationPending>()).IdentityVerificationToken;
        NafathCallback callback = await TestNafath.VerifyInBrowserAsync(client, verificationToken);
        using HttpResponseMessage verified = await TestNafath.CompleteAsync(client, verificationToken, callback);
        Assert.Equal(HttpStatusCode.Created, verified.StatusCode);
        responses.Add(await verified.Content.ReadAsStringAsync());
        using HttpResponseMessage current = await client.GetWithTokenAsync(SessionApi.Current, (await verified.ReadAsync<Session>()).AccessToken);
        responses.Add(await current.Content.ReadAsStringAsync());

        // The user row: the verification's columns changed, and nothing else.
        JsonObject after = await UserRowAsync();
        string[] changed = [.. after.Where(column => !JsonNode.DeepEquals(column.Value, before[column.Key])).Select(column => column.Key).Order(StringComparer.Ordinal)];
        Assert.Equal(VerificationColumns.Order(StringComparer.Ordinal), changed);
        Assert.True(Guid.TryParse(after["nafath_verification_reference"]!.GetValue<string>(), out _));

        // Nowhere in the database: every row of every table, as text.
        Assert.Empty(await host.Database.QueryAsync($"""
            SELECT table_schema || '.' || table_name FROM information_schema.tables
            WHERE table_type = 'BASE TABLE' AND table_schema NOT IN ('pg_catalog', 'information_schema')
              AND query_to_xml(format('SELECT * FROM %I.%I', table_schema, table_name), true, false, '')::text ~ '{string.Join('|', TestNafath.IdentityAttributes)}'
            """));

        // Nor in a log line or a response; nor the client secret, the code, the transaction or the verification token in a log line.
        string logs = host.Logs.Text;
        Assert.Contains("Nafath verified user", logs, StringComparison.Ordinal);
        foreach (string value in TestNafath.IdentityAttributes.Concat([TestNafath.ClientSecret, callback.Code, callback.Transaction, verificationToken]))
        {
            Assert.DoesNotContain(value, logs, StringComparison.Ordinal);
        }

        foreach (string value in TestNafath.IdentityAttributes)
        {
            Assert.All(responses, body => Assert.DoesNotContain(value, body, StringComparison.Ordinal));
        }
    }

    /// <summary>The administrator's view of the user (TASK-031) says when the identity was verified, never the reference.</summary>
    [Fact]
    public async Task TheUserRecordShowsWhenTheIdentityWasVerifiedAndNotTheReference()
    {
        await using IdentityApiFactory api = host.CreateApi(TestNafath.Settings(host.Nafath));
        using HttpClient client = api.CreateClient();
        using HttpResponseMessage signIn = await client.SignInAsync("local.r08", TestDirectory.PersonPassword);
        string verificationToken = (await signIn.ReadAsync<IdentityVerificationPending>()).IdentityVerificationToken;
        (await TestNafath.CompleteAsync(client, verificationToken, await TestNafath.VerifyInBrowserAsync(client, verificationToken))).Dispose();
        string reference = Assert.Single(await host.Database.QueryAsync($"SELECT nafath_verification_reference FROM identity_access.\"user\" WHERE id = '{ExternalUser}'"));
        Session administrator = await client.SignInOrFailAsync(1);

        using HttpResponseMessage response = await client.GetWithTokenAsync($"/api/v1/users/{ExternalUser}", administrator.AccessToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument user = JsonDocument.Parse(body);
        Assert.Equal(JsonValueKind.String, user.RootElement.GetProperty("nafathVerifiedAt").ValueKind);
        Assert.DoesNotContain(reference, body, StringComparison.Ordinal);
    }

    private async Task<JsonObject> UserRowAsync() =>
        JsonNode.Parse(Assert.Single(await host.Database.QueryAsync($"SELECT to_jsonb(u)::text FROM identity_access.\"user\" u WHERE id = '{ExternalUser}'")))!.AsObject();
}
