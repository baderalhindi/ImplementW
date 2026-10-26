using System.Net;
using System.Text.Json.Nodes;
using PMPlatform.Application.Common.Events;
using PMPlatform.Tests.Integration.Identity;

namespace PMPlatform.Tests.Integration.Notifications;

/// <summary>
/// A person's own notifications and preferences through the real API pipeline (TASK-039, served to TASK-040): the unread
/// badge, read and mark-all-read, history, and preferences that the next send honours.
/// </summary>
[Collection(NotificationSuite.Name)]
public sealed class NotificationInboxEndpointTests(NotificationTestHost host)
{
    private const string Inbox = "/api/v1/notifications";
    private const string Preferences = "/api/v1/notification-preferences";

    [Fact]
    public async Task TheBadgeCountsTheCallersUnreadAndMarkAllReadIsUndoneOnlyByNewNotifications()
    {
        using HttpClient client = host.Api.CreateClient();
        string viewer = (await client.SignInOrFailAsync(6)).AccessToken;
        using (HttpResponseMessage cleared = await client.PostAsync($"{Inbox}/read-all", viewer))
        {
            Assert.Equal(HttpStatusCode.OK, cleared.StatusCode);
        }

        Assert.Equal(0, await UnreadAsync(client, viewer));

        Guid first = Guid.NewGuid();
        Guid second = Guid.NewGuid();
        await Report(first).RunAsync(host.Api);
        await Report(second).RunAsync(host.Api);
        await host.Api.DeliverAsync();
        Assert.Equal(2, await UnreadAsync(client, viewer));

        using HttpResponseMessage unread = await client.GetAsync($"{Inbox}?unread=true&eventFamilyCode={NotificationTestHost.ReportFamily}", viewer);
        JsonArray items = (await unread.ReadObjectAsync())["items"]!.AsArray();
        Assert.Equal(2, items.Count);
        JsonObject newest = items[0]!.AsObject();
        Assert.Equal($"/reports/jobs/{second}", newest["deepLink"]!.GetValue<string>());
        Assert.Equal("SENT", newest["status"]!.GetValue<string>());
        Assert.Equal("ar", newest["language"]!.GetValue<string>());
        Assert.Equal("التقرير Portfolio جاهز.", newest["body"]!.GetValue<string>());
        string id = newest["id"]!.GetValue<string>();

        // Marking read needs no Idempotency-Key (R-35: not a sensitive write), and a second time changes nothing.
        using HttpResponseMessage read = await client.SendAsync(HttpMethod.Post, $"{Inbox}/{id}/read", viewer, idempotencyKey: "");
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        string readAt = (await read.ReadObjectAsync())["readAt"]!.GetValue<string>();
        using HttpResponseMessage readAgain = await client.PostAsync($"{Inbox}/{id}/read", viewer);
        Assert.Equal(readAt, (await readAgain.ReadObjectAsync())["readAt"]!.GetValue<string>());
        Assert.Equal(1, await UnreadAsync(client, viewer));

        using HttpResponseMessage all = await client.PostAsync($"{Inbox}/read-all", viewer);
        Assert.Equal(1, (await all.ReadObjectAsync())["markedCount"]!.GetValue<int>());
        Assert.Equal(0, await UnreadAsync(client, viewer));

        // Only a new notification makes the badge count again; the ones marked stay read.
        Guid third = Guid.NewGuid();
        await Report(third).RunAsync(host.Api);
        await host.Api.DeliverAsync();
        Assert.Equal(1, await UnreadAsync(client, viewer));
        using HttpResponseMessage stillRead = await client.GetAsync($"{Inbox}?unread=false&eventFamilyCode={NotificationTestHost.ReportFamily}", viewer);
        Assert.Contains($"/reports/jobs/{first}", await stillRead.Content.ReadAsStringAsync(), StringComparison.Ordinal);

        // Another person cannot open it; nobody without a session reaches the inbox.
        string other = (await client.SignInOrFailAsync(3)).AccessToken;
        using HttpResponseMessage foreign = await client.GetAsync($"{Inbox}/{id}", other);
        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
        using HttpResponseMessage foreignRead = await client.PostAsync($"{Inbox}/{id}/read", other);
        Assert.Equal(HttpStatusCode.NotFound, foreignRead.StatusCode);
        using HttpResponseMessage anonymous = await client.GetAsync($"{Inbox}/unread-count");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
    }

    [Fact]
    public async Task HistoryShowsEveryChannelWithWhatBecameOfIt()
    {
        Guid job = Guid.NewGuid();
        await Report(job).RunAsync(host.Api);
        await host.Api.DeliverAsync();

        using HttpClient client = host.Api.CreateClient();
        string viewer = (await client.SignInOrFailAsync(6)).AccessToken;
        using HttpResponseMessage history = await client.GetAsync($"{Inbox}/history?pageSize=200", viewer);
        List<string> rows = [.. (await history.ReadObjectAsync())["items"]!.AsArray()
            .Select(i => i!.AsObject())
            .Where(i => i["eventType"]!.GetValue<string>() == NotificationTestHost.ReportEvent)
            .Take(3)
            .Select(i => $"{i["channel"]}|{i["status"]}|{i["suppressionReason"]}")
            .Order(StringComparer.Ordinal)];
        Assert.Equal(["EMAIL|SENT|", "IN_APP|SENT|", "SMS|SUPPRESSED|MOBILE_UNVERIFIED"], rows);

        using HttpResponseMessage badFilter = await client.GetAsync($"{Inbox}/history?channel=FAX", viewer);
        Assert.Equal(HttpStatusCode.BadRequest, badFilter.StatusCode);
    }

    /// <summary>TASK-040's check, backed here: turn e-mail off for a family and the next matching event is not e-mailed, while it still appears in-app.</summary>
    [Fact]
    public async Task PreferencesListEveryFamilyRefuseWhatCannotBeChangedAndTheNextSendHonoursThem()
    {
        using HttpClient client = host.Api.CreateClient();
        string viewer = (await client.SignInOrFailAsync(6)).AccessToken;

        using HttpResponseMessage listed = await client.GetAsync(Preferences, viewer);
        Assert.Equal(HttpStatusCode.OK, listed.StatusCode);
        List<string> channels = [.. (await listed.ReadObjectAsync())["families"]!.AsArray()
            .Select(f => f!.AsObject())
            .SelectMany(f => f["channels"]!.AsArray().Select(c => $"{f["eventFamilyCode"]}|{f["isMandatory"]}|{c!["channel"]}|{c["isEnabled"]}|{c["isConfigurable"]}"))];
        Assert.Contains($"{NotificationTestHost.EscalationFamily}|true|EMAIL|true|false", channels);
        Assert.Contains($"{NotificationTestHost.ReportFamily}|false|IN_APP|true|false", channels);
        Assert.Contains($"{NotificationTestHost.ReportFamily}|false|EMAIL|true|true", channels);

        await AssertRefusedAsync(client, viewer, NotificationTestHost.ReportFamily, "IN_APP", 422, "NOTIFICATION_PREFERENCE_NOT_CONFIGURABLE");
        await AssertRefusedAsync(client, viewer, NotificationTestHost.EscalationFamily, "EMAIL", 422, "NOTIFICATION_PREFERENCE_NOT_CONFIGURABLE");
        await AssertRefusedAsync(client, viewer, "TEST_NO_SUCH_FAMILY", "EMAIL", 422, "NOTIFICATION_PREFERENCE_INVALID");
        await AssertRefusedAsync(client, viewer, NotificationTestHost.ReminderFamily, "SMS", 422, "NOTIFICATION_PREFERENCE_INVALID");
        using (HttpResponseMessage twice = await client.PutAsync(Preferences, viewer, new
        {
            preferences = new[]
            {
                new { eventFamilyCode = NotificationTestHost.ReportFamily, channel = "EMAIL", isEnabled = false },
                new { eventFamilyCode = NotificationTestHost.ReportFamily, channel = "EMAIL", isEnabled = true },
            },
        }, ifMatch: null))
        {
            Assert.Equal(HttpStatusCode.BadRequest, twice.StatusCode);
        }

        try
        {
            using HttpResponseMessage off = await client.SendAsync(HttpMethod.Put, Preferences, viewer,
                new { preferences = new[] { new { eventFamilyCode = NotificationTestHost.ReportFamily, channel = "EMAIL", isEnabled = false } } }, idempotencyKey: "");
            Assert.Equal(HttpStatusCode.OK, off.StatusCode);
            Assert.Contains("\"channel\":\"EMAIL\",\"isEnabled\":false,\"isConfigurable\":true", await off.Content.ReadAsStringAsync(), StringComparison.Ordinal);
            Assert.Equal(["false"], await host.Database.QueryAsync($"""
                SELECT a.new_value FROM audit_activity.audit_event e JOIN audit_activity.audit_event_attribute a ON a.audit_event_id = e.id
                WHERE e.event_type = 'Notifications.PreferenceChanged' AND e.actor_user_id = '{NotificationTestHost.UserId(6)}' AND a.attribute_name = 'is_enabled'
                ORDER BY e.occurred_at DESC LIMIT 1
                """));

            Guid job = Guid.NewGuid();
            await Report(job).RunAsync(host.Api);
            await host.Api.DeliverAsync();
            IReadOnlyList<string> deliveries = await host.Database.QueryAsync(NotificationDriver.DeliveriesOf(NotificationTestHost.ReportEvent, job));
            Assert.Contains("6|EMAIL|SUPPRESSED|RECIPIENT_OPTED_OUT", deliveries);
            Assert.Contains("6|IN_APP|SENT|", deliveries);
            Assert.Contains("2|EMAIL|SENT|", deliveries);
        }
        finally
        {
            using HttpResponseMessage on = await client.PutAsync(Preferences, viewer,
                new { preferences = new[] { new { eventFamilyCode = NotificationTestHost.ReportFamily, channel = "EMAIL", isEnabled = true } } }, ifMatch: null);
            Assert.Equal(HttpStatusCode.OK, on.StatusCode);
        }
    }

    [Fact]
    public async Task OperationsAreR01sAndShowNoRenderedContent()
    {
        Guid job = Guid.NewGuid();
        await Report(job).RunAsync(host.Api);
        await host.Api.DeliverAsync();
        string intentId = (await host.Database.QueryAsync($"SELECT id::text FROM notifications.notification_intent WHERE source_reference = '{job}'")).Single();

        using HttpClient client = host.Api.CreateClient();
        string viewer = (await client.SignInOrFailAsync(6)).AccessToken;
        using HttpResponseMessage refused = await client.GetAsync($"/api/v1/notification-intents/{intentId}", viewer);
        Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
        using HttpResponseMessage refusedRedrive = await client.PostAsync($"/api/v1/notification-intents/{intentId}/redrive", viewer);
        Assert.Equal(HttpStatusCode.Forbidden, refusedRedrive.StatusCode);

        string admin = (await client.SignInOrFailAsync(1)).AccessToken;
        using HttpResponseMessage detail = await client.GetAsync($"/api/v1/notification-intents/{intentId}", admin);
        Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
        string body = await detail.Content.ReadAsStringAsync();
        Assert.Contains("\"status\":\"COMPLETED\"", body, StringComparison.Ordinal);
        Assert.DoesNotContain("Portfolio", body, StringComparison.Ordinal);
        Assert.Equal(6, (await detail.ReadObjectAsync())["deliveries"]!.AsArray().Count);

        using HttpResponseMessage completed = await client.PostAsync($"/api/v1/notification-intents/{intentId}/redrive", admin);
        Assert.Equal(HttpStatusCode.Conflict, completed.StatusCode);
    }

    private static async Task AssertRefusedAsync(HttpClient client, string token, string family, string channel, int status, string code)
    {
        using HttpResponseMessage response = await client.PutAsync(Preferences, token,
            new { preferences = new[] { new { eventFamilyCode = family, channel, isEnabled = false } } }, ifMatch: null);
        Assert.Equal(status, (int)response.StatusCode);
        Assert.Equal(code, (await response.ReadObjectAsync())["code"]!.GetValue<string>());
    }

    private static async Task<int> UnreadAsync(HttpClient client, string token)
    {
        using HttpResponseMessage response = await client.GetAsync($"{Inbox}/unread-count", token);
        return (await response.ReadObjectAsync())["unreadCount"]!.GetValue<int>();
    }

    private SourceTransaction Report(Guid job) =>
        new(
            $"SELECT 1",
            TestNotificationSource.Intent(NotificationTestHost.ReportsModule, NotificationTestHost.ReportEvent, NotificationTestHost.ReportFamily, job,
                new EventSubject(NotificationTestHost.ReportsModule, "ReportJob", job, null), new EventScope(null, null, null), $"/reports/jobs/{job}",
                [new NotificationParameter("reportName", "Portfolio")], host.Clock.GetUtcNow()));
}
